using Microsoft.EntityFrameworkCore.Migrations;

namespace FactoryService.Migrations
{
    public partial class ModifyModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommunicationError",
                table: "diagnoses",
                newName: "TartalyElectro");

            migrationBuilder.AddColumn<bool>(
                name: "KocsiElectro",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RFID",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RendszerElectro",
                table: "diagnoses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KocsiElectro",
                table: "diagnoses");

            migrationBuilder.DropColumn(
                name: "RFID",
                table: "diagnoses");

            migrationBuilder.DropColumn(
                name: "RendszerElectro",
                table: "diagnoses");

            migrationBuilder.RenameColumn(
                name: "TartalyElectro",
                table: "diagnoses",
                newName: "CommunicationError");
        }
    }
}
