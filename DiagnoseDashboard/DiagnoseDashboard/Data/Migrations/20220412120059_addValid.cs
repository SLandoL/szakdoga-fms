using Microsoft.EntityFrameworkCore.Migrations;

namespace DiagnoseDashboard.Data.Migrations
{
    public partial class addValid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Valid",
                table: "faultDatas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Valid",
                table: "faultDatas");
        }
    }
}
