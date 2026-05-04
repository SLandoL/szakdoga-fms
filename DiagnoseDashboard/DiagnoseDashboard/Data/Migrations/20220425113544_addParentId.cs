using Microsoft.EntityFrameworkCore.Migrations;

namespace DiagnoseDashboard.Data.Migrations
{
    public partial class addParentId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "faultDatas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "faultDatas");
        }
    }
}
