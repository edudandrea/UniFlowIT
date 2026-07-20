using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyLegalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeFantasia",
                table: "empresas",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RazaoSocial",
                table: "empresas",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE empresas
                SET "RazaoSocial" = "Nome",
                    "NomeFantasia" = "Nome"
                WHERE "RazaoSocial" = ''
                   OR "NomeFantasia" = ''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeFantasia",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "RazaoSocial",
                table: "empresas");
        }
    }
}
