using ClosedXML.Excel;
using dbm_select.Models;
using System;
using System.IO;

namespace dbm_select.Services
{
    /// <summary>
    /// Manages automatic, append-only daily Excel logging.
    /// On every form submission <see cref="AppendRow"/> is called; it either creates or
    /// appends to a date-stamped <c>data_YYYY-MM-DD.xlsx</c> file in the configured folder.
    /// </summary>
    /// <remarks>
    /// This service is intentionally decoupled from <see cref="DatabaseService"/>.
    /// A failure here must never affect the SQLite backup, and vice versa.
    /// No delete or overwrite functionality is exposed — rows are strictly append-only.
    /// </remarks>
    public class ExcelLogService
    {
        // ------------------------------------------------------------------ //
        //  Column headers, ordered to match the OrderLogItem field order.     //
        //  These are written once when a new daily file is first created.      //
        // ------------------------------------------------------------------ //
        private static readonly string[] Headers =
        {
            "STATUS",
            "Category",
            "Name",
            "Date Submitted",
            "Email",
            "School",
            "Course",
            "Package",
            "Large Print",
            "Barong/Filipiniana",
            "Creative",
            "Any Photo",
            "Solo/Group"
        };

        private string? _folderPath;

        // ------------------------------------------------------------------ //
        //  Initialisation                                                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Initialises the service by storing the target folder path.
        /// Creates the folder if it does not already exist.
        /// Must be called before <see cref="AppendRow"/>.
        /// </summary>
        /// <param name="folderPath">Absolute path to the folder where daily Excel files are written.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="folderPath"/> is null or whitespace.</exception>
        public void Init(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path must not be null or empty.", nameof(folderPath));

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            _folderPath = folderPath;
        }

        // ------------------------------------------------------------------ //
        //  Core Public API                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Appends a single data row to today's Excel log file.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item>If <c>data_YYYY-MM-DD.xlsx</c> does not yet exist for today, it is created with a
        ///   styled header row followed by the new data row.</item>
        ///   <item>If the file already exists, the new row is appended after the last used row —
        ///   no existing data is ever modified or removed.</item>
        ///   <item>Column widths are auto-fitted after every write for readability.</item>
        /// </list>
        /// </remarks>
        /// <param name="item">The order record to persist. Must not be <see langword="null"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Init"/> has not been called.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
        public void AppendRow(OrderLogItem item)
        {
            if (_folderPath is null)
                throw new InvalidOperationException(
                    "ExcelLogService has not been initialised. Call Init() before AppendRow().");

            if (item is null)
                throw new ArgumentNullException(nameof(item));

            string filePath = BuildTodayFilePath();
            bool fileExists = File.Exists(filePath);

            using var workbook = fileExists
                ? new XLWorkbook(filePath)
                : new XLWorkbook();

            IXLWorksheet ws;

            if (fileExists)
            {
                // File already has data — open existing sheet (always sheet 1).
                ws = workbook.Worksheet(1);
            }
            else
            {
                // New workbook - create sheet named "Client Logs YYYY-MM-DD"
                ws = workbook.AddWorksheet($"Client Logs {dbm_select.Utils.TimeProvider.Today:yyyy-MM-dd}");
                WriteHeaders(ws);
            }

            // Determine the next empty row index (header occupies row 1).
            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;
            if (nextRow < 2) nextRow = 2; // Defensive: always below the header.

            WriteDataRow(ws, nextRow, item);

            // Auto-fit all column widths so the sheet stays readable without manual resizing.
            ws.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
        }

        // ------------------------------------------------------------------ //
        //  Private Helpers                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>Returns the full path of today's Excel file: <c>&lt;folder&gt;/data_YYYY-MM-DD.xlsx</c>.</summary>
        private string BuildTodayFilePath()
            => Path.Combine(_folderPath!, $"ClientLogs_{dbm_select.Utils.TimeProvider.Today:yyyy-MM-dd}.xlsx");

        /// <summary>
        /// Writes the styled header row to row 1 of the worksheet.
        /// Called exactly once when a new daily file is first created.
        /// </summary>
        private static void WriteHeaders(IXLWorksheet ws)
        {
            for (int col = 0; col < Headers.Length; col++)
            {
                var cell = ws.Cell(1, col + 1);
                cell.Value = Headers[col];

                // Style: dark background, white bold text — consistent with app aesthetic.
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E1E2E");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        /// <summary>
        /// Writes a single <see cref="OrderLogItem"/> into the specified row of the worksheet.
        /// Column order must match <see cref="Headers"/>.
        /// </summary>
        /// <param name="ws">Target worksheet.</param>
        /// <param name="row">1-indexed row number to write into.</param>
        /// <param name="item">The order data to persist.</param>
        private static void WriteDataRow(IXLWorksheet ws, int row, OrderLogItem item)
        {
            ws.Cell(row, 1).Value  = item.Status        ?? "DONE CHOOSING";
            ws.Cell(row, 2).Value  = item.Category      ?? string.Empty;
            ws.Cell(row, 3).Value  = item.Name          ?? string.Empty;
            ws.Cell(row, 4).Value  = item.TimeStamp     ?? string.Empty;
            ws.Cell(row, 5).Value  = item.Email         ?? string.Empty;
            ws.Cell(row, 6).Value  = item.School        ?? string.Empty;
            ws.Cell(row, 7).Value  = item.Course        ?? string.Empty;
            ws.Cell(row, 8).Value  = item.Package       ?? string.Empty;
            ws.Cell(row, 9).Value  = item.Box_LargePrint ?? string.Empty;
            ws.Cell(row, 10).Value  = item.Box_Barong    ?? string.Empty;
            ws.Cell(row, 11).Value = item.Box_Creative  ?? string.Empty;
            ws.Cell(row, 12).Value = item.Box_Any       ?? string.Empty;
            ws.Cell(row, 13).Value = item.Box_SoloGroup ?? string.Empty;
        }
    }
}
