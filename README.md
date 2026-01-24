# Bookify

Bookify is a .NET 9 application that converts documentation websites into PDF books. It accepts a documentation website URL, validates it, crawls internal pages, interprets site navigation as chapters and subchapters, and renders all content into a single PDF book.

## Still under development, it does not crawl the nested pages correctly when traversing the pages. ##

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
  - Enhanced error messages that include the URL being processed
  - Specific handling for page crashes and timeouts
  - Error messages provide context about which page failed and why

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
  3. **Reports initial page count from TOC** (provides early progress visibility)
  4. Discovers all internal pages using `LinkDiscoveryService`
  5. Updates page count with final merged total
  6. Orders pages according to TOC structure
  7. Renders each page to PDF in parallel (max 2 concurrent)
  8. Merges all PDFs in correct order
  9. Returns path to final PDF file
- **Progress Reporting**: 
  - Uses `IProgress<BookJobStatus>` for real-time updates
  - Reports pages total immediately after TOC extraction (before link discovery completes)
  - Updates page count when link discovery completes
  - Reports pages rendered, and current state
  - Updates job status throughout the process
- **Resource Management**: 
  - Creates temporary directory per job (using jobId)
  - Cleans up temporary files after completion
  - Proper disposal of Playwright resources
- **Error Handling**: 
  - **Resilient page rendering**: Individual page failures don't crash the entire job
  - Failed pages are tracked with their URLs and error messages
  - Job completes successfully if at least one page renders successfully
  - Failed pages are logged in the job status error message
  - Job only fails if all pages fail to render
  - Catches exceptions at each stage
  - Reports errors with descriptive messages including which pages failed
  - Updates job status to Failed only when all pages fail or critical errors occur

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
- **Response**: PDF file stream (application/pdf) or error response
- **Behavior**: 
  - Returns PDF file when job is Completed (even if some pages failed)
  - Returns detailed progress info if not completed:
    - Shows page count and progress percentage
    - Displays remaining pages count
    - Provides descriptive messages based on job state
  - Returns error message if job failed
  - 404 if job not found
- **Progress Messages**:
  - `"Discovering pages and analyzing site structure..."` - When pagesTotal is 0 and job is running
  - `"Processing: X of Y pages rendered (Z remaining)"` - When pages are being rendered
  - `"Job is queued and will start shortly..."` - When job is pending

**POST `/api/books/{jobId}/cancel`** - Cancel a running job
- **Response**: 
  ```json
  {
    "message": "Job canceled successfully",
    "jobId": "guid"
  }
  ```
- **Behavior**: 
  - Cancels the job immediately
  - Sets job state to Failed with "Job was canceled by user" message
  - Returns error if job not found, already completed, or cannot be canceled
  - Does not save any partial PDFs

**POST `/api/books/{jobId}/cancel-and-save`** - Cancel job and save partial PDF
- **Response**: 
  ```json
  {
    "message": "Job canceled and partial PDF saved",
    "jobId": "guid",
    "pagesRendered": 5,
    "pagesTotal": 10,
    "outputFilePath": "/path/to/partial{jobId}.pdf"
  }
  ```
- **Behavior**: 
  - Cancels the job
  - Waits 2 seconds for in-progress operations to complete
  - Merges all rendered PDF pages into a partial PDF
  - Sets job state to Completed with the partial PDF path
  - Returns error if no PDFs were rendered or merge fails
  - Partial PDF is named `partial{jobId}.pdf`

#### Job State Management

- **Current Implementation**: In-memory `ConcurrentDictionary<Guid, BookJobStatus>`
- **Service**: `JobStateService` manages job lifecycle
- **Thread Safety**: Uses concurrent collections for thread-safe access
- **Cancellation Support**: 
  - Each job has its own `CancellationTokenSource` for cancellation
  - Cancellation tokens are registered and can be canceled via API endpoints
  - Temporary directories are tracked for partial PDF generation
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

## Tests

> ** WARNING: All tests were generated by AI.**

The solution includes comprehensive test coverage across all three projects. All tests use xUnit framework with NSubstitute for mocking.

### Bookify.Core.Tests

Unit tests for the Core business logic services:

#### UrlValidatorTests (13 tests)
- `ValidateAsync_ValidHttpsUrl_ReturnsNormalizedUri` - Validates successful URL validation with HTTPS
- `ValidateAsync_InvalidUrlFormat_ThrowsArgumentException` - Ensures invalid URL formats are rejected
- `ValidateAsync_NonHttpScheme_ThrowsArgumentException` - Validates only HTTP/HTTPS schemes are allowed
- `ValidateAsync_LocalhostHost_ThrowsArgumentException` - Tests SSRF protection for localhost
- `ValidateAsync_PrivateIpAddress_ThrowsArgumentException` - Tests SSRF protection for private IPs
- `ValidateAsync_NonHtmlContent_ThrowsArgumentException` - Validates Content-Type must be HTML
- `ValidateAsync_HttpError_ThrowsHttpRequestException` - Tests error handling for HTTP failures
- `ValidateAsync_RemovesFragment_ReturnsUriWithoutFragment` - Validates URL fragment removal
- `IsForbiddenIp_PrivateIpRanges_ReturnsTrue` (Theory with 5 test cases) - Tests all private IP ranges:
  - 127.0.0.1
  - 192.168.1.1
  - 10.0.0.1
  - 172.16.0.1
  - 172.31.255.255

