using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeCategoriesAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Anexos",
                table: "base_conhecimento",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioCriador",
                table: "base_conhecimento",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioCriadorId",
                table: "base_conhecimento",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categorias_conhecimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias_conhecimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categorias_conhecimento_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_conhecimento_EmpresaId",
                table: "categorias_conhecimento",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_conhecimento_EmpresaId_Nome",
                table: "categorias_conhecimento",
                columns: new[] { "EmpresaId", "Nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias_conhecimento");

            migrationBuilder.DropColumn(
                name: "Anexos",
                table: "base_conhecimento");

            migrationBuilder.DropColumn(
                name: "UsuarioCriador",
                table: "base_conhecimento");

            migrationBuilder.DropColumn(
                name: "UsuarioCriadorId",
                table: "base_conhecimento");
        }
    }
}
