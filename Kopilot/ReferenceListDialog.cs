using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kopilot;

/// <summary>
/// Generic list dialog for picking a custom agent or skill to insert into the
/// prompt. Renders a multi-column dark <see cref="ListView"/> sorted alphabetically
/// by name and returns the selected reference name in <see cref="SelectedName"/>.
/// </summary>
internal sealed class ReferenceListDialog : Form
{
	private readonly ListView _list;
	private readonly Button   _insertBtn;

	/// <summary>The name of the selected agent/skill, or null if the dialog was cancelled.</summary>
	public string? SelectedName { get; private set; }

	private ReferenceListDialog(string title, IReadOnlyList<(string[] Cells, string Name, string Tooltip)> rows, string[] columnHeaders, int[] columnWidths)
	{
		Text            = title;
		StartPosition   = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.Sizable;
		MinimumSize     = new Size(640, 360);
		Size            = new Size(820, 480);
		BackColor       = AppTheme.Background;
		ForeColor       = AppTheme.TextPrimary;
		Font            = new Font("Segoe UI", 9F);
		ShowInTaskbar   = false;

		_list = new ListView
		{
			Dock          = DockStyle.Fill,
			View          = View.Details,
			FullRowSelect = true,
			GridLines     = false,
			HideSelection = false,
			MultiSelect   = false,
			BackColor     = AppTheme.InputBox,
			ForeColor     = AppTheme.TextPrimary,
			BorderStyle   = BorderStyle.FixedSingle,
			OwnerDraw     = true,
		};

		// Owner-draw the header to match the dark palette (default is grey-on-grey).
		_list.DrawColumnHeader += (_, e) =>
		{
			using var bg = new SolidBrush(AppTheme.Surface);
			e.Graphics.FillRectangle(bg, e.Bounds);
			using var pen = new Pen(AppTheme.ButtonBorder);
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
			TextRenderer.DrawText(e.Graphics, e.Header!.Text, Font, e.Bounds, AppTheme.TextPrimary,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		};
		// Owner-drawing rows is more invasive than we need; defer to the system
		// for sub-items so platform selection rendering still works correctly.
		_list.DrawSubItem += (_, e) => e.DrawDefault = true;
		_list.DrawItem    += (_, e) => e.DrawDefault = true;

		for (int i = 0; i < columnHeaders.Length; i++)
			_list.Columns.Add(columnHeaders[i], columnWidths[i]);

		foreach (var (cells, name, tooltip) in rows)
		{
			var item = new ListViewItem(cells)
			{
				Tag         = name,
				ToolTipText = tooltip,
			};
			_list.Items.Add(item);
		}
		_list.ShowItemToolTips = true;

		var buttonPanel = new Panel
		{
			Dock      = DockStyle.Bottom,
			Height    = 44,
			BackColor = AppTheme.Surface,
			Padding   = new Padding(8),
		};

		_insertBtn = new Button
		{
			Text      = "Insert into Prompt",
			Width     = 150,
			Height    = 28,
			Dock      = DockStyle.Right,
			BackColor = AppTheme.AccentBg,
			ForeColor = AppTheme.AccentText,
			FlatStyle = FlatStyle.Flat,
			Enabled   = false,
		};
		_insertBtn.FlatAppearance.BorderColor = AppTheme.ButtonBorder;
		_insertBtn.Click += (_, _) => Accept();

		var cancelBtn = new Button
		{
			Text      = "Cancel",
			Width     = 90,
			Height    = 28,
			Dock      = DockStyle.Right,
			BackColor = AppTheme.ButtonBg,
			ForeColor = AppTheme.TextPrimary,
			FlatStyle = FlatStyle.Flat,
			Margin    = new Padding(0, 0, 8, 0),
			DialogResult = DialogResult.Cancel,
		};
		cancelBtn.FlatAppearance.BorderColor = AppTheme.ButtonBorder;

		// Right-aligned: add cancel first, then insert (Dock = Right stacks RTL).
		buttonPanel.Controls.Add(_insertBtn);
		var spacer = new Panel { Dock = DockStyle.Right, Width = 8, BackColor = AppTheme.Surface };
		buttonPanel.Controls.Add(spacer);
		buttonPanel.Controls.Add(cancelBtn);

		Controls.Add(_list);
		Controls.Add(buttonPanel);

		_list.SelectedIndexChanged += (_, _) =>
			_insertBtn.Enabled = _list.SelectedItems.Count > 0;
		_list.DoubleClick          += (_, _) => Accept();
		_list.KeyDown              += (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				Accept();
				e.Handled = true;
			}
		};

		AcceptButton = _insertBtn;
		CancelButton = cancelBtn;

		if (_list.Items.Count > 0)
		{
			_list.Items[0].Selected = true;
			_list.Items[0].Focused  = true;
			_list.Select();
		}
	}

	private void Accept()
	{
		if (_list.SelectedItems.Count == 0) return;
		SelectedName = _list.SelectedItems[0].Tag as string;
		DialogResult = DialogResult.OK;
		Close();
	}

	/// <summary>Show a dialog listing custom agents.</summary>
	public static ReferenceListDialog ForAgents(IReadOnlyList<AgentInfo> agents)
	{
		var rows = agents
			.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
			.Select(a => (
				Cells:   new[]
				{
					a.Name,
					string.IsNullOrWhiteSpace(a.Description) ? "" : a.Description!,
				},
				Name:    a.Name,
				Tooltip: $"{a.Name}\nTier: {a.Tier}\n{a.FilePath}\n\n{a.Description ?? "(no description)"}"
			))
			.ToList();

		return new ReferenceListDialog(
			title:          $"Agents in Session ({rows.Count})",
			rows:           rows,
			columnHeaders:  new[] { "Name", "Description" },
			columnWidths:   new[] { 200, 560 });
	}

	/// <summary>Show a dialog listing skills.</summary>
	public static ReferenceListDialog ForSkills(IReadOnlyList<SkillInfo> skills)
	{
		var rows = skills
			.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
			.Select(s => (
				Cells:   new[]
				{
					s.Name,
					s.Triggers.Count == 0 ? "" : string.Join("; ", s.Triggers),
					string.IsNullOrWhiteSpace(s.Description) ? "" : s.Description!,
				},
				Name:    s.Name,
				Tooltip: $"{s.Name}\nTier: {s.Tier}\n{s.FolderPath}\n\n{s.Description ?? "(no description)"}"
			))
			.ToList();

		return new ReferenceListDialog(
			title:          $"Skills in Session ({rows.Count})",
			rows:           rows,
			columnHeaders:  new[] { "Name", "Trigger Words", "Description" },
			columnWidths:   new[] { 180, 280, 360 });
	}
}
