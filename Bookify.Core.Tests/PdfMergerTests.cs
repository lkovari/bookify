using Bookify.Core.Services;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace Bookify.Core.Tests;

public sealed class PdfMergerTests
{
    [Fact]
    public void MergeFiles_ValidPdfFiles_MergesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputFile1 = Path.Combine(tempDir, "input1.pdf");
            var inputFile2 = Path.Combine(tempDir, "input2.pdf");
            var outputFile = Path.Combine(tempDir, "output.pdf");

            CreateTestPdf(inputFile1, "Page 1");
            CreateTestPdf(inputFile2, "Page 2");

            var merger = new PdfMerger();
            merger.MergeFiles(new[] { inputFile1, inputFile2 }, outputFile);

            Assert.True(File.Exists(outputFile));

            using var mergedDoc = PdfReader.Open(outputFile, PdfDocumentOpenMode.ReadOnly);
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

    [Fact]
    public void MergeFiles_NonExistentFile_SkipsAndContinues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputFile1 = Path.Combine(tempDir, "input1.pdf");
            var nonExistentFile = Path.Combine(tempDir, "nonexistent.pdf");
            var outputFile = Path.Combine(tempDir, "output.pdf");

            CreateTestPdf(inputFile1, "Page 1");

            var merger = new PdfMerger();
            merger.MergeFiles(new[] { inputFile1, nonExistentFile }, outputFile);

            Assert.True(File.Exists(outputFile));

            using var mergedDoc = PdfReader.Open(outputFile, PdfDocumentOpenMode.ReadOnly);
            Assert.Equal(1, mergedDoc.PageCount);
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
    public void MergeFiles_EmptyList_ThrowsInvalidOperationException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputFile = Path.Combine(tempDir, "output.pdf");

            var merger = new PdfMerger();
            
            var ex = Assert.Throws<InvalidOperationException>(() => merger.MergeFiles(Array.Empty<string>(), outputFile));
            Assert.Contains("no pages", ex.Message);
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
    public void MergeFiles_MultipleFiles_MergesInOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputFiles = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var file = Path.Combine(tempDir, $"input{i}.pdf");
                CreateTestPdf(file, $"Page {i + 1}");
                inputFiles.Add(file);
            }

            var outputFile = Path.Combine(tempDir, "output.pdf");

            var merger = new PdfMerger();
            merger.MergeFiles(inputFiles, outputFile);

            Assert.True(File.Exists(outputFile));

            using var mergedDoc = PdfReader.Open(outputFile, PdfDocumentOpenMode.ReadOnly);
            Assert.Equal(5, mergedDoc.PageCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static void CreateTestPdf(string filePath, string content)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
        var font = new PdfSharpCore.Drawing.XFont("Arial", 12);
        gfx.DrawString(content, font, PdfSharpCore.Drawing.XBrushes.Black,
            new PdfSharpCore.Drawing.XRect(50, 50, page.Width, page.Height),
            PdfSharpCore.Drawing.XStringFormats.TopLeft);
        gfx.Dispose();
        document.Save(filePath);
        document.Dispose();
    }
}