#### SystemDnsResolverTests (3 tests)
- `ResolveAsync_ValidHost_ReturnsIpAddresses` - Tests DNS resolution for valid hosts
- `ResolveAsync_InvalidHost_ThrowsException` - Tests error handling for invalid hosts
- `ResolveAsync_Localhost_ReturnsLoopbackAddress` - Tests localhost resolution

#### PdfMergerTests (4 tests)
- `MergeFiles_ValidPdfFiles_MergesSuccessfully` - Tests merging multiple PDF files
- `MergeFiles_NonExistentFile_SkipsAndContinues` - Tests graceful handling of missing files
- `MergeFiles_EmptyList_ThrowsInvalidOperationException` - Tests error when no files provided
- `MergeFiles_MultipleFiles_MergesInOrder` - Tests merging 5 files in correct order

#### LinkDiscoveryServiceTests (7 tests)
- `DiscoverAsync_SimpleHtmlWithLinks_ReturnsDiscoveredLinks` - Tests basic link discovery
- `DiscoverAsync_ExternalLinks_ExcludesExternalLinks` - Tests external link exclusion
- `DiscoverAsync_MaxDepthExceeded_StopsAtMaxDepth` - Tests depth limit enforcement
- `DiscoverAsync_MaxPagesExceeded_StopsAtMaxPages` - Tests page limit enforcement
- `DiscoverAsync_InvalidLinks_IgnoresInvalidLinks` - Tests filtering of mailto, tel, javascript links
- `DiscoverAsync_CancellationRequested_StopsDiscovery` - Tests cancellation support
- `DiscoverAsync_HttpError_ContinuesWithOtherLinks` - Tests error resilience

#### GenericNavTocExtractorTests (11 tests)
- `CanHandle_AnyUrl_ReturnsTrue` - Tests universal handler capability
- `ExtractAsync_SimpleHtml_ReturnsTocNode` - Tests basic TOC extraction
- `ExtractAsync_HtmlWithNav_ExtractsNavLinks` - Tests navigation element extraction
- `ExtractAsync_HtmlWithNestedNav_ExtractsHierarchicalStructure` - Tests nested navigation
- `ExtractAsync_NoTitle_UsesH1AsTitle` - Tests fallback to H1 for title
- `ExtractAsync_NoTitleOrH1_UsesUrlAsTitle` - Tests fallback to URL for title
- `ExtractAsync_ExternalLinks_ExcludesExternalLinks` - Tests external link exclusion
- `ExtractAsync_InvalidHtml_ReturnsRootNode` - Tests error handling for invalid HTML
- `ExtractAsync_EmptyHtml_ReturnsRootNode` - Tests error handling for empty HTML
- `ExtractAsync_NavWithFewLinks_UsesAllDocumentLinks` - Tests fallback to all document links
- `ExtractAsync_FragmentLinks_PreservesFragment` - Tests fragment link preservation

#### BookGeneratorTests (7 tests)
- `GenerateFileName_WithTitle_GeneratesFileNameFromTitle` - Tests filename generation with title
- `GenerateFileName_WithoutTitle_UsesJobId` - Tests filename generation without title
- `GenerateFileName_WithWhitespaceTitle_HandlesWhitespace` - Tests whitespace handling
- `GenerateFileName_WithSpecialCharacters_RemovesSpecialCharacters` - Tests special character removal
- `MergePartialPdfs_ValidPdfFiles_MergesSuccessfully` - Tests partial PDF merging
- `MergePartialPdfs_NoPdfFiles_ThrowsInvalidOperationException` - Tests error when no PDFs found
- `MergePartialPdfs_OrdersFilesCorrectly` - Tests file ordering in merge operation

**Total: 45 tests**

### Bookify.WebApi.Tests

Integration tests for the WebApi endpoints and services:

