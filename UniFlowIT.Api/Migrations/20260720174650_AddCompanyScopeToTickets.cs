using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyScopeToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "chamados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SolicitanteUsuarioId",
                table: "chamados",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE chamados
                SET "EmpresaId" = (SELECT "Id" FROM empresas ORDER BY "Id" LIMIT 1)
                WHERE "EmpresaId" IS NULL
                  AND EXISTS (SELECT 1 FROM empresas);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_chamados_EmpresaId",
                table: "chamados",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_chamados_empresas_EmpresaId",
                table: "chamados",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chamados_empresas_EmpresaId",
                table: "chamados");

            migrationBuilder.DropIndex(
                name: "IX_chamados_EmpresaId",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "chamados");

            migrationBuilder.DropColumn(
                name: "SolicitanteUsuarioId",
                table: "chamados");
        }
    }
}
