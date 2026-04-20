namespace Kopilot;

/// <summary>
/// A TabControl subclass that paints the tab strip background and
/// content-area border in dark colours, eliminating the bright
/// edges that the default WinForms theme renderer leaves behind.
/// Individual tab headers are still painted by the DrawItem event.
/// </summary>
internal sealed class DarkTabControl : TabControl
{
	private const int WM_PAINT = 0x000F;

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);

		if (m.Msg == WM_PAINT)
		{
			using var g = CreateGraphics();
			PaintDarkOverlay(g);
		}
	}

	private void PaintDarkOverlay(Graphics g)
	{
		using var stripBrush  = new SolidBrush(AppTheme.Surface);
		using var borderBrush = new SolidBrush(AppTheme.OutputBox);

		var r = DisplayRectangle;

		// Fill the tab strip background to the right of the last tab and
		// to the left of the first tab so no bright pixels show through.
		if (TabCount > 0)
		{
			var lastTab = GetTabRect(TabCount - 1);
			if (lastTab.Right < Width)
			{
				g.FillRectangle(stripBrush,
					lastTab.Right, 0,
					Width - lastTab.Right, lastTab.Bottom + 2);
			}

			var firstTab = GetTabRect(0);
			if (firstTab.Left > 0)
			{
				g.FillRectangle(stripBrush,
					0, 0,
					firstTab.Left, firstTab.Bottom + 2);
			}

			// Cover the bright seam between the tab strip and the content
			// area in the empty region beside the last tab. Drawing in the
			// content-area colour blends it with the page below.
			g.FillRectangle(borderBrush,
				lastTab.Right, lastTab.Bottom,
				Width - lastTab.Right, r.Y - lastTab.Bottom + 2);
		}

		// Overpaint the bright 3D border around the tab content area.
		// Draw four solid bands instead of a pen rectangle so the corners
		// are fully covered without antialiased gaps.
		const int t = 3;
		// top band (under the tab strip)
		g.FillRectangle(borderBrush, r.X - t, r.Y - t, r.Width + 2 * t, t);
		// bottom band
		g.FillRectangle(borderBrush, r.X - t, r.Bottom, r.Width + 2 * t, t);
		// left band
		g.FillRectangle(borderBrush, r.X - t, r.Y - t, t, r.Height + 2 * t);
		// right band
		g.FillRectangle(borderBrush, r.Right, r.Y - t, t, r.Height + 2 * t);
	}
}
