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
    private readonly PromptHistory  _promptHistory = new();
    private readonly List<string> _attachments = new();
    private readonly HashSet<string> _streamingSessions = new();
    // Structured output blocks feeding the WebView2 Rendered tab
    private readonly List<OutputBlock> _outputBlocks = new();
    private bool _webViewReady = false;
    // Rolling meta block used by AppendOutput so plain-text status/setup lines
    // appear in the Rendered tab as well as the Raw tab. Reset (closed) whenever
    // an explicit non-meta WebView block is appended or the output is cleared.
    private OutputBlock? _currentMetaBlock;
    private Color _currentMetaColor;
    // Maps toolCallId → char offset AFTER "  🔧 name  args" text (insertion point for ✓/✗)
    private readonly Dictionary<string, int> _toolStartPositions = new();
    // Maps toolCallId → char offset of the ○ character for retroactive ○→◉/✗ replacement
    private readonly Dictionary<string, int> _subAgentStartPositions = new();
    // Maps sub-agent toolCallId → display name for currently active (in-flight) sub-agents
    private readonly Dictionary<string, string> _activeSubAgents = new();
    // Live sub-agent SESSION ids reported via session.created/session.deleted
    // lifecycle events. Used as a backstop signal that a sub-agent has truly
    // ended even when its paired subagent.completed/failed event is missed.
    private readonly HashSet<string> _activeSubAgentSessions = new();
    // Watchdog that fires if the main session has been idle awaiting sub-agents
    // for an extended period without any sub-agent completion/lifecycle activity.
    // Guards against the UI getting stuck on "Working..." when a subagent.completed
    // event is dropped or never delivered.
    private readonly System.Windows.Forms.Timer _subAgentWatchdog = new() { Interval = 60_000 };
    private int _completedAgentCount = 0;
    // True when the main session went idle but sub-agents are still running (Fleet mode);
    // completion is deferred until the last sub-agent finishes.
    private bool _mainSessionIdle = false;
    private double _totalBytesReceived = 0;
    private string? _mainSessionId;
    // Tracks whether the main session is currently working. The SDK's
    // session.idle event is the authoritative "done" signal: when it fires for
    // the main session (and no live sub-agents remain), this is reset to 0.
    // Increments on each Dispatch are best-effort bookkeeping for display only;
    // never rely on the count being exact across queued sends.
    private int _pendingCount = 0;
    private bool _reconnecting = false; // true while an automatic reconnect is in progress
    private bool _autoRefreshPromptShown = false; // suppresses the 85% nag once per session
    private bool _refreshInProgress = false; // gate to prevent overlapping Compact/Restart calls
    private bool _cliUpdateChecked = false; // ensures the npm CLI check runs only once per session
    // Reason a deferred handoff is pending (e.g. mode/fleet change). When non-null,
    // the next DispatchPromptAsync runs an automatic summary-and-restart before
    // forwarding the user's prompt, so context survives the option change.
    private string? _pendingHandoffReason = null;
    private const int AutoRefreshThresholdPercent = 85;
    private KopilotSettings _settings = new();
    private readonly SessionMetadataStore _sessionStore = new();


    private readonly string? _startupFolder;

    public MainForm() : this(null) { }

    public MainForm(string? startupFolder)
    {
        _startupFolder = startupFolder;
        InitializeComponent();
        WireUpEvents();

        // Load persisted settings and sync with service
        _settings = KopilotSettings.Load();
        _copilot.SkillTreeFolders = _settings.SkillTreeFolders;
        UpdateSkillTreeTooltip();

        // Populate mode combo from the SDK enum and select the first entry
        PopulateModeCombo();
        // Sync service with the UI defaults set above
        _copilot.AutoApprove = checkBoxAutoApprove.Checked;
        _copilot.FleetMode   = checkBoxFleet.Checked;

        // A3: seed the meter with a visible "starting state" so the affordance
        // is discoverable before the first AssistantUsageEvent arrives.
        OnContextUsageChanged(new ContextUsageEventArgs());

        // Apply the dark renderer to ALL ToolStrips (MenuStrip, DropDowns,
        // StatusStrip) via the global manager so dropdowns inherit it too.
        ToolStripManager.Renderer = new DarkMenuRenderer();
    }

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireUpEvents()
    {
        buttonSend.Click += async (_, _) => await SendPromptAsync();
        buttonStop.Click += async (_, _) => await StopAsync();
        menuReferencesAddFile.Click += ButtonAddFile_Click;
        menuReferencesAddFolder.Click += ButtonAddFolder_Click;
        buttonOpenFolder.Click += async (_, _) => await OpenFolderAndConnectAsync();
        buttonHistoryPrev.Click += (_, _) => NavigateHistoryBack();
        buttonHistoryNext.Click += (_, _) => NavigateHistoryForward();
        richTextBoxPrompt.KeyDown += RichTextBoxPrompt_KeyDown;
        richTextBoxPrompt.FilesDropped += (_, paths) =>
        {
            foreach (var path in paths)
                AddAttachment(path);
		};
        AttachPromptContextMenu();

        this.Shown += async (_, _) =>
        {
            await InitializeWebViewAsync();
            await CheckForUpdatesAsync();
            await PopulateModelsAsync();

            if (!string.IsNullOrEmpty(_startupFolder))
                await ConnectToFolderAsync(_startupFolder);
        };

        menuHelpShow.Click += (_, _) => ShowHelpAsync();
        menuHelpAbout.Click += (_, _) =>
        {
            using var dlg = new AboutDialog();
            dlg.ShowDialog(this);
        };
        menuToolsPowershell.Click += (_, _) => OpenPowershell();
        menuSessionSummarize.Click += async (_, _) => await SendQuickCommandAsync(
            "Please provide a concise summary of what we've discussed and accomplished so far in this session.");
        menuSessionClear.Click += (_, _) => ClearActiveOutput();
        menuSessionRefreshCompact.Click += async (_, _) => await RunCompactAsync();
        menuSessionRefreshRestart.Click += async (_, _) => await RunRestartWithSummaryAsync();
        menuSessionRefreshFresh.Click += async (_, _) => await RunFreshStartAsync();
        menuSessionPast.Click += async (_, _) => await BrowsePastSessionsAsync();
        menuToolsExplorer.Click += (_, _) => OpenExplorer();
        menuToolsVSCode.Click += (_, _) => OpenVSCode();
        menuSkillsTree.Click += (_, _) => EditSkillTree();
        menuSkillsListAgents.Click += (_, _) => ShowAgentList();
        menuSkillsListSkills.Click += (_, _) => ShowSkillList();

        checkBoxAutoApprove.CheckedChanged += (_, _) =>
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;

        checkBoxFleet.CheckedChanged += (_, _) =>
        {
            _copilot.FleetMode = checkBoxFleet.Checked;
            if (_copilot.IsConnected && _mainSessionId != null)
            {
                var state = checkBoxFleet.Checked ? "enabled" : "disabled";
                ScheduleHandoff($"Fleet {state}");
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

        _copilot.SubAgentSessionEnded += (_, sessionId) =>
            InvokeOnUI(() => OnSubAgentSessionEnded(sessionId));

        _subAgentWatchdog.Tick += (_, _) =>
        {
            _subAgentWatchdog.Stop();
            if (_mainSessionIdle)
                ForceCompleteStaleSubAgents("watchdog timeout");
        };

        _copilot.PermissionRequested += Copilot_PermissionRequested;
        _copilot.UserInputRequested += Copilot_UserInputRequested;

        _copilot.ContextUsageChanged += (_, args) =>
            InvokeOnUI(() => OnContextUsageChanged(args));
    }

    private void InvokeOnUI(Action action)
    {
        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(action);
        else
            action();
    }

    // ── WebView2 initialization ──────────────────────────────────────────────

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                _copilot.WorkingDirectory ?? AppContext.BaseDirectory,
                ".kopilot", "webview2-data");
            Directory.CreateDirectory(userDataFolder);

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataFolder);
            await webViewOutput.EnsureCoreWebView2Async(env);

            // Navigate to the bundled output.html
            var htmlPath = Path.Combine(AppContext.BaseDirectory, "web", "output.html");
            if (File.Exists(htmlPath))
            {
                webViewOutput.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                _webViewReady = true;
            }
            else
            {
                // Fall back to Raw tab if assets are missing
                tabControlOutput.SelectedTab = tabPageRaw;
            }
        }
        catch
        {
            // WebView2 runtime not available — fall back to Raw tab
            tabControlOutput.SelectedTab = tabPageRaw;
            tabControlOutput.TabPages.Remove(tabPageRendered);
        }
    }

    // ── Tab control dark theme drawing ───────────────────────────────────────

    private void TabControlOutput_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var tabCtrl = (TabControl)sender!;
        var page = tabCtrl.TabPages[e.Index];
        var bounds = tabCtrl.GetTabRect(e.Index);

        bool isSelected = tabCtrl.SelectedIndex == e.Index;
        var bgColor = isSelected ? AppTheme.OutputBox : AppTheme.Surface;
        var fgColor = isSelected ? AppTheme.TextPrimary : AppTheme.TextMuted;

        using var bgBrush = new SolidBrush(bgColor);
        using var fgBrush = new SolidBrush(fgColor);

        // Inflate generously to overpaint the bright visual-styles border
        // that the system renderer draws around each tab header. Extend
        // further downward so the white seam between the tab and the
        // content area is also covered for both selected and unselected tabs.
        var fillRect = bounds;
        fillRect.Inflate(3, 3);
        fillRect.Y      -= 2;   // extend upward to cover the top-edge border
        fillRect.Height += 6;   // extend downward to cover the tab-to-content seam
        e.Graphics!.SetClip(fillRect);
        e.Graphics.FillRectangle(bgBrush, fillRect);
        e.Graphics.ResetClip();

        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        e.Graphics.DrawString(page.Text, tabCtrl.Font, fgBrush, bounds, sf);
    }

    // ── Combo population ──────────────────────────────────────────────────────

    // Maps each SDK mode enum value to the display name used throughout the app.
    private static readonly Dictionary<SessionMode, string> _modeDisplayNames =
        new()
        {
            [SessionMode.Interactive] = "Standard",
            [SessionMode.Plan]        = "Plan",
            [SessionMode.Autopilot]   = "Autopilot",
        };

    /// <summary>
    /// Populates comboBoxMode from the SDK SessionMode enum values,
    /// using display names that match the rest of the app (Interactive -> "Standard").
    /// </summary>
    private void PopulateModeCombo()
    {
        comboBoxMode.Items.Clear();
        foreach (var mode in Enum.GetValues<SessionMode>())
        {
            var display = _modeDisplayNames.TryGetValue(mode, out var name) ? name : mode.ToString();
            comboBoxMode.Items.Add(display);
        }
        comboBoxMode.SelectedIndex = 0;
    }

    /// <summary>
    /// Queries the Copilot SDK for available models and populates comboBoxModel.
    /// Selects the highest-available Claude Opus model by default, falling back to
    /// the highest-available Claude Sonnet model if no Opus model is present.
    /// Falls back to the service's current ActiveModel if the SDK returns nothing.
    /// </summary>
    private async Task PopulateModelsAsync()
    {
        var ids = await _copilot.ListModelsAsync();

        // If the SDK returned nothing (e.g. not yet authenticated), seed the
        // combo with the model the service is already configured to use so the
        // dropdown is never left empty.
        if (ids.Count == 0)
        {
            if (comboBoxModel.Items.Count == 0)
            {
                comboBoxModel.Items.Add(_copilot.ActiveModel);
                comboBoxModel.SelectedIndex = 0;
            }
            return;
        }

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

        var items = comboBoxModel.Items.Cast<string>()
            .Select((m, i) => (model: m, idx: i))
            .ToList();

        var opusIdx = items
            .Where(x => x.model.StartsWith("claude-opus", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.model)
            .Select(x => (int?)x.idx)
            .FirstOrDefault();

        var defaultIdx = opusIdx ?? items
            .Where(x => x.model.StartsWith("claude-sonnet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.model)
            .Select(x => (int?)x.idx)
            .FirstOrDefault() ?? 0;

        comboBoxModel.SelectedIndex = defaultIdx;
    }



    private async Task SendPromptAsync(bool recordHistory = true)
    {
        var prompt = richTextBoxPrompt.Text.Trim();
        var pastedImages = ExtractEmbeddedImagesToTemp(richTextBoxPrompt.Rtf);

        if (string.IsNullOrEmpty(prompt) && pastedImages.Count == 0) return;

        if (recordHistory)
        {
            _promptHistory.Add(prompt);
            UpdateHistoryButtons();
        }
        richTextBoxPrompt.Clear();

        await DispatchPromptAsync(prompt, pastedImages);
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
            "3. Attach files or folders via the References menu (or right-click the prompt).\r\n" +
            "4. Use ▲ / ▼ on the left edge of the prompt to navigate history.\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Toolbar controls ─────────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  Open Folder… – Connect Copilot to a project directory\r\n" +
            "  Model        – Choose the AI model (GPT-4.1, Claude Sonnet/Opus, …)\r\n" +
            "  Mode         – Standard | Plan (plan before acting) | Autopilot (fully autonomous)\r\n" +
            "  Fleet ☐      – Spawn parallel sub-agents for large tasks\r\n" +
            "  Auto-approve – Skip permission prompts; approve all tool operations automatically\r\n" +
            "  Send         – Submit the current prompt (Ctrl+Enter)\r\n" +
            "  Stop         – Cancel an in-progress response\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Menu bar ──────────────────────────────────────────\r\n", AppTheme.ColorMeta);

        AppendOutput("  Session\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "    📝 Summarize      Ask Copilot for a session summary\r\n" +
            "    🗑 Clear Output   Clear the output panel (session not reset)\r\n" +
            "    💤 Refresh ▸      Free context window:\r\n" +
            "       ⚡ Compact        In-place compaction; session ID preserved\r\n" +
            "       🔄 Restart        Save dream file + start fresh session w/ summary\r\n" +
            "       🆕 Fresh start    Discard all context; new session, same folder\r\n" +
            "    📋 Past Sessions… Browse persisted sessions to resume or delete\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Skills & Agents\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "    List Agents…      Pick a custom agent; inserts @agent:name at caret\r\n" +
            "    List Skills…      Pick a skill; inserts @skill:name at caret\r\n" +
            "    🌳 Skill Tree…    Edit folders contributing skills/ and agents/\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  References\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "    📄 Add File…      Attach one or more files to the next prompt\r\n" +
            "    📁 Add Folder…    Attach a folder to the next prompt\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Tools\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "    ⚡ PowerShell     Open terminal in the project folder\r\n" +
            "    📂 File Explorer  Open File Explorer at the project folder\r\n" +
            "    💻 VS Code        Launch VS Code in the project folder\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("  Help\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "    ❓ Show Help      Show this guide (works without a folder open)\r\n" +
            "    About Kopilot    Version, build info, and credits\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Prompt box ────────────────────────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  • Ctrl+Enter          Send the prompt\r\n" +
            "  • Drag & drop         Drop files or folders onto the box to attach\r\n" +
            "  • Paste images        Clipboard images are saved and attached on send\r\n" +
            "  • ▲ / ▼               Navigate prompt history (left edge buttons)\r\n" +
            "  • Right-click menu    Add File… · Add Folder… · List Agents… · List Skills…\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── Reference tokens (inserted at the caret) ──────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  @relative/path/file   Attach a file (chip also appears above output)\r\n" +
            "  @relative/path/folder Attach a folder\r\n" +
            "  @agent:name           Reference a custom agent\r\n" +
            "  @skill:name           Reference a skill\r\n\r\n",
            AppTheme.ColorDefault);

        AppendOutput("── When Copilot asks permission ──────────────────────\r\n", AppTheme.ColorMeta);
        AppendOutput(
            "  ✓ Allow           – Approve this one operation\r\n" +
            "  ✓ Approve Similar – Approve all operations of this type for the session\r\n" +
            "  ✗ Deny            – Reject; Copilot will adjust\r\n\r\n",
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
            "  • Session ▸ 📝 Summarize often to capture progress.\r\n" +
            "  • Session ▸ 📋 Past Sessions… to resume an earlier conversation.\r\n" +
            "  • Watch the Context meter in the status bar. At 85% Kopilot will offer to refresh.\r\n\r\n",
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

    private async Task DispatchPromptAsync(string prompt, IReadOnlyList<string>? extraAttachments = null)
    {
        _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString()  ?? "Standard";
        _copilot.AutoApprove = checkBoxAutoApprove.Checked;
        _copilot.FleetMode   = checkBoxFleet.Checked;

        // If a UI option change scheduled a deferred handoff, run it now BEFORE
        // the user's prompt so context survives mode/fleet switches automatically.
        if (_pendingHandoffReason != null
            && _copilot.IsConnected
            && _mainSessionId != null
            && !_refreshInProgress)
        {
            var reason = _pendingHandoffReason;
            _pendingHandoffReason = null;
            await PerformHandoffAsync(reason, waitForSeedAck: true);
        }

        _pendingCount++;
        UpdateWorkingState();
        SoundService.PlayPromptSent();

        try
        {
            var attachmentsCopy = _attachments.ToList();
            if (extraAttachments is { Count: > 0 })
                attachmentsCopy.AddRange(extraAttachments);
            await _copilot.SendMessageAsync(prompt, attachmentsCopy);

            // Echo user message
            if (_mainSessionId != null)
            {
                SetSessionDescriptionIfEmpty(_mainSessionId, prompt);
                AppendOutput($"\U0001f464 You: {prompt}\r\n\r\n", AppTheme.ColorUser);
                var userBlock = new OutputBlock(BlockKind.User)
                {
                    Label = "\U0001f464 You:",
                    Content = prompt,
                    IsComplete = true
                };
                _outputBlocks.Add(userBlock);
                WebViewAppendBlock(userBlock);
            }
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
        if (richTextBoxOutput.TextLength == 0 && _outputBlocks.Count == 0) return;
        if (MessageBox.Show("Clear all output?", "Clear Output",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            richTextBoxOutput.Clear();
            _toolStartPositions.Clear();
            _subAgentStartPositions.Clear();
            _activeSubAgents.Clear();
            _activeSubAgentSessions.Clear();
            _streamingSessions.Clear();
            _subAgentWatchdog.Stop();
            _completedAgentCount = 0;
            _mainSessionIdle = false;
            // Clear the Rendered tab
            _outputBlocks.Clear();
            _streamingBlocks.Clear();
            _renderedToolBlocks.Clear();
            _renderedSubAgentBlocks.Clear();
            WebViewClearAll();
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

            var fileName = ResolvePowerShellExecutable();

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
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

    private static string ResolvePowerShellExecutable()
    {
        // Prefer PowerShell 7+ (pwsh.exe) so users get their modern profile (and
        // modules like posh-git that are typically installed there). Fall back to
        // the in-box Windows PowerShell 5.1 if pwsh isn't available.
        var pathExt = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathExt.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir.Trim(), "pwsh.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }
        return "powershell.exe";
    }

    private void OpenVSCode()
    {
        // Note: the Copilot CLI's /ide command (which pairs the running session with
        // VS Code's Copilot extension so it can surface diffs and open files) is
        // documented as TUI-only in the Copilot SDK. There is no SDK RPC equivalent,
        // and slash commands are not dispatched through the prompt channel. This
        // button therefore only launches VS Code; live IDE pairing is not available
        // through Kopilot today.
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
        }
    }

    private async Task ApplyModeChangeAsync()
    {
        var mode = comboBoxMode.SelectedItem?.ToString() ?? "Standard";
        _copilot.ActiveMode = mode;

        // Autopilot implies auto-approve
        if (mode == "Autopilot" && !checkBoxAutoApprove.Checked)
            checkBoxAutoApprove.Checked = true;

        // Mode is baked into the session's system message at creation time.
        // Defer the session reset to the next send so we can summarise the
        // current conversation and seed it into the new session automatically.
        if (_copilot.IsConnected && _mainSessionId != null)
            ScheduleHandoff($"Mode changed to {mode}");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Marks the current session for an automatic summary-and-restart on the
    /// next prompt dispatch. Idempotent — repeated changes overwrite the reason.
    /// </summary>
    private void ScheduleHandoff(string reason)
    {
        _pendingHandoffReason = reason;
        AppendOutput(
            $"\r\n[{reason} — context will be summarised and carried into a new session on your next send]\r\n\r\n",
            AppTheme.ColorMeta);
    }

    // ── Context meter & session refresh ──────────────────────────────────────

    private void OnContextUsageChanged(ContextUsageEventArgs args)
    {
        var input = args.InputTokens;
        var max   = args.MaxPromptTokens;
        var pct   = args.Percent;

        string text;
        Color color;
        int barValue;
        Color barColor;

        if (max <= 0)
        {
            text     = "Context: \u2014 / \u2014 (\u2014)";
            color    = Color.FromArgb(148, 148, 148);
            barValue = 0;
            barColor = Color.FromArgb(96, 96, 96);
        }
        else if (input <= 0)
        {
            text     = $"Context: 0 / {FormatTokens(max)} (0%)";
            color    = Color.FromArgb(148, 220, 148); // green
            barValue = 0;
            barColor = Color.FromArgb(148, 220, 148);
        }
        else
        {
            text = $"Context: {FormatTokens(input)} / {FormatTokens(max)} ({pct:0}%)";
            color = pct < 60  ? Color.FromArgb(148, 220, 148)  // green
                  : pct < 85  ? Color.FromArgb(232, 200, 110)  // amber
                              : Color.FromArgb(240, 120, 120); // red
            barColor = color;
            barValue = (int)Math.Clamp(Math.Round(pct), 0, 100);
        }

        toolStripStatusLabelContext.Text      = text;
        toolStripStatusLabelContext.ForeColor = color;
        toolStripProgressBarContext.Value     = barValue;
        toolStripProgressBarContext.ForeColor = barColor;
        toolTipMain.SetToolTip(statusStrip,
            max > 0
                ? $"Context window usage: {input:N0} / {max:N0} prompt tokens ({pct:0.0}%)\nClick \ud83d\udca4 Refresh to free space."
                : "Context window usage will appear here after the first response.");

        UpdateRefreshButtonAffordance(max > 0 ? pct : 0);

        // Auto-prompt at the configured threshold, once per session, and only when
        // we are not already mid-refresh or sending a turn.
        if (!_autoRefreshPromptShown
            && !_refreshInProgress
            && _pendingCount == 0
            && pct >= AutoRefreshThresholdPercent
            && _copilot.IsConnected
            && _mainSessionId != null)
        {
            _autoRefreshPromptShown = true;
            // Defer to avoid blocking the event handler.
            BeginInvoke(new Action(PromptAutoRefresh));
        }
    }

    private static string FormatTokens(double tokens)
    {
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000:0.#}M";
        if (tokens >= 1_000)     return $"{tokens / 1_000:0.#}K";
        return ((int)tokens).ToString();
    }

    // Decorates the Refresh button with a glyph and tooltip that escalates with
    // context-window pressure: idle (\ud83d\udca4) below 60%, warning (\u26a0\ufe0f)
    // at 60%, critical (\ud83d\udd25) at 85%. Keeps the trailing dropdown caret.
    private void UpdateRefreshButtonAffordance(double pct)
    {
        string glyph;
        string tip;

        if (pct >= 85)
        {
            glyph = "\ud83d\udd25"; // fire
            tip   = $"Context at {pct:0}% \u2014 strongly recommend Compact or Restart now.";
        }
        else if (pct >= 60)
        {
            glyph = "\u26a0\ufe0f"; // warning sign
            tip   = $"Context at {pct:0}% \u2014 consider Compact or Restart soon.";
        }
        else
        {
            glyph = "\ud83d\udca4"; // zzz (idle)
            tip   = "Free up context window \u2014 Compact (in place) or Restart with summary";
        }

        var newText = $"{glyph} Refresh";
        if (!string.Equals(menuSessionRefresh.Text, newText, StringComparison.Ordinal))
            menuSessionRefresh.Text = newText;
        menuSessionRefresh.ToolTipText = tip;
    }

    private void PromptAutoRefresh()
    {
        var pct = _copilot.CurrentMaxPromptTokens > 0
            ? (_copilot.CurrentInputTokens / _copilot.CurrentMaxPromptTokens) * 100.0
            : 0;

        var result = MessageBox.Show(this,
            $"Context window is at {pct:0}% — accuracy may start to degrade.\r\n\r\n" +
            "Refresh now?\r\n\r\n" +
            "  Yes  – Compact in place (fast, keeps session ID)\r\n" +
            "  No   – Restart with summary (clean window, new session)\r\n" +
            "  Cancel – Don't ask again this session",
            "Context Window Filling Up",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1);

        switch (result)
        {
            case DialogResult.Yes:
                _ = RunCompactAsync();
                break;
            case DialogResult.No:
                _ = RunRestartWithSummaryAsync();
                break;
            // Cancel: stay silent for the rest of the session.
        }
    }

    private void ShowRefreshMenu()
    {
        // Programmatic equivalent of the menu drop-down (kept for any callers that
        // want to surface the same options outside the menu bar). Drops the
        // submenu open at the cursor.
        menuSessionRefresh.ShowDropDown();
    }

    /// <summary>
    /// Discards the current session entirely — no summary, no seed — and opens
    /// a fresh session rooted at the same folder.  Behaves like clicking
    /// Open Folder on the existing path: reconnects, then offers to read the
    /// project's README.
    /// </summary>
    private async Task RunFreshStartAsync()
    {
        if (_refreshInProgress) return;
        if (_copilot.WorkingDirectory == null)
        {
            MessageBox.Show(this, "No active workspace. Open a folder first.",
                "Nothing to Refresh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            "Start a brand-new session in this folder?\r\n\r\n" +
            "All current conversation context will be discarded — no summary will be carried over.\r\n\r\n" +
            "You will be offered the chance to have the README read, " +
            "just like opening the folder fresh.",
            "Fresh Start", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.OK) return;

        _refreshInProgress = true;
        var oldText = menuSessionRefresh.Text;
        menuSessionRefresh.Enabled = false;
        menuSessionRefresh.Text = "⏳ Fresh start…";
        SetSendingState(true);

        var workingDir = _copilot.WorkingDirectory;

        try
        {
            AppendOutput("\r\n🆕 Discarding context and opening a fresh session…\r\n\r\n", AppTheme.ColorMeta);

            if (_copilot.IsConnected)
                await _copilot.ResetSessionAsync();

            _mainSessionId   = null;
            ResetSessionTrackingState();

            // Sync UI state to service before session creation so the system message is correct
            _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString() ?? "Standard";
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;
            _copilot.FleetMode   = checkBoxFleet.Checked;

            await _copilot.EnsureSessionAsync();

            AppendOutput("─────────── session refreshed ───────────\r\n\r\n", AppTheme.ColorMeta);
            _autoRefreshPromptShown = false;

            await OfferReadReadmeAsync(workingDir);
        }
        catch (Exception ex)
        {
            AppendOutput($"[Fresh start failed: {ex.Message}]\r\n\r\n", AppTheme.ColorError);
            MessageBox.Show(this, $"Fresh start failed:\r\n\r\n{ex.Message}",
                "Refresh Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            menuSessionRefresh.Text = oldText;
            menuSessionRefresh.Enabled = true;
            SetSendingState(false);
            _refreshInProgress = false;
        }
    }

    private async Task RunCompactAsync()
    {
        if (_refreshInProgress) return;
        if (!_copilot.IsConnected || _mainSessionId == null)
        {
            MessageBox.Show(this, "No active session to refresh. Open a folder and send at least one message first.",
                "Nothing to Refresh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _refreshInProgress = true;
        var oldText = menuSessionRefresh.Text;
        menuSessionRefresh.Enabled = false;
        menuSessionRefresh.Text = "⏳ Compacting…";
        SetSendingState(true);

        try
        {
            AppendOutput("\r\n💤 Compacting session in place…\r\n\r\n", AppTheme.ColorMeta);

            var ok = await _copilot.CompactSessionAsync();
            if (ok)
            {
                AppendOutput("─────────── session refreshed ───────────\r\n\r\n", AppTheme.ColorMeta);
                _autoRefreshPromptShown = false; // allow another nag if context fills again
            }
            else
            {
                AppendOutput(
                    "[Compact failed — falling back to Restart with summary]\r\n\r\n",
                    AppTheme.ColorMeta);
                _refreshInProgress = false; // RunRestart will reacquire the gate
                menuSessionRefresh.Text = oldText;
                menuSessionRefresh.Enabled = true;
                SetSendingState(false);
                await RunRestartWithSummaryAsync();
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Compact failed:\r\n\r\n{ex.Message}",
                "Refresh Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            menuSessionRefresh.Text = oldText;
            menuSessionRefresh.Enabled = true;
            SetSendingState(false);
            _refreshInProgress = false;
        }
    }

    // Shared summary prompt for both manual and automatic handoffs. The model is
    // told NOT to use tools so the capture is a pure conversation distillation.
    private const string HandoffSummaryPrompt =
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

    private const string HandoffSeedPrompt =
        "I am providing a session resume document from our previous session in this same workspace. " +
        "Please read it and confirm you understand the context so we can continue where we left off.";

    private Task RunRestartWithSummaryAsync()
        => PerformHandoffAsync("user requested", waitForSeedAck: false);

    /// <summary>
    /// Captures a conversation summary from the current session, persists it to
    /// <c>.kopilot\dreams\</c>, tears the session down, opens a fresh one in the
    /// same workspace, and seeds it with the summary.
    /// </summary>
    /// <param name="reason">Short human-readable trigger (shown in the output panel).</param>
    /// <param name="waitForSeedAck">When true, awaits the assistant's acknowledgement
    /// of the seed before returning.  Used by automatic handoffs so the user's next
    /// prompt arrives after the new session has loaded the context.</param>
    /// <returns>True on success.  On failure the active session is reset so the next
    /// send still works (without preserved context).</returns>
    private async Task<bool> PerformHandoffAsync(string reason, bool waitForSeedAck)
    {
        if (_refreshInProgress) return false;
        if (!_copilot.IsConnected || _mainSessionId == null || _copilot.WorkingDirectory == null)
        {
            // Caller-driven UI flow (manual button) shows a dialog; automatic flow stays silent.
            if (!waitForSeedAck)
            {
                MessageBox.Show(this, "No active session to refresh. Open a folder and send at least one message first.",
                    "Nothing to Refresh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return false;
        }

        _refreshInProgress = true;
        var oldText = menuSessionRefresh.Text;
        menuSessionRefresh.Enabled = false;
        menuSessionRefresh.Text = "⏳ Handoff…";
        SetSendingState(true);

        try
        {
            AppendOutput($"\r\n💤 Capturing session summary — {reason}…\r\n\r\n", AppTheme.ColorMeta);

            var summary = await _copilot.SendAndCaptureResponseAsync(HandoffSummaryPrompt, TimeSpan.FromMinutes(5));

            // Persist the dream alongside manual backups but in its own subfolder
            // so OfferLoadBackupAsync (which scans .kopilot\*.md non-recursively)
            // does not mistake it for a between-session resume.
            var dreamsDir = Path.Combine(_copilot.WorkingDirectory, ".kopilot", "dreams");
            Directory.CreateDirectory(dreamsDir);
            var dreamPath = Path.Combine(dreamsDir, $"dream-{DateTime.Now:yyyy-MM-dd-HHmmss}.md");

            var model = comboBoxModel.SelectedItem?.ToString() ?? string.Empty;
            var mode  = comboBoxMode.SelectedItem?.ToString()  ?? string.Empty;
            var metadata =
                $"\r\n<!-- kopilot-model: {model} -->\r\n" +
                $"<!-- kopilot-mode: {mode} -->\r\n" +
                $"<!-- kopilot-source: dream -->\r\n" +
                $"<!-- kopilot-reason: {reason} -->\r\n";

            await File.WriteAllTextAsync(dreamPath, summary + metadata);

            AppendOutput($"[Dream saved: {dreamPath}]\r\n", AppTheme.ColorMeta);
            AppendOutput("💤 Opening fresh session in this workspace…\r\n\r\n", AppTheme.ColorMeta);

            // Tear down + recreate so the new session honours the latest mode/fleet.
            await _copilot.ResetSessionAsync();
            await _copilot.EnsureSessionAsync();

            // Seed the new session.  When the caller is about to send a user prompt,
            // we wait for the assistant's ACK so the prompt is interpreted with full context.
            var seedFull = HandoffSeedPrompt + "\r\n\r\n" + summary;
            if (waitForSeedAck)
                await _copilot.SendAndCaptureResponseAsync(seedFull, TimeSpan.FromMinutes(2));
            else
                await _copilot.SendMessageAsync(seedFull, Array.Empty<string>());

            AppendOutput("─────────── session refreshed ───────────\r\n\r\n", AppTheme.ColorMeta);
            _autoRefreshPromptShown = false;
            return true;
        }
        catch (Exception ex)
        {
            AppendOutput($"[Handoff failed: {ex.Message} — falling back to clean restart]\r\n\r\n", AppTheme.ColorError);
            // Fall back to a plain reset so the user's next send still works,
            // even though context will be lost.
            try
            {
                await _copilot.ResetSessionAsync();
                _mainSessionId   = null;
                ResetSessionTrackingState();
            }
            catch { /* best-effort */ }
            return false;
        }
        finally
        {
            menuSessionRefresh.Text = oldText;
            menuSessionRefresh.Enabled = true;
            SetSendingState(false);
            _refreshInProgress = false;
        }
    }

    /// <summary>
    /// When a workspace folder is opened, look for README.md (preferred) then
    /// README.txt in the project root. If found, prompt the user for permission
    /// to read it so Kopilot can better understand the project. The prompt
    /// includes an option to first preview the file in VS Code.
    /// </summary>
    private async Task OfferReadReadmeAsync(string projectRoot)
    {
        if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot)) return;

        // Priority order: README.md before README.txt. Windows file system
        // lookups are case-insensitive, so this also matches Readme.md, etc.
        string? readmePath = null;
        foreach (var name in new[] { "README.md", "README.txt" })
        {
            var candidate = Path.Combine(projectRoot, name);
            if (File.Exists(candidate))
            {
                readmePath = candidate;
                break;
            }
        }

        if (readmePath == null) return;

        var fileName = Path.GetFileName(readmePath);

        while (true)
        {
            using var dialog = new ReadmePromptDialog(fileName);
            dialog.ShowDialog(this);

            switch (dialog.Result)
            {
                case ReadmePromptResult.Yes:
                    string content;
                    try
                    {
                        content = await File.ReadAllTextAsync(readmePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this,
                            $"Could not read {fileName}:\r\n\r\n{ex.Message}",
                            "Read Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    AppendOutput($"[Sharing {fileName} with Copilot for project context]\r\n\r\n",
                        AppTheme.ColorMeta);

                    await DispatchPromptAsync(
                        $"Here is the contents of '{fileName}' from the workspace root. " +
                        "Please read it to better understand the project we are working on, " +
                        "then briefly confirm you have done so.\r\n\r\n" +
                        $"```\r\n{content}\r\n```");
                    return;

                case ReadmePromptResult.OpenInVSCode:
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "code",
                            Arguments = $"\"{readmePath}\"",
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this,
                            $"Could not open {fileName} in VS Code:\r\n\r\n{ex.Message}\r\n\r\n" +
                            "Make sure the 'code' command is on your PATH.",
                            "VS Code Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    // Re-prompt so the user can decide after previewing.
                    continue;

                case ReadmePromptResult.No:
                default:
                    return;
            }
        }
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

        await ConnectToFolderAsync(dialog.SelectedPath);
    }

    private async Task ConnectToFolderAsync(string folderPath)
    {
        // If already connected, tear down first so the new CWD takes effect
        if (_copilot.IsConnected)
        {
            await _copilot.DisposeAsync();
            _copilot.Reset();
        }

        ResetSessionTrackingState();

        _copilot.WorkingDirectory = folderPath;
        buttonOpenFolder.Enabled = false;
        toolStripStatusLabelSession.Text = folderPath;
        UpdateTitleBar(folderPath);

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

            await OfferReadReadmeAsync(folderPath);
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

    // ── Session persistence ──────────────────────────────────────────────────

    /// <summary>
    /// Records the current UI settings against the given session ID so the
    /// session can be restored with the correct workspace, model, and mode.
    /// </summary>
    private void SaveSessionMetadata(string sessionId)
    {
        var existing = _sessionStore.Find(sessionId);

        // Prefer the live WorkingDirectory, but never overwrite a previously
        // stored non-empty workspace with an empty one (sessions can be
        // created before a folder is opened, and we don't want to lose the
        // workspace later when SaveSessionMetadata is re-invoked with a
        // null WorkingDirectory).
        var liveWorkspace = _copilot.WorkingDirectory ?? "";
        var workspace = !string.IsNullOrEmpty(liveWorkspace)
            ? liveWorkspace
            : existing?.WorkspaceFolder ?? "";

        _sessionStore.Save(new SessionMetadataEntry
        {
            SessionId       = sessionId,
            WorkspaceFolder = workspace,
            Model           = comboBoxModel.SelectedItem?.ToString() ?? "",
            Mode            = comboBoxMode.SelectedItem?.ToString()  ?? "Standard",
            Fleet           = checkBoxFleet.Checked,
            AutoApprove     = checkBoxAutoApprove.Checked,
            CreatedAt       = existing?.CreatedAt is { } prior && prior != default
                ? prior
                : DateTime.Now,
            Description     = existing?.Description ?? "",
        });
    }

    /// <summary>
    /// Records a brief description (typically the first user prompt) against
    /// the given session so it can be shown in the Past Sessions picker. No-op
    /// if a description has already been stored for this session.
    /// </summary>
    private void SetSessionDescriptionIfEmpty(string sessionId, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return;

        // The metadata row is normally seeded by OnSessionCreated, but that
        // handler is queued via BeginInvoke and may not have run yet on the
        // first prompt of a brand-new session. Create a stub entry on the
        // fly so the description is never silently dropped due to ordering.
        var existing = _sessionStore.Find(sessionId);
        if (existing == null)
        {
            existing = new SessionMetadataEntry
            {
                SessionId       = sessionId,
                WorkspaceFolder = _copilot.WorkingDirectory ?? "",
                Model           = comboBoxModel.SelectedItem?.ToString() ?? "",
                Mode            = comboBoxMode.SelectedItem?.ToString()  ?? "Standard",
                Fleet           = checkBoxFleet.Checked,
                AutoApprove     = checkBoxAutoApprove.Checked,
                CreatedAt       = DateTime.Now,
                Description     = "",
            };
        }
        else if (!string.IsNullOrWhiteSpace(existing.Description))
        {
            return;
        }

        existing.Description = SummariseForDescription(description);
        _sessionStore.Save(existing);
    }

    /// <summary>
    /// Trims and condenses a prompt down to one or two sentences suitable for
    /// the Description column in the Past Sessions list.
    /// </summary>
    private static string SummariseForDescription(string text)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(
            text.Trim(), @"\s+", " ");
        const int max = 240;
        if (collapsed.Length <= max) return collapsed;
        return collapsed.Substring(0, max - 1).TrimEnd() + "\u2026";
    }

    /// <summary>
    /// Builds the list of rows for the session picker dialog by merging SDK-known
    /// sessions with locally stored metadata.
    /// </summary>
    private async Task<List<SessionListDialog.SessionRow>> BuildSessionRowsAsync()
    {
        // Start the client if needed (but don't require a workspace)
        await _copilot.EnsureStartedAsync();
        var liveIds = await _copilot.ListPersistedSessionsAsync();
        var liveSet = new HashSet<string>(liveIds, StringComparer.Ordinal);

        // Prune stale metadata entries
        _sessionStore.Prune(liveSet);

        var rows = new List<SessionListDialog.SessionRow>();
        foreach (var id in liveIds)
        {
            var meta = _sessionStore.Find(id);
            rows.Add(new SessionListDialog.SessionRow
            {
                SessionId = id,
                Workspace = meta?.WorkspaceFolder ?? "",
                Model     = meta?.Model ?? "",
                Mode      = meta?.Mode ?? "",
                CreatedAt = meta?.CreatedAt ?? default,
                Description = meta?.Description ?? "",
                Metadata  = meta,
            });
        }

        // Most recent first
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }

    private async Task BrowsePastSessionsAsync()
    {
        List<SessionListDialog.SessionRow> rows;
        try
        {
            rows = await BuildSessionRowsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not list sessions:\r\n\r\n{ex.Message}",
                "Session List Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (rows.Count == 0)
        {
            MessageBox.Show(this,
                "No persisted sessions found.",
                "Past Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SessionListDialog(
            rows,
            _mainSessionId,
            DeleteSessionsFromDialogAsync);

        var result = dialog.ShowDialog(this);

        if (result == DialogResult.OK
            && dialog.ResumeSelected is { } resumeRow)
        {
            await ResumePersistedSessionAsync(resumeRow.SessionId, resumeRow.Metadata);
        }
    }

    /// <summary>
    /// Delete callback handed to <see cref="SessionListDialog"/>. Returns the
    /// list of session IDs that failed to delete (empty on full success).
    /// </summary>
    private async Task<IReadOnlyList<string>> DeleteSessionsFromDialogAsync(
        IReadOnlyList<string> ids)
    {
        var failed = new List<string>();
        int deleted = 0;
        foreach (var id in ids)
        {
            try
            {
                await _copilot.DeletePersistedSessionAsync(id);
                _sessionStore.Remove(id);
                deleted++;
            }
            catch (Exception ex)
            {
                failed.Add(id);
                AppendOutput($"[Failed to delete {id}: {ex.Message}]\r\n", AppTheme.ColorError);
            }
        }

        if (deleted > 0)
        {
            AppendOutput(
                $"[Deleted {deleted} session{(deleted == 1 ? "" : "s")}]\r\n\r\n",
                AppTheme.ColorMeta);
        }

        return failed;
    }

    private async Task ResumePersistedSessionAsync(
        string sessionId, SessionMetadataEntry? metadata)
    {
        SetSendingState(true);

        try
        {
            // Determine workspace folder
            var workspace = metadata?.WorkspaceFolder;
            if (string.IsNullOrEmpty(workspace) || !Directory.Exists(workspace))
            {
                // If metadata has no workspace or it doesn't exist, ask the user
                using var folderDlg = new FolderBrowserDialog
                {
                    Description = "Select the workspace folder for this session",
                    UseDescriptionForTitle = true,
                    SelectedPath = workspace ?? "",
                };
                if (folderDlg.ShowDialog(this) != DialogResult.OK)
                {
                    SetSendingState(false);
                    return;
                }
                workspace = folderDlg.SelectedPath;
            }

            // Restore UI settings from metadata before connecting
            if (metadata != null)
            {
                // Restore model
                if (!string.IsNullOrEmpty(metadata.Model))
                {
                    var modelIdx = comboBoxModel.Items.Cast<string>()
                        .Select((m, i) => (m, i))
                        .Where(x => x.m.Equals(metadata.Model, StringComparison.OrdinalIgnoreCase))
                        .Select(x => (int?)x.i)
                        .FirstOrDefault();
                    if (modelIdx.HasValue)
                        comboBoxModel.SelectedIndex = modelIdx.Value;
                }

                // Restore mode
                if (!string.IsNullOrEmpty(metadata.Mode))
                {
                    var modeIdx = comboBoxMode.Items.Cast<string>()
                        .Select((m, i) => (m, i))
                        .Where(x => x.m.Equals(metadata.Mode, StringComparison.OrdinalIgnoreCase))
                        .Select(x => (int?)x.i)
                        .FirstOrDefault();
                    if (modeIdx.HasValue)
                        comboBoxMode.SelectedIndex = modeIdx.Value;
                }

                // Restore fleet and auto-approve
                checkBoxFleet.Checked       = metadata.Fleet;
                checkBoxAutoApprove.Checked = metadata.AutoApprove;
            }

            // Tear down existing connection and reconnect with the session's workspace
            if (_copilot.IsConnected)
            {
                await _copilot.DisposeAsync();
                _copilot.Reset();
            }

            ResetSessionTrackingState();
            _copilot.WorkingDirectory = workspace;
            buttonOpenFolder.Enabled = false;
            toolStripStatusLabelSession.Text = workspace;
            UpdateTitleBar(workspace);

            // Sync UI state to service
            _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString() ?? "Standard";
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;
            _copilot.FleetMode   = checkBoxFleet.Checked;

            AppendOutput($"\r\n📋 Resuming session: {sessionId}\r\n\r\n", AppTheme.ColorMeta);

            // Resume the persisted session (instead of creating a new one)
            await _copilot.ResumePersistedSessionAsync(sessionId);

            var version = await _copilot.GetVersionAsync();
            if (!string.IsNullOrEmpty(version))
                toolStripStatusLabelVersion.Text = $"v{version}";

            // Replay conversation history into the output panel
            await ReplaySessionHistoryAsync();

            AppendOutput("─────────── session resumed ───────────\r\n\r\n", AppTheme.ColorMeta);
        }
        catch (Exception ex)
        {
            AppendOutput($"\r\n❌ Resume failed: {ex.Message}\r\n\r\n", AppTheme.ColorError);
            MessageBox.Show(this,
                $"Failed to resume session:\r\n\r\n{ex.Message}",
                "Resume Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            buttonOpenFolder.Enabled = true;
            SetSendingState(false);
        }
    }

    /// <summary>
    /// Fetches the conversation history from the resumed session and replays
    /// it into both the Raw and Rendered output panels so the user can see
    /// what was discussed previously.
    /// </summary>
    private async Task ReplaySessionHistoryAsync()
    {
        var messages = await _copilot.GetSessionMessagesAsync();
        if (messages.Count == 0) return;

        AppendOutput("[Restoring conversation history...]\r\n\r\n", AppTheme.ColorMeta);

        foreach (var (type, content) in messages)
        {
            switch (type.ToLowerInvariant())
            {
                case "user":
                    AppendOutput($"\U0001f464 You: {Truncate(content, 500)}\r\n\r\n",
                        AppTheme.ColorUser);
                    var userBlock = new OutputBlock(BlockKind.User)
                    {
                        Label = "\U0001f464 You:",
                        Content = Truncate(content, 500),
                        IsComplete = true,
                    };
                    _outputBlocks.Add(userBlock);
                    WebViewAppendBlock(userBlock);
                    break;

                case "assistant":
                    AppendOutput($"\U0001f916 Assistant:\r\n{content}\r\n\r\n",
                        AppTheme.ColorAssistant);
                    var assistantBlock = new OutputBlock(BlockKind.Assistant)
                    {
                        Label = "\U0001f916 Assistant:",
                        Content = content,
                        IsComplete = true,
                    };
                    _outputBlocks.Add(assistantBlock);
                    WebViewAppendBlock(assistantBlock);
                    break;

                default:
                    // Tool calls, system messages, etc. — show as meta
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        AppendOutput($"[{type}] {Truncate(content, 200)}\r\n",
                            AppTheme.ColorMeta);
                    }
                    break;
            }
        }

        AppendOutput("\r\n", AppTheme.ColorMeta);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    // ── Skill Tree configuration ──────────────────────────────────────────────

    private void EditSkillTree()
    {
        using var dialog = new SkillTreeDialog(_settings.SkillTreeFolders);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // Replace the persisted list and the live service list with the edited one.
        _settings.SkillTreeFolders = new List<string>(dialog.Folders);
        _copilot.SkillTreeFolders  = _settings.SkillTreeFolders;

        UpdateSkillTreeTooltip();

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

        // Skill Tree contents are baked into the session at creation time
        // (via SkillDirectories / CustomAgents).  Match the mode/fleet pattern:
        // schedule a deferred summary-and-restart so context survives the change.
        if (_copilot.IsConnected && _mainSessionId != null)
            ScheduleHandoff("Skill Tree changed");
    }

    private void UpdateSkillTreeTooltip()
    {
        var folders = _settings.SkillTreeFolders;
        string tip;
        if (folders.Count == 0)
        {
            tip = "Skill Tree: empty\n(click to edit)";
        }
        else
        {
            var lines = new System.Text.StringBuilder();
            lines.Append($"Skill Tree ({folders.Count} folder{(folders.Count == 1 ? "" : "s")}):");
            foreach (var f in folders)
                lines.Append("\n  • ").Append(f);
            lines.Append("\n(click to edit)");
            tip = lines.ToString();
        }
        menuSkillsTree.ToolTipText = tip;
    }

    private void ShowAgentList()
    {
        // Make sure the cache reflects the current Skill Tree even before any
        // session has been created (or after an edit that has not yet
        // reconnected).
        _copilot.RebuildReferenceCache();

        var agents = _copilot.CachedAgents;
        if (agents.Count == 0)
        {
            MessageBox.Show(this,
                "No custom agents were found in the current Skill Tree, project, or personal (~/.copilot) folders.\r\n\r\n" +
                "Add an agents/*.md file under any tier folder to surface it here.",
                "No Agents",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = ReferenceListDialog.ForAgents(agents);
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedName))
            InsertNamedReferenceAtCursor("agent", dlg.SelectedName!);
    }

    private void ShowSkillList()
    {
        _copilot.RebuildReferenceCache();

        var skills = _copilot.CachedSkills;
        if (skills.Count == 0)
        {
            MessageBox.Show(this,
                "No skills (skills/*/SKILL.md) were found in the current Skill Tree, project, or personal (~/.copilot) folders.",
                "No Skills",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = ReferenceListDialog.ForSkills(skills);
        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedName))
            InsertNamedReferenceAtCursor("skill", dlg.SelectedName!);
    }

    /// <summary>
    /// Inserts an "@kind:name" token (e.g. "@agent:doublecheck") at the prompt's
    /// caret position, mirroring the spacing rules used by file/folder attachments
    /// in <see cref="InsertReferenceAtCursor"/>.
    /// </summary>
    private void InsertNamedReferenceAtCursor(string kind, string name)
    {
        var reference = $"@{kind}:{name}";
        var pos  = richTextBoxPrompt.SelectionStart;
        var text = richTextBoxPrompt.Text;

        if (pos > 0 && !char.IsWhiteSpace(text[pos - 1]))
            reference = " " + reference;

        reference += " ";

        richTextBoxPrompt.SelectionLength = 0;
        richTextBoxPrompt.SelectedText    = reference;
        richTextBoxPrompt.Focus();
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
        panelAttachments.Visible = true;

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

    /// <summary>
    /// Builds and attaches a right-click menu to the prompt editor that mirrors
    /// the most-used reference and skill commands from the menu bar (Add File,
    /// Add Folder, List Agents, List Skills) so they are reachable without
    /// leaving the keyboard/cursor focus.
    /// </summary>
    private void AttachPromptContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = AppTheme.StatusBar,
            ForeColor = AppTheme.TextPrimary,
            Renderer  = new DarkMenuRenderer(),
        };

        ToolStripMenuItem Item(string text, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text)
            {
                BackColor = AppTheme.StatusBar,
                ForeColor = AppTheme.TextPrimary,
            };
            item.Click += handler;
            return item;
        }

        menu.Items.Add(Item("📄 Add &File...",   ButtonAddFile_Click));
        menu.Items.Add(Item("📁 Add F&older...", ButtonAddFolder_Click));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Item("List &Agents...",   (_, _) => ShowAgentList()));
        menu.Items.Add(Item("List &Skills...",   (_, _) => ShowSkillList()));

        richTextBoxPrompt.ContextMenuStrip = menu;
    }

    private void RemoveAttachment(string path, Control chip)
    {
        _attachments.Remove(path);
        flowLayoutPanelChips.Controls.Remove(chip);
        chip.Dispose();
        if (flowLayoutPanelChips.Controls.Count == 0)
            panelAttachments.Visible = false;
    }

    /// <summary>
    /// Scans the prompt RichTextBox's RTF for embedded pictures (pasted or
    /// dragged-in images), writes each one to "%TEMP%\Kopilot\clip-image-N.*",
    /// and returns the resulting file paths so the caller can forward them as
    /// attachments to Copilot. Raw PNG/JPEG payloads are written as-is;
    /// metafile/DIB payloads are re-encoded to PNG through GDI+.
    /// </summary>
    private static IReadOnlyList<string> ExtractEmbeddedImagesToTemp(string? rtf)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(rtf)) return results;

        var outDir = Path.Combine(Path.GetTempPath(), "Kopilot");
        Directory.CreateDirectory(outDir);

        int cursor = 0;
        while (true)
        {
            int start = rtf.IndexOf("{\\pict", cursor, StringComparison.Ordinal);
            if (start < 0) break;

            int end = FindRtfGroupEnd(rtf, start);
            if (end < 0) break;

            try
            {
                var (ext, bytes) = ParseRtfPictGroup(rtf, start, end);
                if (bytes is { Length: > 0 })
                {
                    var path = SaveImageBytes(outDir, ext, bytes);
                    if (path != null) results.Add(path);
                }
            }
            catch
            {
                // Swallow any single-image failure so one broken picture
                // doesn't block the rest of the send.
            }

            cursor = end + 1;
        }

        return results;
    }

    /// <summary>
    /// Walks the RTF starting at an opening brace and returns the index of the
    /// matching closing brace, honouring "\{" and "\}" escape sequences.
    /// Returns -1 if the group is unterminated.
    /// </summary>
    private static int FindRtfGroupEnd(string rtf, int open)
    {
        int depth = 0;
        for (int i = open; i < rtf.Length; i++)
        {
            char c = rtf[i];
            if (c == '\\' && i + 1 < rtf.Length)
            {
                // Skip escaped brace; ordinary control words also consume the
                // next char but they never look like a brace, so this suffices.
                char next = rtf[i + 1];
                if (next == '{' || next == '}' || next == '\\') { i++; continue; }
            }
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Parses a "{\pict ...}" group and returns a preferred extension plus the
    /// decoded binary payload. Nested option sub-groups (e.g. "{\*\blipuid ...}")
    /// and control words are skipped so only the hex picture data is collected.
    /// </summary>
    private static (string ext, byte[]? bytes) ParseRtfPictGroup(string rtf, int start, int end)
    {
        string ext = ".bin";
        var hex = new System.Text.StringBuilder((end - start) / 2);

        int i = start + 1;
        while (i < end)
        {
            char c = rtf[i];

            if (c == '{')
            {
                int inner = FindRtfGroupEnd(rtf, i);
                if (inner < 0 || inner >= end) break;
                i = inner + 1;
                continue;
            }

            if (c == '\\')
            {
                // Read control word: "\<letters>[-]<digits>?" optionally
                // followed by a single delimiter space.
                int j = i + 1;
                if (j < end && (rtf[j] == '{' || rtf[j] == '}' || rtf[j] == '\\'))
                {
                    // Escaped literal; not a picture-type marker.
                    i = j + 1;
                    continue;
                }
                int wordStart = j;
                while (j < end && char.IsLetter(rtf[j])) j++;
                string word = rtf.Substring(wordStart, j - wordStart);

                if (word.Length > 0) ext = MatchPictExtension(word) ?? ext;

                if (j < end && (rtf[j] == '-' || char.IsDigit(rtf[j])))
                {
                    if (rtf[j] == '-') j++;
                    while (j < end && char.IsDigit(rtf[j])) j++;
                }
                if (j < end && rtf[j] == ' ') j++;
                i = j;
                continue;
            }

            if (IsHexDigit(c)) hex.Append(c);
            // Any other character (whitespace, newline, CR) is ignored.
            i++;
        }

        if (hex.Length < 2 || (hex.Length & 1) != 0) return (ext, null);
        var bytes = HexToBytes(hex.ToString());
        return (ext, bytes);
    }

    private static string? MatchPictExtension(string controlWord) => controlWord switch
    {
        "pngblip"                                => ".png",
        "jpegblip"                               => ".jpg",
        "emfblip"                                => ".emf",
        "wmetafile"  or "wmetafile8"             => ".wmf",
        "dibitmap"   or "dibitmap0"              => ".dib",
        "wbitmap"    or "wbitmap0"               => ".bmp",
        _                                        => null,
    };

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int k = 0; k < bytes.Length; k++)
            bytes[k] = (byte)((HexNibble(hex[k * 2]) << 4) | HexNibble(hex[k * 2 + 1]));
        return bytes;
    }

    private static int HexNibble(char c) =>
        c <= '9' ? c - '0' :
        c <= 'F' ? c - 'A' + 10 :
                   c - 'a' + 10;

    /// <summary>
    /// Writes the picture bytes to a unique "clip-image-N.*" file under the
    /// provided directory. PNG/JPEG payloads are written verbatim; DIB, WMF,
    /// EMF, and BMP payloads are re-encoded to PNG so the downstream model
    /// always receives a format it can read.
    /// </summary>
    private static string? SaveImageBytes(string dir, string ext, byte[] bytes)
    {
        int next = 1;
        foreach (var existing in Directory.EnumerateFiles(dir, "clip-image-*.*"))
        {
            var name = Path.GetFileNameWithoutExtension(existing);
            var dash = name.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(name.AsSpan(dash + 1), out var n) && n >= next)
                next = n + 1;
        }

        if (ext is ".png" or ".jpg")
        {
            var path = Path.Combine(dir, $"clip-image-{next}{ext}");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        // DIB/WMF/EMF/BMP: transcode to PNG so the attachment is universally
        // consumable. If the round-trip fails, write the raw bytes with the
        // original extension as a best-effort fallback.
        try
        {
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);
            var pngPath = Path.Combine(dir, $"clip-image-{next}.png");
            img.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
            return pngPath;
        }
        catch
        {
            var fallback = Path.Combine(dir, $"clip-image-{next}{ext}");
            File.WriteAllBytes(fallback, bytes);
            return fallback;
        }
    }

    // ── Session ───────────────────────────────────────────────────────────────

    private void OnSessionCreated(string sessionId, bool isSubAgent)
    {
        if (!isSubAgent)
        {
            _mainSessionId = sessionId;
            SaveSessionMetadata(sessionId);
        }
        else
        {
            // Track the sub-agent's session id so we can recognise its termination
            // via session.deleted even if the subagent.completed event is missing.
            _activeSubAgentSessions.Add(sessionId);
        }

        var label = isSubAgent ? "Sub-agent" : "Session";
        AppendOutput($"[{label} {sessionId[..Math.Min(8, sessionId.Length)]}… started]\r\n\r\n",
            AppTheme.ColorMeta);

        // Also push to the Rendered tab
        var sessionBlock = new OutputBlock(BlockKind.Status)
        {
            Content = $"[{label} {sessionId[..Math.Min(8, sessionId.Length)]}... started]",
            IsComplete = true
        };
        _outputBlocks.Add(sessionBlock);
        WebViewAppendBlock(sessionBlock);

        toolStripStatusLabelSession.Text =
            $"Session: {sessionId[..Math.Min(8, sessionId.Length)]}…";
    }

    // ── Output rendering ─────────────────────────────────────────────────────

    private void AppendMessage(SessionMessageEventArgs args)
    {
        // Dual-write: push to the WebView2 Rendered tab
        AppendRenderedMessage(args);

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
                {
                    if (_mainSessionIdle) RestartSubAgentWatchdog();
                    UpdateAgentStatus();
                }
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
                {
                    if (_mainSessionIdle) RestartSubAgentWatchdog();
                    UpdateAgentStatus();
                }
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

            case MessageKind.Status:
                AppendOutput($"[{args.Content}]\r\n", AppTheme.ColorMeta);
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
                // If the SDK reports no live sub-agent sessions, the orchestrator
                // really is finished — any lingering _activeSubAgents entries are
                // stale (a subagent.completed event was lost). Don't keep the user
                // staring at "Working..." forever; force completion.
                if (_activeSubAgentSessions.Count == 0)
                {
                    ForceCompleteStaleSubAgents("main session idle with no live sub-agent sessions");
                    return;
                }

                // Orchestrator finished dispatching but Fleet sub-agents are still running.
                // Defer completion until the last sub-agent fires complete/failed,
                // and arm the watchdog so we recover if that event never arrives.
                _mainSessionIdle = true;
                RestartSubAgentWatchdog();
                UpdateAgentStatus();
            }
            else
            {
                CompleteMainSession();
            }
        }
    }

    /// <summary>
    /// Called when the SDK reports a non-main session was deleted. This is the
    /// most reliable signal that a sub-agent has truly ended, even if its paired
    /// subagent.completed/failed event was dropped.
    /// </summary>
    private void OnSubAgentSessionEnded(string sessionId)
    {
        if (!_activeSubAgentSessions.Remove(sessionId))
            return;

        _streamingSessions.Remove(sessionId);

        // If the main session has already gone idle and no live sub-agent sessions
        // remain, the SDK is truly done. Sweep any stale _activeSubAgents entries
        // and complete; rearm the watchdog only if work is still genuinely pending.
        if (_mainSessionIdle && _activeSubAgentSessions.Count == 0)
        {
            ForceCompleteStaleSubAgents("all sub-agent sessions ended");
        }
        else if (_mainSessionIdle)
        {
            RestartSubAgentWatchdog();
            UpdateAgentStatus();
        }
        else
        {
            UpdateAgentStatus();
        }
    }

    /// <summary>
    /// Recover from a stuck "Working..." state by clearing any leaked sub-agent
    /// tracking entries and completing the main session. Logs a meta line to the
    /// transcript so the discrepancy is visible.
    /// </summary>
    private void ForceCompleteStaleSubAgents(string reason)
    {
        if (_activeSubAgents.Count > 0)
        {
            var names = string.Join(", ", _activeSubAgents.Values);
            AppendOutput(
                $"[Cleared {_activeSubAgents.Count} stale sub-agent entr"
                    + (_activeSubAgents.Count == 1 ? "y" : "ies")
                    + $" ({names}) — {reason}]\r\n",
                AppTheme.ColorMeta);
            _activeSubAgents.Clear();
        }
        CompleteMainSession();
    }

    private void RestartSubAgentWatchdog()
    {
        _subAgentWatchdog.Stop();
        _subAgentWatchdog.Start();
    }

    /// <summary>
    /// Force-reset all per-prompt tracking state. Used by reset/reconnect paths
    /// that bypass the normal CompleteMainSession flow so no stale "Working..."
    /// or sub-agent indicators leak across session boundaries.
    /// </summary>
    private void UpdateTitleBar(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            this.Text = "Kopilot";
        }
        else
        {
            this.Text = $"Kopilot : {folder}";
        }
    }

    private void ResetSessionTrackingState()
    {
        _subAgentWatchdog.Stop();
        _pendingCount        = 0;
        _mainSessionIdle     = false;
        _totalBytesReceived  = 0;
        _completedAgentCount = 0;
        _activeSubAgents.Clear();
        _activeSubAgentSessions.Clear();
        _streamingSessions.Clear();
        _toolStartPositions.Clear();
        _subAgentStartPositions.Clear();
    }

    private void CompleteMainSession()
    {
        _subAgentWatchdog.Stop();
        // Only chime when real work was in flight — back-to-back idle events
        // (e.g. coalesced Dispatch calls) can re-enter this method with a
        // pending count of zero and would otherwise produce a phantom chime.
        bool hadPendingWork = _pendingCount > 0;
        _mainSessionIdle = false;
        // session.idle from the SDK means the main session has nothing left to
        // process. Multiple back-to-back Dispatch calls (e.g. README + backup
        // load on Open Folder) can be coalesced into a single SDK turn, so we
        // must reset to 0 here rather than decrement — otherwise the UI stays
        // stuck on "Working..." with leftover phantom pending counts.
        _pendingCount = 0;
        _totalBytesReceived = 0;
        _activeSubAgents.Clear();
        _activeSubAgentSessions.Clear();
        _streamingSessions.Clear();
        _toolStartPositions.Clear();
        _subAgentStartPositions.Clear();
        _completedAgentCount = 0;
        UpdateWorkingState();
        if (hadPendingWork) SoundService.PlayWorkComplete();
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

    // ── Rendered output (WebView2) ──────────────────────────────────────────

    // Maps sessionId → current streaming OutputBlock for that session
    private readonly Dictionary<string, OutputBlock> _streamingBlocks = new();

    private void AppendRenderedMessage(SessionMessageEventArgs args)
    {
        if (!_webViewReady) return;

        switch (args.Kind)
        {
            case MessageKind.AssistantDelta:
            {
                if (!_streamingBlocks.TryGetValue(args.SessionId, out var block))
                {
                    block = new OutputBlock(BlockKind.Assistant) { Label = "\U0001f916 Assistant:" };
                    _outputBlocks.Add(block);
                    _streamingBlocks[args.SessionId] = block;
                    WebViewAppendBlock(block);
                }
                block.Content += args.Content;
                WebViewUpdateBlock(block);
                break;
            }

            case MessageKind.AssistantFinal:
            {
                if (_streamingBlocks.TryGetValue(args.SessionId, out var block))
                {
                    block.IsComplete = true;
                    _streamingBlocks.Remove(args.SessionId);
                    WebViewFinalizeBlock(block);
                }
                else
                {
                    // Non-streamed final message
                    var finalBlock = new OutputBlock(BlockKind.Assistant)
                    {
                        Label = "\U0001f916 Assistant:",
                        Content = args.Content,
                        IsComplete = true
                    };
                    _outputBlocks.Add(finalBlock);
                    WebViewAppendBlock(finalBlock);
                    WebViewFinalizeBlock(finalBlock);
                }
                break;
            }

            case MessageKind.Reasoning:
            {
                var block = new OutputBlock(BlockKind.Reasoning)
                {
                    Label = "\U0001f4ad Reasoning:",
                    Content = args.Content,
                    IsComplete = true
                };
                _outputBlocks.Add(block);
                WebViewAppendBlock(block);
                break;
            }

            case MessageKind.SubAgentStart:
            {
                FinalizeStreamingBlock(args.SessionId);
                var saName = args.SubAgentDisplayName ?? args.Content;
                var saDesc = string.IsNullOrEmpty(args.SubAgentDescription) ? ""
                    : $" -- {(args.SubAgentDescription.Length > 60 ? args.SubAgentDescription[..60] + "..." : args.SubAgentDescription)}";
                var block = new OutputBlock(BlockKind.SubAgent)
                {
                    Content = $"\u25CB {saName}{saDesc}"
                };
                _outputBlocks.Add(block);
                if (!string.IsNullOrEmpty(args.ToolCallId))
                    _renderedSubAgentBlocks[args.ToolCallId] = block;
                WebViewAppendBlock(block);
                break;
            }

            case MessageKind.SubAgentComplete:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId) &&
                    _renderedSubAgentBlocks.TryGetValue(args.ToolCallId, out var saBlock))
                {
                    _renderedSubAgentBlocks.Remove(args.ToolCallId);
                    var stats = FormatSubAgentStats(args);
                    if (stats != null)
                        WebViewAppendToolStatus(saBlock,
                            $"<span class=\"subagent-complete\">\u25C9</span> {EscapeForJs(stats)}");
                }
                break;
            }

            case MessageKind.SubAgentFailed:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId) &&
                    _renderedSubAgentBlocks.TryGetValue(args.ToolCallId, out var saBlock))
                {
                    _renderedSubAgentBlocks.Remove(args.ToolCallId);
                    var msg = !string.IsNullOrEmpty(args.Content)
                        ? $"<span class=\"subagent-failed\">\u2717 {EscapeForJs(args.SubAgentDisplayName ?? "")}: {EscapeForJs(args.Content)}</span>"
                        : "<span class=\"subagent-failed\">\u2717</span>";
                    WebViewAppendToolStatus(saBlock, msg);
                }
                break;
            }

            case MessageKind.SkillInvoked:
            {
                FinalizeStreamingBlock(args.SessionId);
                var desc = string.IsNullOrEmpty(args.SubAgentDescription) ? ""
                    : $" -- {args.SubAgentDescription}";
                var block = new OutputBlock(BlockKind.Status)
                {
                    Content = $"\U0001f4da Skill: {args.Content}{desc}",
                    IsComplete = true
                };
                _outputBlocks.Add(block);
                WebViewAppendBlock(block);
                break;
            }

            case MessageKind.CustomAgentsUpdated:
            {
                var block = new OutputBlock(BlockKind.Status)
                {
                    Content = $"[{args.Content}]",
                    IsComplete = true
                };
                _outputBlocks.Add(block);
                WebViewAppendBlock(block);
                break;
            }

            case MessageKind.ToolStart:
            {
                FinalizeStreamingBlock(args.SessionId);
                var argPart = string.IsNullOrEmpty(args.ToolArgSummary) ? "" : $"  {args.ToolArgSummary}";
                var block = new OutputBlock(BlockKind.Tool)
                {
                    Content = $"\U0001f527 {args.Content}{argPart}"
                };
                _outputBlocks.Add(block);
                if (!string.IsNullOrEmpty(args.ToolCallId))
                    _renderedToolBlocks[args.ToolCallId] = block;
                WebViewAppendBlock(block);
                break;
            }

            case MessageKind.ToolProgress:
            {
                if (!string.IsNullOrEmpty(args.Content) &&
                    !string.IsNullOrEmpty(args.ToolCallId) &&
                    _renderedToolBlocks.TryGetValue(args.ToolCallId, out var tBlock))
                {
                    WebViewAppendToolStatus(tBlock,
                        $"<br/><span class=\"tool-dim\">\u2502 {EscapeForJs(args.Content)}</span>");
                }
                break;
            }

            case MessageKind.ToolComplete:
            {
                if (!string.IsNullOrEmpty(args.ToolCallId) &&
                    _renderedToolBlocks.TryGetValue(args.ToolCallId, out var tBlock))
                {
                    _renderedToolBlocks.Remove(args.ToolCallId);
                    tBlock.IsComplete = true;
                    var tick = args.ToolSuccess
                        ? "<span class=\"tool-success\"> \u2713</span>"
                        : "<span class=\"tool-failure\"> \u2717</span>";
                    WebViewAppendToolStatus(tBlock, tick);

                    if (!string.IsNullOrEmpty(args.ToolResultSummary))
                    {
                        var cls = args.ToolSuccess ? "tool-dim" : "tool-failure";
                        WebViewAppendToolStatus(tBlock,
                            $"<br/><span class=\"{cls}\">\u2514 {EscapeForJs(args.ToolResultSummary)}</span>");
                    }
                }
                break;
            }

            case MessageKind.Error:
            {
                FinalizeStreamingBlock(args.SessionId);
                var block = new OutputBlock(BlockKind.Error)
                {
                    Content = $"\u274C Error: {args.Content}",
                    IsComplete = true
                };
                _outputBlocks.Add(block);
                WebViewAppendBlock(block);

                if (args.Content.Contains("CAPIError: 400") || args.Content.Contains("400 Bad Request"))
                {
                    var tipBlock = new OutputBlock(BlockKind.Status)
                    {
                        Content = "\U0001f4a1 Tip: This usually means the session's context window is full. " +
                            "Try changing the mode or model to start a fresh session.",
                        IsComplete = true
                    };
                    _outputBlocks.Add(tipBlock);
                    WebViewAppendBlock(tipBlock);
                }
                break;
            }

            case MessageKind.Status:
            {
                var block = new OutputBlock(BlockKind.Status)
                {
                    Content = $"[{args.Content}]",
                    IsComplete = true
                };
                _outputBlocks.Add(block);
                WebViewAppendBlock(block);
                break;
            }
        }
    }

    // Maps toolCallId -> rendered block for tool and sub-agent tracking
    private readonly Dictionary<string, OutputBlock> _renderedToolBlocks = new();
    private readonly Dictionary<string, OutputBlock> _renderedSubAgentBlocks = new();

    private void FinalizeStreamingBlock(string sessionId)
    {
        if (_streamingBlocks.TryGetValue(sessionId, out var block))
        {
            block.IsComplete = true;
            _streamingBlocks.Remove(sessionId);
            WebViewFinalizeBlock(block);
        }
    }

    // ── WebView2 JS bridge helpers ───────────────────────────────────────────

    private void WebViewAppendBlock(OutputBlock block)
    {
        // A structured block is being appended (User, Assistant, Tool, etc.).
        // Close any rolling meta block so subsequent AppendOutput calls start a new
        // Status block below it instead of being merged into the previous one.
        _currentMetaBlock = null;
        WebViewAppendBlockInternal(block);
    }

    private void WebViewAppendBlockInternal(OutputBlock block)
    {
        if (!_webViewReady) return;
        var js = $"appendBlock({JsString(block.Id)}, {JsString(block.CssKind)}, " +
                 $"{JsString(block.Label)}, {JsString(block.Content)}, {(block.IsMarkdown ? "true" : "false")})";
        _ = webViewOutput.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void WebViewUpdateBlock(OutputBlock block)
    {
        if (!_webViewReady) return;
        var js = $"updateBlock({JsString(block.Id)}, {JsString(block.Content)})";
        _ = webViewOutput.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void WebViewFinalizeBlock(OutputBlock block)
    {
        if (!_webViewReady) return;
        // Flush content one last time, then finalize
        var js = $"updateBlock({JsString(block.Id)}, {JsString(block.Content)}); " +
                 $"finalizeBlock({JsString(block.Id)})";
        _ = webViewOutput.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void WebViewAppendToolStatus(OutputBlock block, string html)
    {
        if (!_webViewReady) return;
        var js = $"appendToolStatus({JsString(block.Id)}, {JsString(html)})";
        _ = webViewOutput.CoreWebView2.ExecuteScriptAsync(js);
    }

    private void WebViewClearAll()
    {
        _currentMetaBlock = null;
        if (!_webViewReady) return;
        _ = webViewOutput.CoreWebView2.ExecuteScriptAsync("clearAll()");
    }

    /// <summary>Wraps a string as a JS string literal, escaping special characters.</summary>
    private static string JsString(string? value)
    {
        if (value == null) return "\"\"";
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("<", "\\x3c")
            .Replace(">", "\\x3e");
        return $"\"{escaped}\"";
    }

    /// <summary>Escapes text for safe insertion into HTML within JS calls.</summary>
    private static string EscapeForJs(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    // ── Raw output (RichTextBox) ─────────────────────────────────────────────

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

        // Mirror to the Rendered (WebView) tab so both tabs show the same content.
        MirrorMetaToWebView(text, color);
    }

    /// <summary>
    /// Appends plain-text output (setup banners, status lines, /help text, etc.) to
    /// the Rendered tab as a rolling Status or Error block. Consecutive AppendOutput
    /// calls of the same color coalesce into one block; the block is closed whenever
    /// a structured WebView block is appended via <see cref="WebViewAppendBlock"/> or
    /// the output is cleared via <see cref="WebViewClearAll"/>.
    /// </summary>
    private void MirrorMetaToWebView(string text, Color color)
    {
        if (!_webViewReady) return;
        if (string.IsNullOrEmpty(text)) return;

        BlockKind kind = (color == AppTheme.ColorError) ? BlockKind.Error : BlockKind.Status;

        if (_currentMetaBlock == null
            || _currentMetaBlock.Kind != kind
            || _currentMetaColor != color)
        {
            _currentMetaBlock = new OutputBlock(kind);
            _currentMetaColor = color;
            _currentMetaBlock.Content = text;
            _outputBlocks.Add(_currentMetaBlock);
            WebViewAppendBlockInternal(_currentMetaBlock);
        }
        else
        {
            _currentMetaBlock.Content += text;
            WebViewUpdateBlock(_currentMetaBlock);
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
            status = $"Working…{kb}";
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

    // Used by long-running operations (e.g. session refresh handoff) which need
    // to block the UI exclusively
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
                ResetSessionTrackingState();
                _mainSessionId   = null;
            }
            else if (status == "Not connected" && _reconnecting)
            {
                // Automatic reconnect attempt failed.
                AppendOutput("\r\n❌ Session lost and reconnect failed. Open a folder to reconnect.\r\n\r\n",
                    AppTheme.ColorError);
                _reconnecting    = false;
                ResetSessionTrackingState();
                _mainSessionId   = null;
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
            SoundService.PlayDialog();
            using var dialog = new PermissionDialog(args);
            dialog.ShowDialog(this);
        });
    }

    private void Copilot_UserInputRequested(object? sender, UserInputEventArgs args)
    {
        if (!IsHandleCreated) { args.Answer.TrySetResult(""); return; }
        Invoke(() =>
        {
            SoundService.PlayDialog();
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
        if (string.IsNullOrEmpty(version)) return;

        if (_cliUpdateChecked || !_copilot.IsCliFromPath)
        {
            AppendOutput($"[Copilot CLI v{version}]\r\n", AppTheme.ColorMeta);
            return;
        }

        _cliUpdateChecked = true;

        var latest = await UpdateChecker.GetLatestCliVersionAsync();

        if (latest == null)
        {
            AppendOutput($"[Copilot CLI v{version} — update check unavailable]\r\n", AppTheme.ColorMeta);
            return;
        }

        if (UpdateChecker.IsNewer(version, latest))
        {
            AppendOutput(
                $"[Copilot CLI update available: v{version} -> v{latest} — open a Copilot terminal and run /update]\r\n",
                AppTheme.ColorTool);
        }
        else
        {
            AppendOutput($"[Copilot CLI v{version} — up to date]\r\n", AppTheme.ColorMeta);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _subAgentWatchdog.Stop();
        _subAgentWatchdog.Dispose();
        _ = _copilot.DisposeAsync().AsTask();
    }
}
