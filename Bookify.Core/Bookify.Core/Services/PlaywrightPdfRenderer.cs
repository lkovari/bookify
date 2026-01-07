using Microsoft.Playwright;

namespace Bookify.Core.Services;

public sealed class PlaywrightPdfRenderer : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public async Task<string> RenderPageAsync(Uri url, string outputPath, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaywrightPdfRenderer));
        }

        if (_playwright == null)
        {
            try
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });
            }
            catch (Exception ex) when (ex.Message.Contains("Executable doesn't exist") || ex.Message.Contains("Please run"))
            {
                try
                {
                    await PlaywrightBrowserInstaller.InstallBrowsersAsync();
                    _playwright = await Playwright.CreateAsync();
                    _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = true
                    });
                }
                catch (Exception installEx)
                {
                    throw new InvalidOperationException(
                        "Playwright browsers are not installed and automatic installation failed. " +
                        "Please install them manually using one of these methods:\n" +
                        "1. Install PowerShell: brew install --cask powershell\n" +
                        "2. Then run: cd Bookify.Core/Bookify.Core/bin/Debug/net9.0 && pwsh playwright.ps1 install\n" +
                        "3. Or use Node.js: playwright install chromium\n\n" +
                        $"Installation error: {installEx.Message}\n" +
                        $"Original error: {ex.Message}", ex);
                }
            }
        }

        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(url.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });

            await Task.Delay(2000, cancellationToken);

            await page.AddStyleTagAsync(new PageAddStyleTagOptions
            {
                Content = @"
                    header, .header, nav, .nav, .navigation, .sidebar, aside,
                    .cookie-banner, .cookie-notice, [class*='cookie'],
                    .edit-link, [class*='edit'], .github-edit,
                    footer, .footer, .skip-link, .skip-to-content
                    { display: none !important; }
                "
            });

            await page.EmulateMediaAsync(new PageEmulateMediaOptions
            {
                Media = Media.Screen
            });

            await page.PdfAsync(new PagePdfOptions
            {
                Path = outputPath,
                Format = "A4",
                PrintBackground = true,
                Margin = new Margin
                {
                    Top = "1cm",
                    Right = "1cm",
                    Bottom = "1cm",
                    Left = "1cm"
                }
            });

            return outputPath;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_browser != null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }

        if (_playwright != null)
        {
            _playwright.Dispose();
            _playwright = null;
        }

        _disposed = true;
    }
}

