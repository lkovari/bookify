namespace Bookify.Core.Services;

public static class PlaywrightBrowserInstaller
{
    public static async Task InstallBrowsersAsync()
    {
        try
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw new Exception($"Playwright browser installation failed with exit code {exitCode}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to install Playwright browsers: {ex.Message}. " +
                "Please install manually using: cd Bookify.Core/Bookify.Core/bin/Debug/net9.0 && pwsh playwright.ps1 install", ex);
        }
    }
}

