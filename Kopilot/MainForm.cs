namespace Kopilot;

using System.Runtime.InteropServices;
using GitHub.Copilot.SDK.Rpc;

public partial class MainForm : Form
{
    [DllImport("user32.dll")] private static extern bool SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);
    private const int WM_SETREDRAW = 0x000B;

    /// <summary>
    /// Suspends visual redraws on <paramref name="rtb"/>, runs <paramref name="action"/>,
    /// then resumes and invalidates — preventing the scroll-up/scroll-down flicker caused
    /// by retroactive Select+insert sequences.
    /// </summary>
    private static void WithoutRedraw(RichTextBox rtb, Action action)
    {
        SendMessage(rtb.Handle, WM_SETREDRAW, false, 0);
        try   { action(); }
        finally
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, true, 0);
            rtb.Invalidate();
        }
    }
    private readonly CopilotService _copilot = new();
    private readonly AudioService   _audio   = new();
    private readonly PromptHistory  _promptHistory = new();
    private readonly List<string> _attachments = new();
    private readonly HashSet<string> _streamingSessions = new();
    // Maps toolCallId → char offset AFTER "  🔧 name  args" text (insertion point for ✓/✗)
    private readonly Dictionary<string, int> _toolStartPositions = new();
    // Maps toolCallId → char offset of the ○ character for retroactive ○→◉/✗ replacement
    private readonly Dictionary<string, int> _subAgentStartPositions = new();
    // Maps sub-agent toolCallId → display name for currently active (in-flight) sub-agents
    private readonly Dictionary<string, string> _activeSubAgents = new();
    private int _completedAgentCount = 0;
    // True when the main session went idle but sub-agents are still running (Fleet mode);
    // completion is deferred until the last sub-agent finishes.
    private bool _mainSessionIdle = false;
    private double _totalBytesReceived = 0;
    private string? _mainSessionId;
    private int _pendingCount = 0; // number of prompts awaiting a response
    private bool _reconnecting = false; // true while an automatic reconnect is in progress
    private bool _skipCloseBackupPrompt = false; // set after backup-on-close completes
    private KopilotSettings _settings = new();


    public MainForm()
    {
        InitializeComponent();
        WireUpEvents();

        // Load persisted settings and sync with service
        _settings = KopilotSettings.Load();
        _copilot.OrgFolder = _settings.OrgFolder;
        if (!string.IsNullOrEmpty(_settings.OrgFolder))
            toolTipMain.SetToolTip(buttonSetOrgFolder,
                $"Org folder: {_settings.OrgFolder}\n(click to change)");

        // Populate mode combo from the SDK enum and select the first entry
        PopulateModeCombo();
        // Sync service with the UI defaults set above
        _copilot.AutoApprove = checkBoxAutoApprove.Checked;
        _copilot.FleetMode   = checkBoxFleet.Checked;
    }

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireUpEvents()
    {
        buttonSend.Click += async (_, _) => await SendPromptAsync();
        buttonStop.Click += async (_, _) => await StopAsync();
        buttonAddFile.Click += ButtonAddFile_Click;
        buttonAddFolder.Click += ButtonAddFolder_Click;
        buttonOpenFolder.Click += async (_, _) => await OpenFolderAndConnectAsync();
        buttonHistoryPrev.Click += (_, _) => NavigateHistoryBack();
        buttonHistoryNext.Click += (_, _) => NavigateHistoryForward();
        richTextBoxPrompt.KeyDown += RichTextBoxPrompt_KeyDown;
        richTextBoxPrompt.FilesDropped += (_, paths) =>
        {
            foreach (var path in paths)
                AddAttachment(path);
		};

        this.Shown += async (_, _) =>
        {
            await CheckForUpdatesAsync();
            await PopulateModelsAsync();
        };

        buttonHelp.Click += (_, _) => ShowHelpAsync();
		buttonPowershell.Click += (_, _) => OpenPowershell();
        buttonSummarize.Click += async (_, _) => await SendQuickCommandAsync(
            "Please provide a concise summary of what we've discussed and accomplished so far in this session.");
        buttonClearOutput.Click += (_, _) => ClearActiveOutput();
        buttonBackup.Click += async (_, _) => await BackupSessionAsync();
        buttonOpenExplorer.Click += (_, _) => OpenExplorer();
        buttonOpenVSCode.Click += async (_, _) => await OpenVSCodeAsync();
        buttonSetOrgFolder.Click += (_, _) => SetOrgFolder();

        checkBoxAutoApprove.CheckedChanged += (_, _) =>
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;

        checkBoxFleet.CheckedChanged += async (_, _) =>
        {
            _copilot.FleetMode = checkBoxFleet.Checked;
            if (_copilot.IsConnected && _mainSessionId != null)
            {
                var state = checkBoxFleet.Checked ? "enabled" : "disabled";
                AppendOutput($"\r\n[Fleet mode {state} — new session will start on next send]\r\n\r\n", AppTheme.ColorMeta);
                await _copilot.ResetSessionAsync();
                _mainSessionId = null;
                _pendingCount = 0;
                _mainSessionIdle = false;
            }
        };

        comboBoxModel.SelectedIndexChanged += async (_, _) =>
        {
            var model = comboBoxModel.SelectedItem?.ToString() ?? "gpt-4.1";
            try { await _copilot.UpdateModelAsync(model); }
            catch { /* ignore */ }
        };

        comboBoxMode.SelectedIndexChanged += async (_, _) => await ApplyModeChangeAsync();

        _copilot.ConnectionStateChanged += (_, state) =>
            InvokeOnUI(() => UpdateConnectionStatus(state));

        _copilot.SessionCreated += (_, args) =>
            InvokeOnUI(() => OnSessionCreated(args.SessionId, args.IsSubAgent));

        _copilot.MessageReceived += (_, args) =>
            InvokeOnUI(() => AppendMessage(args));

        _copilot.SessionIdleForSession += (_, sessionId) =>
            InvokeOnUI(() => OnSessionIdle(sessionId));

        _copilot.PermissionRequested += Copilot_PermissionRequested;
        _copilot.UserInputRequested += Copilot_UserInputRequested;
    }

    private void InvokeOnUI(Action action)
    {
        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(action);
        else
            action();
    }

    // ── Combo population ──────────────────────────────────────────────────────

    // Maps each SDK mode enum value to the display name used throughout the app.
    private static readonly Dictionary<SessionModeGetResultMode, string> _modeDisplayNames =
        new()
        {
            [SessionModeGetResultMode.Interactive] = "Standard",
            [SessionModeGetResultMode.Plan]        = "Plan",
            [SessionModeGetResultMode.Autopilot]   = "Autopilot",
        };

    /// <summary>
    /// Populates comboBoxMode from the SDK SessionModeGetResultMode enum values,
    /// using display names that match the rest of the app (Interactive -> "Standard").
    /// </summary>
    private void PopulateModeCombo()
    {
        comboBoxMode.Items.Clear();
        foreach (var mode in Enum.GetValues<SessionModeGetResultMode>())
        {
            var display = _modeDisplayNames.TryGetValue(mode, out var name) ? name : mode.ToString();
            comboBoxMode.Items.Add(display);
        }
        comboBoxMode.SelectedIndex = 0;
    }

    /// <summary>
    /// Queries the Copilot SDK for available models and populates comboBoxModel.
    /// Selects the highest-available Claude Sonnet model by default.
    /// Falls back silently if the SDK is unavailable.
    /// </summary>
    private async Task PopulateModelsAsync()
    {
        var ids = await _copilot.ListModelsAsync();
        if (ids.Count == 0) return;

        comboBoxModel.BeginUpdate();
        try
        {
            comboBoxModel.Items.Clear();
            foreach (var id in ids)
                comboBoxModel.Items.Add(id);
        }
        finally
        {
            comboBoxModel.EndUpdate();
        }

        var sonnetIdx = comboBoxModel.Items.Cast<string>()
            .Select((m, i) => (model: m, idx: i))
            .Where(x => x.model.StartsWith("claude-sonnet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.model)
            .Select(x => x.idx)
            .DefaultIfEmpty(0)
            .First();
        comboBoxModel.SelectedIndex = sonnetIdx;
    }



    private async Task SendPromptAsync(bool recordHistory = true)
    {
        var prompt = richTextBoxPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        if (recordHistory)
        {
            _promptHistory.Add(prompt);
            UpdateHistoryButtons();
        }
        richTextBoxPrompt.Clear();

        await DispatchPromptAsync(prompt);
    }

    private async Task StopAsync()
    {
        try { await _copilot.AbortAsync(); }
        catch { /* ignore */ }

        // Allow the session idle event to settle before sending the stop instruction.
        await Task.Delay(300);
        await DispatchPromptAsync("STOP. Do not perform any further actions. Wait for my next instruction.");
    }

    private void ShowHelp()
    {
        AppendOutput("❓ Kopilot — Quick Help\r\n\r\n", AppTheme.ColorUser);

        AppendOutput("── Getting started ──────────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "1. Click 📂 Open Folder… to connect Copilot to a project directory.\r\n" +
            "2. Type a prompt and press Send (or Ctrl+Enter).\r\n" +
            "3. Attach files or folders with 📄 / 📁 before sending for extra context.\r\n" +
            "4. Use ▲ / ▼ to navigate your prompt history.\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Toolbar controls ─────────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  Model        – Choose the AI model (GPT-4.1, Claude Sonnet/Opus, …)\r\n" +
            "  Mode         – Standard | Plan (plan before acting) | Autopilot (fully autonomous)\r\n" +
            "  Fleet ☐      – Spawn parallel sub-agents for large tasks\r\n" +
            "  Auto-approve – Skip permission prompts; approve all tool operations automatically\r\n" +
            "  Stop         – Cancel an in-progress response\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Quick command buttons ─────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  ❓ Help       – Show this guide (works without a folder open)\r\n" +
            "  ⚡ PowerShell – Open terminal in the project folder\r\n" +
            "  📂 Explorer   – Open File Explorer at the project folder\r\n" +
            "  💻 VS Code    – Launch VS Code and connect the IDE\r\n" +
            "  📝 Summarize  – Ask Copilot for a session summary\r\n" +
            "  💾 Backup     – Save a Markdown resume of the session\r\n" +
            "  🗑 Clear      – Clear the output panel\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── When Copilot asks permission ──────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  ✓ Allow          – Approve this one operation\r\n" +
            "  ✓ Approve Similar – Approve all operations of this type for the session\r\n" +
            "  ✗ Deny           – Reject; Copilot will adjust\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── What Copilot can do for you ───────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  • Read, write, and edit files.\r\n" +
            "  • Run shell commands and scripts.\r\n" +
            "  • Search and navigate the codebase.\r\n" +
            "  • Explain, refactor, and debug code.\r\n" +
            "  • Plan multi-step tasks before acting.\r\n" +
            "  • Spawn parallel agents (Fleet) for large jobs.\r\n" +
            "  • Call MCP tools and external services.\r\n" +
            "  • Fetch URLs and access memory.\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Tips ──────────────────────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  • Use Plan mode for big tasks so you can review before Copilot acts.\r\n" +
            "  • Drag files onto the prompt box to attach them.\r\n" +
            "  • Click 📝 Summarize often to capture progress.\r\n" +
            "  • Click 💾 Backup to save a resume you can attach to a future session.\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Copilot slash commands (/…) ───────────────────────\r\n", AppTheme.ColorMeta);

        AppendOutput("  Conversation\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /clear, /new [PROMPT]         Start a new conversation\r\n" +
            "  /compact                       Summarize history to save context window\r\n" +
            "  /copy                          Copy last response to clipboard\r\n" +
            "  /diff                          Review changes made in the current directory\r\n" +
            "  /plan [PROMPT]                 Create an implementation plan before coding\r\n" +
            "  /undo, /rewind                 Revert last turn and undo file changes\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Session\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /cwd, /cd [PATH]               Show or change the working directory\r\n" +
            "  /rename [NAME]                 Rename the current session\r\n" +
            "  /resume [SESSION-ID]           Switch to a different session\r\n" +
            "  /session [subcommand]          Show session info and workspace summary\r\n" +
            "  /tasks                         View and manage background tasks and subagents\r\n" +
            "  /usage                         Show session usage metrics and statistics\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Agents & Research\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /agent                         Browse and select available agents\r\n" +
            "  /delegate [PROMPT]             Delegate task to a remote agent (creates PR)\r\n" +
            "  /fleet [PROMPT]                Run task in parallel with sub-agents\r\n" +
            "  /pr [view|create|fix|auto]     Operate on pull requests for the current branch\r\n" +
            "  /research TOPIC                Deep research with GitHub search and web sources\r\n" +
            "  /review [PROMPT]               Run the code review agent\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Permissions & Paths\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /add-dir PATH                  Add directory to allowed file access list\r\n" +
            "  /allow-all, /yolo              Enable all permissions (tools, paths, URLs)\r\n" +
            "  /list-dirs                     List all allowed directories\r\n" +
            "  /reset-allowed-tools           Reset the list of allowed tools\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Tools & Integrations\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /ide                           Connect to an IDE workspace (e.g. VS Code)\r\n" +
            "  /lsp [show|test|reload|help]   Manage language server configuration\r\n" +
            "  /mcp [show|add|edit|delete|…]  Manage MCP server configuration\r\n" +
            "  /plugin [install|list|…]       Manage plugins and plugin marketplaces\r\n" +
            "  /skills [list|add|remove|…]    Manage agent skills\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Configuration\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /experimental [on|off|show]    Toggle or show experimental features\r\n" +
            "  /init                          Initialize Copilot custom instructions\r\n" +
            "  /instructions                  View and toggle custom instruction files\r\n" +
            "  /keep-alive [on|busy|NUMBERm]  Prevent the machine from going to sleep\r\n" +
            "  /model, /models [MODEL]        Select the AI model\r\n" +
            "  /on-air, /streamer-mode        Toggle streamer mode (hides model names)\r\n" +
            "  /terminal-setup                Configure terminal for multiline input\r\n" +
            "  /theme [show|set|list]         View or configure the terminal theme\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Info & Sharing\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /changelog [SUMMARIZE]         Display the CLI changelog\r\n" +
            "  /context                       Show context window token usage\r\n" +
            "  /feedback                      Send feedback about the CLI to GitHub\r\n" +
            "  /help                          Show CLI built-in help\r\n" +
            "  /remote                        Enable remote access from GitHub.com / Mobile\r\n" +
            "  /share [file|gist]             Share session as Markdown file or GitHub gist\r\n" +
            "  /version                       Display version info and check for updates\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Auth & Lifecycle\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  /login                         Log in to Copilot\r\n" +
            "  /logout                        Log out of Copilot\r\n" +
            "  /update                        Update the CLI to the latest version\r\n" +
            "  /user [show|list|switch]       Manage the current GitHub user\r\n" +
            "  /restart                       Restart the CLI, preserving the session\r\n" +
            "  /exit, /quit                   Exit the CLI\r\n\r\n",
            AppTheme.ColorDefault);
    }

    private void ShowHelpAsync()
    {
        ShowHelp();
    }

    private async Task SendQuickCommandAsync(string prompt)
    {
        if (!_copilot.IsConnected)
        {
            MessageBox.Show("Open a folder to connect to Copilot first.",
                "Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        await DispatchPromptAsync(prompt);
    }

    private async Task DispatchPromptAsync(string prompt)
    {
        _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString()  ?? "Standard";
        _copilot.AutoApprove = checkBoxAutoApprove.Checked;
        _copilot.FleetMode   = checkBoxFleet.Checked;

        _pendingCount++;
        UpdateWorkingState();

        try
        {
            var attachmentsCopy = _attachments.ToList();
            await _copilot.SendMessageAsync(prompt, attachmentsCopy);
            _audio.PlayPromptSent();

            // Echo user message
            if (_mainSessionId != null)
                AppendOutput($"👤 You: {prompt}\r\n\r\n", AppTheme.ColorUser);
        }
        catch (Exception ex)
        {
            _pendingCount = Math.Max(0, _pendingCount - 1);
            UpdateWorkingState();
            if (_mainSessionId != null)
                AppendOutput($"\r\n❌ Error sending: {ex.Message}\r\n", AppTheme.ColorError);
        }
    }

    private void ClearActiveOutput()
    {
        if (richTextBoxOutput.TextLength == 0) return;
        if (MessageBox.Show("Clear all output?", "Clear Output",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            richTextBoxOutput.Clear();
            _toolStartPositions.Clear();
            _subAgentStartPositions.Clear();
            _activeSubAgents.Clear();
            _completedAgentCount = 0;
            _mainSessionIdle = false;
        }
    }

    private void OpenExplorer()
    {
        var dir = _copilot.WorkingDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            MessageBox.Show("No session folder is open yet. Use 'Open Folder' first.",
                "No Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    private void OpenPowershell()
    {
        var dir = _copilot.WorkingDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            MessageBox.Show("No session folder is open yet. Use 'Open Folder' first.",
                "No Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var scriptsPath = Path.Combine(Application.StartupPath, "scripts.ps1");
            var arguments = File.Exists(scriptsPath)
                ? $"-NoExit -Command \". '{scriptsPath.Replace("'", "''")}'\""
                : "-NoExit";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                WorkingDirectory = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open PowerShell:\n\n{ex.Message}",
                "PowerShell Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task OpenVSCodeAsync()
    {
        var dir = _copilot.WorkingDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            MessageBox.Show("No session folder is open yet. Use 'Open Folder' first.",
                "No Folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not launch VS Code:\n\n{ex.Message}\n\n" +
                "Make sure the 'code' command is on your PATH " +
                "(VS Code → Command Palette → 'Install code command in PATH').",
                "VS Code Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Give VS Code a moment to register with Copilot before sending /ide
        await Task.Delay(2000);
        await SendQuickCommandAsync("/ide");
    }

    private async Task ApplyModeChangeAsync()
    {
        var mode = comboBoxMode.SelectedItem?.ToString() ?? "Standard";
        _copilot.ActiveMode = mode;

        // Autopilot implies auto-approve
        if (mode == "Autopilot" && !checkBoxAutoApprove.Checked)
            checkBoxAutoApprove.Checked = true;

        // Mode is baked into the session's system message at creation time, so we
        // reset the active session. The existing output tab stays visible; a new
        // session (and tab) will be created on the next send.
        if (_copilot.IsConnected && _mainSessionId != null)
        {
            AppendOutput(
                $"\r\n[Mode changed to {mode} — new session will start on next send]\r\n\r\n",
                AppTheme.ColorMeta);

            await _copilot.ResetSessionAsync();
            _mainSessionId = null;
            _pendingCount  = 0;
            _mainSessionIdle = false;
            UpdateWorkingState();
        }
    }

    private async Task BackupSessionAsync()
    {
        if (!_copilot.IsConnected || _mainSessionId == null)
        {
            MessageBox.Show("No active session to back up. Open a folder and send at least one message first.",
                "Nothing to Back Up", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Ask user where to save
        var kopilotDir = _copilot.WorkingDirectory != null
            ? Path.Combine(_copilot.WorkingDirectory, ".kopilot")
            : null;
        var backupInitialDir = kopilotDir != null && Directory.Exists(kopilotDir)
            ? kopilotDir
            : _copilot.WorkingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        using var saveDialog = new SaveFileDialog
        {
            Title = "Save Session Backup",
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = "md",
            FileName = $"copilot-session-{DateTime.Now:yyyy-MM-dd-HHmm}.md",
            InitialDirectory = backupInitialDir,
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK) return;

        var filePath = saveDialog.FileName;

        buttonBackup.Enabled = false;
        buttonBackup.Text = "⏳ Backing up…";

        const string backupPrompt =
            """
            Write a brief session-resume note in Markdown — use ONLY what is already in our conversation history. Do NOT use any tools, do NOT read any files or directories.

            Structure it as:

            # Session Resume

            ## Goal
            One or two sentences: what we set out to accomplish.

            ## What Was Done
            Short bullet list of the key things completed or decided this session.

            ## Current State
            One paragraph describing exactly where things stand right now.

            ## Next Step
            The single most important thing to do when resuming.

            ## Context to Remember
            Any non-obvious decisions, constraints, or gotchas a new session needs to know.

            Keep the whole document under one page. Draw entirely from our conversation — do not invoke tools.
            """;

        try
        {
            SetSendingState(true);

            // Echo the backup request in the output
            AppendOutput("💾 Generating session backup document…\r\n\r\n", AppTheme.ColorMeta);

            var markdown = await _copilot.SendAndCaptureResponseAsync(backupPrompt, TimeSpan.FromMinutes(5));

            await File.WriteAllTextAsync(filePath, markdown);

            AppendOutput($"[Backup saved to: {filePath}]\r\n\r\n", AppTheme.ColorMeta);

            MessageBox.Show($"Session backup saved to:\n{filePath}",
                "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Backup failed:\n\n{ex.Message}",
                "Backup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            buttonBackup.Enabled = true;
            buttonBackup.Text = "💾 Backup";
            SetSendingState(false);
        }
    }

    private async Task OfferLoadBackupAsync(string projectRoot)
    {
        var kopilotDir = Path.Combine(projectRoot, ".kopilot");
        if (!Directory.Exists(kopilotDir)) return;

        var newest = Directory.GetFiles(kopilotDir, "*.md")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        if (newest == null) return;

        var fileName = Path.GetFileName(newest);
        var modified = File.GetLastWriteTime(newest);

        var result = MessageBox.Show(
            $"A session backup was found in .kopilot:\r\n\r\n" +
            $"  {fileName}\r\n" +
            $"  Saved: {modified:g}\r\n\r\n" +
            "Load it to continue the previous session?",
            "Resume Previous Session?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (result != DialogResult.Yes) return;

        string content;
        try
        {
            content = await File.ReadAllTextAsync(newest);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read backup file:\r\n\r\n{ex.Message}",
                "Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        AppendOutput($"[Loading session backup: {fileName}]\r\n\r\n", AppTheme.ColorMeta);

        await DispatchPromptAsync(
            "I am providing a session resume document from our last session. " +
            "Please read it and confirm you understand the context so we can continue where we left off.\r\n\r\n" +
            content);
    }

    private async Task OpenFolderAndConnectAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the project root folder for Copilot",
            UseDescriptionForTitle = true,
            SelectedPath = _copilot.WorkingDirectory ?? "",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // If already connected, tear down first so the new CWD takes effect
        if (_copilot.IsConnected)
        {
            await _copilot.DisposeAsync();
            _copilot.Reset();
        }

        _pendingCount = 0;
        _mainSessionIdle = false;

        _copilot.WorkingDirectory = dialog.SelectedPath;
        buttonOpenFolder.Enabled = false;
        toolStripStatusLabelSession.Text = dialog.SelectedPath;

        // Offer to open VS Code so the IDE connection is ready before the session starts
        var openVSCode = MessageBox.Show(
            "Would you like to open VS Code in this folder first?\n\n" +
            "The IDE connection will be established before the Copilot session starts.",
            "Open VS Code?", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (openVSCode == DialogResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "code",
                    Arguments = $"\"{dialog.SelectedPath}\"",
                    UseShellExecute = true,
                });

                for (int i = 10; i > 0; i--)
                {
                    toolStripStatusLabelSession.Text = $"Waiting for VS Code… {i}s";
                    await Task.Delay(1000);
                }

                toolStripStatusLabelSession.Text = dialog.SelectedPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not launch VS Code:\n\n{ex.Message}\n\n" +
                    "Make sure the 'code' command is on your PATH " +
                    "(VS Code → Command Palette → 'Install code command in PATH').",
                    "VS Code Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        try
        {
            // Sync UI state to service before session creation so the system message is correct
            _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString() ?? "Standard";
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;
            _copilot.FleetMode   = checkBoxFleet.Checked;

            await _copilot.EnsureSessionAsync();
            var version = await _copilot.GetVersionAsync();
            if (!string.IsNullOrEmpty(version))
                toolStripStatusLabelVersion.Text = $"v{version}";

            if (_copilot.KopilotPath != null)
                AppendOutput($"[Scratchpad: {_copilot.KopilotPath}]\r\n", AppTheme.ColorMeta);

            // Report which instruction tiers were loaded
            foreach (var (label, folder) in _copilot.GetTierFolders())
            {
                var instructionsPath = System.IO.Path.Combine(folder, "kopilot-instructions.md");
                var agentsDir        = System.IO.Path.Combine(folder, "agents");
                var skillsDir        = System.IO.Path.Combine(folder, "skills");

                var hasMd     = System.IO.File.Exists(instructionsPath);
                var agentCount = System.IO.Directory.Exists(agentsDir)
                    ? System.IO.Directory.GetFiles(agentsDir, "*.md", System.IO.SearchOption.TopDirectoryOnly).Length
                    : 0;
                var hasSkills = System.IO.Directory.Exists(skillsDir);

                var parts = new System.Text.StringBuilder();
                if (hasMd)     parts.Append("instructions");
                if (agentCount > 0)
                {
                    if (parts.Length > 0) parts.Append(", ");
                    parts.Append($"{agentCount} agent{(agentCount == 1 ? "" : "s")}");
                }
                if (hasSkills)
                {
                    if (parts.Length > 0) parts.Append(", ");
                    parts.Append("skills");
                }

                var summary = parts.Length > 0 ? parts.ToString() : "no files";
                AppendOutput($"[{label} tier: {folder} ({summary})]\r\n", AppTheme.ColorMeta);
            }

            AppendOutput("\r\n", AppTheme.ColorMeta);

            await OfferLoadBackupAsync(dialog.SelectedPath);
        }
        catch (Exception ex)
        {
            buttonOpenFolder.Enabled = true;
            MessageBox.Show(
                $"Failed to connect to Copilot CLI:\n\n{ex.Message}\n\n" +
                "Make sure 'copilot' is installed and authenticated (run 'copilot auth login').",
                "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            buttonOpenFolder.Enabled = true;
        }
    }

    // ── Org folder configuration ──────────────────────────────────────────────

    private void SetOrgFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the organization-level instructions folder\n" +
                          "(should contain kopilot-instructions.md and/or agents/ and skills/ subfolders)",
            UseDescriptionForTitle = true,
            SelectedPath = _settings.OrgFolder ?? "",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _settings.OrgFolder = dialog.SelectedPath;
        _copilot.OrgFolder  = dialog.SelectedPath;

        toolTipMain.SetToolTip(buttonSetOrgFolder,
            $"Org folder: {dialog.SelectedPath}\n(click to change)");

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save settings to kopilot.ini:\n\n{ex.Message}",
                "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ── Prompt history navigation ─────────────────────────────────────────────


    private void NavigateHistoryBack()
    {
        var text = _promptHistory.NavigateBack(richTextBoxPrompt.Text);
        richTextBoxPrompt.Text = text;
        richTextBoxPrompt.SelectionStart = text.Length;
        UpdateHistoryButtons();
    }

    private void NavigateHistoryForward()
    {
        var text = _promptHistory.NavigateForward();
        richTextBoxPrompt.Text = text;
        richTextBoxPrompt.SelectionStart = text.Length;
        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        buttonHistoryPrev.Enabled = _promptHistory.CanGoBack;
        buttonHistoryNext.Enabled = _promptHistory.CanGoForward;
    }

    private void RichTextBoxPrompt_KeyDown(object? sender, KeyEventArgs e)    {
        if (e.KeyCode == Keys.Enter && e.Control)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SendPromptAsync();
        }
    }

    // ── File / Folder attachment ──────────────────────────────────────────────

    private void ButtonAddFile_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Attach File(s) to Prompt",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var path in dialog.FileNames)
                AddAttachment(path);
        }
    }

    private void ButtonAddFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a Folder to Attach",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddAttachment(dialog.SelectedPath);
    }

    private static bool IsHiddenAttachment(string path) =>
        Path.GetFileName(path).Equals("kopilot-instructions.md", StringComparison.OrdinalIgnoreCase);

    private void AddAttachment(string path)
    {
        if (_attachments.Contains(path)) return;
        _attachments.Add(path);

        // Behind-the-scenes files are attached silently — no chip, no prompt reference.
        if (IsHiddenAttachment(path)) return;

        var chip = new Button
        {
            Text = Path.GetFileName(path) + "  ✕",
            AutoSize = true,
            BackColor = AppTheme.ButtonBg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = AppTheme.TextPrimary,
            Margin = new Padding(2, 2, 2, 2),
            Padding = new Padding(6, 2, 6, 2),
            Tag = path,
            Height = 24,
            UseVisualStyleBackColor = false,
        };
        chip.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        chip.FlatAppearance.BorderSize = 1;
        toolTipMain.SetToolTip(chip, path);
        chip.Click += (_, _) => RemoveAttachment(path, chip);

        flowLayoutPanelChips.Controls.Add(chip);

        InsertReferenceAtCursor(path);
    }

    private void InsertReferenceAtCursor(string path)
    {
        // Normalise directories (strip trailing separator so GetFileName works)
        path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Prefer a relative path from the working directory; fall back to just the name
        string reference;
        var root = _copilot.WorkingDirectory;
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            var rel = Path.GetRelativePath(root, path);
            // GetRelativePath returns the original path (or starts with ..) when outside root
            reference = rel.StartsWith("..") ? Path.GetFileName(path) : rel;
        }
        else
        {
            reference = Path.GetFileName(path);
        }

        if (string.IsNullOrEmpty(reference)) return;

        reference = $"@{reference}";
        var pos  = richTextBoxPrompt.SelectionStart;
        var text = richTextBoxPrompt.Text;

        if (pos > 0 && !char.IsWhiteSpace(text[pos - 1]))
            reference = " " + reference;

        reference += " ";

        richTextBoxPrompt.SelectionLength = 0;
        richTextBoxPrompt.SelectedText = reference;
        richTextBoxPrompt.Focus();
    }

    private void RemoveAttachment(string path, Control chip)
    {
        _attachments.Remove(path);
        flowLayoutPanelChips.Controls.Remove(chip);
        chip.Dispose();
    }

    // ── Session ───────────────────────────────────────────────────────────────

    private void OnSessionCreated(string sessionId, bool isSubAgent)
    {
        if (!isSubAgent)
        {
            _mainSessionId = sessionId;
            _audio.PlaySessionStart();
        }

        var label = isSubAgent ? "Sub-agent" : "Session";
        AppendOutput($"[{label} {sessionId[..Math.Min(8, sessionId.Length)]}… started]\r\n\r\n",
            AppTheme.ColorMeta);

        toolStripStatusLabelSession.Text =
            $"Session: {sessionId[..Math.Min(8, sessionId.Length)]}…";
    }

    // ── Output rendering ─────────────────────────────────────────────────────

    private void AppendMessage(SessionMessageEventArgs args)
    {
        bool scrollNeeded = false;

        switch (args.Kind)
        {
            case MessageKind.AssistantDelta:
                if (!_streamingSessions.Contains(args.SessionId))
                {
                    AppendOutput("🤖 Assistant:\r\n", AppTheme.ColorAssistant);
                    _streamingSessions.Add(args.SessionId);
                }
                AppendOutput(args.Content, AppTheme.ColorDefault);
                scrollNeeded = true;
                break;

            case MessageKind.AssistantFinal:
                if (!_streamingSessions.Contains(args.SessionId))
                    AppendOutput($"🤖 Assistant:\r\n{args.Content}\r\n\r\n", AppTheme.ColorAssistant);
                scrollNeeded = true;
                break;

            case MessageKind.Reasoning:
                AppendOutput($"💭 Reasoning:\r\n{args.Content}\r\n\r\n", AppTheme.ColorReasoning);
                scrollNeeded = true;
                break;

            case MessageKind.SubAgentStart:
            {
                if (_streamingSessions.Remove(args.SessionId))
                    AppendOutput("\r\n", AppTheme.ColorDefault);
                AppendOutput("\r\n", AppTheme.ColorDefault);
                if (!string.IsNullOrEmpty(args.ToolCallId))
                {
                    _activeSubAgents[args.ToolCallId] = args.SubAgentDisplayName ?? args.Content;
                    _subAgentStartPositions[args.ToolCallId] = richTextBoxOutput.TextLength;
                }
                var saName = args.SubAgentDisplayName ?? args.Content;
                var saDesc = string.IsNullOrEmpty(args.SubAgentDescription) ? ""
                    : $" — {(args.SubAgentDescription.Length > 60 ? args.SubAgentDescription[..60] + "…" : args.SubAgentDescription)}";
                AppendOutput($"○ {saName}{saDesc}\r\n", AppTheme.ColorSubAgent);
                UpdateAgentStatus();
                scrollNeeded = true;
                break;
            }

            case MessageKind.SubAgentComplete:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId))
                {
                    _activeSubAgents.Remove(args.ToolCallId);
                    _completedAgentCount++;
                    if (_subAgentStartPositions.TryGetValue(args.ToolCallId, out var saPos))
                    {
                        _subAgentStartPositions.Remove(args.ToolCallId);
                        WithoutRedraw(richTextBoxOutput, () =>
                        {
                            richTextBoxOutput.Select(saPos, 1);
                            richTextBoxOutput.SelectionColor = AppTheme.ColorAssistant;
                            richTextBoxOutput.SelectedText = "◉";
                            richTextBoxOutput.SelectionStart = richTextBoxOutput.TextLength;
                        });
                    }
                }
                var stats = FormatSubAgentStats(args);
                if (stats != null)
                {
                    AppendOutput($"  ↳ {stats}\r\n", AppTheme.ColorToolDim);
                    scrollNeeded = true;
                }
                if (_mainSessionIdle && _activeSubAgents.Count == 0)
                    CompleteMainSession();
                else
                    UpdateAgentStatus();
                break;
            }

            case MessageKind.SubAgentFailed:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId))
                {
                    _activeSubAgents.Remove(args.ToolCallId);
                    _completedAgentCount++;
                    if (_subAgentStartPositions.TryGetValue(args.ToolCallId, out var saPos))
                    {
                        _subAgentStartPositions.Remove(args.ToolCallId);
                        WithoutRedraw(richTextBoxOutput, () =>
                        {
                            richTextBoxOutput.Select(saPos, 1);
                            richTextBoxOutput.SelectionColor = AppTheme.ColorError;
                            richTextBoxOutput.SelectedText = "✗";
                            richTextBoxOutput.SelectionStart = richTextBoxOutput.TextLength;
                        });
                    }
                }
                if (!string.IsNullOrEmpty(args.Content))
                {
                    AppendOutput($"  ✗ {args.SubAgentDisplayName}: {args.Content}\r\n", AppTheme.ColorError);
                    scrollNeeded = true;
                }
                var failStats = FormatSubAgentStats(args);
                if (failStats != null)
                {
                    AppendOutput($"  ↳ {failStats}\r\n", AppTheme.ColorToolDim);
                    scrollNeeded = true;
                }
                if (_mainSessionIdle && _activeSubAgents.Count == 0)
                    CompleteMainSession();
                else
                    UpdateAgentStatus();
                break;
            }

            case MessageKind.SkillInvoked:
            {
                if (_streamingSessions.Remove(args.SessionId))
                    AppendOutput("\r\n", AppTheme.ColorDefault);
                var desc = string.IsNullOrEmpty(args.SubAgentDescription) ? ""
                    : $" — {args.SubAgentDescription}";
                AppendOutput($"  📚 Skill: {args.Content}{desc}\r\n", AppTheme.ColorMeta);
                scrollNeeded = true;
                break;
            }

            case MessageKind.CustomAgentsUpdated:
                AppendOutput($"[{args.Content}]\r\n\r\n", AppTheme.ColorMeta);
                scrollNeeded = true;
                break;

            case MessageKind.ToolStart:
            {
                if (_streamingSessions.Remove(args.SessionId))
                    AppendOutput("\r\n", AppTheme.ColorDefault);
                var argPart = string.IsNullOrEmpty(args.ToolArgSummary) ? "" : $"  {args.ToolArgSummary}";
                AppendOutput($"  🔧 {args.Content}{argPart}", AppTheme.ColorTool);
                if (!string.IsNullOrEmpty(args.ToolCallId))
                    _toolStartPositions[args.ToolCallId] = richTextBoxOutput.TextLength;
                scrollNeeded = true;
                break;
            }

            case MessageKind.ToolProgress:
                if (!string.IsNullOrEmpty(args.Content))
                {
                    AppendOutput($"\r\n  │ {args.Content}", AppTheme.ColorToolDim);
                    scrollNeeded = true;
                }
                break;

            case MessageKind.ToolComplete:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId) &&
                    _toolStartPositions.TryGetValue(args.ToolCallId, out var insertAt))
                {
                    _toolStartPositions.Remove(args.ToolCallId);
                    string tick = args.ToolSuccess ? " ✓\r\n" : " ✗\r\n";
                    WithoutRedraw(richTextBoxOutput, () =>
                    {
                        richTextBoxOutput.Select(insertAt, 0);
                        richTextBoxOutput.SelectionColor = args.ToolSuccess ? AppTheme.ColorAssistant : AppTheme.ColorError;
                        richTextBoxOutput.SelectedText = tick;
                        foreach (var key in _toolStartPositions.Keys.ToList())
                            if (_toolStartPositions[key] >= insertAt)
                                _toolStartPositions[key] += tick.Length;
                        foreach (var key in _subAgentStartPositions.Keys.ToList())
                            if (_subAgentStartPositions[key] >= insertAt)
                                _subAgentStartPositions[key] += tick.Length;
                        richTextBoxOutput.SelectionStart = richTextBoxOutput.TextLength;
                    });
                    if (!string.IsNullOrEmpty(args.ToolResultSummary))
                    {
                        var rc = args.ToolSuccess ? AppTheme.ColorToolDim : AppTheme.ColorError;
                        AppendOutput($"  └ {args.ToolResultSummary}\r\n", rc);
                        scrollNeeded = true;
                    }
                }
                else
                {
                    AppendOutput(args.ToolSuccess ? "  ✓\r\n" : "  ✗\r\n",
                        args.ToolSuccess ? AppTheme.ColorAssistant : AppTheme.ColorError);
                    scrollNeeded = true;
                }
                break;
            }

            case MessageKind.BytesUpdate:
                _totalBytesReceived = args.TotalBytes;
                UpdateWorkingState();
                break; // no text appended — no scroll needed

            case MessageKind.Error:
                if (_streamingSessions.Remove(args.SessionId))
                    AppendOutput("\r\n", AppTheme.ColorDefault);
                AppendOutput($"\r\n❌ Error: {args.Content}\r\n\r\n", AppTheme.ColorError);
                if (args.Content.Contains("CAPIError: 400") || args.Content.Contains("400 Bad Request"))
                    AppendOutput(
                        "💡 Tip: This usually means the session's context window is full. " +
                        "Try changing the mode or model to start a fresh session.\r\n\r\n",
                        AppTheme.ColorMeta);
                scrollNeeded = true;
                break;
        }

        if (scrollNeeded)
            richTextBoxOutput.ScrollToCaret();
    }

    private void OnSessionIdle(string sessionId)
    {
        if (_streamingSessions.Remove(sessionId))
            AppendOutput("\r\n\r\n", AppTheme.ColorDefault);

        if (sessionId == _mainSessionId)
        {
            if (_activeSubAgents.Count > 0)
            {
                // Orchestrator finished dispatching but Fleet sub-agents are still running.
                // Defer completion until the last sub-agent fires complete/failed.
                _mainSessionIdle = true;
                UpdateAgentStatus();
            }
            else
            {
                CompleteMainSession();
            }
        }
    }

    private void CompleteMainSession()
    {
        _mainSessionIdle = false;
        _pendingCount = Math.Max(0, _pendingCount - 1);
        if (_pendingCount == 0)
        {
            _totalBytesReceived = 0;
            _activeSubAgents.Clear();
            _completedAgentCount = 0;
        }
        UpdateWorkingState();
        _audio.PlayPromptComplete();
    }

    private static string? FormatSubAgentStats(SessionMessageEventArgs args)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(args.SubAgentModel))
            parts.Add(args.SubAgentModel);
        if (args.SubAgentTotalCalls is > 0)
            parts.Add($"{(int)args.SubAgentTotalCalls} call{((int)args.SubAgentTotalCalls == 1 ? "" : "s")}");
        if (args.SubAgentTotalTokens is > 0)
        {
            var t = args.SubAgentTotalTokens.Value;
            parts.Add(t >= 1000 ? $"{t / 1000:F1}K tokens" : $"{(int)t} tokens");
        }
        if (args.SubAgentDurationMs is > 0)
            parts.Add($"{args.SubAgentDurationMs.Value / 1000:F1}s");
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    private void AppendOutput(string text, Color color)
    {
        // Suppress redraws for the entire append + scroll sequence.
        // ScrollToCaret must happen while WM_SETREDRAW is still false so the scroll
        // position is already at the bottom when painting resumes.  If ScrollToCaret
        // were called after Invalidate() the control would queue a WM_PAINT at the old
        // scroll position, then scroll (BitBlt), then paint the new bottom strip --
        // producing the "top-half jitter" seen during streaming.
        SendMessage(richTextBoxOutput.Handle, WM_SETREDRAW, false, 0);
        try
        {
            AppendColoredText(richTextBoxOutput, text, color);
            richTextBoxOutput.ScrollToCaret();
        }
        finally
        {
            SendMessage(richTextBoxOutput.Handle, WM_SETREDRAW, true, 0);
            richTextBoxOutput.Invalidate();
        }
    }

    private static void AppendColoredText(RichTextBox box, string text, Color color)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = color;
        box.AppendText(text);
        // TextBoxBase.AppendText restores the selection to the pre-append position
        // (old TextLength), leaving the caret at the START of the appended text rather
        // than the end.  Explicitly move to the true end so ScrollToCaret does not
        // jump back on the next delta.
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = box.ForeColor;
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private void UpdateWorkingState()
    {
        bool working = _pendingCount > 0;
        buttonSend.Enabled = _copilot.IsConnected;
        buttonStop.Enabled = working;

        string status;
        if (working)
        {
            var kb = _totalBytesReceived > 0 ? $" · {_totalBytesReceived / 1024:F1} KiB" : "";
            status = _pendingCount > 1
                ? $"Working… ({_pendingCount} pending{kb})"
                : $"Working…{kb}";
        }
        else
        {
            status = _copilot.IsConnected ? "Connected" : "Not connected";
        }
        toolStripStatusLabelConnection.Text = status;
        UpdateAgentStatus();
    }

    private void UpdateAgentStatus()
    {
        string msg;
        if (_pendingCount == 0)
        {
            msg = "Ready for next command";
        }
        else if (_activeSubAgents.Count == 0)
        {
            msg = "Working…";
        }
        else
        {
            var names = _activeSubAgents.Values.ToList();
            string agentPart = names.Count switch
            {
                1 => $"Waiting for {names[0]}",
                2 => $"Agents: {names[0]} · {names[1]}",
                3 => $"Agents: {names[0]} · {names[1]} · {names[2]}",
                _ => $"{names.Count} agents active",
            };
            string donePart = _completedAgentCount > 0
                ? $" ({_completedAgentCount} done)"
                : "";
            msg = agentPart + donePart;
        }
        toolStripStatusLabelAgentStatus.Text = msg;
    }

    // Used only by BackupSessionAsync which needs to block the UI exclusively
    private void SetSendingState(bool isSending)
    {
        buttonSend.Enabled = !isSending && _copilot.IsConnected;
        buttonStop.Enabled = isSending || _pendingCount > 0;
        toolStripStatusLabelConnection.Text = isSending ? "Working…" :
            (_copilot.IsConnected ? "Connected" : "Not connected");
    }

    private void UpdateConnectionStatus(string status)
    {
        if (status == "Connected")
        {
            _reconnecting = false;
            UpdateWorkingState();
            _ = ShowCliVersionAsync();
        }
        else
        {
            if (status == "Reconnecting...")
            {
                // Session was lost mid-conversation; clear pending state so the UI
                // doesn't stay stuck on "Working…" while the reconnect happens.
                if (_pendingCount > 0 || _mainSessionId != null)
                    AppendOutput("\r\n⚠️ Session lost — reconnecting…\r\n\r\n", AppTheme.ColorError);

                _reconnecting    = true;
                _pendingCount    = 0;
                _mainSessionId   = null;
                _mainSessionIdle = false;
            }
            else if (status == "Not connected" && _reconnecting)
            {
                // Automatic reconnect attempt failed.
                AppendOutput("\r\n❌ Session lost and reconnect failed. Open a folder to reconnect.\r\n\r\n",
                    AppTheme.ColorError);
                _reconnecting    = false;
                _pendingCount    = 0;
                _mainSessionId   = null;
                _mainSessionIdle = false;
            }

            toolStripStatusLabelConnection.Text = status;
            buttonSend.Enabled = false;
            buttonStop.Enabled = false;
        }
    }

    // ── Permission / input dialogs ────────────────────────────────────────────

    private void Copilot_PermissionRequested(object? sender, PermissionEventArgs args)
    {
        // Called on SDK background thread; Invoke marshals to UI thread.
        if (!IsHandleCreated) { args.Decision.TrySetResult(false); return; }
        Invoke(() =>
        {
            using var dialog = new PermissionDialog(args);
            dialog.ShowDialog(this);
        });
    }

    private void Copilot_UserInputRequested(object? sender, UserInputEventArgs args)
    {
        if (!IsHandleCreated) { args.Answer.TrySetResult(""); return; }
        Invoke(() =>
        {
            using var dialog = new UserInputDialog(args);
            dialog.ShowDialog(this);
        });
    }

    // ── Update checking ───────────────────────────────────────────────────────

    /// <summary>
    /// Checks NuGet for a newer GitHub.Copilot.SDK release and notifies the user
    /// if one is found. Runs asynchronously at startup without blocking the UI.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        var current = UpdateChecker.GetCurrentSdkVersion();
        string? latest;

        try
        {
            latest = await UpdateChecker.GetLatestSdkVersionAsync();
        }
        catch
        {
            latest = null;
        }

        if (latest == null)
        {
            AppendOutput($"[SDK v{current} — update check unavailable]\r\n", AppTheme.ColorMeta);
            return;
        }

        if (UpdateChecker.IsNewer(current, latest))
        {
            AppendOutput(
                $"[Update available: SDK v{current} -> v{latest}]\r\n",
                AppTheme.ColorTool);

            using var dlg = new UpdateNotificationDialog(current, latest);
            dlg.ShowDialog(this);
        }
        else
        {
            AppendOutput($"[SDK v{current} — up to date]\r\n", AppTheme.ColorMeta);
        }
    }

    /// <summary>Displays the running Copilot CLI version in the output panel after a connection.</summary>
    private async Task ShowCliVersionAsync()
    {
        var version = await _copilot.GetVersionAsync();
        if (!string.IsNullOrEmpty(version))
            AppendOutput($"[Copilot CLI v{version}]\r\n", AppTheme.ColorMeta);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (_skipCloseBackupPrompt) return;
        if (!_copilot.IsConnected || _mainSessionId == null) return;

        var result = MessageBox.Show(
            "Would you like to save a Backup before closing?\r\n\r\n" +
            "A backup lets you resume this session later.",
            "Save Backup?",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            e.Cancel = true;
            _ = BackupThenCloseAsync();
        }
    }

    private async Task BackupThenCloseAsync()
    {
        await BackupSessionAsync();
        _skipCloseBackupPrompt = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _audio.Dispose();
        _ = _copilot.DisposeAsync().AsTask();
    }
}
