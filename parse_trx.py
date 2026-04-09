import xml.etree.ElementTree as ET
from openpyxl import Workbook
import os

# Parse the TRX file
tree = ET.parse('e:\\CarRentalSystem\\CarRentalSystem\\CarRentalSystem\\CarRentalSystem.Tests\\TestResults\\test_results.trx')
root = tree.getroot()

# Namespace for TRX
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}

# Create Excel workbook
wb = Workbook()
ws = wb.active
ws.title = "Test Results"

# Headers
ws['A1'] = 'Test Name'
ws['B1'] = 'Outcome'
ws['C1'] = 'Duration'
ws['D1'] = 'Start Time'
ws['E1'] = 'End Time'

# Extract results
results = root.findall('.//t:UnitTestResult', ns)
row = 2
for result in results:
    test_name = result.get('testName')
    outcome = result.get('outcome')
    duration = result.get('duration')
    start_time = result.get('startTime')
    end_time = result.get('endTime')
    
    ws[f'A{row}'] = test_name
    ws[f'B{row}'] = outcome
    ws[f'C{row}'] = duration
    ws[f'D{row}'] = start_time
    ws[f'E{row}'] = end_time
    row += 1

# Save the Excel file
wb.save('e:\\CarRentalSystem\\CarRentalSystem\\CarRentalSystem\\test_results.xlsx')
print("Excel file created successfully.")