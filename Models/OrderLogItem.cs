using MiniExcelLibs.Attributes;

namespace dbm_select.Models
{
    public class OrderLogItem
    {
        // 1. STATUS
        [ExcelColumn(Name = "STATUS", Width = 20, Index = 0)]
        public string Status { get; set; } = "DONE CHOOSING";


        // 3. Name
        [ExcelColumn(Name = "Name", Width = 30, Index = 2)]
        public string Name { get; set; } = "";

        // 4. Date Submitted
        [ExcelColumn(Name = "Date Submitted", Width = 25, Index = 3)]
        public string TimeStamp { get; set; } = "";

        // 5. Email
        [ExcelColumn(Name = "Email", Width = 30, Index = 4)]
        public string Email { get; set; } = "";

        // 6. School
        [ExcelColumn(Name = "School", Width = 30, Index = 5)]
        public string School { get; set; } = "";

        // 7. Course
        [ExcelColumn(Name = "Course", Width = 30, Index = 6)]
        public string Course { get; set; } = "";

        // 8. Package 
        [ExcelColumn(Name = "Package", Width = 15, Index = 7)]
        public string Package { get; set; } = "";

        // 9. Large Print 
        [ExcelColumn(Name = "Large Print", Width = 40, Index = 8)]
        public string Box_LargePrint { get; set; } = "";

        // 10. Barong/Filipiniana 
        [ExcelColumn(Name = "Barong/Filipiniana", Width = 40, Index = 9)]
        public string Box_Barong { get; set; } = "";

        // 11. Creative 
        [ExcelColumn(Name = "Creative", Width = 40, Index = 10)]
        public string Box_Creative { get; set; } = "";

        // 12. Any Photo 
        [ExcelColumn(Name = "Any Photo", Width = 40, Index = 11)]
        public string Box_Any { get; set; } = "";

        // 13. Solo/Group 
        [ExcelColumn(Name = "Solo/Group", Width = 40, Index = 12)]
        public string Box_SoloGroup { get; set; } = "";
    }
}