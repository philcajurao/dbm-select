using MiniExcelLibs.Attributes; // ✅ Required for custom headers

namespace dbm_select.Models
{
    public class OrderLogItem
    {
        // 1. STATUS (Default: DONE CHOOSING)
        [ExcelColumn(Name = "STATUS", Width = 20, Index = 0)]
        public string Status { get; set; } = "DONE CHOOSING";

        // 2. Name
        [ExcelColumn(Name = "Name", Width = 30, Index = 1)]
        public string Name { get; set; } = ""; // Renamed from Client for clarity

        // 3. Email
        [ExcelColumn(Name = "Email", Width = 30, Index = 2)]
        public string Email { get; set; } = "";

        // 4. Package
        [ExcelColumn(Name = "Package", Width = 15, Index = 3)]
        public string Package { get; set; } = "";

        // 5. 8x10
        [ExcelColumn(Name = "8x10", Width = 40, Index = 4)]
        public string Box_8x10 { get; set; } = "";

        // 6. Barong/Filipiniana
        [ExcelColumn(Name = "Barong/Filipiniana", Width = 40, Index = 5)]
        public string Box_Barong { get; set; } = "";

        // 7. Creative
        [ExcelColumn(Name = "Creative", Width = 40, Index = 6)]
        public string Box_Creative { get; set; } = "";

        // 8. Any Photo
        [ExcelColumn(Name = "Any Photo", Width = 40, Index = 7)]
        public string Box_Any { get; set; } = "";

        // 9. instax
        [ExcelColumn(Name = "instax", Width = 40, Index = 8)]
        public string Box_Instax { get; set; } = "";

        // 10. TimeStamp
        [ExcelColumn(Name = "TimeStamp", Width = 25, Index = 9)]
        public string TimeStamp { get; set; } = ""; // Renamed from Date
    }
}