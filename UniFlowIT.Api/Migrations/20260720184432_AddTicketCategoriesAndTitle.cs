using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCategoriesAndTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "chamados",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "categorias_chamado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subcategorias = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrioridadePadrao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias_chamado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categorias_chamado_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_chamado_EmpresaId",
                table: "categorias_chamado",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_categorias_chamado_EmpresaId_Nome",
                table: "categorias_chamado",
                columns: new[] { "EmpresaId", "Nome" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "categorias_chamado");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "chamados");
        }
    }
}
