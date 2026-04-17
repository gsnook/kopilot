namespace Kopilot;

partial class UpdateNotificationDialog
{
	/// <summary>Required designer variable.</summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>Clean up any resources being used.</summary>
	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
			components.Dispose();
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	/// <summary>Required method for Designer support.</summary>
	private void InitializeComponent()
	{
		labelTitle         = new System.Windows.Forms.Label();
		labelVersions      = new System.Windows.Forms.Label();
		labelCommandHeader = new System.Windows.Forms.Label();
		textBoxCommand     = new System.Windows.Forms.TextBox();
		labelNote          = new System.Windows.Forms.Label();
		panelButtons       = new System.Windows.Forms.Panel();
		buttonCopy         = new System.Windows.Forms.Button();
		buttonNuGet        = new System.Windows.Forms.Button();
		buttonDismiss      = new System.Windows.Forms.Button();
		panelButtons.SuspendLayout();
		this.SuspendLayout();

		// labelTitle — "Update Available" heading
		labelTitle.AutoSize  = false;
		labelTitle.Dock      = System.Windows.Forms.DockStyle.Top;
		labelTitle.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
		labelTitle.ForeColor = AppTheme.ColorTool;
		labelTitle.Name      = "labelTitle";
		labelTitle.Padding   = new System.Windows.Forms.Padding(0, 0, 0, 4);
		labelTitle.Size      = new System.Drawing.Size(528, 28);
		labelTitle.TabIndex  = 0;
		labelTitle.Text      = "Update Available — GitHub.Copilot.SDK";

		// labelVersions — current / latest version lines
		labelVersions.AutoSize  = false;
		labelVersions.Dock      = System.Windows.Forms.DockStyle.Top;
		labelVersions.Font      = new System.Drawing.Font("Consolas", 9F);
		labelVersions.ForeColor = AppTheme.TextPrimary;
		labelVersions.Name      = "labelVersions";
		labelVersions.Padding   = new System.Windows.Forms.Padding(6, 6, 6, 6);
		labelVersions.Size      = new System.Drawing.Size(528, 46);
		labelVersions.TabIndex  = 1;

		// labelCommandHeader — instruction line above the command
		labelCommandHeader.AutoSize  = false;
		labelCommandHeader.Dock      = System.Windows.Forms.DockStyle.Top;
		labelCommandHeader.Font      = new System.Drawing.Font("Segoe UI", 9F);
		labelCommandHeader.ForeColor = AppTheme.TextMuted;
		labelCommandHeader.Name      = "labelCommandHeader";
		labelCommandHeader.Padding   = new System.Windows.Forms.Padding(0, 6, 0, 2);
		labelCommandHeader.Size      = new System.Drawing.Size(528, 24);
		labelCommandHeader.TabIndex  = 2;
		labelCommandHeader.Text      = "Run in the Kopilot project directory, then rebuild:";

		// textBoxCommand — read-only, selectable, code-styled command box
		textBoxCommand.BackColor    = AppTheme.OutputBox;
		textBoxCommand.BorderStyle  = System.Windows.Forms.BorderStyle.FixedSingle;
		textBoxCommand.Dock         = System.Windows.Forms.DockStyle.Top;
		textBoxCommand.Font         = new System.Drawing.Font("Consolas", 9.5F);
		textBoxCommand.ForeColor    = AppTheme.ColorAssistant;
		textBoxCommand.Name         = "textBoxCommand";
		textBoxCommand.ReadOnly     = true;
		textBoxCommand.Size         = new System.Drawing.Size(528, 24);
		textBoxCommand.TabIndex     = 3;
		textBoxCommand.TabStop      = false;

		// labelNote — footnote explaining the CLI is bundled
		labelNote.AutoSize  = false;
		labelNote.Dock      = System.Windows.Forms.DockStyle.Top;
		labelNote.Font      = new System.Drawing.Font("Segoe UI", 8.5F);
		labelNote.ForeColor = AppTheme.TextMuted;
		labelNote.Name      = "labelNote";
		labelNote.Padding   = new System.Windows.Forms.Padding(0, 6, 0, 0);
		labelNote.Size      = new System.Drawing.Size(528, 24);
		labelNote.TabIndex  = 4;
		labelNote.Text      = "Updating the SDK also brings the latest bundled Copilot CLI binary.";

		// buttonCopy — copies the update command to the clipboard
		buttonCopy.Anchor                    = System.Windows.Forms.AnchorStyles.Right;
		buttonCopy.BackColor                 = AppTheme.AccentBg;
		buttonCopy.FlatAppearance.BorderSize = 0;
		buttonCopy.FlatStyle                 = System.Windows.Forms.FlatStyle.Flat;
		buttonCopy.Font                      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
		buttonCopy.ForeColor                 = AppTheme.AccentText;
		buttonCopy.Location                  = new System.Drawing.Point(236, 9);
		buttonCopy.Name                      = "buttonCopy";
		buttonCopy.Size                      = new System.Drawing.Size(130, 32);
		buttonCopy.TabIndex                  = 0;
		buttonCopy.Text                      = "Copy Command";
		buttonCopy.UseVisualStyleBackColor   = false;
		buttonCopy.Click                    += ButtonCopy_Click;

		// buttonNuGet — opens the NuGet page for this package
		buttonNuGet.Anchor                            = System.Windows.Forms.AnchorStyles.Right;
		buttonNuGet.BackColor                         = AppTheme.ButtonBg;
		buttonNuGet.FlatAppearance.BorderColor        = AppTheme.ButtonBorder;
		buttonNuGet.FlatAppearance.BorderSize         = 1;
		buttonNuGet.FlatStyle                         = System.Windows.Forms.FlatStyle.Flat;
		buttonNuGet.Font                              = new System.Drawing.Font("Segoe UI", 9F);
		buttonNuGet.ForeColor                         = AppTheme.TextPrimary;
		buttonNuGet.Location                          = new System.Drawing.Point(372, 9);
		buttonNuGet.Name                              = "buttonNuGet";
		buttonNuGet.Size                              = new System.Drawing.Size(100, 32);
		buttonNuGet.TabIndex                          = 1;
		buttonNuGet.Text                              = "View on NuGet";
		buttonNuGet.UseVisualStyleBackColor           = false;
		buttonNuGet.Click                            += ButtonNuGet_Click;

		// buttonDismiss — closes the dialog without acting
		buttonDismiss.Anchor                            = System.Windows.Forms.AnchorStyles.Right;
		buttonDismiss.BackColor                         = AppTheme.ButtonBg;
		buttonDismiss.FlatAppearance.BorderColor        = AppTheme.ButtonBorder;
		buttonDismiss.FlatAppearance.BorderSize         = 1;
		buttonDismiss.FlatStyle                         = System.Windows.Forms.FlatStyle.Flat;
		buttonDismiss.Font                              = new System.Drawing.Font("Segoe UI", 9F);
		buttonDismiss.ForeColor                         = AppTheme.TextPrimary;
		buttonDismiss.Location                          = new System.Drawing.Point(478, 9);
		buttonDismiss.Name                              = "buttonDismiss";
		buttonDismiss.Size                              = new System.Drawing.Size(70, 32);
		buttonDismiss.TabIndex                          = 2;
		buttonDismiss.Text                              = "Later";
		buttonDismiss.UseVisualStyleBackColor           = false;
		buttonDismiss.Click                            += ButtonDismiss_Click;

		// panelButtons
		panelButtons.Controls.Add(buttonDismiss);
		panelButtons.Controls.Add(buttonNuGet);
		panelButtons.Controls.Add(buttonCopy);
		panelButtons.BackColor = AppTheme.Background;
		panelButtons.Dock      = System.Windows.Forms.DockStyle.Bottom;
		panelButtons.Name      = "panelButtons";
		panelButtons.Padding   = new System.Windows.Forms.Padding(8, 8, 12, 8);
		panelButtons.Size      = new System.Drawing.Size(560, 50);
		panelButtons.TabIndex  = 5;

		// UpdateNotificationDialog — controls added in reverse visual order for DockStyle.Top stacking
		this.Controls.Add(panelButtons);
		this.Controls.Add(labelNote);
		this.Controls.Add(textBoxCommand);
		this.Controls.Add(labelCommandHeader);
		this.Controls.Add(labelVersions);
		this.Controls.Add(labelTitle);

		this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
		this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor           = AppTheme.Background;
		this.ClientSize          = new System.Drawing.Size(560, 222);
		this.Font                = new System.Drawing.Font("Segoe UI", 9F);
		this.FormBorderStyle     = System.Windows.Forms.FormBorderStyle.FixedDialog;
		this.MaximizeBox         = false;
		this.MinimizeBox         = false;
		this.Name                = "UpdateNotificationDialog";
		this.Padding             = new System.Windows.Forms.Padding(16, 16, 16, 0);
		this.ShowInTaskbar       = false;
		this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text                = "Update Available";

		panelButtons.ResumeLayout(false);
		this.ResumeLayout(false);
		this.PerformLayout();
	}

	#endregion

	private System.Windows.Forms.Label    labelTitle;
	private System.Windows.Forms.Label    labelVersions;
	private System.Windows.Forms.Label    labelCommandHeader;
	private System.Windows.Forms.TextBox  textBoxCommand;
	private System.Windows.Forms.Label    labelNote;
	private System.Windows.Forms.Panel    panelButtons;
	private System.Windows.Forms.Button   buttonCopy;
	private System.Windows.Forms.Button   buttonNuGet;
	private System.Windows.Forms.Button   buttonDismiss;
}
