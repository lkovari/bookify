using Microsoft.Playwright;

namespace Bookify.Core.Services;

public sealed class PlaywrightPdfRenderer : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    /// <summary>
    /// Loads the page in Chromium and returns the fully rendered DOM HTML.
    /// Critical for SPA sites where navigation is rendered client-side.
    /// </summary>
    public async Task<string> GetRenderedHtmlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaywrightPdfRenderer));
        }

        await EnsureBrowserAsync().ConfigureAwait(false);

        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            }).ConfigureAwait(false);

            // For SPA/hash routing we typically want to give a bit more time for nav + content to settle.
            if (!string.IsNullOrEmpty(url.Fragment) && url.Fragment.StartsWith("#/", StringComparison.Ordinal))
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
                await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }

            return await page.ContentAsync().ConfigureAwait(false);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }

    public async Task<string> RenderPageAsync(Uri url, string outputPath, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PlaywrightPdfRenderer));
        }

        await EnsureBrowserAsync().ConfigureAwait(false);

        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            try
            {
                await page.GotoAsync(url.ToString(), new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                }).ConfigureAwait(false);
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("crashed") || ex.Message.Contains("Target closed"))
            {
                throw new InvalidOperationException($"Page crashed while navigating to \"{url}\", waiting until \"networkidle\"", ex);
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException($"Timeout while navigating to \"{url}\", waiting until \"networkidle\"", ex);
            }

            if (!string.IsNullOrEmpty(url.Fragment) && url.Fragment.StartsWith("#/", StringComparison.Ordinal))
            {
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
                }
                catch (PlaywrightException ex) when (ex.Message.Contains("crashed") || ex.Message.Contains("Target closed"))
                {
                    throw new InvalidOperationException($"Page crashed while waiting for networkidle on \"{url}\"", ex);
                }
                await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }

            await page.AddStyleTagAsync(new PageAddStyleTagOptions
            {
                Content = @"
                    header, .header, nav, .nav, .navigation, .sidebar, aside,
                    .cookie-banner, .cookie-notice, [class*='cookie'],
                    .edit-link, [class*='edit'], .github-edit,
                    footer, .footer, .skip-link, .skip-to-content
                    { display: none !important; }
                "
            }).ConfigureAwait(false);

            await page.EmulateMediaAsync(new PageEmulateMediaOptions
            {
                Media = Media.Screen
            }).ConfigureAwait(false);

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
            }).ConfigureAwait(false);

            return outputPath;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Error rendering page \"{url}\": {ex.Message}", ex);
        }
        finally
        {
            try
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task EnsureBrowserAsync()
    {
        if (_playwright != null && _browser != null)
        {
            return;
        }

        try
        {
            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.Message.Contains("Executable doesn't exist") || ex.Message.Contains("Please run"))
        {
            try
            {
                await PlaywrightBrowserInstaller.InstallBrowsersAsync().ConfigureAwait(false);
                _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                }).ConfigureAwait(false);
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_browser != null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
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