using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Kopilot;

/// <summary>
/// Checks NuGet for newer versions of the GitHub.Copilot.SDK package
/// and npm for newer versions of the Copilot CLI binary.
/// </summary>
internal static class UpdateChecker
{
	private const string NuGetUrl =
		"https://api.nuget.org/v3-flatcontainer/github.copilot.sdk/index.json";

	// All platform-specific CLI packages share the same version number, so win32-x64
	// is used as a stable reference regardless of the current machine architecture.
	private const string NpmCliUrl =
		"https://registry.npmjs.org/@github%2Fcopilot-win32-x64/latest";

	private static readonly HttpClient _http = new()
	{
		Timeout = TimeSpan.FromSeconds(15),
	};

	static UpdateChecker()
	{
		_http.DefaultRequestHeaders.UserAgent.ParseAdd("Kopilot/1.0");
	}

	/// <summary>Returns the informational version of the loaded GitHub.Copilot.SDK assembly.</summary>
	public static string GetCurrentSdkVersion()
	{
		var asm = typeof(GitHub.Copilot.SDK.CopilotClient).Assembly;
		var infoVer = asm
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
			?.InformationalVersion;

		if (string.IsNullOrEmpty(infoVer))
			return asm.GetName().Version?.ToString(3) ?? "unknown";

		// Strip build metadata hash appended by SourceLink (e.g. "+abc1234")
		var plus = infoVer.IndexOf('+');
		return plus >= 0 ? infoVer[..plus] : infoVer;
	}

	/// <summary>
	/// Fetches all published versions of GitHub.Copilot.SDK from NuGet and returns
	/// the latest one. Returns null if the check fails or the network is unavailable.
	/// </summary>
	public static async Task<string?> GetLatestSdkVersionAsync()
	{
		try
		{
			var index = await _http
				.GetFromJsonAsync<NuGetIndex>(NuGetUrl)
				.ConfigureAwait(false);

			return index?.Versions?.LastOrDefault();
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Returns true when <paramref name="candidate"/> is strictly newer than
	/// <paramref name="current"/> using a SemVer-aware comparison.
	/// </summary>
	public static bool IsNewer(string current, string candidate)
		=> Compare(candidate, current) > 0;

	// ── Helpers ──────────────────────────────────────────────────────────────

	/// <summary>SemVer-aware comparison. Returns negative / zero / positive.</summary>
	private static int Compare(string a, string b)
	{
		var (aMaj, aMin, aPatch, aPre) = Parse(a);
		var (bMaj, bMin, bPatch, bPre) = Parse(b);

		var cmp = aMaj.CompareTo(bMaj);
		if (cmp != 0) return cmp;

		cmp = aMin.CompareTo(bMin);
		if (cmp != 0) return cmp;

		cmp = aPatch.CompareTo(bPatch);
		if (cmp != 0) return cmp;

		// Stable release outranks any pre-release of the same numeric version.
		if (string.IsNullOrEmpty(aPre) && !string.IsNullOrEmpty(bPre)) return  1;
		if (!string.IsNullOrEmpty(aPre) && string.IsNullOrEmpty(bPre)) return -1;

		return string.Compare(aPre, bPre, StringComparison.OrdinalIgnoreCase);
	}

	private static (int major, int minor, int patch, string pre) Parse(string v)
	{
		var pre = "";
		var dash = v.IndexOf('-');
		if (dash >= 0)
		{
			pre = v[(dash + 1)..];
			v   = v[..dash];
		}

		var parts = v.Split('.');
		int.TryParse(parts.Length > 0 ? parts[0] : "0", out var major);
		int.TryParse(parts.Length > 1 ? parts[1] : "0", out var minor);
		int.TryParse(parts.Length > 2 ? parts[2] : "0", out var patch);
		return (major, minor, patch, pre);
	}

	private sealed class NuGetIndex
	{
		[JsonPropertyName("versions")]
		public List<string>? Versions { get; set; }
	}

	// ── CLI version check ─────────────────────────────────────────────────────

	/// <summary>
	/// Fetches the latest <c>@github/copilot-win32-x64</c> version from the npm
	/// registry. Returns null if the check fails or the network is unavailable.
	/// </summary>
	public static async Task<string?> GetLatestCliVersionAsync()
	{
		try
		{
			var pkg = await _http
				.GetFromJsonAsync<NpmPackage>(NpmCliUrl)
				.ConfigureAwait(false);

			return pkg?.Version;
		}
		catch
		{
			return null;
		}
	}

	private sealed class NpmPackage
	{
		[JsonPropertyName("version")]
		public string? Version { get; set; }
	}
}
