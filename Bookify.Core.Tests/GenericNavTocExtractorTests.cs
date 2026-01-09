using Bookify.Core.Abstractions;
using Bookify.Core.Models;
using Bookify.Core.Services;
using Xunit;

namespace Bookify.Core.Tests;

public sealed class GenericNavTocExtractorTests
{
    [Fact]
    public void CanHandle_AnyUrl_ReturnsTrue()
    {
        var extractor = new GenericNavTocExtractor();

        var result = extractor.CanHandle(new Uri("https://example.com"));

        Assert.True(result);
    }

    [Fact]
    public async Task ExtractAsync_SimpleHtml_ReturnsTocNode()
    {
        var html = @"
<html>
<head><title>Test Page</title></head>
<body>
    <h1>Main Title</h1>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.Equal(url, result.Url);
        Assert.Equal("Test Page", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_HtmlWithNav_ExtractsNavLinks()
    {
        var html = @"
<html>
<head><title>Test Page</title></head>
<body>
    <nav>
        <a href=""/page1"">Page 1</a>
        <a href=""/page2"">Page 2</a>
    </nav>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Children);
        Assert.True(result.Children.Count >= 2);
    }

    [Fact]
    public async Task ExtractAsync_HtmlWithNestedNav_ExtractsHierarchicalStructure()
    {
        var html = @"
<html>
<head><title>Test Page</title></head>
<body>
    <nav>
        <ul>
            <li><a href=""/section1"">Section 1</a>
                <ul>
                    <li><a href=""/section1/page1"">Page 1</a></li>
                </ul>
            </li>
        </ul>
    </nav>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Children);
        var section1 = result.Children.FirstOrDefault(c => c.Title.Contains("Section 1"));
        Assert.NotNull(section1);
    }

    [Fact]
    public async Task ExtractAsync_NoTitle_UsesH1AsTitle()
    {
        var html = @"
<html>
<body>
    <h1>Main Heading</h1>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.Equal("Main Heading", result.Title);
    }

    [Fact]
    public async Task ExtractAsync_NoTitleOrH1_UsesUrlAsTitle()
    {
        var html = @"
<html>
<body>
    <p>Content</p>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com/page");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.Equal(url.ToString(), result.Title);
    }

    [Fact]
    public async Task ExtractAsync_ExternalLinks_ExcludesExternalLinks()
    {
        var html = @"
<html>
<body>
    <nav>
        <a href=""/internal"">Internal</a>
        <a href=""https://external.com/page"">External</a>
    </nav>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.All(result.Children, child => Assert.Equal("example.com", child.Url.Host));
    }

    [Fact]
    public async Task ExtractAsync_InvalidHtml_ReturnsRootNode()
    {
        var html = "not valid html";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.Equal(url, result.Url);
    }

    [Fact]
    public async Task ExtractAsync_EmptyHtml_ReturnsRootNode()
    {
        var html = "";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.Equal(url, result.Url);
    }

    [Fact]
    public async Task ExtractAsync_NavWithFewLinks_UsesAllDocumentLinks()
    {
        var html = @"
<html>
<body>
    <nav>
        <a href=""/page1"">Page 1</a>
    </nav>
    <main>
        <a href=""/page2"">Page 2</a>
        <a href=""/page3"">Page 3</a>
    </main>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Children);
    }

    [Fact]
    public async Task ExtractAsync_FragmentLinks_PreservesFragment()
    {
        var html = @"
<html>
<body>
    <nav>
        <a href=""#/section1"">Section 1</a>
    </nav>
</body>
</html>";

        var extractor = new GenericNavTocExtractor();
        var url = new Uri("https://example.com");

        var result = await extractor.ExtractAsync(url, html);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Children);
    }
}

