using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace CarRentalSystem.Tests
{
    public class TrxToExcelConverter
    {
        public static void ConvertTrxToExcel(string trxFilePath, string excelFilePath)
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

            // Create Excel workbook
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Test Results");

                // Add headers
                worksheet.Cell(1, 1).Value = "Test Name";
                worksheet.Cell(1, 2).Value = "Outcome";
                worksheet.Cell(1, 3).Value = "Duration";
                worksheet.Cell(1, 4).Value = "Start Time";
                worksheet.Cell(1, 5).Value = "End Time";
                worksheet.Cell(1, 6).Value = "Error Message";
                worksheet.Cell(1, 7).Value = "Stack Trace";

                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Add data
                for (int i = 0; i < testResults.Count; i++)
                {
                    var result = testResults[i];
                    worksheet.Cell(i + 2, 1).Value = result.TestName;
                    worksheet.Cell(i + 2, 2).Value = result.Outcome;
                    worksheet.Cell(i + 2, 3).Value = result.Duration;
                    worksheet.Cell(i + 2, 4).Value = result.StartTime;
                    worksheet.Cell(i + 2, 5).Value = result.EndTime;
                    worksheet.Cell(i + 2, 6).Value = result.ErrorMessage;
                    worksheet.Cell(i + 2, 7).Value = result.StackTrace;
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Save the workbook
                workbook.SaveAs(excelFilePath);
            }

            Console.WriteLine($"Excel file generated: {excelFilePath}");
        }
    }
}