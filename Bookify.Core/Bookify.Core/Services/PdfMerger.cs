using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Bookify.Core.Services;

public sealed class PdfMerger
{
    public void MergeFiles(IEnumerable<string> inputFiles, string outputFile)
    {
        using var outputDocument = new PdfDocument();

        foreach (var inputFile in inputFiles)
        {
            if (!File.Exists(inputFile))
            {
                continue;
            }

            using var inputDocument = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
            var pageCount = inputDocument.PageCount;

            for (var i = 0; i < pageCount; i++)
            {
                var page = inputDocument.Pages[i];
                outputDocument.AddPage(page);
            }
        }

        outputDocument.Save(outputFile);
    }
}

