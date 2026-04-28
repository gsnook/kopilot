using System.Text.RegularExpressions;

namespace Kopilot;

/// <summary>
/// Detects bare image-file references in Copilot's assistant markdown
/// (e.g. <c>screenshot.png</c>, <c>assets/diagram.svg</c>,
/// <c>@docs/img/foo.jpg</c>, or <c>C:\path\to\bar.png</c>) and appends
/// an inline thumbnail beneath each unique reference by injecting a
/// standard <c>![alt](url)</c> markdown line. The original path text
/// is preserved verbatim.
///
/// References inside <em>inline</em> backtick spans are still detected
/// (Copilot routinely formats file paths as <c>`path`</c>); only
/// fenced code blocks and existing markdown image syntax are skipped.
///
/// Resolution rules:
/// <list type="bullet">
///   <item>Relative paths are served from the workspace via the
///         workspace virtual host.</item>
///   <item>Absolute local paths (drive-letter or rooted) are served via
///         the matching per-drive virtual host.</item>
///   <item>Remote URLs (<c>http://</c>, <c>https://</c>, <c>file://</c>)
///         are intentionally <em>not</em> rewritten — emitting them as
///         &lt;img&gt; tags would let the model exfiltrate data through
///         crafted URLs or expose arbitrary files via file://.</item>
/// </list>
/// </summary>
internal static class ImageReferenceTransformer
{
	private const string ImageExtensions = @"png|jpe?g|gif|webp|svg|bmp|ico|tiff?|avif";

	private static readonly Regex CodeFenceRegex = new(
		@"```[\s\S]*?```",
		RegexOptions.Compiled);

	private static readonly Regex MarkdownImageRegex = new(
		@"!\[[^\]]*\]\([^)]+\)",
		RegexOptions.Compiled);

