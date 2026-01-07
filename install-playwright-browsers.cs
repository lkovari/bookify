using Microsoft.Playwright;

Console.WriteLine("Installing Playwright browsers for .NET...");
try
{
    var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
    if (exitCode == 0)
    {
        Console.WriteLine("Playwright browsers installed successfully!");
    }
    else
    {
        Console.WriteLine($"Installation completed with exit code: {exitCode}");
        Environment.Exit(exitCode);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error installing Playwright browsers: {ex.Message}");
    Environment.Exit(1);
}

