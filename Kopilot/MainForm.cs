namespace Kopilot;

public partial class MainForm : Form
{
    private readonly CopilotService _copilot = new();
    private readonly List<string> _attachments = new();
    private readonly Dictionary<string, RichTextBox> _sessionOutputs = new();
    private readonly HashSet<string> _streamingSessions = new();
    private string? _mainSessionId;
    private int _subAgentCount = 0;

    public MainForm()
    {
        InitializeComponent();
        WireUpEvents();
        comboBoxModel.SelectedIndex = 0;
        comboBoxMode.SelectedIndex = 0;
    }

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireUpEvents()
    {
        buttonSend.Click += async (_, _) => await SendPromptAsync();
        buttonStop.Click += async (_, _) => await StopAsync();
        buttonAddFile.Click += ButtonAddFile_Click;
        buttonAddFolder.Click += ButtonAddFolder_Click;
        buttonOpenFolder.Click += async (_, _) => await OpenFolderAndConnectAsync();
        richTextBoxPrompt.KeyDown += RichTextBoxPrompt_KeyDown;

        buttonHelp.Click += async (_, _) => await SendQuickCommandAsync(
            "What can you help me with? Give a brief overview of your capabilities.");
        buttonCommands.Click += async (_, _) => await SendQuickCommandAsync(
            "List all the tools, operations and built-in capabilities available to you in this session.");
        buttonSummarize.Click += async (_, _) => await SendQuickCommandAsync(
            "Please provide a concise summary of what we've discussed and accomplished so far in this session.");
        buttonClearOutput.Click += (_, _) => ClearActiveOutput();
        buttonBackup.Click += async (_, _) => await BackupSessionAsync();

        checkBoxAutoApprove.CheckedChanged += (_, _) =>
            _copilot.AutoApprove = checkBoxAutoApprove.Checked;

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
            InvokeOnUI(() => AddSessionTab(args.SessionId, args.IsSubAgent));

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

    private async Task SendPromptAsync()
    {
        var prompt = richTextBoxPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        SetSendingState(true);
        richTextBoxPrompt.Clear();

        _copilot.ActiveMode  = comboBoxMode.SelectedItem?.ToString()  ?? "Standard";
        _copilot.AutoApprove = checkBoxAutoApprove.Checked;

        try
        {
            var attachmentsCopy = _attachments.ToList();
            await _copilot.SendMessageAsync(prompt, attachmentsCopy);

            // Echo user message after session tab exists (tab is created synchronously
            // inside SendMessageAsync via SessionCreated → InvokeOnUI → AddSessionTab)
            if (_mainSessionId != null)
                AppendToSession(_mainSessionId, $"👤 You: {prompt}\r\n\r\n", AppTheme.ColorUser);
        }
        catch (Exception ex)
        {
            SetSendingState(false);
            if (_mainSessionId != null)
                AppendToSession(_mainSessionId, $"\r\n❌ Error sending: {ex.Message}\r\n", AppTheme.ColorError);
        }
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
        richTextBoxPrompt.Text = prompt;
        await SendPromptAsync();
    }

    private void ClearActiveOutput()
    {
        var activeTab = tabControlSessions.SelectedTab;
        if (activeTab == null) return;
        if (_sessionOutputs.TryGetValue(activeTab.Name, out var box))
            box.Clear();
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
            if (_sessionOutputs.TryGetValue(_mainSessionId, out var box))
                AppendColoredText(box,
                    $"\r\n[Mode changed to {mode} — new session will start on next send]\r\n\r\n",
                    AppTheme.ColorMeta);

            await _copilot.ResetSessionAsync();
            _mainSessionId = null;
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
            Please write a comprehensive session-resume document in Markdown format.
            The goal is to allow someone (or yourself in a new session) to quickly pick up exactly where we left off.
            
            Include all of the following that are relevant:
            
            # Session Resume Document
            
            ## Project Overview
            Brief description of the project, its purpose, and tech stack.
            
            ## Work Completed This Session
            Bullet-point summary of everything accomplished.
            
            ## Current State
            What is the exact current state of the codebase / work? What was the last thing done?
            
            ## Open Tasks & Next Steps
            Numbered list of what still needs to be done, in priority order.
            
            ## Key Decisions & Context
            Important decisions made, trade-offs, constraints, or context that the next session must know.
            
            ## Important File Paths & Commands
            Any specific files, directories, commands, or configurations that are critical.
            
            ## How to Resume
            Step-by-step instructions for starting the next session, including any commands to run first.
            
            Write the full document now — do not truncate or summarise sections.
            """;

        try
        {
            SetSendingState(true);

            // Echo the backup request in the output
            if (_sessionOutputs.TryGetValue(_mainSessionId, out var box))
                AppendColoredText(box, "💾 Generating session backup document…\r\n\r\n", AppTheme.ColorMeta);

            var markdown = await _copilot.SendAndCaptureResponseAsync(backupPrompt, TimeSpan.FromMinutes(5));

            await File.WriteAllTextAsync(filePath, markdown);

            if (_sessionOutputs.TryGetValue(_mainSessionId ?? "", out var box2))
                AppendColoredText(box2, $"[Backup saved to: {filePath}]\r\n\r\n", AppTheme.ColorMeta);

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

        _copilot.WorkingDirectory = dialog.SelectedPath;
        buttonOpenFolder.Enabled = false;
        toolStripStatusLabelSession.Text = dialog.SelectedPath;

        try
        {
            await _copilot.EnsureStartedAsync();
            var version = await _copilot.GetVersionAsync();
            if (!string.IsNullOrEmpty(version))
                toolStripStatusLabelVersion.Text = $"v{version}";
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

    private void RichTextBoxPrompt_KeyDown(object? sender, KeyEventArgs e)
    {
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
    }

    private void RemoveAttachment(string path, Control chip)
    {
        _attachments.Remove(path);
        flowLayoutPanelChips.Controls.Remove(chip);
        chip.Dispose();
    }

    // ── Session tabs ─────────────────────────────────────────────────────────

    private void AddSessionTab(string sessionId, bool isSubAgent)
    {
        if (!isSubAgent)
            _mainSessionId = sessionId;

        var tabTitle = isSubAgent
            ? $"Sub-agent {++_subAgentCount}"
            : "Session";

        var tabPage = new TabPage(tabTitle)
        {
            Name = sessionId,
            BackColor = AppTheme.OutputBox,
        };

        var outputFont = new Font("Cascadia Code", 10F);
        if (outputFont.Name != "Cascadia Code")
        {
            outputFont.Dispose();
            outputFont = new Font("Consolas", 10F);
        }

        var outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = outputFont,
            BackColor = AppTheme.OutputBox,
            ForeColor = AppTheme.TextPrimary,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
        };

        tabPage.Controls.Add(outputBox);
        tabControlSessions.TabPages.Add(tabPage);
        tabControlSessions.SelectedTab = tabPage;
        _sessionOutputs[sessionId] = outputBox;

        AppendColoredText(outputBox, $"[Session {sessionId[..Math.Min(8, sessionId.Length)]}... started]\r\n\r\n",
            AppTheme.ColorMeta);

        toolStripStatusLabelSession.Text =
            $"Session: {sessionId[..Math.Min(8, sessionId.Length)]}…";
    }

    // ── Output rendering ─────────────────────────────────────────────────────

    private void AppendMessage(SessionMessageEventArgs args)
    {
        if (!_sessionOutputs.TryGetValue(args.SessionId, out var box)) return;

        switch (args.Kind)
        {
            case MessageKind.AssistantDelta:
                if (!_streamingSessions.Contains(args.SessionId))
                {
                    AppendColoredText(box, "🤖 Assistant:\r\n", AppTheme.ColorAssistant);
                    _streamingSessions.Add(args.SessionId);
                }
                AppendColoredText(box, args.Content, AppTheme.ColorDefault);
                break;

            case MessageKind.AssistantFinal:
                // In streaming mode the deltas already rendered; do nothing.
                // In non-streaming mode, render the full content now.
                if (!_streamingSessions.Contains(args.SessionId))
                    AppendColoredText(box, $"🤖 Assistant:\r\n{args.Content}\r\n\r\n", AppTheme.ColorAssistant);
                break;

            case MessageKind.Reasoning:
                AppendColoredText(box, $"💭 Reasoning:\r\n{args.Content}\r\n\r\n",
                    AppTheme.ColorReasoning);
                break;

            case MessageKind.ToolStart:
                if (_streamingSessions.Remove(args.SessionId))
                    AppendColoredText(box, "\r\n", AppTheme.ColorDefault);
                AppendColoredText(box, $"  🔧 {args.Content}…  ", AppTheme.ColorTool);
                break;

            case MessageKind.ToolComplete:
                AppendColoredText(box, "✓\r\n", AppTheme.ColorAssistant);
                break;

            case MessageKind.Error:
                if (_streamingSessions.Remove(args.SessionId))
                    AppendColoredText(box, "\r\n", AppTheme.ColorDefault);
                AppendColoredText(box, $"\r\n❌ Error: {args.Content}\r\n\r\n",
                    AppTheme.ColorError);
                break;
        }

        box.ScrollToCaret();
    }

    private void OnSessionIdle(string sessionId)
    {
        if (_streamingSessions.Remove(sessionId))
        {
            if (_sessionOutputs.TryGetValue(sessionId, out var box))
                AppendColoredText(box, "\r\n\r\n", AppTheme.ColorDefault);
        }

        if (sessionId == _mainSessionId)
            SetSendingState(false);
    }

    private void AppendToSession(string sessionId, string text, Color color)
    {
        if (!_sessionOutputs.TryGetValue(sessionId, out var box)) return;
        AppendColoredText(box, text, color);
        box.ScrollToCaret();
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

    private void SetSendingState(bool isSending)
    {
        buttonSend.Enabled = !isSending && _copilot.IsConnected;
        buttonStop.Enabled = isSending;
        toolStripStatusLabelConnection.Text = isSending ? "Working…" :
            (_copilot.IsConnected ? "Connected" : "Not connected");
    }

    private void UpdateConnectionStatus(string status)
    {
        toolStripStatusLabelConnection.Text = status;
        // Enable Send as soon as the CLI server is up
        if (status == "Connected")
            buttonSend.Enabled = true;
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
        _ = _copilot.DisposeAsync().AsTask();
    }
}
