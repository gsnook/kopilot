using System.IO;
using System.Text;

namespace Kopilot;

/// <summary>
/// Reads and writes kopilot.ini, stored adjacent to the Kopilot executable.
///
/// Supported format:
///   [Org]
///   Folder=C:\path\to\org
/// </summary>
internal sealed class KopilotSettings
{
	private static readonly string IniPath =
		Path.Combine(Application.StartupPath, "kopilot.ini");

	/// <summary>Path to the organization-level tier folder, or null if not configured.</summary>
	public string? OrgFolder { get; set; }

	/// <summary>Loads settings from kopilot.ini; returns defaults if the file does not exist.</summary>
	public static KopilotSettings Load()
	{
		var settings = new KopilotSettings();
		if (!File.Exists(IniPath)) return settings;

		string? section = null;

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
					continue;
				}

				var eq = line.IndexOf('=');
				if (eq < 0) continue;

				var key = line[..eq].Trim().ToLowerInvariant();
				var val = line[(eq + 1)..].Trim();

				if (section == "org" && key == "folder" && !string.IsNullOrEmpty(val))
					settings.OrgFolder = val;
			}
		}
		catch { /* best-effort; return defaults on any read error */ }

		return settings;
	}

	/// <summary>Writes settings to kopilot.ini, overwriting any previous content.</summary>
	public void Save()
	{
		var sb = new StringBuilder();
		sb.Append("[Org]\r\n");
		sb.Append($"Folder={OrgFolder ?? ""}\r\n");

		File.WriteAllText(IniPath, sb.ToString(), Encoding.ASCII);
	}
}