	// Boundary-anchored: the reference must start at line-begin or after a
	// whitespace / quote / paren / bracket character, and end before
	// whitespace, sentence punctuation, quote, paren, or bracket. The
	// reference itself is an optional `@` or scheme/drive prefix followed
	// by a path of word/dot/dash segments separated by / or \, ending
	// with a recognised image extension.
	//
	// Group 3 optionally consumes a trailing backtick so that paths
	// written as `inline code` have the thumbnail injection land
	// AFTER the closing backtick (otherwise the injected paragraph
	// would be swallowed by the inline-code span).
	private static readonly Regex ImageReferenceRegex = new(
		@"(^|[\s(\[""'`])" +
		@"(@?(?:https?://|file:///?|[A-Za-z]:[\\/])?" +
		@"[\w.\-]+(?:[\\/][\w.\-]+)*\.(?:" + ImageExtensions + @"))" +
		@"(`?)" +
		@"(?=$|[\s)\]""'`,;:!?]|\.(?:\s|$))",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// Returns <paramref name="content"/> with thumbnail-injection markdown
	/// appended after each unique image reference. <paramref name="workspaceVirtualHost"/>
	/// serves files under <paramref name="workspaceRoot"/>; <paramref name="driveHostFormat"/>
	/// is a <see cref="string.Format(string, object?)"/> template used to build a per-drive
	/// host name (e.g. <c>"kopilot-drive-{0}.local"</c> where <c>{0}</c> is the lowercase
	/// drive letter).
	/// </summary>
	public static string Apply(
		string? content,
		string? workspaceRoot,
		string? workspaceVirtualHost,
		string? driveHostFormat)
	{
		if (string.IsNullOrEmpty(content)) return content ?? "";

		// Step 1 — mask regions we must not rewrite (fenced code blocks +
		// existing markdown images). Inline `code` spans are intentionally
		// NOT masked: Copilot routinely formats file paths as `path`, and
		// we still want to detect those.
		var masks = new System.Collections.Generic.List<string>();
		string Mask(Match m)
		{
			var token = "\u0000KPMASK" + masks.Count + "\u0000";
			masks.Add(m.Value);
			return token;
		}

		var masked = CodeFenceRegex.Replace(content, Mask);
		masked = MarkdownImageRegex.Replace(masked, Mask);

		// Step 2 — detect image refs, dedupe per block, and stash thumbnails.
		var thumbs = new System.Collections.Generic.List<(string Reference, string Url)>();
		var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

		masked = ImageReferenceRegex.Replace(masked, match =>
		{
			var reference = match.Groups[2].Value;
			var url = ResolveUrl(reference, workspaceRoot, workspaceVirtualHost, driveHostFormat);
			if (url == null) return match.Value;
			if (!seen.Add(url)) return match.Value;
			thumbs.Add((reference, url));
			return match.Value + "\u0000KPTHUMB" + (thumbs.Count - 1) + "\u0000";
		});

		// Step 3 — restore masked regions verbatim.
		masked = Regex.Replace(masked, @"\u0000KPMASK(\d+)\u0000",
			m => masks[int.Parse(m.Groups[1].Value)]);

		// Step 4 — materialize thumbnail markers as standalone markdown
		// image paragraphs so they render on their own line beneath the
		// original path text.
		masked = Regex.Replace(masked, @"\u0000KPTHUMB(\d+)\u0000", m =>
		{
			var t = thumbs[int.Parse(m.Groups[1].Value)];
			var alt = t.Reference.Replace("[", "").Replace("]", "");
			return "\n\n![" + alt + "](" + t.Url + ")\n\n";
		});

		return masked;
	}

	private static string? ResolveUrl(
		string reference,
		string? workspaceRoot,
		string? workspaceVirtualHost,
		string? driveHostFormat)
	{
		var s = reference;
		if (s.Length > 0 && s[0] == '@') s = s.Substring(1);

		// Remote URLs and file:// URIs are intentionally not rewritten.
		// http(s) would let the model fetch arbitrary external content
		// (potential exfiltration vector); file:// is redundant with the
		// drive-host path below and would bypass our scoping.
		if (Regex.IsMatch(s, @"^(https?|file)://", RegexOptions.IgnoreCase))
			return null;

		var fwd = s.Replace('\\', '/');
		bool hasDrive = Regex.IsMatch(fwd, @"^[A-Za-z]:/");
		bool isAbsolute = hasDrive || (fwd.Length > 0 && fwd[0] == '/');

		if (isAbsolute)
		{
			// 1) If the path lives inside the open workspace, prefer the
			//    workspace host so the URL is short and stable.
			if (!string.IsNullOrEmpty(workspaceRoot) && !string.IsNullOrEmpty(workspaceVirtualHost))
			{
				var rootFwd = workspaceRoot.Replace('\\', '/').TrimEnd('/');
				if (string.Equals(fwd, rootFwd, System.StringComparison.OrdinalIgnoreCase))
					return "https://" + workspaceVirtualHost + "/";
				if (fwd.Length > rootFwd.Length + 1 &&
					fwd.StartsWith(rootFwd + "/", System.StringComparison.OrdinalIgnoreCase))
				{
					var rel = fwd.Substring(rootFwd.Length + 1);
					return "https://" + workspaceVirtualHost + "/" + EncodePathSegments(rel);
				}
			}

			// 2) Otherwise route through the per-drive virtual host.
			if (!string.IsNullOrEmpty(driveHostFormat) && hasDrive)
			{
				var letter = char.ToLowerInvariant(fwd[0]);
				var afterDrive = fwd.Substring(3); // skip "X:/"
				var host = string.Format(driveHostFormat, letter);
				return "https://" + host + "/" + EncodePathSegments(afterDrive);
			}

			// Rooted paths without a drive letter ("/foo/bar.png") are not
			// supportable on Windows without further info; skip them.
			return null;
		}

		// Relative path — served from the workspace root.
		if (string.IsNullOrEmpty(workspaceVirtualHost)) return null;
		var relative = fwd.StartsWith("./") ? fwd.Substring(2) : fwd;
		return "https://" + workspaceVirtualHost + "/" + EncodePathSegments(relative);
	}

	private static string EncodePathSegments(string path)
	{
		var parts = path.Split('/');
		for (int i = 0; i < parts.Length; i++)
			parts[i] = System.Uri.EscapeDataString(parts[i]);
		return string.Join("/", parts);
	}
}
