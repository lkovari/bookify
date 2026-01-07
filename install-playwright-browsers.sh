#!/bin/bash

echo "Installing Playwright browsers for .NET Bookify project..."

cd "$(dirname "$0")"

if command -v dotnet &> /dev/null; then
    echo "Using .NET Playwright installation..."
    dotnet run --project Bookify.Core/Bookify.Core -- install-playwright-browsers.cs 2>/dev/null || {
        echo "Trying alternative method..."
        cd Bookify.Core/Bookify.Core/bin/Debug/net9.0
        if [ -f "playwright.ps1" ]; then
            if command -v pwsh &> /dev/null; then
                pwsh playwright.ps1 install
                echo "Playwright browsers installed successfully!"
            else
                echo "Error: PowerShell (pwsh) is required but not found."
                echo "Installing browsers using Node.js Playwright CLI as fallback..."
                if command -v node &> /dev/null; then
                    if ! command -v playwright &> /dev/null; then
                        npm install -g playwright
                    fi
                    playwright install chromium
                    echo "Chromium installed via Node.js CLI."
                else
                    echo "Error: Neither pwsh nor node found. Please install one of them."
                    exit 1
                fi
            fi
        else
            echo "Error: playwright.ps1 not found. Please build the project first: dotnet build"
            exit 1
        fi
    }
else
    echo "Error: dotnet command not found."
    exit 1
fi

