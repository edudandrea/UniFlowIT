using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureKnowledgeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE base_conhecimento SET \"Status\" = 'Publicado' WHERE \"Status\" IS NULL OR \"Status\" = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "base_conhecimento",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Publicado",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "base_conhecimento",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldDefaultValue: "Publicado");
        }
    }
}
