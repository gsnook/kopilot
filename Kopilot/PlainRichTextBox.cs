using System.Runtime.InteropServices;

namespace Kopilot;

/// <summary>
/// A RichTextBox that enforces a single fixed color scheme (white-on-black)
/// and always pastes as plain text, stripping all formatting.
///
/// Two-layer defence:
///   1. WM_PASTE + EM_PASTESPECIAL are intercepted so clipboard content is
///      always inserted as plain text with the control's own font/color.
///   2. OnTextChanged normalizes every character's formatting after any edit,
///      catching any pathway that bypasses the paste intercept.
/// </summary>
internal sealed class PlainRichTextBox : RichTextBox
{
    private const int WM_PASTE       = 0x0302;
    private const int EM_PASTESPECIAL = 0x0440;
    private const int WM_SETREDRAW   = 0x000B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private bool _normalizing;

    public PlainRichTextBox()
    {
        BackColor = Color.Black;
        ForeColor = Color.White;
        AllowDrop = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PASTE || m.Msg == EM_PASTESPECIAL)
        {
            PastePlainText();
            return;
        }
        base.WndProc(ref m);
    }

    private void PastePlainText()
    {
        // Try to get text from the clipboard in order of preference
        string text =
            Clipboard.ContainsText(TextDataFormat.UnicodeText) ? Clipboard.GetText(TextDataFormat.UnicodeText) :
            Clipboard.ContainsText(TextDataFormat.Text)        ? Clipboard.GetText(TextDataFormat.Text) :
            Clipboard.ContainsText()                           ? Clipboard.GetText() :
            string.Empty;

        if (string.IsNullOrEmpty(text)) return;

        SelectionFont       = Font;
        SelectionColor      = ForeColor;
        SelectionBackColor  = BackColor;
        SelectedText        = text;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        NormalizeFormatting();
    }

    /// <summary>
    /// Walks every character and resets its formatting to the control's
    /// own font and colors, preventing any styling from persisting.
    /// Uses WM_SETREDRAW to suppress flicker during the operation.
    /// </summary>
    private void NormalizeFormatting()
    {
        if (_normalizing || TextLength == 0) return;
        _normalizing = true;
        try
        {
            int savedStart  = SelectionStart;
            int savedLength = SelectionLength;

            SendMessage(Handle, WM_SETREDRAW, 0, 0);
            try
            {
                SelectAll();
                SelectionFont      = Font;
                SelectionColor     = ForeColor;
                SelectionBackColor = BackColor;
            }
            finally
            {
                SendMessage(Handle, WM_SETREDRAW, 1, 0);
                Invalidate();
            }

            Select(savedStart, savedLength);
        }
        finally
        {
            _normalizing = false;
        }
    }
}
