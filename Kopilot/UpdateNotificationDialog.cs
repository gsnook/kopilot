namespace Kopilot;

/// <summary>
/// Displayed at startup when a newer version of GitHub.Copilot.SDK is available on NuGet.
/// Shows the current and latest versions and provides a command the user can copy to update
/// the package reference, then rebuild Kopilot to apply it.
/// </summary>
internal sealed partial class UpdateNotificationDialog : Form
{
	private readonly string _updateCommand;

	public UpdateNotificationDialog(string currentVersion, string latestVersion)
	{
		_updateCommand =
			$"dotnet add package GitHub.Copilot.SDK --version {latestVersion}";

		InitializeComponent();

		labelVersions.Text =
			$"Current version:  {currentVersion}\r\nLatest version:   {latestVersion}";
		textBoxCommand.Text = _updateCommand;
	}

	private void ButtonCopy_Click(object? sender, EventArgs e)
	{
		Clipboard.SetText(_updateCommand);
		buttonCopy.Text    = "Copied!";
		buttonCopy.Enabled = false;
	}

	private void ButtonNuGet_Click(object? sender, EventArgs e)
	{
		// Extract the version from the stored command string.
		var marker  = "--version ";
		var idx     = _updateCommand.IndexOf(marker, StringComparison.Ordinal);
		var version = idx >= 0 ? _updateCommand[(idx + marker.Length)..].Trim() : "";
		var url     = $"https://www.nuget.org/packages/GitHub.Copilot.SDK/{version}";

		try
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName        = url,
				UseShellExecute = true,
			});
		}
		catch { /* best-effort */ }
	}

	private void ButtonDismiss_Click(object? sender, EventArgs e)
	{
		DialogResult = DialogResult.Cancel;
		Close();
	}
}
