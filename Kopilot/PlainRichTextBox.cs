using System.Runtime.InteropServices;

namespace Kopilot;

/// <summary>
/// A RichTextBox that enforces a single fixed color scheme
/// and always pastes as plain text, stripping all formatting.
///
/// Two-layer defence:
///   1. WM_PASTE + EM_PASTESPECIAL are intercepted so clipboard content is
///      always inserted as plain text with the control's own font/color.
///   2. OnTextChanged normalizes every character's formatting after any edit,
///      catching any pathway that bypasses the paste intercept.
///
/// Drag-and-drop of FILES is accepted and surfaced via FilesDropped; all
/// other drag sources (text, RTF, etc.) are rejected so formatting can't
/// be introduced via drag.
/// </summary>
internal sealed class PlainRichTextBox : RichTextBox
{
    private const int WM_PASTE       = 0x0302;
    private const int EM_PASTESPECIAL = 0x0440;
    private const int WM_SETREDRAW   = 0x000B;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private bool _normalizing;

    /// <summary>Raised when one or more files (or folders) are dropped onto the editor.</summary>
    public event EventHandler<IReadOnlyList<string>>? FilesDropped;

    public PlainRichTextBox()
    {
        BackColor = AppTheme.InputBox;
        ForeColor = AppTheme.TextPrimary;
        AllowDrop = true;
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        // Do NOT call base — prevents RichTextBox accepting text/RTF drags
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            FilesDropped?.Invoke(this, paths);
        // Do NOT call base — prevents any text insertion from the drag source
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PASTE || m.Msg == EM_PASTESPECIAL)
        {
            // Images on the clipboard are allowed through to the native RichEdit
            // handler so they embed as a visible preview. MainForm extracts them
            // from the RTF at send-time and forwards them as blob attachments.
            // Text-only pastes still route through our plain-text path so
            // formatting (fonts, colors, styles) is stripped as before.
            if (ClipboardHasImage())
            {
                base.WndProc(ref m);
                return;
            }
            PastePlainText();
            return;
        }
        base.WndProc(ref m);
    }

    private void PastePlainText()
    {
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

    /// <summary>
    /// Returns true when the clipboard contains picture data in any of the
    /// formats commonly produced by screen capture tools, browsers, and Office
    /// apps (CF_BITMAP, CF_DIB, DIBv5, and the "PNG" synthetic format).
    /// </summary>
    private static bool ClipboardHasImage()
    {
        try
        {
            if (Clipboard.ContainsImage())              return true;
            if (Clipboard.ContainsData("PNG"))          return true;
            if (Clipboard.ContainsData(DataFormats.Dib))return true;
            if (Clipboard.ContainsData(DataFormats.Bitmap)) return true;
            if (Clipboard.ContainsData("DeviceIndependentBitmap")) return true;
        }
        catch
        {
            // Clipboard access can fail transiently; treat as "no image".
        }
        return false;
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
