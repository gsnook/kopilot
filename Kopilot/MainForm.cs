namespace Kopilot;

using System.Runtime.InteropServices;

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


    public MainForm()
    {
        InitializeComponent();
        WireUpEvents();
        // Default to the highest-available Claude Sonnet model
        var sonnetIdx = comboBoxModel.Items.Cast<string>()
            .Select((m, i) => (model: m, idx: i))
            .Where(x => x.model.StartsWith("claude-sonnet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.model)
            .Select(x => x.idx)
            .DefaultIfEmpty(0)
            .First();
        comboBoxModel.SelectedIndex = sonnetIdx;
        comboBoxMode.SelectedIndex = 0;
        // Sync service with the UI defaults set in the designer
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

        buttonHelp.Click += async (_, _) => await SendQuickCommandAsync(
            "What can you help me with? Give a brief overview of your capabilities.");
        buttonCommands.Click += async (_, _) => await SendQuickCommandAsync(
            "List all the tools, operations and built-in capabilities available to you in this session.");
        buttonSummarize.Click += async (_, _) => await SendQuickCommandAsync(
            "Please provide a concise summary of what we've discussed and accomplished so far in this session.");
        buttonClearOutput.Click += (_, _) => ClearActiveOutput();
        buttonBackup.Click += async (_, _) => await BackupSessionAsync();
        buttonOpenExplorer.Click += (_, _) => OpenExplorer();
        buttonOpenVSCode.Click += async (_, _) => await OpenVSCodeAsync();

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

    // ── Sending ───────────────────────────────────────────────────────────────

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
        using var saveDialog = new SaveFileDialog
        {
            Title = "Save Session Backup",
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = "md",
            FileName = $"copilot-session-{DateTime.Now:yyyy-MM-dd-HHmm}.md",
            InitialDirectory = _copilot.WorkingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
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
                AppendOutput($"[Scratchpad: {_copilot.KopilotPath}]\r\n\r\n", AppTheme.ColorMeta);

            // Generate all dialog line pools in the background — non-blocking
            _ = GenerateAllDialogLinesAsync();
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

    private async Task GenerateAllDialogLinesAsync()
    {
        var personality = AudioService.LoadVoicePersonality();
        try
        {
            // Generate all three cue pools in parallel
            var ssTask = _copilot.GenerateDialogBatchAsync(DialogCue.SessionStart,   personality);
            var psTask = _copilot.GenerateDialogBatchAsync(DialogCue.PromptSent,     personality);
            var pcTask = _copilot.GenerateDialogBatchAsync(DialogCue.PromptComplete, personality);

            await Task.WhenAll(ssTask, psTask, pcTask);

            InvokeOnUI(() =>
            {
                _audio.LoadLines(DialogCue.SessionStart,   ssTask.Result);
                _audio.LoadLines(DialogCue.PromptSent,     psTask.Result);
                _audio.LoadLines(DialogCue.PromptComplete, pcTask.Result);
            });
        }
        catch { /* voice generation is non-critical — silent failure is fine */ }
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

    private void AddAttachment(string path)
    {
        if (_attachments.Contains(path)) return;
        _attachments.Add(path);

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
        AppendColoredText(richTextBoxOutput, text, color);
        richTextBoxOutput.ScrollToCaret();
    }

    private static void AppendColoredText(RichTextBox box, string text, Color color)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = color;
        box.AppendText(text);
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

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _audio.Dispose();
        _ = _copilot.DisposeAsync().AsTask();
    }
}
