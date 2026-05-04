using Microsoft.EntityFrameworkCore.Migrations;

namespace FactoryService.Migrations
{
    public partial class ModifyModelAgain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KocsiElectroKomm",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "KommKozElectroKomm",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TartalyElectroKomm",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KocsiElectroKomm",
                table: "diagnoses");

            migrationBuilder.DropColumn(
                name: "KommKozElectroKomm",
                table: "diagnoses");

            migrationBuilder.DropColumn(
                name: "TartalyElectroKomm",
                table: "diagnoses");
        }
    }
}