#### JobStateServiceTests (13 tests)
- `SetStatus_NewJob_StoresStatus` - Tests storing new job status
- `SetStatus_ExistingJob_UpdatesStatus` - Tests updating existing job status
- `GetStatus_NonExistentJob_ReturnsNull` - Tests retrieval of non-existent jobs
- `GetAllJobs_NoJobs_ReturnsEmptyList` - Tests empty job list
- `GetAllJobs_MultipleJobs_ReturnsAllJobs` - Tests retrieving multiple jobs
- `GetAllJobs_ReturnsJobsOrderedByJobIdDescending` - Tests job ordering
- `RegisterCancellationToken_NewJob_StoresToken` - Tests cancellation token registration
- `CancelJob_NonExistentJob_ReturnsFalse` - Tests canceling non-existent jobs
- `CancelJob_AlreadyCanceled_ReturnsFalse` - Tests canceling already canceled jobs
- `RegisterTempDirectory_NewJob_StoresDirectory` - Tests temporary directory registration
- `GetTempDirectory_NonExistentJob_ReturnsNull` - Tests retrieval of non-existent temp directories
- `RemoveJob_ExistingJob_RemovesAllData` - Tests complete job removal
- `RemoveJob_DisposesCancellationTokenSource` - Tests proper resource disposal

#### ApiEndpointTests (11 tests)
- `Get_Root_RedirectsToSwagger` - Tests root endpoint redirects to Swagger
- `Post_CreateBook_ReturnsJobId` - Tests book creation endpoint
- `Get_ListBooks_ReturnsJobsList` - Tests job listing endpoint
- `Get_BookStatus_NonExistentJob_ReturnsNotFound` - Tests 404 for non-existent jobs
- `Get_BookStatus_ExistingJob_ReturnsStatus` - Tests job status retrieval
- `Get_DownloadBook_NonExistentJob_ReturnsNotFound` - Tests 404 for download of non-existent jobs
- `Get_DownloadBook_JobNotCompleted_ReturnsBadRequest` - Tests download before completion
- `Post_CancelBook_NonExistentJob_ReturnsNotFound` - Tests 404 for canceling non-existent jobs
- `Post_CancelBook_ExistingPendingJob_ReturnsOk` - Tests successful job cancellation
- `Post_CancelAndSaveBook_NonExistentJob_ReturnsNotFound` - Tests 404 for cancel-and-save
- `Post_CancelAndSaveBook_NoPdfFiles_ReturnsBadRequest` - Tests cancel-and-save with no PDFs

**Total: 24 tests**

### Bookify.Worker.Tests

Unit tests for the Worker background service:

#### WorkerTests (5 tests)
- `Worker_Constructor_InitializesSuccessfully` - Tests worker instantiation
- `ExecuteAsync_WhenStarted_LogsInformation` - Tests logging when worker starts
- `ExecuteAsync_WhenCancellationRequested_StopsGracefully` - Tests graceful cancellation
- `ExecuteAsync_RunsInLoop_UntilCancellation` - Tests worker loop execution
- `ExecuteAsync_LoggingDisabled_DoesNotLog` - Tests behavior when logging is disabled

**Total: 5 tests**

### Test Summary

- **Bookify.Core.Tests**: 45 tests covering URL validation, DNS resolution, PDF operations, link discovery, TOC extraction, and book generation
- **Bookify.WebApi.Tests**: 24 tests covering job state management and API endpoints
- **Bookify.Worker.Tests**: 5 tests covering worker lifecycle and cancellation
- **Grand Total**: 74 tests

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

## Recent Improvements

This section documents recent enhancements and improvements that have been implemented:

### Resilient Page Rendering (2024)

- **Individual Page Failure Handling**: The system now gracefully handles individual page failures without crashing the entire job
  - If a page crashes or times out during rendering, it's skipped and other pages continue processing
  - Failed pages are tracked with their URLs and error messages
  - The job completes successfully as long as at least one page renders successfully
  - Failed page information is included in the job status error message for debugging

- **Enhanced Error Messages**: Improved error reporting throughout the system
  - Page rendering errors now include the specific URL that failed
  - Error messages distinguish between page crashes, timeouts, and other failures
  - Better context for debugging issues with specific pages

### Early Progress Reporting (2024)

- **TOC-Based Page Count**: The system now reports an initial page count immediately after TOC extraction
  - Previously, `pagesTotal` remained 0 until link discovery completed (which could take a long time)
  - Now, as soon as the TOC is extracted from the navigation, an initial page count is reported
  - This provides much earlier visibility into the job's scope
  - The count is updated when link discovery completes with the final merged total

- **Improved API Response Messages**: Enhanced progress messages in the `/api/books/{jobId}/file` endpoint
  - More descriptive messages based on job state
  - Shows remaining pages count: `"Processing: X of Y pages rendered (Z remaining)"`
  - Better messages for different states (Pending, Running, etc.)

### Benefits

These improvements provide:
- **Better User Experience**: Users see progress information much earlier in the process
- **Higher Success Rate**: Jobs complete successfully even when some pages fail
- **Better Debugging**: Clear error messages help identify problematic pages
- **More Reliable**: The system is more resilient to individual page failures

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
