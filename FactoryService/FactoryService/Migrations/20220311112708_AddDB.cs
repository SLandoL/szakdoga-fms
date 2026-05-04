using Microsoft.EntityFrameworkCore.Migrations;

namespace FactoryService.Migrations
{
    public partial class AddDB : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagnoses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarSpeed = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    System = table.Column<bool>(type: "bit", nullable: false),
                    Pause = table.Column<bool>(type: "bit", nullable: false),
                    WakeUp = table.Column<bool>(type: "bit", nullable: false),
                    PLCFailure = table.Column<bool>(type: "bit", nullable: false),
                    CommunicationError = table.Column<bool>(type: "bit", nullable: false),
                    CommunicationPowerError = table.Column<bool>(type: "bit", nullable: false),
                    CarError = table.Column<bool>(type: "bit", nullable: false),
                    ContainerEmpty = table.Column<bool>(type: "bit", nullable: false),
                    LED = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnoses", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnoses");
        }
    }
}
