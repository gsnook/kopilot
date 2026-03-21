namespace Kopilot;

partial class PermissionDialog
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
        labelTitle = new System.Windows.Forms.Label();
        labelKind = new System.Windows.Forms.Label();
        labelDetails = new System.Windows.Forms.Label();
        panelButtons = new System.Windows.Forms.Panel();
        buttonDeny = new System.Windows.Forms.Button();
        buttonAllow = new System.Windows.Forms.Button();
        panelButtons.SuspendLayout();
        this.SuspendLayout();

        // labelTitle
        labelTitle.AutoSize = false;
        labelTitle.Dock = System.Windows.Forms.DockStyle.Top;
        labelTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        labelTitle.Location = new System.Drawing.Point(16, 16);
        labelTitle.Name = "labelTitle";
        labelTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 4);
        labelTitle.Size = new System.Drawing.Size(428, 28);
        labelTitle.TabIndex = 0;
        labelTitle.Text = "Copilot wants to perform an operation:";

        // labelKind
        labelKind.AutoSize = false;
        labelKind.Dock = System.Windows.Forms.DockStyle.Top;
        labelKind.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        labelKind.Location = new System.Drawing.Point(16, 52);
        labelKind.Name = "labelKind";
        labelKind.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
        labelKind.Size = new System.Drawing.Size(428, 28);
        labelKind.TabIndex = 1;
        labelKind.Text = "Operation: ";

        // labelDetails
        labelDetails.AutoSize = false;
        labelDetails.BackColor = System.Drawing.SystemColors.ControlLight;
        labelDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        labelDetails.Dock = System.Windows.Forms.DockStyle.Top;
        labelDetails.Font = new System.Drawing.Font("Consolas", 9F);
        labelDetails.Location = new System.Drawing.Point(16, 88);
        labelDetails.MaximumSize = new System.Drawing.Size(428, 72);
        labelDetails.Name = "labelDetails";
        labelDetails.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
        labelDetails.Size = new System.Drawing.Size(428, 52);
        labelDetails.TabIndex = 2;
        labelDetails.Text = "";

        // panelButtons
        panelButtons.Controls.Add(buttonDeny);
        panelButtons.Controls.Add(buttonAllow);
        panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
        panelButtons.Location = new System.Drawing.Point(0, 176);
        panelButtons.Name = "panelButtons";
        panelButtons.Padding = new System.Windows.Forms.Padding(8, 8, 12, 8);
        panelButtons.Size = new System.Drawing.Size(460, 50);
        panelButtons.TabIndex = 3;

        // buttonAllow
        buttonAllow.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonAllow.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
        buttonAllow.FlatAppearance.BorderSize = 0;
        buttonAllow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonAllow.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonAllow.ForeColor = System.Drawing.Color.White;
        buttonAllow.Location = new System.Drawing.Point(268, 8);
        buttonAllow.Name = "buttonAllow";
        buttonAllow.Size = new System.Drawing.Size(88, 32);
        buttonAllow.TabIndex = 0;
        buttonAllow.Text = "✓ Allow";
        buttonAllow.UseVisualStyleBackColor = false;
        buttonAllow.Click += ButtonAllow_Click;

        // buttonDeny
        buttonDeny.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonDeny.Location = new System.Drawing.Point(362, 8);
        buttonDeny.Name = "buttonDeny";
        buttonDeny.Size = new System.Drawing.Size(88, 32);
        buttonDeny.TabIndex = 1;
        buttonDeny.Text = "✗ Deny";
        buttonDeny.UseVisualStyleBackColor = true;
        buttonDeny.Click += ButtonDeny_Click;

        // PermissionDialog
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(460, 226);
        this.Controls.Add(panelButtons);
        this.Controls.Add(labelDetails);
        this.Controls.Add(labelKind);
        this.Controls.Add(labelTitle);
        this.Font = new System.Drawing.Font("Segoe UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "PermissionDialog";
        this.Padding = new System.Windows.Forms.Padding(16, 16, 16, 0);
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Permission Request";

        panelButtons.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label labelTitle;
    private System.Windows.Forms.Label labelKind;
    private System.Windows.Forms.Label labelDetails;
    private System.Windows.Forms.Panel panelButtons;
    private System.Windows.Forms.Button buttonAllow;
    private System.Windows.Forms.Button buttonDeny;
}
