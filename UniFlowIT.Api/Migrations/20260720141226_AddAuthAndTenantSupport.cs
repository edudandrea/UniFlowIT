using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndTenantSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantSlug",
                table: "empresas",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE empresas
                SET "TenantSlug" = CONCAT('empresa-', "Id")
                WHERE "TenantSlug" = ''
                """);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_empresas_TenantSlug",
                table: "empresas",
                column: "TenantSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_Email",
                table: "usuarios");

            migrationBuilder.DropIndex(
                name: "IX_empresas_TenantSlug",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "TenantSlug",
                table: "empresas");
        }
    }
}
