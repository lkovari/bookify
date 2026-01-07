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

The Core project contains the main business logic organized into services:

#### 1. URL Validation (`UrlValidator`)

The URL validation process ensures security and accessibility:

- **Format Validation**: Uses `Uri.TryCreate` to validate URL format
- **Scheme Validation**: Only allows `http` and `https` schemes
- **SSRF Protection**: 
  - Blocks localhost, 127.0.0.1, and private IP ranges (10.x, 192.168.x, 172.16-31.x)
  - Resolves DNS and validates IP addresses against forbidden ranges
  - Prevents access to internal network resources
- **Content Validation**: 
  - Performs HTTP GET request (not HEAD) to verify accessibility
  - Follows redirects (max 5 redirects)
  - Requires HTTP 200 status code
  - Validates Content-Type contains `text/html`
- **URL Normalization**: 
  - Strips fragments (#section)
  - Returns canonical final URL after redirects
  - Uses `IDnsResolver` interface for testability (implemented by `SystemDnsResolver`)

#### 2. Page Discovery (`LinkDiscoveryService`)

Implements breadth-first search (BFS) crawling with intelligent deduplication:

- **Crawling Strategy**: 
  - BFS algorithm ensures pages are discovered in logical order
  - Configurable limits: max pages (default: 300), max depth (default: 10)
  - Uses `ConcurrentDictionary` for thread-safe discovery tracking
- **Link Extraction**: 
  - Parses HTML using AngleSharp
  - Extracts all `<a href>` elements
  - Filters out non-navigable links (mailto, tel, javascript)
- **URL Normalization and Deduplication**: 
  - Strips query parameters (e.g., `/page` and `/page?id=1` are treated as same)
  - Removes fragments
  - Handles index pages (`/index` and `/` are normalized to same URL)
  - Groups by normalized URL to eliminate duplicates
  - Case-insensitive host matching
- **Scope Control**: 
  - Only crawls pages from the same host as the start URL
  - External links are preserved in PDF but never crawled
  - Validates scheme (http/https only)

#### 3. Table of Contents Extraction (`ITocExtractor` / `GenericNavTocExtractor`)

Extracts navigation structure using strategy pattern:

- **Extraction Strategy**: 
  - Searches for `<nav>` elements, `[role='navigation']`, and common navigation classes
  - Analyzes link density to identify navigation sections
  - Falls back to extracting all internal links if no navigation found
- **TOC Structure**: 
  - Creates hierarchical `TocNode` tree
  - Main navigation items become top-level chapters
  - Sub-navigation items become subchapters
  - Preserves URL relationships
- **Extensibility**: 
  - `ITocExtractor` interface allows site-specific implementations
  - `GenericNavTocExtractor` provides fallback for unknown sites
  - Future: Site-specific extractors (AngularDevTocExtractor, NextJsTocExtractor)

#### 4. PDF Rendering (`PlaywrightPdfRenderer`)

Renders web pages to PDF using headless Chromium:

- **Browser Management**: 
  - Creates single browser instance per renderer (reused for all pages)
  - Implements `IAsyncDisposable` for proper resource cleanup
  - Launches Chromium in headless mode
- **Page Rendering Process**: 
  - Navigates to URL with `NetworkIdle` wait strategy
  - Waits 2 seconds after load for SPAs to fully render
  - 60-second timeout for slow-loading pages
  - Creates isolated browser context per page
- **Content Optimization**: 
  - Injects CSS to hide non-content elements:
    - Headers, navigation, sidebars
    - Cookie banners and notices
    - Edit links (GitHub, etc.)
    - Footer elements
  - Emulates screen media (not print) for better rendering
- **PDF Generation**: 
  - Exports to A4 format with 1cm margins
  - Enables background graphics
  - Saves to temporary directory with indexed filenames
- **Error Handling**: 
  - Automatic browser installation if missing
  - Graceful error handling with informative messages

#### 5. PDF Merging (`PdfMerger`)

Combines individual page PDFs into a single book:

- **Merging Process**: 
  - Uses PdfSharpCore library
  - Merges PDFs in TOC-ordered sequence
  - Preserves page order using indexed filenames
  - Handles missing files gracefully
- **Output**: 
  - Creates single PDF file
  - Maintains page sequence from TOC structure
  - Future: Add PDF bookmarks using TOC hierarchy

#### 6. Book Generation Orchestration (`BookGenerator`)

Coordinates the entire conversion workflow:

- **Process Flow**: 
  1. Validates input URL using `UrlValidator`
  2. Fetches initial HTML and extracts TOC using `ITocExtractor`
  3. Discovers all internal pages using `LinkDiscoveryService`
  4. Orders pages according to TOC structure
  5. Renders each page to PDF in parallel (max 2 concurrent)
  6. Merges all PDFs in correct order
  7. Returns path to final PDF file
- **Progress Reporting**: 
  - Uses `IProgress<BookJobStatus>` for real-time updates
  - Reports pages total, pages rendered, and current state
  - Updates job status throughout the process
- **Resource Management**: 
  - Creates temporary directory per job (using jobId)
  - Cleans up temporary files after completion
  - Proper disposal of Playwright resources
- **Error Handling**: 
  - Catches exceptions at each stage
  - Reports errors with descriptive messages
  - Updates job status to Failed on errors

### WebApi Project (Bookify.WebApi)

The WebApi project provides RESTful HTTP endpoints for book conversion:

#### API Endpoints

**POST `/api/books`** - Create a new book conversion job
- **Request Body**: 
  ```json
  {
    "url": "https://example.com",
    "title": "Optional Book Title"
  }
  ```
- **Response**: `{ "jobId": "guid" }`
- **Behavior**: 
  - Creates new job with Pending status
  - Starts background task for conversion
  - Returns immediately with jobId
  - Job runs asynchronously in background

**GET `/api/books`** - List all jobs
- **Response**: 
  ```json
  {
    "jobs": [...],
    "count": 5
  }
  ```
- **Behavior**: Returns all jobs with their current status

**GET `/api/books/{jobId}`** - Get job status
- **Response**: 
  ```json
  {
    "jobId": "guid",
    "state": "Running",
    "pagesTotal": 10,
    "pagesRendered": 5,
    "progress": 50.0,
    "isCompleted": false,
    "isFailed": false,
    "canDownload": false,
    "errorMessage": null
  }
  ```
- **States**: `Pending`, `Running`, `Completed`, `Failed`
- **Behavior**: Returns detailed status with progress percentage

**GET `/api/books/{jobId}/file`** - Download generated PDF
- **Response**: PDF file stream (application/pdf)
- **Behavior**: 
  - Returns PDF file when job is Completed
  - Returns error with progress info if not completed
  - Returns error message if job failed
  - 404 if job not found

#### Job State Management

- **Current Implementation**: In-memory `ConcurrentDictionary<Guid, BookJobStatus>`
- **Service**: `JobStateService` manages job lifecycle
- **Thread Safety**: Uses concurrent collections for thread-safe access
- **Limitations**: 
  - Jobs lost on application restart
  - Not suitable for multi-instance deployments
  - No persistence

#### Dependency Injection

- Services registered as singletons for shared state
- `PlaywrightPdfRenderer` created per job (not from DI) to avoid disposal issues
- HTTP clients created via `IHttpClientFactory` for proper lifecycle management
- All Core services instantiated in background task scope

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

## Future Improvements

This section outlines potential enhancements and improvements that can be made to the Bookify project:

### Infrastructure & Scalability

- **Persistent Job Storage**: Replace in-memory `ConcurrentDictionary` with database (PostgreSQL, SQL Server) or distributed cache (Redis) for job state management
- **Message Queue Integration**: Use message queues (RabbitMQ, Azure Service Bus, AWS SQS) for job processing to enable horizontal scaling
- **Distributed Processing**: Support multiple Worker instances processing jobs from a shared queue
- **Job Persistence**: Persist job state across application restarts
- **Job History**: Store completed and failed jobs for auditing and retry capabilities
- **Rate Limiting**: Implement rate limiting per user/IP to prevent abuse
- **Caching**: Cache TOC structures and page content for frequently requested sites

### PDF Generation Enhancements

- **PDF Bookmarks**: Add interactive table of contents as PDF bookmarks using TOC hierarchy
- **Cover Page**: Generate custom cover page with title, URL, and generation date
- **Page Numbering**: Add page numbers and headers/footers to PDF pages
- **Metadata**: Add PDF metadata (title, author, subject, keywords)
- **Custom Styling**: Allow users to customize PDF appearance (margins, fonts, colors)
- **Multi-column Layout**: Support multi-column layouts for better content organization
- **Image Optimization**: Optimize images before embedding in PDF
- **Table of Contents Page**: Add dedicated TOC page at the beginning of the PDF

### TOC Extraction Improvements

- **Site-Specific Extractors**: 
  - `AngularDevTocExtractor` for angular.dev documentation
  - `NextJsTocExtractor` for nextjs.org documentation
  - `VueJsTocExtractor` for vuejs.org documentation
  - `ReactTocExtractor` for react.dev documentation
- **Machine Learning**: Use ML models to better identify navigation structures
- **Sitemap Integration**: Parse XML sitemaps when available for better page discovery
- **Robots.txt Support**: Respect robots.txt rules for crawling
- **Custom Selectors**: Allow users to specify CSS selectors for navigation elements

### Crawling & Discovery

- **Excluded URLs**: Allow users to specify URL patterns or specific URLs to exclude from crawling (e.g., exclude `/api/*`, `/admin/*`, or specific pages)
- **External URLs Processing**: Allow users to specify external URLs or domains that should be processed even though they're on different hosts (e.g., include related documentation sites or cross-domain content)
- **Smarter Deduplication**: Content-based deduplication to detect similar pages with different URLs
- **Content Analysis**: Analyze page content to determine if it's worth including
- **Pagination Detection**: Automatically detect and follow pagination links
- **JavaScript Rendering**: Better support for JavaScript-heavy SPAs with longer wait times
- **Dynamic Content**: Handle infinite scroll and lazy-loaded content
- **Authentication Support**: Support sites requiring authentication (cookies, tokens)
- **Cookie Consent**: Automatically accept cookie consent dialogs
- **Language Detection**: Detect and filter by language if needed

### User Experience

- **Web UI**: Build a web interface for submitting jobs and viewing progress
- **Email Notifications**: Send email when book conversion completes
- **Webhook Support**: Allow webhook callbacks for job completion
- **Batch Processing**: Support converting multiple URLs in a single request
- **Preview Mode**: Generate preview PDF with first few pages before full conversion
- **Resume Failed Jobs**: Allow retrying failed jobs or resuming from last successful page
- **Job Scheduling**: Schedule book generation for specific times
- **Download Links**: Generate temporary download links with expiration

### Performance & Optimization

- **Parallel Rendering**: Increase parallel page rendering (currently limited to 2)
- **Streaming PDF Generation**: Stream PDF generation instead of waiting for all pages
- **Incremental Merging**: Merge PDFs incrementally as pages are rendered
- **Resource Pooling**: Reuse browser instances across multiple jobs
- **CDN Integration**: Store generated PDFs in cloud storage (S3, Azure Blob) with CDN
- **Compression**: Compress PDFs to reduce file size
- **Lazy Loading**: Only render pages that are actually needed

### Security & Validation

- **Input Validation**: Add FluentValidation for request validation
- **Rate Limiting**: Implement per-user and per-IP rate limiting
- **Authentication**: Add API key or OAuth authentication
- **Authorization**: Role-based access control for different user types
- **Audit Logging**: Log all API requests and job activities
- **Content Filtering**: Filter out inappropriate or malicious content
- **Virus Scanning**: Scan generated PDFs for malware

### Monitoring & Observability

- **Structured Logging**: Integrate Serilog with structured logging
- **Metrics**: Add application metrics (Prometheus, Application Insights)
- **Health Checks**: Implement health check endpoints
- **Distributed Tracing**: Add OpenTelemetry for distributed tracing
- **Error Tracking**: Integrate error tracking (Sentry, Application Insights)
- **Performance Monitoring**: Track conversion times and identify bottlenecks
- **Dashboard**: Build monitoring dashboard for job statistics

### Testing

- **Integration Tests**: Add more comprehensive integration tests
- **E2E Tests**: End-to-end tests for complete conversion workflows
- **Load Testing**: Performance and load testing
- **Security Testing**: Penetration testing and security audits
- **Browser Compatibility**: Test with different Playwright browser engines
- **Mock Services**: Improve testability with better mocking strategies

### Documentation & Developer Experience

- **API Documentation**: Enhanced Swagger/OpenAPI documentation with examples
- **Code Comments**: Add XML documentation comments to public APIs
- **Architecture Diagrams**: Visual documentation of system architecture
- **Deployment Guides**: Docker, Kubernetes, and cloud deployment guides
- **Contributing Guidelines**: Guidelines for contributors
- **Changelog**: Maintain changelog for version history

### Optional Packages

- **Serilog.AspNetCore + Serilog.Sinks.Console**: Structured logging
- **FluentValidation.AspNetCore**: Request validation
- **Hellang.Middleware.ProblemDetails**: Standardized error responses
- **Polly**: Resilience and transient-fault-handling
- **MediatR**: CQRS pattern implementation
- **AutoMapper**: Object-to-object mapping
- **Hangfire**: Background job processing
- **MassTransit**: Message bus abstraction

## Development Notes

- The solution uses .NET 9 with top-level statements
- All projects use nullable reference types
- Implicit usings are enabled
- Test projects use xUnit with FluentAssertions and NSubstitute
- The architecture follows clean architecture principles with Core as the center
