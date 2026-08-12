using Microsoft.EntityFrameworkCore;
using UniFlowIT.Api.Models;

namespace UniFlowIT.Api.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Empresa> Empresas => Set<Empresa>();
        public DbSet<Users> Users => Set<Users>();
        public DbSet<UserPerfil> UserPerfis => Set<UserPerfil>();
        public DbSet<Chamado> Chamados => Set<Chamado>();
        public DbSet<CategoriaChamado> CategoriasChamados => Set<CategoriaChamado>();
        public DbSet<ComunicacaoChamado> ComunicacoesChamados => Set<ComunicacaoChamado>();
        public DbSet<AnexoChamado> AnexosChamados => Set<AnexoChamado>();
        public DbSet<ArtigoConhecimento> BaseConhecimento => Set<ArtigoConhecimento>();
        public DbSet<CategoriaConhecimento> CategoriasConhecimento => Set<CategoriaConhecimento>();
        public DbSet<ControleEquipamento> ControleEquipamentos => Set<ControleEquipamento>();
        public DbSet<InventarioEquipamento> InventarioEquipamentos => Set<InventarioEquipamento>();
        public DbSet<LinkMonitorado> LinksMonitorados => Set<LinkMonitorado>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("empresas");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(160).IsRequired();
                entity.Property(item => item.RazaoSocial).HasMaxLength(180).IsRequired();
                entity.Property(item => item.NomeFantasia).HasMaxLength(180).IsRequired();
                entity.Property(item => item.TenantSlug).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Cnpj).HasMaxLength(20);
                entity.Property(item => item.Email).HasMaxLength(160);
                entity.HasIndex(item => item.TenantSlug).IsUnique();
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.ToTable("usuarios");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(160).IsRequired();
                entity.Property(item => item.Login).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Email).HasMaxLength(160).IsRequired();
                entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
                entity.HasIndex(item => item.Login).IsUnique();
                entity.HasIndex(item => item.Email).IsUnique();
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserPerfil>(entity =>
            {
                entity.ToTable("perfis_usuario");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(60).IsRequired();
                entity.HasIndex(item => item.Nome).IsUnique();
            });

            modelBuilder.Entity<Chamado>(entity =>
            {
                entity.ToTable("chamados");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Numero).HasMaxLength(32).IsRequired();
                entity.Property(item => item.Titulo).HasMaxLength(180).IsRequired();
                entity.Property(item => item.Solicitante).HasMaxLength(160).IsRequired();
                entity.Property(item => item.Categoria).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Subcategoria).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Tipo).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.Prioridade).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.AtendenteNome).HasMaxLength(160);
                entity.Property(item => item.OrigemAutomacao).HasMaxLength(120);
                entity.HasIndex(item => item.Numero).IsUnique();
                entity.HasIndex(item => item.EmpresaId);
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.OwnsOne(item => item.EquipamentoRelacionado, equipamento =>
                {
                    equipamento.Property(item => item.Hostname).HasMaxLength(120);
                    equipamento.Property(item => item.SistemaOperacional).HasMaxLength(120);
                    equipamento.Property(item => item.UsuarioLogado).HasMaxLength(120);
                    equipamento.Property(item => item.Ip).HasMaxLength(60);
                    equipamento.Property(item => item.Navegador).HasMaxLength(120);
                });

                entity.HasMany(item => item.Anexos)
                    .WithOne()
                    .HasForeignKey("ChamadoId")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(item => item.Comunicacoes)
                    .WithOne()
                    .HasForeignKey(item => item.ChamadoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CategoriaChamado>(entity =>
            {
                entity.ToTable("categorias_chamado");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(100).IsRequired();
                entity.Property(item => item.Subcategorias).HasMaxLength(1000);
                entity.Property(item => item.PrioridadePadrao).HasConversion<string>().HasMaxLength(30);
                entity.HasIndex(item => item.EmpresaId);
                entity.HasIndex(item => new { item.EmpresaId, item.Nome }).IsUnique();
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AnexoChamado>(entity =>
            {
                entity.ToTable("anexos_chamado");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.NomeArquivo).HasMaxLength(240).IsRequired();
                entity.Property(item => item.TipoConteudo).HasMaxLength(120);
                entity.Property(item => item.Url).HasMaxLength(500);
            });

            modelBuilder.Entity<ComunicacaoChamado>(entity =>
            {
                entity.ToTable("comunicacoes_chamado");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.AutorNome).HasMaxLength(160).IsRequired();
                entity.Property(item => item.AutorPerfil).HasMaxLength(40).IsRequired();
                entity.Property(item => item.Mensagem).IsRequired();
            });

            modelBuilder.Entity<ArtigoConhecimento>(entity =>
            {
                entity.ToTable("base_conhecimento");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Titulo).HasMaxLength(180).IsRequired();
                entity.Property(item => item.Categoria).HasMaxLength(80).IsRequired();
                entity.Property(item => item.UsuarioCriador).HasMaxLength(160);
                entity.HasIndex(item => item.EmpresaId);
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CategoriaConhecimento>(entity =>
            {
                entity.ToTable("categorias_conhecimento");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(100).IsRequired();
                entity.HasIndex(item => item.EmpresaId);
                entity.HasIndex(item => new { item.EmpresaId, item.Nome }).IsUnique();
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ControleEquipamento>(entity =>
            {
                entity.ToTable("controle_equipamentos");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Patrimonio).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Tipo).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
                entity.HasIndex(item => item.EmpresaId);
                entity.HasIndex(item => item.Patrimonio);
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<InventarioEquipamento>(entity =>
            {
                entity.ToTable("inventario_equipamentos");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Patrimonio).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Hostname).HasMaxLength(120).IsRequired();
                entity.HasIndex(item => item.EmpresaId);
                entity.HasIndex(item => item.Hostname);
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LinkMonitorado>(entity =>
            {
                entity.ToTable("links_monitorados");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(160).IsRequired();
                entity.Property(item => item.Tipo).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Local).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Firewall).HasMaxLength(120);
                entity.Property(item => item.Endereco).HasMaxLength(160).IsRequired();
                entity.Property(item => item.Cep).HasMaxLength(9);
                entity.HasIndex(item => item.EmpresaId);
                entity.HasOne(item => item.Empresa)
                    .WithMany()
                    .HasForeignKey(item => item.EmpresaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
