using Microsoft.EntityFrameworkCore.Migrations;

namespace DiagnoseDashboard.Data.Migrations
{
    public partial class parentIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "faultDatas");

            migrationBuilder.AddColumn<string>(
                name: "ParentIds",
                table: "faultDatas",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentIds",
                table: "faultDatas");

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "faultDatas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
