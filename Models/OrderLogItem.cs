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

        // 3. Date Submitted
        [ExcelColumn(Name = "Date Submitted", Width = 25, Index = 2)]
        public string TimeStamp { get; set; } = "";

        // 4. Email
        [ExcelColumn(Name = "Email", Width = 30, Index = 3)]
        public string Email { get; set; } = "";

        // --- NEW FIELDS ---
        // 5. School
        [ExcelColumn(Name = "School", Width = 30, Index = 4)]
        public string School { get; set; } = "";

        // 6. Course
        [ExcelColumn(Name = "Course", Width = 30, Index = 5)]
        public string Course { get; set; } = "";
        // ------------------

        // 7. Package (Index Shifted from 4 to 6)
        [ExcelColumn(Name = "Package", Width = 15, Index = 6)]
        public string Package { get; set; } = "";

        // 8. Large Print (Index Shifted from 5 to 7)
        [ExcelColumn(Name = "Large Print", Width = 40, Index = 7)]
        public string Box_LargePrint { get; set; } = "";

        // 9. Barong/Filipiniana (Index Shifted from 6 to 8)
        [ExcelColumn(Name = "Barong/Filipiniana", Width = 40, Index = 8)]
        public string Box_Barong { get; set; } = "";

        // 10. Creative (Index Shifted from 7 to 9)
        [ExcelColumn(Name = "Creative", Width = 40, Index = 9)]
        public string Box_Creative { get; set; } = "";

        // 11. Any Photo (Index Shifted from 8 to 10)
        [ExcelColumn(Name = "Any Photo", Width = 40, Index = 10)]
        public string Box_Any { get; set; } = "";

        // 12. Solo/Group (Index Shifted from 9 to 11)
        [ExcelColumn(Name = "Solo/Group", Width = 40, Index = 11)]
        public string Box_SoloGroup { get; set; } = "";

        // 13. Barkada (Index Shifted from 10 to 12)
        [ExcelColumn(Name = "Barkada", Width = 40, Index = 12)]
        public string Box_Barkada { get; set; } = "";
    }
}