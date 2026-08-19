using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentAgentHardwareMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscoLivreGb",
                table: "inventario_equipamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Gpu",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MemoriaLivreGb",
                table: "inventario_equipamentos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscoLivreGb",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Gpu",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "MemoriaLivreGb",
                table: "inventario_equipamentos");
        }
    }
}
