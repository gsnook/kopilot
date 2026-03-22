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
        tabControlSessions        = new System.Windows.Forms.TabControl();
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
        buttonOpenFolder          = new System.Windows.Forms.Button();
        buttonStop                = new System.Windows.Forms.Button();
        buttonSend                = new System.Windows.Forms.Button();
        statusStrip               = new System.Windows.Forms.StatusStrip();
        toolStripStatusLabelConnection = new System.Windows.Forms.ToolStripStatusLabel();
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
        statusStrip.SuspendLayout();
        this.SuspendLayout();

        // ── splitContainerMain ──────────────────────────────────────────────
        splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
        splitContainerMain.Location = new System.Drawing.Point(0, 0);
        splitContainerMain.Name = "splitContainerMain";
        splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
        splitContainerMain.Panel1MinSize = 200;
        splitContainerMain.Panel2MinSize = 180;
        splitContainerMain.Size = new System.Drawing.Size(1200, 778);
        splitContainerMain.SplitterDistance = 520;
        splitContainerMain.TabIndex = 0;
        splitContainerMain.Panel1.Controls.Add(tabControlSessions);
        splitContainerMain.Panel2.Controls.Add(tableLayoutPanelPrompt);

        // ── tabControlSessions ──────────────────────────────────────────────
        tabControlSessions.Dock = System.Windows.Forms.DockStyle.Fill;
        tabControlSessions.Font = new System.Drawing.Font("Segoe UI", 9F);
        tabControlSessions.Location = new System.Drawing.Point(0, 0);
        tabControlSessions.Name = "tabControlSessions";
        tabControlSessions.Size = new System.Drawing.Size(1200, 520);
        tabControlSessions.TabIndex = 0;

        // ── tableLayoutPanelPrompt ──────────────────────────────────────────
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
        panelAttachments.Dock = System.Windows.Forms.DockStyle.Fill;
        panelAttachments.Location = new System.Drawing.Point(0, 0);
        panelAttachments.Name = "panelAttachments";
        panelAttachments.Size = new System.Drawing.Size(1200, 38);
        panelAttachments.TabIndex = 0;

        // labelAttach
        labelAttach.AutoSize = true;
        labelAttach.Font = new System.Drawing.Font("Segoe UI", 9F);
        labelAttach.Location = new System.Drawing.Point(6, 10);
        labelAttach.Name = "labelAttach";
        labelAttach.Size = new System.Drawing.Size(76, 15);
        labelAttach.TabIndex = 0;
        labelAttach.Text = "Attachments:";

        // buttonAddFile
        buttonAddFile.FlatAppearance.BorderSize = 0;
        buttonAddFile.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonAddFile.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonAddFile.Location = new System.Drawing.Point(90, 6);
        buttonAddFile.Name = "buttonAddFile";
        buttonAddFile.Size = new System.Drawing.Size(88, 26);
        buttonAddFile.TabIndex = 1;
        buttonAddFile.Text = "📄 Add File";
        buttonAddFile.UseVisualStyleBackColor = true;
        toolTipMain.SetToolTip(buttonAddFile, "Attach a file to the prompt");

        // buttonAddFolder
        buttonAddFolder.FlatAppearance.BorderSize = 0;
        buttonAddFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonAddFolder.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonAddFolder.Location = new System.Drawing.Point(183, 6);
        buttonAddFolder.Name = "buttonAddFolder";
        buttonAddFolder.Size = new System.Drawing.Size(100, 26);
        buttonAddFolder.TabIndex = 2;
        buttonAddFolder.Text = "📁 Add Folder";
        buttonAddFolder.UseVisualStyleBackColor = true;
        toolTipMain.SetToolTip(buttonAddFolder, "Attach a folder to the prompt");

        // flowLayoutPanelChips
        flowLayoutPanelChips.AutoSize = true;
        flowLayoutPanelChips.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        flowLayoutPanelChips.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        flowLayoutPanelChips.Location = new System.Drawing.Point(290, 6);
        flowLayoutPanelChips.Name = "flowLayoutPanelChips";
        flowLayoutPanelChips.Size = new System.Drawing.Size(0, 26);
        flowLayoutPanelChips.TabIndex = 3;
        flowLayoutPanelChips.WrapContents = false;

        // ── richTextBoxPrompt ───────────────────────────────────────────────
        richTextBoxPrompt.AcceptsTab = true;
        richTextBoxPrompt.BackColor = System.Drawing.Color.Black;
        richTextBoxPrompt.ForeColor = System.Drawing.Color.White;
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
        panelActions.Controls.Add(buttonOpenFolder);
        panelActions.Controls.Add(buttonStop);
        panelActions.Controls.Add(buttonSend);
        panelActions.Dock = System.Windows.Forms.DockStyle.Fill;
        panelActions.Location = new System.Drawing.Point(0, 214);
        panelActions.Name = "panelActions";
        panelActions.Padding = new System.Windows.Forms.Padding(4, 4, 8, 4);
        panelActions.Size = new System.Drawing.Size(1200, 44);
        panelActions.TabIndex = 2;

        // checkBoxAutoApprove
        checkBoxAutoApprove.AutoSize = true;
        checkBoxAutoApprove.Font = new System.Drawing.Font("Segoe UI", 9F);
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
        labelModel.Location = new System.Drawing.Point(158, 14);
        labelModel.Name = "labelModel";
        labelModel.Size = new System.Drawing.Size(42, 15);
        labelModel.TabIndex = 1;
        labelModel.Text = "Model:";

        // comboBoxModel
        comboBoxModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        comboBoxModel.Font = new System.Drawing.Font("Segoe UI", 9F);
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

        // buttonOpenFolder
        buttonOpenFolder.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
        buttonOpenFolder.FlatAppearance.BorderSize = 0;
        buttonOpenFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonOpenFolder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonOpenFolder.ForeColor = System.Drawing.Color.White;
        buttonOpenFolder.Location = new System.Drawing.Point(388, 8);
        buttonOpenFolder.Name = "buttonOpenFolder";
        buttonOpenFolder.Size = new System.Drawing.Size(130, 28);
        buttonOpenFolder.TabIndex = 6;
        buttonOpenFolder.Text = "📂 Open Folder…";
        buttonOpenFolder.UseVisualStyleBackColor = false;
        toolTipMain.SetToolTip(buttonOpenFolder, "Select a project folder and connect to Copilot");

        // buttonStop
        buttonStop.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonStop.Enabled = false;
        buttonStop.Font = new System.Drawing.Font("Segoe UI", 9F);
        buttonStop.Location = new System.Drawing.Point(1066, 8);
        buttonStop.Name = "buttonStop";
        buttonStop.Size = new System.Drawing.Size(60, 28);
        buttonStop.TabIndex = 4;
        buttonStop.Text = "⬛ Stop";
        buttonStop.UseVisualStyleBackColor = true;
        toolTipMain.SetToolTip(buttonStop, "Stop the current Copilot response");

        // buttonSend
        buttonSend.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonSend.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
        buttonSend.Enabled = false;
        buttonSend.FlatAppearance.BorderSize = 0;
        buttonSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonSend.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonSend.ForeColor = System.Drawing.Color.White;
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
            toolStripStatusLabelSep,
            toolStripStatusLabelSession,
        });
        statusStrip.Location = new System.Drawing.Point(0, 778);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new System.Drawing.Size(1200, 22);
        statusStrip.TabIndex = 1;

        toolStripStatusLabelConnection.Name = "toolStripStatusLabelConnection";
        toolStripStatusLabelConnection.Size = new System.Drawing.Size(89, 17);
        toolStripStatusLabelConnection.Text = "Not connected";

        toolStripStatusLabelSep.Name = "toolStripStatusLabelSep";
        toolStripStatusLabelSep.Size = new System.Drawing.Size(6, 17);

        toolStripStatusLabelSession.Name = "toolStripStatusLabelSession";
        toolStripStatusLabelSession.Size = new System.Drawing.Size(0, 17);
        toolStripStatusLabelSession.Spring = true;
        toolStripStatusLabelSession.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // ── MainForm ────────────────────────────────────────────────────────
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
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
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.SplitContainer splitContainerMain;
    private System.Windows.Forms.TabControl tabControlSessions;
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
    private System.Windows.Forms.Button buttonOpenFolder;
    private System.Windows.Forms.Button buttonStop;
    private System.Windows.Forms.Button buttonSend;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelConnection;
    private System.Windows.Forms.ToolStripSeparator toolStripStatusLabelSep;
    private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelSession;
    private System.Windows.Forms.ToolTip toolTipMain;
}
