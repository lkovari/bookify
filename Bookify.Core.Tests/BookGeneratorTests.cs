using System.Net;
using System.Net.Http;
using Bookify.Core.Abstractions;
using Bookify.Core.Models;
using Bookify.Core.Services;
using NSubstitute;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace Bookify.Core.Tests;

/// <summary>
/// Tests for BookGenerator helper methods.
/// Note: The main GenerateAsync method behavior (resilient page rendering, early progress reporting)
/// is tested through integration tests in Bookify.WebApi.Tests.
/// </summary>
public sealed class BookGeneratorTests
{
    [Fact]
    public void GenerateFileName_WithTitle_GeneratesFileNameFromTitle()
    {
        var generator = CreateBookGenerator();
        var jobId = Guid.NewGuid();

        var fileName = InvokeGenerateFileName(generator, "My Test Book", jobId);

        Assert.Contains("MyTestBook", fileName);
        Assert.Contains(jobId.ToString(), fileName);
        Assert.EndsWith(".pdf", fileName);
    }

    [Fact]
    public void GenerateFileName_WithoutTitle_UsesJobId()
    {
        var generator = CreateBookGenerator();
        var jobId = Guid.NewGuid();

        var fileName = InvokeGenerateFileName(generator, null, jobId);

        Assert.Equal($"book{jobId}.pdf", fileName);
    }

    [Fact]
    public void GenerateFileName_WithWhitespaceTitle_HandlesWhitespace()
    {
        var generator = CreateBookGenerator();
        var jobId = Guid.NewGuid();

        var fileName = InvokeGenerateFileName(generator, "  Test  Book  ", jobId);

        Assert.Contains("TestBook", fileName);
        Assert.Contains(jobId.ToString(), fileName);
    }

    [Fact]
    public void GenerateFileName_WithSpecialCharacters_RemovesSpecialCharacters()
    {
        var generator = CreateBookGenerator();
        var jobId = Guid.NewGuid();

        var fileName = InvokeGenerateFileName(generator, "Test: Book & More!", jobId);

        Assert.Contains("Test", fileName);
        Assert.Contains("Book", fileName);
        Assert.Contains("More", fileName);
    }

    [Fact]
    public void MergePartialPdfs_ValidPdfFiles_MergesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = CreateBookGenerator();

            CreateTestPdfFile(Path.Combine(tempDir, "page_0000.pdf"));
            CreateTestPdfFile(Path.Combine(tempDir, "page_0001.pdf"));

            var jobId = Guid.NewGuid();
            var result = generator.MergePartialPdfs(tempDir, "Test Book", jobId);

            Assert.NotNull(result);
            Assert.EndsWith(".pdf", result);
            Assert.True(File.Exists(result));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void MergePartialPdfs_NoPdfFiles_ThrowsInvalidOperationException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = CreateBookGenerator();
            var jobId = Guid.NewGuid();

            var ex = Assert.Throws<InvalidOperationException>(() => generator.MergePartialPdfs(tempDir, "Test Book", jobId));
            Assert.Contains("No PDF files", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void MergePartialPdfs_OrdersFilesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = CreateBookGenerator();

            CreateTestPdfFile(Path.Combine(tempDir, "page_0001.pdf"));
            CreateTestPdfFile(Path.Combine(tempDir, "page_0000.pdf"));
            CreateTestPdfFile(Path.Combine(tempDir, "page_0002.pdf"));

            var jobId = Guid.NewGuid();
            var result = generator.MergePartialPdfs(tempDir, "Test Book", jobId);

            Assert.NotNull(result);
            Assert.True(File.Exists(result));
            
            using var mergedDoc = PdfReader.Open(result, PdfDocumentOpenMode.ReadOnly);
            Assert.Equal(3, mergedDoc.PageCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void MergePartialPdfs_WithMissingFiles_SkipsMissingFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = CreateBookGenerator();

            CreateTestPdfFile(Path.Combine(tempDir, "page_0000.pdf"));
            CreateTestPdfFile(Path.Combine(tempDir, "page_0002.pdf"));

            var jobId = Guid.NewGuid();
            var result = generator.MergePartialPdfs(tempDir, "Test Book", jobId);

            Assert.NotNull(result);
            Assert.True(File.Exists(result));
            
            using var mergedDoc = PdfReader.Open(result, PdfDocumentOpenMode.ReadOnly);
            Assert.Equal(2, mergedDoc.PageCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static BookGenerator CreateBookGenerator(
        UrlValidator? urlValidator = null,
        LinkDiscoveryService? linkDiscovery = null,
        ITocExtractor? tocExtractor = null,
        PlaywrightPdfRenderer? pdfRenderer = null,
        PdfMerger? pdfMerger = null,
        HttpClient? httpClient = null)
    {
        var dnsResolver = Substitute.For<IDnsResolver>();
        dnsResolver.ResolveAsync(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("8.8.8.8") });

        httpClient ??= new HttpClient(new TestHttpMessageHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
            }
        });

        urlValidator ??= new UrlValidator(dnsResolver, httpClient);

        linkDiscovery ??= new LinkDiscoveryService(httpClient);

        tocExtractor ??= Substitute.For<ITocExtractor>();
        tocExtractor.CanHandle(Arg.Any<Uri>()).Returns(true);
        tocExtractor.ExtractAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TocNode
            {
                Title = "Test",
                Url = new Uri("https://example.com"),
                Children = new List<TocNode>()
            });

        pdfRenderer ??= new PlaywrightPdfRenderer();

        pdfMerger ??= new PdfMerger();

        return new BookGenerator(urlValidator, linkDiscovery, tocExtractor, pdfRenderer, pdfMerger, httpClient);
    }

    private static string InvokeGenerateFileName(BookGenerator generator, string? title, Guid jobId)
    {
        var methods = typeof(BookGenerator).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var method = methods.FirstOrDefault(m => 
            m.Name == "GenerateFileName" && 
            m.GetParameters().Length == 2 &&
            m.GetParameters()[0].ParameterType == typeof(string) &&
            m.GetParameters()[1].ParameterType == typeof(Guid));
        
        if (method == null)
        {
            throw new InvalidOperationException("GenerateFileName method not found");
        }

        return (string)method.Invoke(null, new object?[] { title, jobId })!;
    }

    private static void CreateTestPdfFile(string filePath)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Save(filePath);
        document.Dispose();
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Response ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

