using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using QuestPDF.Fluent;
using System.IO.Compression;
using System.Text.Json;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;
namespace TradeSystem.API.Controllers.TradeDataController
{
    [Route("api/[controller]")]
    [ApiController]
    public class PDFGenerationController : ControllerBase
    {
        #region Private Files Area
        private readonly IConfiguration _configuration;
        private static string MongoServer = "";
        private static string DBName = "";
        private static string MainDataCol = "";
        private readonly IWebHostEnvironment _environment;
        #endregion

        #region Constructor Area
        public PDFGenerationController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _environment = environment;
            _configuration = configuration;
            MongoServer = _configuration.GetSection("PDFGenerationConfig").GetSection("MongoServer").Value;
            MainDataCol = _configuration.GetSection("PDFGenerationConfig").GetSection("MainDataColl").Value;
            DBName = _configuration.GetSection("PDFGenerationConfig").GetSection("DBName").Value;
        }
        #endregion

        [HttpGet("GeneratePDFZip")]
        public async Task<IActionResult> GeneratePDFZip()
        {
            // Initialize MongoDB connection
            var client = new MongoClient(MongoServer);
            var database = client.GetDatabase(DBName);
            var collection = database.GetCollection<BsonDocument>(MainDataCol);

            QuestPDF.Settings.License = LicenseType.Community;

            Console.WriteLine("Retrieving documents from the collection...");
            var documents = await collection.Find(new BsonDocument()).ToListAsync();

            // Define path in wwwroot to store generated PDFs
            var pdfDirectory = Path.Combine(_environment.WebRootPath, "generated_pdfs", Guid.NewGuid().ToString());
            Directory.CreateDirectory(pdfDirectory); // Ensure the directory exists

            // Generate individual PDFs for each document
            foreach (var document in documents)
            {
                string masterBOLNumber = document.Contains("Master_BOL_Number") ? document["Master_BOL_Number"].ToString() : "Unknown";
                string houseBOLNumber = document.Contains("House_BOL_Number") ? document["House_BOL_Number"].ToString() : "Unknown";
                string pdfFilePath = Path.Combine(pdfDirectory, $"{masterBOLNumber}_{houseBOLNumber}.pdf");

                Console.WriteLine($"Generating PDF for: {masterBOLNumber} - {houseBOLNumber}");

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);

                        page.Footer().Column(column =>
                        {
                            column.Item().Row(row => row.RelativeColumn().Height(1).Background(Colors.Grey.Darken1));
                            column.Item().AlignCenter().Text(text =>
                            {
                                text.Span("Page ").FontSize(10);
                                text.CurrentPageNumber().FontSize(10);
                                text.Span(" of ").FontSize(10);
                                text.TotalPages().FontSize(10);
                            });
                        });

                        page.Content().Column(column =>
                        {
                            column.Item().Text($"{masterBOLNumber} - {houseBOLNumber}")
                                .FontSize(18).Bold().Underline();

                            column.Item().PaddingVertical(10).Row(row =>
                            {
                                row.RelativeColumn().Height(1).Background(Colors.Grey.Darken1);
                            });

                            AddFieldIfExists(column, document, "Source_Name", "Source Name");
                            AddFieldIfExists(column, document, "Trade_Update_Date", "Trade Update Date");
                            AddFieldIfExists(column, document, "Run_Date", "Run Date");
                            AddFieldIfExists(column, document, "OriginalSupplier", "Original Supplier");
                            AddFieldIfExists(column, document, "OriginalCustomer", "Original Customer");
                            AddFieldIfExists(column, document, "ProductName", "Product Name");

                            column.Item().PaddingVertical(10).Row(row =>
                            {
                                row.RelativeColumn().Height(1).Background(Colors.Grey.Darken1);
                            });

                            if (document.Contains("features_arr") && document["features_arr"].IsBsonArray)
                            {
                                var featuresArray = document["features_arr"].AsBsonArray;

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                    });

                                    table.Cell().Text("Feature Name").Bold();
                                    table.Cell().Text("Feature Value").Bold();

                                    foreach (var feature in featuresArray)
                                    {
                                        var featureName = feature["n"].AsString;
                                        var featureValue = feature["v"].AsString;
                                        table.Cell().Text(featureName);
                                        table.Cell().Text(featureValue);
                                    }
                                });
                            }
                        });
                    });
                }).GeneratePdf(pdfFilePath);
            }

            // Define ZIP file path in the generated PDFs directory
            var zipFilePath = Path.Combine(pdfDirectory, "AllDocuments.zip");

            // Use `using` to ensure the ZIP creation is completed before accessing it
            using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (var pdfFile in Directory.GetFiles(pdfDirectory, "*.pdf"))
                {
                    zipArchive.CreateEntryFromFile(pdfFile, Path.GetFileName(pdfFile));
                }
            }

            // Return ZIP file as a download after ensuring it is fully released
            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
            Directory.Delete(pdfDirectory, true); // Clean up temporary files
            return File(zipBytes, "application/zip", "AllDocuments.zip");
        }

        // Helper method to add fields if they exist
        private void AddFieldIfExists(ColumnDescriptor column, BsonDocument document, string fieldName, string displayName)
        {
            if (document.TryGetValue(fieldName, out var value))
            {
                column.Item().Text($"{displayName}: {value}").FontSize(12);
            }
        }
    }
}
