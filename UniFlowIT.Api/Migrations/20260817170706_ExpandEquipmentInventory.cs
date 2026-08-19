using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEquipmentInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UsuarioAtual",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SistemaOperacional",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Processador",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "inventario_equipamentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Filial",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCompra",
                table: "inventario_equipamentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "inventario_equipamentos",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "inventario_equipamentos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Modelo",
                table: "inventario_equipamentos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotaFiscalNome",
                table: "inventario_equipamentos",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotaFiscalUrl",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NumeroNotaFiscal",
                table: "inventario_equipamentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Online",
                table: "inventario_equipamentos",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Responsavel",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ResponsavelUsuarioId",
                table: "inventario_equipamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "inventario_equipamentos",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unidade",
                table: "inventario_equipamentos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UnidadeEmpresaId",
                table: "inventario_equipamentos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventario_equipamentos_Patrimonio",
                table: "inventario_equipamentos",
                column: "Patrimonio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventario_equipamentos_Patrimonio",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "DataCompra",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Marca",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Modelo",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "NotaFiscalNome",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "NotaFiscalUrl",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "NumeroNotaFiscal",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Online",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Responsavel",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "ResponsavelUsuarioId",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "Unidade",
                table: "inventario_equipamentos");

            migrationBuilder.DropColumn(
                name: "UnidadeEmpresaId",
                table: "inventario_equipamentos");

            migrationBuilder.AlterColumn<string>(
                name: "UsuarioAtual",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "SistemaOperacional",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Processador",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "Filial",
                table: "inventario_equipamentos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);
        }
    }
}
