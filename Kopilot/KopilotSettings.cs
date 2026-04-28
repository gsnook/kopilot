using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kopilot;

/// <summary>
/// Reads and writes kopilot.ini, stored adjacent to the Kopilot executable.
///
/// Supported format:
///   [SkillTree]
///   Folder=C:\path\to\folder1
///   Folder=C:\path\to\folder2
///
/// Legacy format (auto-migrated on first load, then rewritten as [SkillTree]):
///   [Org]
///   Folder=C:\path\to\org
/// </summary>
internal sealed class KopilotSettings
{
	private static readonly string IniPath =
		Path.Combine(Application.StartupPath, "kopilot.ini");

	/// <summary>
	/// Ordered list of Skill Tree folders.  Each folder is searched for a <c>skills/</c>
	/// subdirectory and an <c>agents/</c> subdirectory when a Copilot session starts.
	/// Later entries override earlier entries for agent-name collisions.
	/// </summary>
	public List<string> SkillTreeFolders { get; set; } = new();

	/// <summary>
	/// When true, every user prompt is reduced by <see cref="CavemanTransformer"/>
	/// before being sent to the model. Persisted under the <c>[Caveman]</c>
	/// section as <c>Enabled=true|false</c>.
	/// </summary>
	public bool CavemanMode { get; set; }

	/// <summary>Loads settings from kopilot.ini; returns defaults if the file does not exist.</summary>
	public static KopilotSettings Load()
	{
		var settings = new KopilotSettings();
		if (!File.Exists(IniPath)) return settings;

		string? section = null;
		string? legacyOrgFolder = null;
		bool sawSkillTreeSection = false;
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			foreach (var rawLine in File.ReadLines(IniPath))
			{
				var line = rawLine.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
					continue;

				if (line.StartsWith('[') && line.EndsWith(']'))
				{
					section = line[1..^1].Trim().ToLowerInvariant();
					if (section == "skilltree") sawSkillTreeSection = true;
					continue;
				}

				var eq = line.IndexOf('=');
				if (eq < 0) continue;

				var key = line[..eq].Trim().ToLowerInvariant();
				var val = line[(eq + 1)..].Trim();
				if (string.IsNullOrEmpty(val)) continue;

				if (section == "skilltree" && key == "folder")
				{
					if (seen.Add(val))
						settings.SkillTreeFolders.Add(val);
				}
				else if (section == "org" && key == "folder")
				{
					legacyOrgFolder = val;
				}
				else if (section == "caveman" && key == "enabled")
				{
					settings.CavemanMode =
						val.Equals("true", System.StringComparison.OrdinalIgnoreCase)
						|| val == "1"
						|| val.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
						|| val.Equals("on",  System.StringComparison.OrdinalIgnoreCase);
				}
			}
		}
		catch { /* best-effort; return defaults on any read error */ }

		// Migration: if no [SkillTree] section was present but a legacy [Org] Folder
		// value exists, seed the new list with that single value.  The next Save()
		// will rewrite the file in the new format and drop the [Org] section.
		if (!sawSkillTreeSection && !string.IsNullOrEmpty(legacyOrgFolder))
			settings.SkillTreeFolders.Add(legacyOrgFolder);

		return settings;
	}

	/// <summary>Writes settings to kopilot.ini, overwriting any previous content.</summary>
	public void Save()
	{
		var sb = new StringBuilder();
		sb.Append("[SkillTree]\r\n");

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var folder in SkillTreeFolders)
		{
			if (string.IsNullOrWhiteSpace(folder)) continue;
			var trimmed = folder.Trim();
			if (!seen.Add(trimmed)) continue;
			sb.Append($"Folder={trimmed}\r\n");
		}

		sb.Append("\r\n[Caveman]\r\n");
		sb.Append($"Enabled={(CavemanMode ? "true" : "false")}\r\n");

		File.WriteAllText(IniPath, sb.ToString(), Encoding.ASCII);
	}
}
