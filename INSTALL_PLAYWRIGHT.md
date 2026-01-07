# Installing Playwright Browsers for .NET

The .NET Playwright package requires browsers to be installed using its own installation method. Here are the options:

## Option 1: Using PowerShell (Recommended)

If you have PowerShell installed:

```bash
cd Bookify.Core/Bookify.Core/bin/Debug/net9.0
pwsh playwright.ps1 install
```

If PowerShell is not installed on macOS:
```bash
brew install --cask powershell
```

## Option 2: Using Node.js Playwright CLI

If PowerShell is not available, you can use Node.js Playwright CLI, but you need to ensure browsers are installed to the correct location:

```bash
npm install -g playwright
playwright install chromium
```

Note: This installs browsers to `~/Library/Caches/ms-playwright/` which should work with .NET Playwright.

## Option 3: Programmatic Installation

You can also install browsers programmatically by running this in your terminal after building:

```bash
cd Bookify.Core/Bookify.Core/bin/Debug/net9.0
dotnet exec Microsoft.Playwright.dll install chromium
```

However, the DLL path might need to be adjusted based on your build output.

## Verify Installation

After installation, verify that Chromium exists at:
```
~/Library/Caches/ms-playwright/chromium-*/chrome-mac/Chromium.app/Contents/MacOS/Chromium
```

## Quick Fix for Current Issue

Since you already have Node.js Playwright installed, try:

```bash
playwright install chromium
```

This should install Chromium to the location that .NET Playwright expects.

