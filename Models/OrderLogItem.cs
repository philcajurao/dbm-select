using MiniExcelLibs.Attributes;

namespace dbm_select.Models
{
    public class OrderLogItem
    {
        // 1. STATUS
        [ExcelColumn(Name = "STATUS", Width = 20, Index = 0)]
        public string Status { get; set; } = "DONE CHOOSING";

        // 2. Name
        [ExcelColumn(Name = "Name", Width = 30, Index = 1)]
        public string Name { get; set; } = "";

        // 3. Date Submitted (Moved to 3rd Column)
        [ExcelColumn(Name = "Date Submitted", Width = 25, Index = 2)]
        public string TimeStamp { get; set; } = "";

        // 4. Email
        [ExcelColumn(Name = "Email", Width = 30, Index = 3)]
        public string Email { get; set; } = "";

        // 5. Package
        [ExcelColumn(Name = "Package", Width = 15, Index = 4)]
        public string Package { get; set; } = "";

        // 6. Large Print
        [ExcelColumn(Name = "Large Print", Width = 40, Index = 5)]
        public string Box_LargePrint { get; set; } = "";

        // 7. Barong/Filipiniana
        [ExcelColumn(Name = "Barong/Filipiniana", Width = 40, Index = 6)]
        public string Box_Barong { get; set; } = "";

        // 8. Creative
        [ExcelColumn(Name = "Creative", Width = 40, Index = 7)]
        public string Box_Creative { get; set; } = "";

        // 9. Any Photo
        [ExcelColumn(Name = "Any Photo", Width = 40, Index = 8)]
        public string Box_Any { get; set; } = "";

        // 10. Solo/Group
        [ExcelColumn(Name = "Solo/Group", Width = 40, Index = 9)]
        public string Box_SoloGroup { get; set; } = "";

        // 11. Barkada
        [ExcelColumn(Name = "Barkada", Width = 40, Index = 10)]
        public string Box_Barkada { get; set; } = "";
    }
}