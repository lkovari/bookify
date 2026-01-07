# Bookify

Bookify is a .NET 9 application that converts documentation websites into PDF books. It accepts a documentation website URL, validates it, crawls internal pages, interprets site navigation as chapters and subchapters, and renders all content into a single PDF book.

## Solution Structure

The solution consists of six projects organized into application projects and test projects:

### Application Projects

1. **Bookify.Core** (Class Library)
   - Contains all business logic
   - Handles URL validation, crawling, rendering, and PDF generation
   - No dependencies on WebApi or Worker projects
   - Target Framework: .NET 9.0

2. **Bookify.WebApi** (ASP.NET Core Web API)
   - Provides HTTP endpoints for job management
   - Handles UI integration
   - References Bookify.Core for business logic
   - Does NOT reference Bookify.Worker (clean architecture)
   - Target Framework: .NET 9.0

3. **Bookify.Worker** (Worker Service)
   - Runs long-running conversion jobs in the background
   - References Bookify.Core for business logic
   - Target Framework: .NET 9.0

### Test Projects

4. **Bookify.Core.Tests** (xUnit Unit Tests)
   - Unit tests for Core business logic
   - References Bookify.Core
   - Target Framework: .NET 9.0

5. **Bookify.WebApi.Tests** (xUnit Integration Tests)
   - Integration tests using WebApplicationFactory
   - References Bookify.WebApi
   - Target Framework: .NET 9.0

6. **Bookify.Worker.Tests** (xUnit Tests)
   - Orchestration and unit tests for Worker
   - References Bookify.Worker and Bookify.Core
   - Target Framework: .NET 9.0

## Project References

The solution maintains a clean architecture with the following reference structure:

- **Bookify.WebApi** → **Bookify.Core**
- **Bookify.Worker** → **Bookify.Core**
- **Bookify.Core.Tests** → **Bookify.Core**
- **Bookify.Worker.Tests** → **Bookify.Worker**, **Bookify.Core**
- **Bookify.WebApi.Tests** → **Bookify.WebApi**

Note: Bookify.Core has no dependencies on other application projects, maintaining separation of concerns.

## NuGet Packages

### Bookify.Core

- **Microsoft.Playwright** (Version 1.48.0)
  - Used for browser automation and PDF rendering
  - Provides headless browser capabilities to render web pages

- **AngleSharp** (Version 1.1.2)
  - HTML parsing and DOM manipulation
  - Used for extracting links and parsing HTML content

- **PdfSharpCore** (Version 1.3.65)
  - PDF generation and merging
  - Creates and combines PDF pages into a single book

### Bookify.WebApi

- **Swashbuckle.AspNetCore** (Version 6.9.0)
  - Swagger/OpenAPI documentation
  - Provides API documentation and testing interface

### Test Projects

All test projects include:

- **Microsoft.NET.Test.Sdk** (Version 18.0.1)
  - Test SDK for .NET
  - Required for running xUnit tests

- **xunit** (Version 2.9.2)
  - xUnit testing framework

- **xunit.runner.visualstudio** (Version 2.8.2)
  - Visual Studio test runner integration

- **coverlet.collector** (Version 6.0.2)
  - Code coverage collection

**Bookify.WebApi.Tests** additionally includes:

- **Microsoft.AspNetCore.Mvc.Testing** (Version 9.0.0)
  - Integration testing support for ASP.NET Core
  - Provides WebApplicationFactory for testing

**Bookify.Core.Tests** and **Bookify.Worker.Tests** additionally include:

- **NSubstitute** (Version 5.3.0)
  - Mocking framework for unit tests

- **FluentAssertions** (Version 7.0.0)
  - Fluent assertion library for more readable test assertions

## How It Works

### Core Project (Bookify.Core)

The Core project contains the main business logic:

1. **URL Validation**
   - Validates that URLs are real, reachable, and safe
   - Implements SSRF protection by blocking localhost, private IP ranges, and internal networks
   - Ensures URLs are HTML-based (Content-Type: text/html)
   - Normalizes URLs and strips fragments

2. **Page Discovery**
   - Crawls only internal pages (same host)
   - Extracts links from HTML using AngleSharp
   - Implements BFS crawling with configurable limits (max pages, max depth)
   - Filters out external URLs, mailto, tel, and javascript links

