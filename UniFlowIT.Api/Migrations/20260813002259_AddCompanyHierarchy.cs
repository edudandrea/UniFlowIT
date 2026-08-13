using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaContratanteId",
                table: "empresas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoUnidade",
                table: "empresas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Contratante");

            migrationBuilder.CreateIndex(
                name: "IX_empresas_EmpresaContratanteId",
                table: "empresas",
                column: "EmpresaContratanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_empresas_EmpresaContratanteId",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "EmpresaContratanteId",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "TipoUnidade",
                table: "empresas");
        }
    }
}
