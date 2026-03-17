using System.IO;
using PdfSharpCore.Pdf;
using VetCV.HtmlRendererCore.PdfSharpCore;

namespace FinanceHubFunctions.Services
{
    public static class HtmlPdfService
    {
        public static byte[] ConvertHtmlToPdf(string html)
        {
            var document = PdfGenerator.GeneratePdf(html, PdfSharpCore.PageSize.A4);
            using var stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }
    }
}
