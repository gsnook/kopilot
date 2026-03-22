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

        splitContainerMain        = new System.Windows.Forms.SplitContainer();
        richTextBoxOutput         = new System.Windows.Forms.RichTextBox();
        panelQuickCommands        = new System.Windows.Forms.Panel();
        buttonHelp                = new System.Windows.Forms.Button();
        buttonCommands            = new System.Windows.Forms.Button();
        buttonSummarize           = new System.Windows.Forms.Button();
        buttonClearOutput         = new System.Windows.Forms.Button();
        buttonBackup              = new System.Windows.Forms.Button();
        tableLayoutPanelPrompt    = new System.Windows.Forms.TableLayoutPanel();
        panelAttachments          = new System.Windows.Forms.Panel();
        labelAttach               = new System.Windows.Forms.Label();
        buttonAddFile             = new System.Windows.Forms.Button();
        buttonAddFolder           = new System.Windows.Forms.Button();
        flowLayoutPanelChips      = new System.Windows.Forms.FlowLayoutPanel();
        richTextBoxPrompt         = new PlainRichTextBox();
        panelActions              = new System.Windows.Forms.Panel();
        checkBoxAutoApprove       = new System.Windows.Forms.CheckBox();
        labelModel                = new System.Windows.Forms.Label();
        comboBoxModel             = new System.Windows.Forms.ComboBox();
        labelMode                 = new System.Windows.Forms.Label();
        comboBoxMode              = new System.Windows.Forms.ComboBox();
        buttonOpenFolder          = new System.Windows.Forms.Button();
        buttonStop                = new System.Windows.Forms.Button();
        buttonSend                = new System.Windows.Forms.Button();
        statusStrip               = new System.Windows.Forms.StatusStrip();
        toolStripStatusLabelConnection = new System.Windows.Forms.ToolStripStatusLabel();
        toolStripStatusLabelVersion = new System.Windows.Forms.ToolStripStatusLabel();
        toolStripStatusLabelSep   = new System.Windows.Forms.ToolStripSeparator();
        toolStripStatusLabelSession = new System.Windows.Forms.ToolStripStatusLabel();
        toolTipMain               = new System.Windows.Forms.ToolTip(components);

        ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
        splitContainerMain.Panel1.SuspendLayout();
        splitContainerMain.Panel2.SuspendLayout();
        splitContainerMain.SuspendLayout();
        tableLayoutPanelPrompt.SuspendLayout();
        panelAttachments.SuspendLayout();
        panelActions.SuspendLayout();
        panelQuickCommands.SuspendLayout();
        statusStrip.SuspendLayout();
        this.SuspendLayout();

        // ── splitContainerMain ──────────────────────────────────────────────
        splitContainerMain.BackColor = AppTheme.Background;
        splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
        splitContainerMain.Location = new System.Drawing.Point(0, 0);
        splitContainerMain.Name = "splitContainerMain";
        splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
        splitContainerMain.Panel1MinSize = 200;
        splitContainerMain.Panel2MinSize = 180;
        splitContainerMain.Size = new System.Drawing.Size(1200, 778);
        splitContainerMain.SplitterDistance = 520;
        splitContainerMain.TabIndex = 0;
        splitContainerMain.Panel1.Controls.Add(richTextBoxOutput);
        splitContainerMain.Panel1.Controls.Add(panelQuickCommands);
        splitContainerMain.Panel2.Controls.Add(tableLayoutPanelPrompt);

        // ── richTextBoxOutput ───────────────────────────────────────────────
        richTextBoxOutput.BackColor = AppTheme.OutputBox;
        richTextBoxOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
        richTextBoxOutput.DetectUrls = false;
        richTextBoxOutput.Dock = System.Windows.Forms.DockStyle.Fill;
        richTextBoxOutput.Font = new System.Drawing.Font("Consolas", 10F);
        richTextBoxOutput.ForeColor = AppTheme.TextPrimary;
        richTextBoxOutput.Location = new System.Drawing.Point(0, 0);
        richTextBoxOutput.Name = "richTextBoxOutput";
        richTextBoxOutput.ReadOnly = true;
        richTextBoxOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
        richTextBoxOutput.Size = new System.Drawing.Size(1200, 520);
        richTextBoxOutput.TabIndex = 0;
        richTextBoxOutput.Text = "";

        // ── panelQuickCommands ──────────────────────────────────────────────
        panelQuickCommands.BackColor = AppTheme.Surface;
        panelQuickCommands.Controls.Add(buttonHelp);
        panelQuickCommands.Controls.Add(buttonCommands);
        panelQuickCommands.Controls.Add(buttonSummarize);
        panelQuickCommands.Controls.Add(buttonClearOutput);
        panelQuickCommands.Controls.Add(buttonBackup);
        panelQuickCommands.Dock = System.Windows.Forms.DockStyle.Top;
        panelQuickCommands.Name = "panelQuickCommands";
        panelQuickCommands.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
        panelQuickCommands.Size = new System.Drawing.Size(1200, 36);
        panelQuickCommands.TabIndex = 1;

        // buttonHelp
        buttonHelp.BackColor = AppTheme.ButtonBg;
        buttonHelp.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonHelp.FlatAppearance.BorderSize = 1;
        buttonHelp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonHelp.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        buttonHelp.ForeColor = AppTheme.TextPrimary;
        buttonHelp.Location = new System.Drawing.Point(6, 5);
        buttonHelp.Name = "buttonHelp";
        buttonHelp.Size = new System.Drawing.Size(72, 26);
        buttonHelp.TabIndex = 0;
        buttonHelp.Text = "❓ Help";
        buttonHelp.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonHelp, "Ask Copilot to describe its capabilities");

        // buttonCommands
        buttonCommands.BackColor = AppTheme.ButtonBg;
        buttonCommands.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonCommands.FlatAppearance.BorderSize = 1;
        buttonCommands.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonCommands.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        buttonCommands.ForeColor = AppTheme.TextPrimary;
        buttonCommands.Location = new System.Drawing.Point(84, 5);
        buttonCommands.Name = "buttonCommands";
        buttonCommands.Size = new System.Drawing.Size(96, 26);
        buttonCommands.TabIndex = 1;
        buttonCommands.Text = "📋 Commands";
        buttonCommands.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonCommands, "List available tools and capabilities");

        // buttonSummarize
        buttonSummarize.BackColor = AppTheme.ButtonBg;
        buttonSummarize.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonSummarize.FlatAppearance.BorderSize = 1;
        buttonSummarize.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonSummarize.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        buttonSummarize.ForeColor = AppTheme.TextPrimary;
        buttonSummarize.Location = new System.Drawing.Point(186, 5);
        buttonSummarize.Name = "buttonSummarize";
        buttonSummarize.Size = new System.Drawing.Size(90, 26);
        buttonSummarize.TabIndex = 2;
        buttonSummarize.Text = "📝 Summarize";
        buttonSummarize.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonSummarize, "Ask Copilot to summarize the session so far");

        // buttonClearOutput
        buttonClearOutput.BackColor = AppTheme.ButtonBg;
        buttonClearOutput.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonClearOutput.FlatAppearance.BorderSize = 1;
        buttonClearOutput.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonClearOutput.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        buttonClearOutput.ForeColor = AppTheme.TextPrimary;
        buttonClearOutput.Location = new System.Drawing.Point(282, 5);
        buttonClearOutput.Name = "buttonClearOutput";
        buttonClearOutput.Size = new System.Drawing.Size(68, 26);
        buttonClearOutput.TabIndex = 3;
        buttonClearOutput.Text = "🗑 Clear";
        buttonClearOutput.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonClearOutput, "Clear the current output window");

        // buttonBackup
        buttonBackup.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonBackup.BackColor = AppTheme.ButtonBg;
        buttonBackup.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonBackup.FlatAppearance.BorderSize = 1;
        buttonBackup.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonBackup.Font = new System.Drawing.Font("Segoe UI", 8.5F);
        buttonBackup.ForeColor = AppTheme.TextPrimary;
        buttonBackup.Location = new System.Drawing.Point(1104, 5);
        buttonBackup.Name = "buttonBackup";
        buttonBackup.Size = new System.Drawing.Size(88, 26);
        buttonBackup.TabIndex = 4;
        buttonBackup.Text = "💾 Backup";
        buttonBackup.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonBackup, "Ask Copilot to write a session-resume document to a Markdown file");

        // ── tableLayoutPanelPrompt ──────────────────────────────────────────
        tableLayoutPanelPrompt.BackColor = AppTheme.Background;
        tableLayoutPanelPrompt.ColumnCount = 1;
        tableLayoutPanelPrompt.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        tableLayoutPanelPrompt.Controls.Add(panelAttachments, 0, 0);
        tableLayoutPanelPrompt.Controls.Add(richTextBoxPrompt, 0, 1);
        tableLayoutPanelPrompt.Controls.Add(panelActions, 0, 2);
        tableLayoutPanelPrompt.Dock = System.Windows.Forms.DockStyle.Fill;
        tableLayoutPanelPrompt.Location = new System.Drawing.Point(0, 0);
        tableLayoutPanelPrompt.Name = "tableLayoutPanelPrompt";
        tableLayoutPanelPrompt.RowCount = 3;
        tableLayoutPanelPrompt.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
        tableLayoutPanelPrompt.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        tableLayoutPanelPrompt.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
        tableLayoutPanelPrompt.Size = new System.Drawing.Size(1200, 258);
        tableLayoutPanelPrompt.TabIndex = 0;

        // ── panelAttachments ────────────────────────────────────────────────
        panelAttachments.Controls.Add(flowLayoutPanelChips);
        panelAttachments.Controls.Add(buttonAddFolder);
        panelAttachments.Controls.Add(buttonAddFile);
        panelAttachments.Controls.Add(labelAttach);
        panelAttachments.BackColor = AppTheme.Background;
        panelAttachments.Dock = System.Windows.Forms.DockStyle.Fill;
        panelAttachments.Location = new System.Drawing.Point(0, 0);
        panelAttachments.Name = "panelAttachments";
        panelAttachments.Size = new System.Drawing.Size(1200, 38);
        panelAttachments.TabIndex = 0;

        // labelAttach
        labelAttach.AutoSize = true;
        labelAttach.Font = new System.Drawing.Font("Segoe UI", 9F);
        labelAttach.ForeColor = AppTheme.TextMuted;
        labelAttach.Location = new System.Drawing.Point(6, 10);
        labelAttach.Name = "labelAttach";
        labelAttach.Size = new System.Drawing.Size(76, 15);
        labelAttach.TabIndex = 0;
        labelAttach.Text = "Attachments:";

        // buttonAddFile
        buttonAddFile.BackColor = AppTheme.ButtonBg;
        buttonAddFile.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonAddFile.FlatAppearance.BorderSize = 1;
        buttonAddFile.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonAddFile.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonAddFile.ForeColor = AppTheme.TextPrimary;
        buttonAddFile.Location = new System.Drawing.Point(90, 6);
        buttonAddFile.Name = "buttonAddFile";
        buttonAddFile.Size = new System.Drawing.Size(88, 26);
        buttonAddFile.TabIndex = 1;
        buttonAddFile.Text = "📄 Add File";
        buttonAddFile.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonAddFile, "Attach a file to the prompt");

        // buttonAddFolder
        buttonAddFolder.BackColor = AppTheme.ButtonBg;
        buttonAddFolder.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonAddFolder.FlatAppearance.BorderSize = 1;
        buttonAddFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonAddFolder.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonAddFolder.ForeColor = AppTheme.TextPrimary;
        buttonAddFolder.Location = new System.Drawing.Point(183, 6);
        buttonAddFolder.Name = "buttonAddFolder";
        buttonAddFolder.Size = new System.Drawing.Size(100, 26);
        buttonAddFolder.TabIndex = 2;
        buttonAddFolder.Text = "📁 Add Folder";
        buttonAddFolder.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonAddFolder, "Attach a folder to the prompt");

        // flowLayoutPanelChips
        flowLayoutPanelChips.AutoSize = true;
        flowLayoutPanelChips.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        flowLayoutPanelChips.BackColor = AppTheme.Background;
        flowLayoutPanelChips.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        flowLayoutPanelChips.Location = new System.Drawing.Point(290, 6);
        flowLayoutPanelChips.Name = "flowLayoutPanelChips";
        flowLayoutPanelChips.Size = new System.Drawing.Size(0, 26);
        flowLayoutPanelChips.TabIndex = 3;
        flowLayoutPanelChips.WrapContents = false;

        // ── richTextBoxPrompt ───────────────────────────────────────────────
        richTextBoxPrompt.AcceptsTab = true;
        richTextBoxPrompt.BackColor = AppTheme.InputBox;
        richTextBoxPrompt.ForeColor = AppTheme.TextPrimary;
        richTextBoxPrompt.Dock = System.Windows.Forms.DockStyle.Fill;
        richTextBoxPrompt.Font = new System.Drawing.Font("Segoe UI", 11F);
        richTextBoxPrompt.Location = new System.Drawing.Point(3, 41);
        richTextBoxPrompt.Name = "richTextBoxPrompt";
        richTextBoxPrompt.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
        richTextBoxPrompt.Size = new System.Drawing.Size(1194, 170);
        richTextBoxPrompt.TabIndex = 1;
        richTextBoxPrompt.Text = "";
        toolTipMain.SetToolTip(richTextBoxPrompt, "Ctrl+Enter to send");

        // ── panelActions ────────────────────────────────────────────────────
        panelActions.Controls.Add(checkBoxAutoApprove);
        panelActions.Controls.Add(labelModel);
        panelActions.Controls.Add(comboBoxModel);
        panelActions.Controls.Add(labelMode);
        panelActions.Controls.Add(comboBoxMode);
        panelActions.Controls.Add(buttonOpenFolder);
        panelActions.Controls.Add(buttonStop);
        panelActions.Controls.Add(buttonSend);
        panelActions.BackColor = AppTheme.Background;
        panelActions.Dock = System.Windows.Forms.DockStyle.Fill;
        panelActions.Location = new System.Drawing.Point(0, 214);
        panelActions.Name = "panelActions";
        panelActions.Padding = new System.Windows.Forms.Padding(4, 4, 8, 4);
        panelActions.Size = new System.Drawing.Size(1200, 44);
        panelActions.TabIndex = 2;

        // checkBoxAutoApprove
        checkBoxAutoApprove.AutoSize = true;
        checkBoxAutoApprove.BackColor = System.Drawing.Color.Transparent;
        checkBoxAutoApprove.Checked = true;
        checkBoxAutoApprove.CheckState = System.Windows.Forms.CheckState.Checked;
        checkBoxAutoApprove.Font = new System.Drawing.Font("Segoe UI", 9F);
        checkBoxAutoApprove.ForeColor = AppTheme.TextPrimary;
        checkBoxAutoApprove.Location = new System.Drawing.Point(8, 12);
        checkBoxAutoApprove.Name = "checkBoxAutoApprove";
        checkBoxAutoApprove.Size = new System.Drawing.Size(136, 19);
        checkBoxAutoApprove.TabIndex = 0;
        checkBoxAutoApprove.Text = "Auto-approve tools";
        checkBoxAutoApprove.UseVisualStyleBackColor = true;
        toolTipMain.SetToolTip(checkBoxAutoApprove, "Automatically approve all tool executions without prompting");

        // labelModel
        labelModel.AutoSize = true;
        labelModel.Font = new System.Drawing.Font("Segoe UI", 9F);
        labelModel.ForeColor = AppTheme.TextPrimary;
        labelModel.Location = new System.Drawing.Point(158, 14);
        labelModel.Name = "labelModel";
        labelModel.Size = new System.Drawing.Size(42, 15);
        labelModel.TabIndex = 1;
        labelModel.Text = "Model:";

        // comboBoxModel
        comboBoxModel.BackColor = AppTheme.InputBox;
        comboBoxModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        comboBoxModel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        comboBoxModel.Font = new System.Drawing.Font("Segoe UI", 9F);
        comboBoxModel.ForeColor = AppTheme.TextPrimary;
        comboBoxModel.FormattingEnabled = true;
        comboBoxModel.Items.AddRange(new object[] {
            "gpt-4.1",
            "gpt-5",
            "claude-sonnet-4.5",
            "claude-sonnet-4.6",
            "claude-opus-4.5"
        });
        comboBoxModel.Location = new System.Drawing.Point(206, 10);
        comboBoxModel.Name = "comboBoxModel";
        comboBoxModel.Size = new System.Drawing.Size(175, 23);
        comboBoxModel.TabIndex = 2;

        // labelMode
        labelMode.AutoSize = true;
        labelMode.Font = new System.Drawing.Font("Segoe UI", 9F);
        labelMode.ForeColor = AppTheme.TextPrimary;
        labelMode.Location = new System.Drawing.Point(390, 14);
        labelMode.Name = "labelMode";
        labelMode.Size = new System.Drawing.Size(38, 15);
        labelMode.TabIndex = 7;
        labelMode.Text = "Mode:";

        // comboBoxMode
        comboBoxMode.BackColor = AppTheme.InputBox;
        comboBoxMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        comboBoxMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        comboBoxMode.Font = new System.Drawing.Font("Segoe UI", 9F);
        comboBoxMode.ForeColor = AppTheme.TextPrimary;
        comboBoxMode.FormattingEnabled = true;
        comboBoxMode.Items.AddRange(new object[] {
            "Standard",
            "Plan",
            "Autopilot",
        });
        comboBoxMode.Location = new System.Drawing.Point(434, 10);
        comboBoxMode.Name = "comboBoxMode";
        comboBoxMode.Size = new System.Drawing.Size(110, 23);
        comboBoxMode.TabIndex = 8;
        toolTipMain.SetToolTip(comboBoxMode, "Standard: normal chat  |  Plan: plan before acting  |  Autopilot: fully autonomous");

        // buttonOpenFolder
        buttonOpenFolder.BackColor = AppTheme.AccentBg;
        buttonOpenFolder.FlatAppearance.BorderSize = 0;
        buttonOpenFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonOpenFolder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonOpenFolder.ForeColor = AppTheme.AccentText;
        buttonOpenFolder.Location = new System.Drawing.Point(554, 8);
        buttonOpenFolder.Name = "buttonOpenFolder";
        buttonOpenFolder.Size = new System.Drawing.Size(130, 28);
        buttonOpenFolder.TabIndex = 6;
        buttonOpenFolder.Text = "📂 Open Folder…";
        buttonOpenFolder.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonOpenFolder, "Select a project folder and connect to Copilot");

        // buttonStop
        buttonStop.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonStop.BackColor = AppTheme.ButtonBg;
        buttonStop.Enabled = false;
        buttonStop.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
        buttonStop.FlatAppearance.BorderSize = 1;
        buttonStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonStop.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonStop.ForeColor = AppTheme.TextPrimary;
        buttonStop.Location = new System.Drawing.Point(1066, 8);
        buttonStop.Name = "buttonStop";
        buttonStop.Size = new System.Drawing.Size(60, 28);
        buttonStop.TabIndex = 4;
        buttonStop.Text = "⬛ Stop";
        buttonStop.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonStop, "Stop the current Copilot response");

        // buttonSend
        buttonSend.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonSend.BackColor = AppTheme.AccentBg;
        buttonSend.Enabled = false;
        buttonSend.FlatAppearance.BorderSize = 0;
        buttonSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonSend.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonSend.ForeColor = AppTheme.AccentText;
        buttonSend.Location = new System.Drawing.Point(1132, 8);
        buttonSend.Name = "buttonSend";
        buttonSend.Size = new System.Drawing.Size(60, 28);
        buttonSend.TabIndex = 5;
        buttonSend.Text = "▶ Send";
        buttonSend.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonSend, "Send prompt to Copilot (Ctrl+Enter)");

        // ── statusStrip ─────────────────────────────────────────────────────
        statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            toolStripStatusLabelConnection,
            toolStripStatusLabelVersion,
            toolStripStatusLabelSep,
            toolStripStatusLabelSession,
        });
        statusStrip.BackColor = AppTheme.StatusBar;
        statusStrip.Location = new System.Drawing.Point(0, 778);
        statusStrip.Name = "statusStrip";
        statusStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
        statusStrip.Size = new System.Drawing.Size(1200, 22);
        statusStrip.TabIndex = 1;

        toolStripStatusLabelConnection.ForeColor = AppTheme.TextPrimary;
        toolStripStatusLabelConnection.Name = "toolStripStatusLabelConnection";
        toolStripStatusLabelConnection.Size = new System.Drawing.Size(89, 17);
        toolStripStatusLabelConnection.Text = "Not connected";

        toolStripStatusLabelVersion.ForeColor = AppTheme.TextMuted;
        toolStripStatusLabelVersion.Name = "toolStripStatusLabelVersion";
        toolStripStatusLabelVersion.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
        toolStripStatusLabelVersion.Size = new System.Drawing.Size(0, 17);
        toolStripStatusLabelVersion.Text = "";

        toolStripStatusLabelSep.ForeColor = AppTheme.TextMuted;
        toolStripStatusLabelSep.Name = "toolStripStatusLabelSep";
        toolStripStatusLabelSep.Size = new System.Drawing.Size(6, 17);

        toolStripStatusLabelSession.ForeColor = AppTheme.TextMuted;
        toolStripStatusLabelSession.Name = "toolStripStatusLabelSession";
        toolStripStatusLabelSession.Size = new System.Drawing.Size(0, 17);
        toolStripStatusLabelSession.Spring = true;
        toolStripStatusLabelSession.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // ── MainForm ────────────────────────────────────────────────────────
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = AppTheme.Background;
        this.ClientSize = new System.Drawing.Size(1200, 800);
        this.Controls.Add(splitContainerMain);
        this.Controls.Add(statusStrip);
        this.Font = new System.Drawing.Font("Segoe UI", 9F);
        this.MinimumSize = new System.Drawing.Size(900, 600);
        this.Name = "MainForm";
        this.Text = "Kopilot";

        ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
        splitContainerMain.Panel1.ResumeLayout(false);
        splitContainerMain.Panel2.ResumeLayout(false);
        splitContainerMain.ResumeLayout(false);
        tableLayoutPanelPrompt.ResumeLayout(false);
        tableLayoutPanelPrompt.PerformLayout();
        panelAttachments.ResumeLayout(false);
        panelAttachments.PerformLayout();
        panelActions.ResumeLayout(false);
        panelActions.PerformLayout();
        panelQuickCommands.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
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
