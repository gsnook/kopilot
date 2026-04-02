namespace Kopilot;

partial class MainForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        splitContainerMain = new SplitContainer();
        richTextBoxPrompt = new PlainRichTextBox();
        panelHistoryNav = new Panel();
        buttonHistoryNext = new Button();
        buttonHistoryPrev = new Button();
        panelActions = new Panel();
        checkBoxAutoApprove = new CheckBox();
        labelModel = new Label();
        comboBoxModel = new ComboBox();
        labelMode = new Label();
        comboBoxMode = new ComboBox();
        buttonOpenFolder = new Button();
        buttonStop = new Button();
        buttonSend = new Button();
        checkBoxFleet = new CheckBox();
        richTextBoxOutput = new RichTextBox();
        panelQuickCommands = new Panel();
        buttonHelp = new Button();
        buttonPowershell = new Button();
        buttonSummarize = new Button();
        buttonClearOutput = new Button();
        buttonBackup = new Button();
        buttonOpenExplorer = new Button();
        buttonOpenVSCode = new Button();
        panelAttachments = new Panel();
        flowLayoutPanelChips = new FlowLayoutPanel();
        buttonAddFolder = new Button();
        buttonAddFile = new Button();
        labelAttach = new Label();
        statusStrip = new StatusStrip();
        toolStripStatusLabelConnection = new ToolStripStatusLabel();
        toolStripStatusLabelVersion = new ToolStripStatusLabel();
        toolStripStatusLabelSep = new ToolStripSeparator();
        toolStripStatusLabelAgentStatus = new ToolStripStatusLabel();
        toolStripStatusLabelSession = new ToolStripStatusLabel();
        toolTipMain = new ToolTip(components);
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
        splitContainerMain.Panel1.SuspendLayout();
        splitContainerMain.Panel2.SuspendLayout();
        splitContainerMain.SuspendLayout();
        panelHistoryNav.SuspendLayout();
        panelActions.SuspendLayout();
        panelQuickCommands.SuspendLayout();
        panelAttachments.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // splitContainerMain
        // 
        splitContainerMain.BackColor = Color.FromArgb(64, 64, 64);
        splitContainerMain.Dock = DockStyle.Fill;
        splitContainerMain.Location = new Point(0, 0);
        splitContainerMain.Name = "splitContainerMain";
        splitContainerMain.Orientation = Orientation.Horizontal;
        // 
        // splitContainerMain.Panel1
        // 
        splitContainerMain.Panel1.Controls.Add(richTextBoxPrompt);
        splitContainerMain.Panel1.Controls.Add(panelHistoryNav);
        splitContainerMain.Panel1.Controls.Add(panelActions);
        splitContainerMain.Panel1MinSize = 100;
        // 
        // splitContainerMain.Panel2
        // 
        splitContainerMain.Panel2.Controls.Add(richTextBoxOutput);
        splitContainerMain.Panel2.Controls.Add(panelQuickCommands);
        splitContainerMain.Panel2.Controls.Add(panelAttachments);
        splitContainerMain.Panel2MinSize = 180;
        splitContainerMain.Size = new Size(1034, 1095);
        splitContainerMain.SplitterDistance = 260;
        splitContainerMain.TabIndex = 0;
        // 
        // richTextBoxPrompt
        // 
        richTextBoxPrompt.AcceptsTab = true;
        richTextBoxPrompt.AllowDrop = true;
        richTextBoxPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        richTextBoxPrompt.BackColor = Color.FromArgb(52, 52, 52);
        richTextBoxPrompt.Font = new Font("Segoe UI", 11F);
        richTextBoxPrompt.ForeColor = Color.FromArgb(218, 218, 218);
        richTextBoxPrompt.Location = new Point(26, 38);
        richTextBoxPrompt.Name = "richTextBoxPrompt";
        richTextBoxPrompt.ScrollBars = RichTextBoxScrollBars.Vertical;
        richTextBoxPrompt.Size = new Size(996, 222);
        richTextBoxPrompt.TabIndex = 1;
        richTextBoxPrompt.Text = "";
        toolTipMain.SetToolTip(richTextBoxPrompt, "Ctrl+Enter to send");
        // 
        // panelHistoryNav
        // 
        panelHistoryNav.BackColor = Color.FromArgb(52, 52, 52);
        panelHistoryNav.Controls.Add(buttonHistoryNext);
        panelHistoryNav.Controls.Add(buttonHistoryPrev);
        panelHistoryNav.Dock = DockStyle.Left;
        panelHistoryNav.Location = new Point(0, 38);
        panelHistoryNav.Name = "panelHistoryNav";
        panelHistoryNav.Size = new Size(20, 222);
        panelHistoryNav.TabIndex = 3;
        // 
        // buttonHistoryNext
        // 
        buttonHistoryNext.BackColor = Color.FromArgb(52, 52, 52);
        buttonHistoryNext.Dock = DockStyle.Bottom;
        buttonHistoryNext.Enabled = false;
        buttonHistoryNext.FlatAppearance.BorderSize = 0;
        buttonHistoryNext.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 72, 72);
        buttonHistoryNext.FlatStyle = FlatStyle.Flat;
        buttonHistoryNext.Font = new Font("Segoe UI", 7F);
        buttonHistoryNext.ForeColor = Color.FromArgb(148, 148, 148);
        buttonHistoryNext.Location = new Point(0, 202);
        buttonHistoryNext.Name = "buttonHistoryNext";
        buttonHistoryNext.Size = new Size(20, 20);
        buttonHistoryNext.TabIndex = 0;
        buttonHistoryNext.TabStop = false;
        buttonHistoryNext.Text = "▼";
        toolTipMain.SetToolTip(buttonHistoryNext, "Next prompt (newer)");
        buttonHistoryNext.UseVisualStyleBackColor = false;
        // 
        // buttonHistoryPrev
        // 
        buttonHistoryPrev.BackColor = Color.FromArgb(52, 52, 52);
        buttonHistoryPrev.Dock = DockStyle.Top;
        buttonHistoryPrev.Enabled = false;
        buttonHistoryPrev.FlatAppearance.BorderSize = 0;
        buttonHistoryPrev.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 72, 72);
        buttonHistoryPrev.FlatStyle = FlatStyle.Flat;
        buttonHistoryPrev.Font = new Font("Segoe UI", 7F);
        buttonHistoryPrev.ForeColor = Color.FromArgb(148, 148, 148);
        buttonHistoryPrev.Location = new Point(0, 0);
        buttonHistoryPrev.Name = "buttonHistoryPrev";
        buttonHistoryPrev.Size = new Size(20, 20);
        buttonHistoryPrev.TabIndex = 1;
        buttonHistoryPrev.TabStop = false;
        buttonHistoryPrev.Text = "▲";
        toolTipMain.SetToolTip(buttonHistoryPrev, "Previous prompt (older)");
        buttonHistoryPrev.UseVisualStyleBackColor = false;
        // 
        // panelActions
        // 
        panelActions.BackColor = Color.FromArgb(64, 64, 64);
        panelActions.Controls.Add(checkBoxAutoApprove);
        panelActions.Controls.Add(labelModel);
        panelActions.Controls.Add(comboBoxModel);
        panelActions.Controls.Add(labelMode);
        panelActions.Controls.Add(comboBoxMode);
        panelActions.Controls.Add(buttonOpenFolder);
        panelActions.Controls.Add(buttonStop);
        panelActions.Controls.Add(buttonSend);
        panelActions.Controls.Add(checkBoxFleet);
        panelActions.Dock = DockStyle.Top;
        panelActions.Location = new Point(0, 0);
        panelActions.Name = "panelActions";
        panelActions.Padding = new Padding(4, 4, 8, 4);
        panelActions.Size = new Size(1034, 38);
        panelActions.TabIndex = 2;
        // 
        // checkBoxAutoApprove
        // 
        checkBoxAutoApprove.Anchor = AnchorStyles.Right;
        checkBoxAutoApprove.AutoSize = true;
        checkBoxAutoApprove.BackColor = Color.Transparent;
        checkBoxAutoApprove.Checked = true;
        checkBoxAutoApprove.CheckState = CheckState.Checked;
        checkBoxAutoApprove.Font = new Font("Segoe UI", 9F);
        checkBoxAutoApprove.ForeColor = Color.FromArgb(218, 218, 218);
        checkBoxAutoApprove.Location = new Point(674, 9);
        checkBoxAutoApprove.Name = "checkBoxAutoApprove";
        checkBoxAutoApprove.Size = new Size(129, 19);
        checkBoxAutoApprove.TabIndex = 0;
        checkBoxAutoApprove.Text = "Auto-approve tools";
        toolTipMain.SetToolTip(checkBoxAutoApprove, "Automatically approve all tool executions without prompting");
        checkBoxAutoApprove.UseVisualStyleBackColor = true;
        // 
        // labelModel
        // 
        labelModel.AutoSize = true;
        labelModel.Font = new Font("Segoe UI", 9F);
        labelModel.ForeColor = Color.FromArgb(218, 218, 218);
        labelModel.Location = new Point(146, 11);
        labelModel.Name = "labelModel";
        labelModel.Size = new Size(44, 15);
        labelModel.TabIndex = 1;
        labelModel.Text = "Model:";
        // 
        // comboBoxModel
        // 
        comboBoxModel.BackColor = Color.FromArgb(52, 52, 52);
        comboBoxModel.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBoxModel.FlatStyle = FlatStyle.Flat;
        comboBoxModel.Font = new Font("Segoe UI", 9F);
        comboBoxModel.ForeColor = Color.FromArgb(218, 218, 218);
        comboBoxModel.FormattingEnabled = true;
        comboBoxModel.Items.AddRange(new object[] { "gpt-4.1", "gpt-5", "claude-sonnet-4.5", "claude-sonnet-4.6", "claude-opus-4.5" });
        comboBoxModel.Location = new Point(199, 7);
        comboBoxModel.Name = "comboBoxModel";
        comboBoxModel.Size = new Size(184, 23);
        comboBoxModel.TabIndex = 2;
        // 
        // labelMode
        // 
        labelMode.AutoSize = true;
        labelMode.Font = new Font("Segoe UI", 9F);
        labelMode.ForeColor = Color.FromArgb(218, 218, 218);
        labelMode.Location = new Point(392, 11);
        labelMode.Name = "labelMode";
        labelMode.Size = new Size(41, 15);
        labelMode.TabIndex = 7;
        labelMode.Text = "Mode:";
        // 
        // comboBoxMode
        // 
        comboBoxMode.BackColor = Color.FromArgb(52, 52, 52);
        comboBoxMode.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBoxMode.FlatStyle = FlatStyle.Flat;
        comboBoxMode.Font = new Font("Segoe UI", 9F);
        comboBoxMode.ForeColor = Color.FromArgb(218, 218, 218);
        comboBoxMode.FormattingEnabled = true;
        comboBoxMode.Items.AddRange(new object[] { "Standard", "Plan", "Autopilot" });
        comboBoxMode.Location = new Point(442, 7);
        comboBoxMode.Name = "comboBoxMode";
        comboBoxMode.Size = new Size(184, 23);
        comboBoxMode.TabIndex = 8;
        toolTipMain.SetToolTip(comboBoxMode, "Standard: normal chat  |  Plan: plan before acting  |  Autopilot: fully autonomous");
        // 
        // buttonOpenFolder
        // 
        buttonOpenFolder.BackColor = Color.FromArgb(60, 112, 160);
        buttonOpenFolder.FlatAppearance.BorderSize = 0;
        buttonOpenFolder.FlatStyle = FlatStyle.Flat;
        buttonOpenFolder.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        buttonOpenFolder.ForeColor = Color.FromArgb(235, 235, 235);
        buttonOpenFolder.Location = new Point(7, 4);
        buttonOpenFolder.Name = "buttonOpenFolder";
        buttonOpenFolder.Size = new Size(130, 28);
        buttonOpenFolder.TabIndex = 6;
        buttonOpenFolder.Text = "📂 Open Folder…";
        toolTipMain.SetToolTip(buttonOpenFolder, "Select a project folder and connect to Copilot");
        buttonOpenFolder.UseVisualStyleBackColor = false;
        // 
        // buttonStop
        // 
        buttonStop.Anchor = AnchorStyles.Right;
        buttonStop.BackColor = Color.FromArgb(86, 86, 86);
        buttonStop.Enabled = false;
        buttonStop.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonStop.FlatStyle = FlatStyle.Flat;
        buttonStop.Font = new Font("Segoe UI", 9F);
        buttonStop.ForeColor = Color.FromArgb(218, 218, 218);
        buttonStop.Location = new Point(900, 5);
        buttonStop.Name = "buttonStop";
        buttonStop.Size = new Size(60, 28);
        buttonStop.TabIndex = 4;
        buttonStop.Text = "⬛ Stop";
        toolTipMain.SetToolTip(buttonStop, "Stop the current Copilot response");
        buttonStop.UseVisualStyleBackColor = false;
        // 
        // buttonSend
        // 
        buttonSend.Anchor = AnchorStyles.Right;
        buttonSend.BackColor = Color.FromArgb(60, 112, 160);
        buttonSend.Enabled = false;
        buttonSend.FlatAppearance.BorderSize = 0;
        buttonSend.FlatStyle = FlatStyle.Flat;
        buttonSend.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        buttonSend.ForeColor = Color.FromArgb(235, 235, 235);
        buttonSend.Location = new Point(966, 5);
        buttonSend.Name = "buttonSend";
        buttonSend.Size = new Size(60, 28);
        buttonSend.TabIndex = 5;
        buttonSend.Text = "▶ Send";
        toolTipMain.SetToolTip(buttonSend, "Send prompt to Copilot (Ctrl+Enter)");
        buttonSend.UseVisualStyleBackColor = false;
        // 
        // checkBoxFleet
        // 
        checkBoxFleet.Anchor = AnchorStyles.Right;
        checkBoxFleet.AutoSize = true;
        checkBoxFleet.BackColor = Color.Transparent;
        checkBoxFleet.Font = new Font("Segoe UI", 9F);
        checkBoxFleet.ForeColor = Color.FromArgb(218, 218, 218);
        checkBoxFleet.Location = new Point(803, 9);
        checkBoxFleet.Name = "checkBoxFleet";
        checkBoxFleet.Size = new Size(85, 19);
        checkBoxFleet.TabIndex = 1;
        checkBoxFleet.Text = "Fleet mode";
        toolTipMain.SetToolTip(checkBoxFleet, "Activate Fleet mode — Copilot spawns and coordinates multiple sub-agents to work in parallel on complex tasks");
        checkBoxFleet.UseVisualStyleBackColor = true;
        // 
        // richTextBoxOutput
        // 
        richTextBoxOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        richTextBoxOutput.BackColor = Color.FromArgb(0, 0, 0);
        richTextBoxOutput.BorderStyle = BorderStyle.None;
        richTextBoxOutput.DetectUrls = false;
        richTextBoxOutput.Font = new Font("Consolas", 10F);
        richTextBoxOutput.ForeColor = Color.FromArgb(218, 218, 218);
        richTextBoxOutput.Location = new Point(6, 32);
        richTextBoxOutput.Name = "richTextBoxOutput";
        richTextBoxOutput.ReadOnly = true;
        richTextBoxOutput.ScrollBars = RichTextBoxScrollBars.Vertical;
        richTextBoxOutput.Size = new Size(1016, 758);
        richTextBoxOutput.TabIndex = 0;
        richTextBoxOutput.Text = "";
        // 
        // panelQuickCommands
        // 
        panelQuickCommands.BackColor = Color.FromArgb(74, 74, 74);
        panelQuickCommands.Controls.Add(buttonHelp);
        panelQuickCommands.Controls.Add(buttonPowershell);
        panelQuickCommands.Controls.Add(buttonSummarize);
        panelQuickCommands.Controls.Add(buttonClearOutput);
        panelQuickCommands.Controls.Add(buttonBackup);
        panelQuickCommands.Controls.Add(buttonOpenExplorer);
        panelQuickCommands.Controls.Add(buttonOpenVSCode);
        panelQuickCommands.Dock = DockStyle.Bottom;
        panelQuickCommands.Location = new Point(0, 790);
        panelQuickCommands.Name = "panelQuickCommands";
        panelQuickCommands.Padding = new Padding(4);
        panelQuickCommands.Size = new Size(1034, 41);
        panelQuickCommands.TabIndex = 1;
        // 
        // buttonHelp
        // 
        buttonHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        buttonHelp.BackColor = Color.FromArgb(86, 86, 86);
        buttonHelp.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonHelp.FlatStyle = FlatStyle.Flat;
        buttonHelp.Font = new Font("Segoe UI", 8.5F);
        buttonHelp.ForeColor = Color.FromArgb(218, 218, 218);
        buttonHelp.Location = new Point(7, 7);
        buttonHelp.Name = "buttonHelp";
        buttonHelp.Size = new Size(72, 26);
        buttonHelp.TabIndex = 0;
        buttonHelp.Text = "❓ Help";
        toolTipMain.SetToolTip(buttonHelp, "Ask Copilot for a capabilities and tools overview");
        buttonHelp.UseVisualStyleBackColor = false;
        // 
        // buttonPowershell
        // 
        buttonPowershell.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        buttonPowershell.BackColor = Color.FromArgb(86, 86, 86);
        buttonPowershell.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonPowershell.FlatStyle = FlatStyle.Flat;
        buttonPowershell.Font = new Font("Segoe UI", 8.5F);
        buttonPowershell.ForeColor = Color.FromArgb(218, 218, 218);
        buttonPowershell.Location = new Point(85, 7);
        buttonPowershell.Name = "buttonPowershell";
        buttonPowershell.Size = new Size(96, 26);
        buttonPowershell.TabIndex = 1;
        buttonPowershell.Text = "⚡ PowerShell";
        toolTipMain.SetToolTip(buttonPowershell, "Open PowerShell in the current project folder");
        buttonPowershell.UseVisualStyleBackColor = false;
        // 
        // buttonSummarize
        // 
        buttonSummarize.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        buttonSummarize.BackColor = Color.FromArgb(86, 86, 86);
        buttonSummarize.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonSummarize.FlatStyle = FlatStyle.Flat;
        buttonSummarize.Font = new Font("Segoe UI", 8.5F);
        buttonSummarize.ForeColor = Color.FromArgb(218, 218, 218);
        buttonSummarize.Location = new Point(377, 7);
        buttonSummarize.Name = "buttonSummarize";
        buttonSummarize.Size = new Size(100, 26);
        buttonSummarize.TabIndex = 2;
        buttonSummarize.Text = "📝 Summarize";
        toolTipMain.SetToolTip(buttonSummarize, "Ask Copilot to summarize the session so far");
        buttonSummarize.UseVisualStyleBackColor = false;
        // 
        // buttonClearOutput
        // 
        buttonClearOutput.Anchor = AnchorStyles.Bottom;
        buttonClearOutput.BackColor = Color.FromArgb(86, 86, 86);
        buttonClearOutput.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonClearOutput.FlatStyle = FlatStyle.Flat;
        buttonClearOutput.Font = new Font("Segoe UI", 8.5F);
        buttonClearOutput.ForeColor = Color.FromArgb(218, 218, 218);
        buttonClearOutput.Location = new Point(519, 7);
        buttonClearOutput.Name = "buttonClearOutput";
        buttonClearOutput.Size = new Size(68, 26);
        buttonClearOutput.TabIndex = 3;
        buttonClearOutput.Text = "🗑 Clear";
        toolTipMain.SetToolTip(buttonClearOutput, "Clear the current output window");
        buttonClearOutput.UseVisualStyleBackColor = false;
        // 
        // buttonBackup
        // 
        buttonBackup.Anchor = AnchorStyles.Right;
        buttonBackup.BackColor = Color.FromArgb(86, 86, 86);
        buttonBackup.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonBackup.FlatStyle = FlatStyle.Flat;
        buttonBackup.Font = new Font("Segoe UI", 8.5F);
        buttonBackup.ForeColor = Color.FromArgb(218, 218, 218);
        buttonBackup.Location = new Point(938, 7);
        buttonBackup.Name = "buttonBackup";
        buttonBackup.Size = new Size(88, 26);
        buttonBackup.TabIndex = 4;
        buttonBackup.Text = "💾 Backup";
        toolTipMain.SetToolTip(buttonBackup, "Ask Copilot to write a session-resume document to a Markdown file");
        buttonBackup.UseVisualStyleBackColor = false;
        // 
        // buttonOpenExplorer
        // 
        buttonOpenExplorer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        buttonOpenExplorer.BackColor = Color.FromArgb(86, 86, 86);
        buttonOpenExplorer.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonOpenExplorer.FlatStyle = FlatStyle.Flat;
        buttonOpenExplorer.Font = new Font("Segoe UI", 8.5F);
        buttonOpenExplorer.ForeColor = Color.FromArgb(218, 218, 218);
        buttonOpenExplorer.Location = new Point(187, 8);
        buttonOpenExplorer.Name = "buttonOpenExplorer";
        buttonOpenExplorer.Size = new Size(88, 26);
        buttonOpenExplorer.TabIndex = 5;
        buttonOpenExplorer.Text = "📂 Explorer";
        toolTipMain.SetToolTip(buttonOpenExplorer, "Open File Explorer in the current session folder");
        buttonOpenExplorer.UseVisualStyleBackColor = false;
        // 
        // buttonOpenVSCode
        // 
        buttonOpenVSCode.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        buttonOpenVSCode.BackColor = Color.FromArgb(86, 86, 86);
        buttonOpenVSCode.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonOpenVSCode.FlatStyle = FlatStyle.Flat;
        buttonOpenVSCode.Font = new Font("Segoe UI", 8.5F);
        buttonOpenVSCode.ForeColor = Color.FromArgb(218, 218, 218);
        buttonOpenVSCode.Location = new Point(281, 8);
        buttonOpenVSCode.Name = "buttonOpenVSCode";
        buttonOpenVSCode.Size = new Size(90, 26);
        buttonOpenVSCode.TabIndex = 6;
        buttonOpenVSCode.Text = "💻 VS Code";
        toolTipMain.SetToolTip(buttonOpenVSCode, "Open VS Code in the session folder and connect with /ide");
        buttonOpenVSCode.UseVisualStyleBackColor = false;
        // 
        // panelAttachments
        // 
        panelAttachments.BackColor = Color.FromArgb(64, 64, 64);
        panelAttachments.Controls.Add(flowLayoutPanelChips);
        panelAttachments.Controls.Add(buttonAddFolder);
        panelAttachments.Controls.Add(buttonAddFile);
        panelAttachments.Controls.Add(labelAttach);
        panelAttachments.Dock = DockStyle.Top;
        panelAttachments.Location = new Point(0, 0);
        panelAttachments.Name = "panelAttachments";
        panelAttachments.Size = new Size(1034, 32);
        panelAttachments.TabIndex = 0;
        // 
        // flowLayoutPanelChips
        // 
        flowLayoutPanelChips.AutoSize = true;
        flowLayoutPanelChips.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flowLayoutPanelChips.BackColor = Color.FromArgb(64, 64, 64);
        flowLayoutPanelChips.Location = new Point(290, 6);
        flowLayoutPanelChips.Name = "flowLayoutPanelChips";
        flowLayoutPanelChips.Size = new Size(0, 0);
        flowLayoutPanelChips.TabIndex = 3;
        flowLayoutPanelChips.WrapContents = false;
        // 
        // buttonAddFolder
        // 
        buttonAddFolder.BackColor = Color.FromArgb(86, 86, 86);
        buttonAddFolder.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonAddFolder.FlatStyle = FlatStyle.Flat;
        buttonAddFolder.Font = new Font("Segoe UI", 9F);
        buttonAddFolder.ForeColor = Color.FromArgb(218, 218, 218);
        buttonAddFolder.Location = new Point(184, 0);
        buttonAddFolder.Name = "buttonAddFolder";
        buttonAddFolder.Size = new Size(100, 26);
        buttonAddFolder.TabIndex = 2;
        buttonAddFolder.Text = "📁 Add Folder";
        toolTipMain.SetToolTip(buttonAddFolder, "Attach a folder to the prompt");
        buttonAddFolder.UseVisualStyleBackColor = false;
        // 
        // buttonAddFile
        // 
        buttonAddFile.BackColor = Color.FromArgb(86, 86, 86);
        buttonAddFile.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonAddFile.FlatStyle = FlatStyle.Flat;
        buttonAddFile.Font = new Font("Segoe UI", 9F);
        buttonAddFile.ForeColor = Color.FromArgb(218, 218, 218);
        buttonAddFile.Location = new Point(91, 0);
        buttonAddFile.Name = "buttonAddFile";
        buttonAddFile.Size = new Size(88, 26);
        buttonAddFile.TabIndex = 1;
        buttonAddFile.Text = "📄 Add File";
        toolTipMain.SetToolTip(buttonAddFile, "Attach a file to the prompt");
        buttonAddFile.UseVisualStyleBackColor = false;
        // 
        // labelAttach
        // 
        labelAttach.AutoSize = true;
        labelAttach.Font = new Font("Segoe UI", 9F);
        labelAttach.ForeColor = Color.FromArgb(148, 148, 148);
        labelAttach.Location = new Point(7, 4);
        labelAttach.Name = "labelAttach";
        labelAttach.Size = new Size(78, 15);
        labelAttach.TabIndex = 0;
        labelAttach.Text = "Attachments:";
        // 
        // statusStrip
        // 
        statusStrip.BackColor = Color.FromArgb(56, 56, 56);
        statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabelConnection, toolStripStatusLabelVersion, toolStripStatusLabelSep, toolStripStatusLabelAgentStatus, toolStripStatusLabelSession });
        statusStrip.Location = new Point(0, 1095);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1034, 23);
        statusStrip.TabIndex = 1;
        // 
        // toolStripStatusLabelConnection
        // 
        toolStripStatusLabelConnection.ForeColor = Color.FromArgb(218, 218, 218);
        toolStripStatusLabelConnection.Name = "toolStripStatusLabelConnection";
        toolStripStatusLabelConnection.Size = new Size(86, 18);
        toolStripStatusLabelConnection.Text = "Not connected";
        // 
        // toolStripStatusLabelVersion
        // 
        toolStripStatusLabelVersion.ForeColor = Color.FromArgb(148, 148, 148);
        toolStripStatusLabelVersion.Name = "toolStripStatusLabelVersion";
        toolStripStatusLabelVersion.Padding = new Padding(6, 0, 0, 0);
        toolStripStatusLabelVersion.Size = new Size(6, 18);
        // 
        // toolStripStatusLabelSep
        // 
        toolStripStatusLabelSep.ForeColor = Color.FromArgb(148, 148, 148);
        toolStripStatusLabelSep.Name = "toolStripStatusLabelSep";
        toolStripStatusLabelSep.Size = new Size(6, 23);
        // 
        // toolStripStatusLabelAgentStatus
        // 
        toolStripStatusLabelAgentStatus.ForeColor = Color.FromArgb(200, 200, 200);
        toolStripStatusLabelAgentStatus.Name = "toolStripStatusLabelAgentStatus";
        toolStripStatusLabelAgentStatus.Size = new Size(921, 18);
        toolStripStatusLabelAgentStatus.Spring = true;
        toolStripStatusLabelAgentStatus.Text = "Ready for next command";
        // 
        // toolStripStatusLabelSession
        // 
        toolStripStatusLabelSession.ForeColor = Color.FromArgb(148, 148, 148);
        toolStripStatusLabelSession.Name = "toolStripStatusLabelSession";
        toolStripStatusLabelSession.Size = new Size(0, 18);
        toolStripStatusLabelSession.TextAlign = ContentAlignment.MiddleRight;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(64, 64, 64);
        ClientSize = new Size(1034, 1118);
        Controls.Add(splitContainerMain);
        Controls.Add(statusStrip);
        Font = new Font("Segoe UI", 9F);
        Icon = (Icon)resources.GetObject("$this.Icon");
        MinimumSize = new Size(1050, 600);
        Name = "MainForm";
        Text = "Kopilot";
        splitContainerMain.Panel1.ResumeLayout(false);
        splitContainerMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
        splitContainerMain.ResumeLayout(false);
        panelHistoryNav.ResumeLayout(false);
        panelActions.ResumeLayout(false);
        panelActions.PerformLayout();
        panelQuickCommands.ResumeLayout(false);
        panelAttachments.ResumeLayout(false);
        panelAttachments.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.SplitContainer splitContainerMain;
    private System.Windows.Forms.RichTextBox richTextBoxOutput;
    private System.Windows.Forms.Panel panelQuickCommands;
    private System.Windows.Forms.Button buttonHelp;
    private System.Windows.Forms.Button buttonPowershell;
    private System.Windows.Forms.Button buttonSummarize;
    private System.Windows.Forms.Button buttonClearOutput;
    private System.Windows.Forms.Button buttonBackup;
    private System.Windows.Forms.Button buttonOpenExplorer;
    private System.Windows.Forms.Button buttonOpenVSCode;
    private System.Windows.Forms.Panel panelAttachments;
    private System.Windows.Forms.Label labelAttach;
    private System.Windows.Forms.Button buttonAddFile;
    private System.Windows.Forms.Button buttonAddFolder;
    private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelChips;
    private PlainRichTextBox richTextBoxPrompt;
    private System.Windows.Forms.Panel panelActions;
    private System.Windows.Forms.Panel panelHistoryNav;
    private System.Windows.Forms.Button buttonHistoryPrev;
    private System.Windows.Forms.Button buttonHistoryNext;
    private System.Windows.Forms.CheckBox checkBoxAutoApprove;
    private System.Windows.Forms.CheckBox checkBoxFleet;
    private System.Windows.Forms.Label labelModel;
    private System.Windows.Forms.ComboBox comboBoxModel;
    private System.Windows.Forms.Label labelMode;
    private System.Windows.Forms.ComboBox comboBoxMode;
    private System.Windows.Forms.Button buttonOpenFolder;
    private System.Windows.Forms.Button buttonStop;
    private System.Windows.Forms.Button buttonSend;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelConnection;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelVersion;
    private System.Windows.Forms.ToolStripSeparator toolStripStatusLabelSep;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelAgentStatus;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelSession;
    private System.Windows.Forms.ToolTip toolTipMain;
}
