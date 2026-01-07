#!/bin/bash

echo "Installing Playwright browsers for Bookify project..."

if command -v node &> /dev/null; then
    echo "Using Node.js Playwright CLI..."
    if ! command -v playwright &> /dev/null; then
        echo "Installing Playwright CLI globally..."
        npm install -g playwright
    fi
    echo "Installing Chromium (required for .NET Playwright)..."
    playwright install chromium
    echo "Playwright browsers installed successfully using Node.js CLI."
elif [ -f "Bookify.Core/Bookify.Core/bin/Debug/net9.0/playwright.ps1" ]; then
    echo "Using .NET Playwright installation script..."
    if command -v pwsh &> /dev/null; then
        cd Bookify.Core/Bookify.Core/bin/Debug/net9.0
        pwsh playwright.ps1 install
        echo "Playwright browsers installed successfully using PowerShell script."
    else
        echo "Error: PowerShell (pwsh) is required but not found."
        echo "Please install PowerShell: brew install --cask powershell"
        echo "Or use Node.js Playwright: npm install -g playwright && playwright install chromium"
        exit 1
    fi
else
    echo "Error: Could not find Playwright installation method."
    echo "Please build the project first: dotnet build"
    exit 1
fi

