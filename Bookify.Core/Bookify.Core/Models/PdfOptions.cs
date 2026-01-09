namespace Bookify.Core.Models;

public record PdfOptions
{
    public string Format { get; init; } = "A4";
    public bool PrintBackground { get; init; } = true;
    public PdfMargins? Margin { get; init; }
    public string? Scale { get; init; }
    public bool DisplayHeaderFooter { get; init; } = false;
    public string? HeaderTemplate { get; init; }
    public string? FooterTemplate { get; init; }
}

public record PdfMargins
{
    public string Top { get; init; } = "1cm";
    public string Right { get; init; } = "1cm";
    public string Bottom { get; init; } = "1cm";
    public string Left { get; init; } = "1cm";
}

