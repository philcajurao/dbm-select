using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using dbm_select.Models;

namespace dbm_select.Services
{
    public class DatabaseService
    {
        private string? _dbPath;

        public void Init(string dbFolderPath)
        {
            if (!Directory.Exists(dbFolderPath))
            {
                Directory.CreateDirectory(dbFolderPath);
            }
            _dbPath = Path.Combine(dbFolderPath, "ClientLogs.db");
            EnsureCreated();
        }

        private string GetConnectionString()
        {
            if (string.IsNullOrEmpty(_dbPath))
            {
                throw new InvalidOperationException("Database Service has not been initialized with a folder path.");
            }
            return new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        private void EnsureCreated()
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            // Enable WAL mode for better concurrency (multiple readers, one writer, no locks)
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS OrderLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Status TEXT,
                        Category TEXT,
                        Name TEXT,
                        TimeStamp TEXT,
                        Email TEXT,
                        School TEXT,
                        Course TEXT,
                        Package TEXT,
                        Box_LargePrint TEXT,
                        Box_Barong TEXT,
                        Box_Creative TEXT,
                        Box_Any TEXT,
                        Box_SoloGroup TEXT,
                        ContactNumber TEXT
                    );";
                command.ExecuteNonQuery();
            }

            // Check if column exists, and alter table if it doesn't to migrate existing databases
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(OrderLogs);";
                var columns = new List<string>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(reader.GetString(1)); // Column name is the second field (index 1)
                    }
                }

                if (!columns.Contains("Category"))
                {
                    using (var alterCommand = connection.CreateCommand())
                    {
                        alterCommand.CommandText = "ALTER TABLE OrderLogs ADD COLUMN Category TEXT;";
                        alterCommand.ExecuteNonQuery();
                    }
                }

                if (!columns.Contains("ContactNumber"))
                {
                    using var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE OrderLogs ADD COLUMN ContactNumber TEXT;";
                    alterCommand.ExecuteNonQuery();
                }
            }

            // Backfill Category for existing records where it is empty or null
            using (var updateCommand = connection.CreateCommand())
            {
                updateCommand.CommandText = @"
                    UPDATE OrderLogs 
                    SET    Category = 'Senior High' 
                    WHERE  (Category IS NULL OR Category = '') AND School = 'Senior High School';

                    UPDATE OrderLogs 
                    SET    Category = 'N/A (Others)' 
                    WHERE  (Category IS NULL OR Category = '') AND School = 'N/A' AND Course = 'N/A';

                    UPDATE OrderLogs 
                    SET    Category = 'College' 
                    WHERE  (Category IS NULL OR Category = '');";
                updateCommand.ExecuteNonQuery();
            }
        }

        public void InsertOrder(OrderLogItem item)
        {
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO OrderLogs (
                    Status, Category, Name, TimeStamp, Email, School, Course, Package, 
                    Box_LargePrint, Box_Barong, Box_Creative, Box_Any, Box_SoloGroup, ContactNumber
                ) VALUES (
                    $Status, $Category, $Name, $TimeStamp, $Email, $School, $Course, $Package, 
                    $Box_LargePrint, $Box_Barong, $Box_Creative, $Box_Any, $Box_SoloGroup, $ContactNumber
                );";

            command.Parameters.AddWithValue("$Status", item.Status ?? "DONE CHOOSING");
            command.Parameters.AddWithValue("$Category", item.Category ?? "College");
            command.Parameters.AddWithValue("$Name", item.Name ?? string.Empty);
            command.Parameters.AddWithValue("$TimeStamp", item.TimeStamp ?? dbm_select.Utils.TimeProvider.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("$Email", item.Email ?? string.Empty);
            command.Parameters.AddWithValue("$School", item.School ?? string.Empty);
            command.Parameters.AddWithValue("$Course", item.Course ?? string.Empty);
            command.Parameters.AddWithValue("$Package", item.Package ?? string.Empty);
            command.Parameters.AddWithValue("$Box_LargePrint", item.Box_LargePrint ?? "Empty");
            command.Parameters.AddWithValue("$Box_Barong", item.Box_Barong ?? "N/A");
            command.Parameters.AddWithValue("$Box_Creative", item.Box_Creative ?? "N/A");
            command.Parameters.AddWithValue("$Box_Any", item.Box_Any ?? "N/A");
            command.Parameters.AddWithValue("$Box_SoloGroup", item.Box_SoloGroup ?? "N/A");
            command.Parameters.AddWithValue("$ContactNumber", item.ContactNumber ?? string.Empty);

            command.ExecuteNonQuery();
        }

        public List<OrderLogItem> GetAllOrders()
        {
            var list = new List<OrderLogItem>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Status, Category, Name, TimeStamp, Email, School, Course, Package, Box_LargePrint, Box_Barong, Box_Creative, Box_Any, Box_SoloGroup, ContactNumber FROM OrderLogs ORDER BY TimeStamp DESC;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadRow(reader));
            }

            return list;
        }

        public List<OrderLogItem> GetOrdersByRange(DateTime fromDate, DateTime toDate)
        {
            var list = new List<OrderLogItem>();
            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Status, Category, Name, TimeStamp, Email, School, Course, Package, Box_LargePrint, Box_Barong, Box_Creative, Box_Any, Box_SoloGroup, ContactNumber 
                FROM OrderLogs 
                WHERE TimeStamp BETWEEN $FromDate AND $ToDate 
                ORDER BY TimeStamp DESC;";

            command.Parameters.AddWithValue("$FromDate", fromDate.ToString("yyyy-MM-dd 00:00:00"));
            command.Parameters.AddWithValue("$ToDate", toDate.ToString("yyyy-MM-dd 23:59:59"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadRow(reader));
            }

            return list;
        }

        private OrderLogItem ReadRow(SqliteDataReader reader)
        {
            return new OrderLogItem
            {
                Status = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Category = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                TimeStamp = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                School = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Course = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Package = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Box_LargePrint = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Box_Barong = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                Box_Creative = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Box_Any = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                Box_SoloGroup = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                ContactNumber = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
            };
        }

        // ------------------------------------------------------------------ //
        // NEW — used by the export dialog to validate the selected date range //
        // All existing methods above are completely unchanged.                 //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the number of <c>OrderLogs</c> records whose <c>TimeStamp</c> falls within
        /// the specified date range (both bounds inclusive, full calendar days).
        /// </summary>
        /// <remarks>
        /// This method is intentionally lightweight — it issues a single <c>COUNT(*)</c> query
        /// and is called on every date-picker change in the export dialog to provide live feedback.
        /// </remarks>
        /// <param name="fromDate">Inclusive start date. Time component is ignored; range starts at 00:00:00.</param>
        /// <param name="toDate">Inclusive end date. Time component is ignored; range ends at 23:59:59.</param>
        /// <returns>
        /// The number of matching records, or <c>0</c> when no records exist for the range
        /// or when the service has not yet been initialised.
        /// </returns>
        public int GetOrderCountByRange(DateTime fromDate, DateTime toDate)
        {
            // Guard: if not initialised, report zero rather than throwing in a UI callback.
            if (string.IsNullOrEmpty(_dbPath))
                return 0;

            using var connection = new SqliteConnection(GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*)
                FROM   OrderLogs
                WHERE  TimeStamp BETWEEN $FromDate AND $ToDate;";

            command.Parameters.AddWithValue("$FromDate", fromDate.ToString("yyyy-MM-dd 00:00:00"));
            command.Parameters.AddWithValue("$ToDate",   toDate.ToString("yyyy-MM-dd 23:59:59"));

            var result = command.ExecuteScalar();

            // ExecuteScalar returns long for SQLite COUNT — cast safely.
            return result is long count ? (int)count : 0;
        }
    }
}
