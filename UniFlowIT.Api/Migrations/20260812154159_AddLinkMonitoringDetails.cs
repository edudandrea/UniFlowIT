using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkMonitoringDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "links_monitorados",
                type: "numeric",
                nullable: false,
                defaultValue: -23.561m);

            migrationBuilder.AddColumn<string>(
                name: "Local",
                table: "links_monitorados",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Matriz");

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "links_monitorados",
                type: "numeric",
                nullable: false,
                defaultValue: -46.656m);

            migrationBuilder.AddColumn<int>(
                name: "PingMs",
                table: "links_monitorados",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "links_monitorados");

            migrationBuilder.DropColumn(
                name: "Local",
                table: "links_monitorados");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "links_monitorados");

            migrationBuilder.DropColumn(
                name: "PingMs",
                table: "links_monitorados");
        }
    }
}
