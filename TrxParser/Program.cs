using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace TrxParser
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: TrxParser <trxFilePath> <excelFilePath>");
                return;
            }

            string trxFilePath = args[0];
            string excelFilePath = args[1];

            try
            {
                ConvertTrxToExcel(trxFilePath, excelFilePath);
                Console.WriteLine("Conversion completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void ConvertTrxToExcel(string trxFilePath, string excelFilePath)
        {
            if (!File.Exists(trxFilePath))
            {
                throw new FileNotFoundException("TRX file not found.", trxFilePath);
            }

            // Load the TRX file as XML
            XDocument trxDoc = XDocument.Load(trxFilePath);

            // Extract test definitions
            var testDefinitions = trxDoc.Descendants("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTest")
                .ToDictionary(
                    ut => (string)ut.Attribute("id"),
                    ut => (string)ut.Attribute("name")
                );

            // Extract test results
            var testResults = trxDoc.Descendants("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult")
                .Select(result => new
                {
                    TestId = (string)result.Attribute("testId"),
                    TestName = testDefinitions.ContainsKey((string)result.Attribute("testId"))
                        ? testDefinitions[(string)result.Attribute("testId")]
                        : "Unknown",
                    Outcome = (string)result.Attribute("outcome"),
                    Duration = (string)result.Attribute("duration"),
                    StartTime = (string)result.Attribute("startTime"),
                    EndTime = (string)result.Attribute("endTime"),
                    ErrorMessage = result.Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Output")?
                        .Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}ErrorInfo")?
                        .Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Message")?.Value ?? "",
                    StackTrace = result.Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Output")?
                        .Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}ErrorInfo")?
                        .Element("{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}StackTrace")?.Value ?? ""
                })
                .ToList();

            // If template exists, use it; else create new
            XLWorkbook workbook;
            IXLWorksheet worksheet;
            if (File.Exists("Template_TestCase.xlsx"))
            {
                workbook = new XLWorkbook("Template_TestCase.xlsx");
                worksheet = workbook.Worksheet(1) ?? workbook.Worksheets.Add("Test Results");
            }
            else
            {
                workbook = new XLWorkbook();
                worksheet = workbook.Worksheets.Add("Test Results");

                // Add headers like template
                worksheet.Cell(1, 1).Value = "Test Case ID";
                worksheet.Cell(1, 2).Value = "Test Case Name";
                worksheet.Cell(1, 3).Value = "Description";
                worksheet.Cell(1, 4).Value = "Preconditions";
                worksheet.Cell(1, 5).Value = "Test Steps";
                worksheet.Cell(1, 6).Value = "Expected Result";
                worksheet.Cell(1, 7).Value = "Actual Result";
                worksheet.Cell(1, 8).Value = "Status";
                worksheet.Cell(1, 9).Value = "Comments";

                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            // Find the first empty row
            int row = 2;
            while (!worksheet.Cell(row, 1).IsEmpty())
            {
                row++;
            }

            // Add data
            int testIdCounter = 1;
            foreach (var result in testResults)
            {
                worksheet.Cell(row, 1).Value = $"TC-{testIdCounter:D3}";
                worksheet.Cell(row, 2).Value = result.TestName;
                worksheet.Cell(row, 3).Value = result.TestName; // Description as test name
                worksheet.Cell(row, 4).Value = "Hệ thống đang chạy"; // Preconditions
                worksheet.Cell(row, 5).Value = "Chạy test tự động"; // Test Steps
                worksheet.Cell(row, 6).Value = "Đạt"; // Expected Result
                worksheet.Cell(row, 7).Value = result.Outcome == "Passed" ? "Đạt" : "Không đạt"; // Actual Result
                worksheet.Cell(row, 8).Value = result.Outcome == "Passed" ? "Đạt" : "Không đạt"; // Status
                worksheet.Cell(row, 9).Value = $"Thời gian: {result.Duration}"; // Comments
                row++;
                testIdCounter++;
            }

            // Auto-fit columns
            worksheet.Columns(1,9).AdjustToContents();

            // Save the workbook
            workbook.SaveAs(excelFilePath);
        }
    }
}