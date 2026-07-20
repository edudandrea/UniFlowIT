using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyScopeToOperationalModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "links_monitorados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "inventario_equipamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "controle_equipamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmpresaId",
                table: "base_conhecimento",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE links_monitorados
                SET "EmpresaId" = (SELECT "Id" FROM empresas ORDER BY "Id" LIMIT 1)
                WHERE "EmpresaId" IS NULL
                  AND EXISTS (SELECT 1 FROM empresas);

                UPDATE inventario_equipamentos
                SET "EmpresaId" = (SELECT "Id" FROM empresas ORDER BY "Id" LIMIT 1)
                WHERE "EmpresaId" IS NULL
                  AND EXISTS (SELECT 1 FROM empresas);

                UPDATE controle_equipamentos
                SET "EmpresaId" = (SELECT "Id" FROM empresas ORDER BY "Id" LIMIT 1)
                WHERE "EmpresaId" IS NULL
                  AND EXISTS (SELECT 1 FROM empresas);

                UPDATE base_conhecimento
                SET "EmpresaId" = (SELECT "Id" FROM empresas ORDER BY "Id" LIMIT 1)
                WHERE "EmpresaId" IS NULL
                  AND EXISTS (SELECT 1 FROM empresas);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_links_monitorados_EmpresaId",
                table: "links_monitorados",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_equipamentos_EmpresaId",
                table: "inventario_equipamentos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_controle_equipamentos_EmpresaId",
                table: "controle_equipamentos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_base_conhecimento_EmpresaId",
                table: "base_conhecimento",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_base_conhecimento_empresas_EmpresaId",
                table: "base_conhecimento",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_controle_equipamentos_empresas_EmpresaId",
                table: "controle_equipamentos",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inventario_equipamentos_empresas_EmpresaId",
                table: "inventario_equipamentos",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_links_monitorados_empresas_EmpresaId",
                table: "links_monitorados",
                column: "EmpresaId",
                principalTable: "empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_base_conhecimento_empresas_EmpresaId",
                table: "base_conhecimento");

            migrationBuilder.DropForeignKey(
                name: "FK_controle_equipamentos_empresas_EmpresaId",
                table: "controle_equipamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_inventario_equipamentos_empresas_EmpresaId",
                table: "inventario_equipamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_links_monitorados_empresas_EmpresaId",
                table: "links_monitorados");

            migrationBuilder.DropIndex(
                name: "IX_links_monitorados_EmpresaId",
                table: "links_monitorados");

            migrationBuilder.DropIndex(
                name: "IX_inventario_equipamentos_EmpresaId",
                table: "inventario_equipamentos");

            migrationBuilder.DropIndex(
                name: "IX_controle_equipamentos_EmpresaId",
                table: "controle_equipamentos");

            migrationBuilder.DropIndex(
                name: "IX_base_conhecimento_EmpresaId",
                table: "base_conhecimento");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "links_monitorados");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "controle_equipamentos");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "base_conhecimento");
        }
    }
}
