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
        splitContainerMain = new SplitContainer();
        richTextBoxOutput = new RichTextBox();
        panelQuickCommands = new Panel();
        buttonHelp = new Button();
        buttonCommands = new Button();
        buttonSummarize = new Button();
        buttonClearOutput = new Button();
        buttonBackup = new Button();
        buttonOpenExplorer = new Button();
        tableLayoutPanelPrompt = new TableLayoutPanel();
        panelAttachments = new Panel();
        flowLayoutPanelChips = new FlowLayoutPanel();
        buttonAddFolder = new Button();
        buttonAddFile = new Button();
        labelAttach = new Label();
        richTextBoxPrompt = new PlainRichTextBox();
        panelActions = new Panel();
        checkBoxAutoApprove = new CheckBox();
        labelModel = new Label();
        comboBoxModel = new ComboBox();
        labelMode = new Label();
        comboBoxMode = new ComboBox();
        buttonOpenFolder = new Button();
        buttonStop = new Button();
        buttonSend = new Button();
        statusStrip = new StatusStrip();
        toolStripStatusLabelConnection = new ToolStripStatusLabel();
        toolStripStatusLabelVersion = new ToolStripStatusLabel();
        toolStripStatusLabelSep = new ToolStripSeparator();
        toolStripStatusLabelSession = new ToolStripStatusLabel();
        toolTipMain = new ToolTip(components);
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
        splitContainerMain.Panel1.SuspendLayout();
        splitContainerMain.Panel2.SuspendLayout();
        splitContainerMain.SuspendLayout();
        panelQuickCommands.SuspendLayout();
        tableLayoutPanelPrompt.SuspendLayout();
        panelAttachments.SuspendLayout();
        panelActions.SuspendLayout();
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
        splitContainerMain.Panel1.Controls.Add(richTextBoxOutput);
        splitContainerMain.Panel1.Controls.Add(panelQuickCommands);
        splitContainerMain.Panel1MinSize = 200;
        // 
        // splitContainerMain.Panel2
        // 
        splitContainerMain.Panel2.Controls.Add(tableLayoutPanelPrompt);
        splitContainerMain.Panel2MinSize = 180;
        splitContainerMain.Size = new Size(1200, 777);
        splitContainerMain.SplitterDistance = 519;
        splitContainerMain.TabIndex = 0;
        // 
        // richTextBoxOutput
        // 
        richTextBoxOutput.BackColor = Color.FromArgb(0, 0, 0);
        richTextBoxOutput.BorderStyle = BorderStyle.None;
        richTextBoxOutput.DetectUrls = false;
        richTextBoxOutput.Dock = DockStyle.Fill;
        richTextBoxOutput.Font = new Font("Consolas", 10F);
        richTextBoxOutput.ForeColor = Color.FromArgb(218, 218, 218);
        richTextBoxOutput.Location = new Point(0, 36);
        richTextBoxOutput.Name = "richTextBoxOutput";
        richTextBoxOutput.ReadOnly = true;
        richTextBoxOutput.ScrollBars = RichTextBoxScrollBars.Vertical;
        richTextBoxOutput.Size = new Size(1200, 483);
        richTextBoxOutput.TabIndex = 0;
        richTextBoxOutput.Text = "";
        // 
        // panelQuickCommands
        // 
        panelQuickCommands.BackColor = Color.FromArgb(74, 74, 74);
        panelQuickCommands.Controls.Add(buttonHelp);
        panelQuickCommands.Controls.Add(buttonCommands);
        panelQuickCommands.Controls.Add(buttonSummarize);
        panelQuickCommands.Controls.Add(buttonClearOutput);
        panelQuickCommands.Controls.Add(buttonBackup);
        panelQuickCommands.Controls.Add(buttonOpenExplorer);
        panelQuickCommands.Dock = DockStyle.Top;
        panelQuickCommands.Location = new Point(0, 0);
        panelQuickCommands.Name = "panelQuickCommands";
        panelQuickCommands.Padding = new Padding(4);
        panelQuickCommands.Size = new Size(1200, 36);
        panelQuickCommands.TabIndex = 1;
        // 
        // buttonHelp
        // 
        buttonHelp.BackColor = Color.FromArgb(86, 86, 86);
        buttonHelp.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonHelp.FlatStyle = FlatStyle.Flat;
        buttonHelp.Font = new Font("Segoe UI", 8.5F);
        buttonHelp.ForeColor = Color.FromArgb(218, 218, 218);
        buttonHelp.Location = new Point(6, 5);
        buttonHelp.Name = "buttonHelp";
        buttonHelp.Size = new Size(72, 26);
        buttonHelp.TabIndex = 0;
        buttonHelp.Text = "❓ Help";
        toolTipMain.SetToolTip(buttonHelp, "Ask Copilot to describe its capabilities");
        buttonHelp.UseVisualStyleBackColor = false;
        // 
        // buttonCommands
        // 
        buttonCommands.BackColor = Color.FromArgb(86, 86, 86);
        buttonCommands.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonCommands.FlatStyle = FlatStyle.Flat;
        buttonCommands.Font = new Font("Segoe UI", 8.5F);
        buttonCommands.ForeColor = Color.FromArgb(218, 218, 218);
        buttonCommands.Location = new Point(84, 5);
        buttonCommands.Name = "buttonCommands";
        buttonCommands.Size = new Size(96, 26);
        buttonCommands.TabIndex = 1;
        buttonCommands.Text = "📋 Commands";
        toolTipMain.SetToolTip(buttonCommands, "List available tools and capabilities");
        buttonCommands.UseVisualStyleBackColor = false;
        // 
        // buttonSummarize
        // 
        buttonSummarize.BackColor = Color.FromArgb(86, 86, 86);
        buttonSummarize.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonSummarize.FlatStyle = FlatStyle.Flat;
        buttonSummarize.Font = new Font("Segoe UI", 8.5F);
        buttonSummarize.ForeColor = Color.FromArgb(218, 218, 218);
        buttonSummarize.Location = new Point(186, 5);
        buttonSummarize.Name = "buttonSummarize";
        buttonSummarize.Size = new Size(100, 26);
        buttonSummarize.TabIndex = 2;
        buttonSummarize.Text = "📝 Summarize";
        toolTipMain.SetToolTip(buttonSummarize, "Ask Copilot to summarize the session so far");
        buttonSummarize.UseVisualStyleBackColor = false;
        // 
        // buttonClearOutput
        // 
        buttonClearOutput.BackColor = Color.FromArgb(86, 86, 86);
        buttonClearOutput.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonClearOutput.FlatStyle = FlatStyle.Flat;
        buttonClearOutput.Font = new Font("Segoe UI", 8.5F);
        buttonClearOutput.ForeColor = Color.FromArgb(218, 218, 218);
        buttonClearOutput.Location = new Point(557, 5);
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
        buttonBackup.Location = new Point(1104, 5);
        buttonBackup.Name = "buttonBackup";
        buttonBackup.Size = new Size(88, 26);
        buttonBackup.TabIndex = 4;
        buttonBackup.Text = "💾 Backup";
        toolTipMain.SetToolTip(buttonBackup, "Ask Copilot to write a session-resume document to a Markdown file");
        buttonBackup.UseVisualStyleBackColor = false;
        // 
        // buttonOpenExplorer
        // 
        buttonOpenExplorer.Anchor = AnchorStyles.Right;
        buttonOpenExplorer.BackColor = Color.FromArgb(86, 86, 86);
        buttonOpenExplorer.FlatAppearance.BorderColor = Color.FromArgb(108, 108, 108);
        buttonOpenExplorer.FlatStyle = FlatStyle.Flat;
        buttonOpenExplorer.Font = new Font("Segoe UI", 8.5F);
        buttonOpenExplorer.ForeColor = Color.FromArgb(218, 218, 218);
        buttonOpenExplorer.Location = new Point(292, 5);
        buttonOpenExplorer.Name = "buttonOpenExplorer";
        buttonOpenExplorer.Size = new Size(88, 26);
        buttonOpenExplorer.TabIndex = 5;
        buttonOpenExplorer.Text = "📂 Explorer";
        toolTipMain.SetToolTip(buttonOpenExplorer, "Open File Explorer in the current session folder");
        buttonOpenExplorer.UseVisualStyleBackColor = false;
        // 
        // tableLayoutPanelPrompt
        // 
        tableLayoutPanelPrompt.BackColor = Color.FromArgb(64, 64, 64);
        tableLayoutPanelPrompt.ColumnCount = 1;
        tableLayoutPanelPrompt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanelPrompt.Controls.Add(panelAttachments, 0, 0);
        tableLayoutPanelPrompt.Controls.Add(richTextBoxPrompt, 0, 1);
        tableLayoutPanelPrompt.Controls.Add(panelActions, 0, 2);
        tableLayoutPanelPrompt.Dock = DockStyle.Fill;
        tableLayoutPanelPrompt.Location = new Point(0, 0);
        tableLayoutPanelPrompt.Name = "tableLayoutPanelPrompt";
        tableLayoutPanelPrompt.RowCount = 3;
        tableLayoutPanelPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        tableLayoutPanelPrompt.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanelPrompt.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        tableLayoutPanelPrompt.Size = new Size(1200, 254);
        tableLayoutPanelPrompt.TabIndex = 0;
        // 
        // panelAttachments
        // 
        panelAttachments.BackColor = Color.FromArgb(64, 64, 64);
        panelAttachments.Controls.Add(flowLayoutPanelChips);
        panelAttachments.Controls.Add(buttonAddFolder);
        panelAttachments.Controls.Add(buttonAddFile);
        panelAttachments.Controls.Add(labelAttach);
        panelAttachments.Dock = DockStyle.Fill;
        panelAttachments.Location = new Point(3, 3);
        panelAttachments.Name = "panelAttachments";
        panelAttachments.Size = new Size(1194, 32);
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
        buttonAddFolder.Location = new Point(183, 6);
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
        buttonAddFile.Location = new Point(90, 6);
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
        labelAttach.Location = new Point(6, 10);
        labelAttach.Name = "labelAttach";
        labelAttach.Size = new Size(78, 15);
        labelAttach.TabIndex = 0;
        labelAttach.Text = "Attachments:";
        // 
        // richTextBoxPrompt
        // 
        richTextBoxPrompt.AcceptsTab = true;
        richTextBoxPrompt.AllowDrop = true;
        richTextBoxPrompt.BackColor = Color.FromArgb(52, 52, 52);
        richTextBoxPrompt.Dock = DockStyle.Fill;
        richTextBoxPrompt.Font = new Font("Segoe UI", 11F);
        richTextBoxPrompt.ForeColor = Color.FromArgb(218, 218, 218);
        richTextBoxPrompt.Location = new Point(3, 41);
        richTextBoxPrompt.Name = "richTextBoxPrompt";
        richTextBoxPrompt.ScrollBars = RichTextBoxScrollBars.Vertical;
        richTextBoxPrompt.Size = new Size(1194, 166);
        richTextBoxPrompt.TabIndex = 1;
        richTextBoxPrompt.Text = "";
        toolTipMain.SetToolTip(richTextBoxPrompt, "Ctrl+Enter to send");
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
        panelActions.Dock = DockStyle.Fill;
        panelActions.Location = new Point(3, 213);
        panelActions.Name = "panelActions";
        panelActions.Padding = new Padding(4, 4, 8, 4);
        panelActions.Size = new Size(1194, 38);
        panelActions.TabIndex = 2;
        // 
        // checkBoxAutoApprove
        // 
        checkBoxAutoApprove.AutoSize = true;
        checkBoxAutoApprove.BackColor = Color.Transparent;
        checkBoxAutoApprove.Checked = true;
        checkBoxAutoApprove.CheckState = CheckState.Checked;
        checkBoxAutoApprove.Font = new Font("Segoe UI", 9F);
        checkBoxAutoApprove.ForeColor = Color.FromArgb(218, 218, 218);
        checkBoxAutoApprove.Location = new Point(8, 12);
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
        labelModel.Location = new Point(158, 14);
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
        comboBoxModel.Location = new Point(206, 10);
        comboBoxModel.Name = "comboBoxModel";
        comboBoxModel.Size = new Size(175, 23);
        comboBoxModel.TabIndex = 2;
        // 
        // labelMode
        // 
        labelMode.AutoSize = true;
        labelMode.Font = new Font("Segoe UI", 9F);
        labelMode.ForeColor = Color.FromArgb(218, 218, 218);
        labelMode.Location = new Point(390, 14);
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
        comboBoxMode.Location = new Point(434, 10);
        comboBoxMode.Name = "comboBoxMode";
        comboBoxMode.Size = new Size(110, 23);
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
        buttonOpenFolder.Location = new Point(554, 8);
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
        buttonStop.Location = new Point(1060, 5);
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
        buttonSend.Location = new Point(1126, 5);
        buttonSend.Name = "buttonSend";
        buttonSend.Size = new Size(60, 28);
        buttonSend.TabIndex = 5;
        buttonSend.Text = "▶ Send";
        toolTipMain.SetToolTip(buttonSend, "Send prompt to Copilot (Ctrl+Enter)");
        buttonSend.UseVisualStyleBackColor = false;
        // 
        // statusStrip
        // 
        statusStrip.BackColor = Color.FromArgb(56, 56, 56);
        statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabelConnection, toolStripStatusLabelVersion, toolStripStatusLabelSep, toolStripStatusLabelSession });
        statusStrip.Location = new Point(0, 777);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1200, 23);
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
        // toolStripStatusLabelSession
        // 
        toolStripStatusLabelSession.ForeColor = Color.FromArgb(148, 148, 148);
        toolStripStatusLabelSession.Name = "toolStripStatusLabelSession";
        toolStripStatusLabelSession.Size = new Size(1087, 18);
        toolStripStatusLabelSession.Spring = true;
        toolStripStatusLabelSession.TextAlign = ContentAlignment.MiddleRight;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(64, 64, 64);
        ClientSize = new Size(1200, 800);
        Controls.Add(splitContainerMain);
        Controls.Add(statusStrip);
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(900, 600);
        Name = "MainForm";
        Text = "Kopilot";
        splitContainerMain.Panel1.ResumeLayout(false);
        splitContainerMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
        splitContainerMain.ResumeLayout(false);
        panelQuickCommands.ResumeLayout(false);
        tableLayoutPanelPrompt.ResumeLayout(false);
        panelAttachments.ResumeLayout(false);
        panelAttachments.PerformLayout();
        panelActions.ResumeLayout(false);
        panelActions.PerformLayout();
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
    private System.Windows.Forms.Button buttonCommands;
    private System.Windows.Forms.Button buttonSummarize;
    private System.Windows.Forms.Button buttonClearOutput;
    private System.Windows.Forms.Button buttonBackup;
    private System.Windows.Forms.Button buttonOpenExplorer;
    private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPrompt;
    private System.Windows.Forms.Panel panelAttachments;
    private System.Windows.Forms.Label labelAttach;
    private System.Windows.Forms.Button buttonAddFile;
    private System.Windows.Forms.Button buttonAddFolder;
    private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelChips;
    private PlainRichTextBox richTextBoxPrompt;
    private System.Windows.Forms.Panel panelActions;
    private System.Windows.Forms.CheckBox checkBoxAutoApprove;
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
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelSession;
    private System.Windows.Forms.ToolTip toolTipMain;
}
