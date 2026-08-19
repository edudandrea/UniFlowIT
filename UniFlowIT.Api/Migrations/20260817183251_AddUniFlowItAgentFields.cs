using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUniFlowItAgentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "inventario_equipamentos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "inventario_equipamentos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RustDeskId",
                table: "inventario_equipamentos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_equipamentos_AgentId",
                table: "inventario_equipamentos",
                column: "AgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventario_equipamentos_AgentId",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "RustDeskId",
                table: "inventario_equipamentos");
        }
    }
}
