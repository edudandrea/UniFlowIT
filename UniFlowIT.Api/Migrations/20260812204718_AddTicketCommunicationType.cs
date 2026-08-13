using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCommunicationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "comunicacoes_chamado",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Mural");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "comunicacoes_chamado");
        }
    }
}
