namespace Kopilot;

partial class UserInputDialog
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
        labelHeading = new System.Windows.Forms.Label();
        labelQuestion = new System.Windows.Forms.Label();
        listBoxChoices = new System.Windows.Forms.ListBox();
        labelOrType = new System.Windows.Forms.Label();
        textBoxAnswer = new System.Windows.Forms.TextBox();
        panelButtons = new System.Windows.Forms.Panel();
        buttonSubmit = new System.Windows.Forms.Button();
        panelButtons.SuspendLayout();
        this.SuspendLayout();

        // labelHeading
        labelHeading.AutoSize = false;
        labelHeading.Dock = System.Windows.Forms.DockStyle.Top;
        labelHeading.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        labelHeading.Location = new System.Drawing.Point(16, 16);
        labelHeading.Name = "labelHeading";
        labelHeading.Padding = new System.Windows.Forms.Padding(0, 0, 0, 4);
        labelHeading.Size = new System.Drawing.Size(448, 26);
        labelHeading.TabIndex = 0;
        labelHeading.Text = "Copilot needs your input:";

        // labelQuestion
        labelQuestion.AutoSize = false;
        labelQuestion.Dock = System.Windows.Forms.DockStyle.Top;
        labelQuestion.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        labelQuestion.Location = new System.Drawing.Point(16, 50);
        labelQuestion.MaximumSize = new System.Drawing.Size(448, 60);
        labelQuestion.Name = "labelQuestion";
        labelQuestion.Padding = new System.Windows.Forms.Padding(0, 4, 0, 4);
        labelQuestion.Size = new System.Drawing.Size(448, 44);
        labelQuestion.TabIndex = 1;
        labelQuestion.Text = "";

        // listBoxChoices
        listBoxChoices.Dock = System.Windows.Forms.DockStyle.Top;
        listBoxChoices.Font = new System.Drawing.Font("Segoe UI", 9F);
        listBoxChoices.IntegralHeight = false;
        listBoxChoices.Location = new System.Drawing.Point(16, 102);
        listBoxChoices.Name = "listBoxChoices";
        listBoxChoices.Size = new System.Drawing.Size(448, 90);
        listBoxChoices.TabIndex = 2;

        // labelOrType
        labelOrType.AutoSize = false;
        labelOrType.Dock = System.Windows.Forms.DockStyle.Top;
        labelOrType.Font = new System.Drawing.Font("Segoe UI", 9F);
        labelOrType.ForeColor = System.Drawing.SystemColors.GrayText;
        labelOrType.Location = new System.Drawing.Point(16, 200);
        labelOrType.Name = "labelOrType";
        labelOrType.Padding = new System.Windows.Forms.Padding(0, 4, 0, 2);
        labelOrType.Size = new System.Drawing.Size(448, 22);
        labelOrType.TabIndex = 3;
        labelOrType.Text = "Or type a custom answer:";

        // textBoxAnswer
        textBoxAnswer.Dock = System.Windows.Forms.DockStyle.Top;
        textBoxAnswer.Font = new System.Drawing.Font("Segoe UI", 9.5F);
        textBoxAnswer.Location = new System.Drawing.Point(16, 230);
        textBoxAnswer.Name = "textBoxAnswer";
        textBoxAnswer.Size = new System.Drawing.Size(448, 24);
        textBoxAnswer.TabIndex = 4;
        textBoxAnswer.KeyDown += TextBoxAnswer_KeyDown;

        // panelButtons
        panelButtons.Controls.Add(buttonSubmit);
        panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
        panelButtons.Location = new System.Drawing.Point(0, 270);
        panelButtons.Name = "panelButtons";
        panelButtons.Padding = new System.Windows.Forms.Padding(8, 8, 12, 8);
        panelButtons.Size = new System.Drawing.Size(480, 50);
        panelButtons.TabIndex = 5;

        // buttonSubmit
        buttonSubmit.Anchor = System.Windows.Forms.AnchorStyles.Right;
        buttonSubmit.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
        buttonSubmit.FlatAppearance.BorderSize = 0;
        buttonSubmit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        buttonSubmit.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        buttonSubmit.ForeColor = System.Drawing.Color.White;
        buttonSubmit.Location = new System.Drawing.Point(372, 8);
        buttonSubmit.Name = "buttonSubmit";
        buttonSubmit.Size = new System.Drawing.Size(96, 32);
        buttonSubmit.TabIndex = 0;
        buttonSubmit.Text = "Submit";
        buttonSubmit.UseVisualStyleBackColor = false;
        buttonSubmit.Click += ButtonSubmit_Click;

        // UserInputDialog
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(480, 320);
        this.Controls.Add(panelButtons);
        this.Controls.Add(textBoxAnswer);
        this.Controls.Add(labelOrType);
        this.Controls.Add(listBoxChoices);
        this.Controls.Add(labelQuestion);
        this.Controls.Add(labelHeading);
        this.Font = new System.Drawing.Font("Segoe UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "UserInputDialog";
        this.Padding = new System.Windows.Forms.Padding(16, 16, 16, 0);
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Copilot Needs Input";

        panelButtons.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label labelHeading;
    private System.Windows.Forms.Label labelQuestion;
    private System.Windows.Forms.ListBox listBoxChoices;
    private System.Windows.Forms.Label labelOrType;
    private System.Windows.Forms.TextBox textBoxAnswer;
    private System.Windows.Forms.Panel panelButtons;
    private System.Windows.Forms.Button buttonSubmit;
}