3. **Table of Contents Extraction**
   - Interprets site navigation structure
   - Main menu items become chapters
   - Sub-menu items become subchapters
   - Uses strategy pattern (ITocExtractor) for extensibility
   - Provides generic fallback extractor and site-specific extractors

4. **PDF Rendering**
   - Uses Playwright to render each page
   - Injects CSS to hide headers, sidebars, cookie banners, and edit links
   - Emulates screen media for better rendering
   - Exports each page to PDF
   - Handles retries and timeouts

5. **PDF Merging**
   - Merges individual page PDFs in TOC order
   - Creates a single final PDF file
   - Supports PDF bookmarks using TOC structure

6. **Book Generation Orchestration**
   - Coordinates the entire conversion process
   - Validates URL → Extracts TOC → Discovers pages → Renders pages → Merges PDFs
   - Reports progress via IProgress<BookJobStatus>
   - Cleans up temporary files

### WebApi Project (Bookify.WebApi)

The WebApi project provides HTTP endpoints:

- **POST /api/books**
  - Accepts: `{ url: string, title?: string }`
  - Returns: `{ jobId: Guid }`
  - Starts background conversion task

- **GET /api/books/{jobId}**
  - Returns: `BookJobStatus` with current progress
  - Status states: Pending, Running, Completed, Failed

- **GET /api/books/{jobId}/file**
  - Streams the final PDF when conversion is completed
  - Returns 404 if job is not completed or not found

The WebApi uses in-memory storage (ConcurrentDictionary) for job state management in the MVP version. This can be replaced with a database or distributed cache in production.

### Worker Project (Bookify.Worker)

The Worker project runs background jobs:

- Processes conversion jobs queued by the WebApi
- Uses BookGenerator from Core to perform conversions
- Can be scaled independently from the WebApi
- Handles long-running operations without blocking HTTP requests

## Running the Project

### Prerequisites

- .NET 9 SDK or newer
- macOS (tested on macOS)
- JetBrains Rider Community (or any .NET IDE)

### Build and Restore

```bash
dotnet restore
dotnet build
```

### Install Playwright Browsers

Playwright browsers are required for PDF rendering. Since Playwright is used in `Bookify.Core`, install browsers using one of these methods:

**Method 1: Using the installation script (Recommended for macOS)**

```bash
./install-playwright.sh
```

This script automatically detects and uses the best available method (Node.js Playwright CLI or PowerShell script).

**Method 2: Using Node.js Playwright CLI (Recommended if you have Node.js)**

```bash
npm install -g playwright
playwright install
```

**Method 3: Using PowerShell script (if PowerShell is installed)**

```bash
cd Bookify.Core/Bookify.Core/bin/Debug/net9.0
pwsh playwright.ps1 install
```

The browsers will be installed to the standard Playwright cache location (`~/Library/Caches/ms-playwright/` on macOS) and will be automatically discovered by the .NET Playwright package.

### Run Tests

Run all tests:

```bash
dotnet test
```

Run specific test project:

```bash
dotnet test Bookify.WebApi.Tests
dotnet test Bookify.Core.Tests
dotnet test Bookify.Worker.Tests
```

### Run the Application

Run the WebApi:

```bash
cd Bookify.WebApi
dotnet run
```

The API will be available at the URL shown in the console (typically `https://localhost:5001` or `http://localhost:5000`).

Run the Worker:

```bash
cd Bookify.Worker
dotnet run
```

## Architecture Constraints

- **Max Pages**: Configurable limit per book (default: 200-300 pages)
- **Rate Limiting**: Rendering is rate-limited to avoid DoS
- **SSRF Protection**: Blocks internal/private network targets
- **External Links**: Preserved as clickable links in PDF, never crawled
- **Internal Links Only**: Only pages from the same host are crawled

## Future Enhancements

- Database or distributed cache for job state management
- Site-specific TOC extractors (AngularDevTocExtractor, NextJsTocExtractor)
- PDF bookmarks using TOC structure
- Optional packages:
  - Serilog.AspNetCore + Serilog.Sinks.Console (structured logging)
  - FluentValidation.AspNetCore (request validation)
  - Hellang.Middleware.ProblemDetails (standardized error responses)

## Development Notes

- The solution uses .NET 9 with top-level statements
- All projects use nullable reference types
- Implicit usings are enabled
- Test projects use xUnit with FluentAssertions and NSubstitute
- The architecture follows clean architecture principles with Core as the center
