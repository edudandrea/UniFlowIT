using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UniFlowIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "base_conhecimento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Publicado = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_conhecimento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chamados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Solicitante = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Subcategoria = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Prioridade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    EquipamentoRelacionado_Hostname = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EquipamentoRelacionado_SistemaOperacional = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EquipamentoRelacionado_UsuarioLogado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EquipamentoRelacionado_Ip = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EquipamentoRelacionado_Navegador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AtendenteId = table.Column<int>(type: "integer", nullable: true),
                    AtendenteNome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AvaliacaoNota = table.Column<int>(type: "integer", nullable: true),
                    AvaliacaoComentario = table.Column<string>(type: "text", nullable: true),
                    OrigemAutomacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EncerradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chamados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "controle_equipamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Patrimonio = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Fabricante = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: false),
                    Serial = table.Column<string>(type: "text", nullable: false),
                    FilialOrigem = table.Column<string>(type: "text", nullable: false),
                    FilialDestino = table.Column<string>(type: "text", nullable: false),
                    ResponsavelEnvio = table.Column<string>(type: "text", nullable: false),
                    ResponsavelRecebimento = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecebidoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_controle_equipamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Endereco = table.Column<string>(type: "text", nullable: false),
                    Telefone = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Complemento = table.Column<string>(type: "text", nullable: true),
                    Bairro = table.Column<string>(type: "text", nullable: true),
                    Cidade = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: true),
                    Cep = table.Column<string>(type: "text", nullable: true),
                    InscricaoMunicipal = table.Column<string>(type: "text", nullable: true),
                    InscricaoEstadual = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    AcessoBloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    MotivoBloqueio = table.Column<string>(type: "text", nullable: true),
                    BloqueadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventario_equipamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Patrimonio = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Hostname = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsuarioAtual = table.Column<string>(type: "text", nullable: false),
                    Filial = table.Column<string>(type: "text", nullable: false),
                    SistemaOperacional = table.Column<string>(type: "text", nullable: false),
                    Processador = table.Column<string>(type: "text", nullable: false),
                    MemoriaGb = table.Column<int>(type: "integer", nullable: false),
                    DiscoGb = table.Column<int>(type: "integer", nullable: false),
                    Ip = table.Column<string>(type: "text", nullable: false),
                    UltimaLeituraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventario_equipamentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "links_monitorados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Firewall = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IntervaloLeituraSegundos = table.Column<int>(type: "integer", nullable: false),
                    Disponivel = table.Column<bool>(type: "boolean", nullable: false),
                    ChamadoAbertoId = table.Column<int>(type: "integer", nullable: true),
                    UltimaLeituraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_links_monitorados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "perfis_usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfis_usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "anexos_chamado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomeArquivo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    TipoConteudo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChamadoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anexos_chamado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_anexos_chamado_chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comunicacoes_chamado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChamadoId = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<int>(type: "integer", nullable: false),
                    AutorNome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AutorPerfil = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Mensagem = table.Column<string>(type: "text", nullable: false),
                    EnviadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Lida = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicacoes_chamado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comunicacoes_chamado_chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Login = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SenhaHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "text", nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshTokenRevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TemporaryPasswordExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usuarios_empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anexos_chamado_ChamadoId",
                table: "anexos_chamado",
                column: "ChamadoId");

            migrationBuilder.CreateIndex(
                name: "IX_chamados_Numero",
                table: "chamados",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comunicacoes_chamado_ChamadoId",
                table: "comunicacoes_chamado",
                column: "ChamadoId");

            migrationBuilder.CreateIndex(
                name: "IX_controle_equipamentos_Patrimonio",
                table: "controle_equipamentos",
                column: "Patrimonio");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_equipamentos_Hostname",
                table: "inventario_equipamentos",
                column: "Hostname");

            migrationBuilder.CreateIndex(
                name: "IX_perfis_usuario_Nome",
                table: "perfis_usuario",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_EmpresaId",
                table: "usuarios",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Login",
                table: "usuarios",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexos_chamado");

            migrationBuilder.DropTable(
                name: "base_conhecimento");

            migrationBuilder.DropTable(
                name: "comunicacoes_chamado");

            migrationBuilder.DropTable(
                name: "controle_equipamentos");

            migrationBuilder.DropTable(
                name: "inventario_equipamentos");

            migrationBuilder.DropTable(
                name: "links_monitorados");

            migrationBuilder.DropTable(
                name: "perfis_usuario");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "chamados");

            migrationBuilder.DropTable(
                name: "empresas");
        }
    }
}
