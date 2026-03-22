namespace Kopilot;

/// <summary>
/// A RichTextBox that enforces a fixed color scheme (white-on-black) and
/// always pastes as plain text, stripping formatting from the clipboard.
/// </summary>
internal sealed class PlainRichTextBox : RichTextBox
{
    private const int WM_PASTE = 0x0302;

    public PlainRichTextBox()
    {
        BackColor = Color.Black;
        ForeColor = Color.White;
        // Disable drag-drop to prevent richly-formatted drops from other apps
        AllowDrop = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PASTE)
        {
            InsertPlainText(Clipboard.GetText(TextDataFormat.UnicodeText));
            return;
        }
        base.WndProc(ref m);
    }

    private void InsertPlainText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // Ensure the pasted text adopts the control's own colors
        SelectionColor = ForeColor;
        SelectionBackColor = BackColor;
        SelectedText = text;
    }
}
