using Microsoft.EntityFrameworkCore;
using OEletronico.Models.Enums;

namespace OEletronico.Models.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tabelas do banco
        public DbSet<Pessoa> Pessoas { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<RegistroPonto> RegistrosPonto { get; set; }
        public DbSet<BancoHoras> BancosHoras { get; set; }
        public DbSet<MovimentacaoEstoque> MovimentacoesEstoque { get; set; }
        public DbSet<Relatorio> Relatorios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Índices únicos
            modelBuilder.Entity<Usuario>().HasIndex(u => u.Login).IsUnique();
            modelBuilder.Entity<Pessoa>().HasIndex(p => p.Email).IsUnique();
            modelBuilder.Entity<Produto>().HasIndex(p => p.Codigo).IsUnique();

            // Relacionamentos 1:1
            modelBuilder.Entity<Pessoa>()
                .HasOne(p => p.Usuario)
                .WithOne(u => u.Pessoa)
                .HasForeignKey<Usuario>(u => u.PessoaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Pessoa>()
                .HasOne(p => p.BancoHoras)
                .WithOne(b => b.Pessoa)
                .HasForeignKey<BancoHoras>(b => b.PessoaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Tipo da coluna de preço
            modelBuilder.Entity<Produto>()
                .Property(p => p.Preco)
                .HasColumnType("decimal(10,2)");

            // Admin inicial
            modelBuilder.Entity<Pessoa>().HasData(new Pessoa
            {
                Id = 1,
                Nome = "Administrador",
                Email = "admin@oeletronico.com",
                Cargo = CargoEnum.Admin,
                Setor = "Administração",
                DataAdmissao = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            modelBuilder.Entity<Usuario>().HasData(new Usuario
            {
                Id = 1,
                Login = "admin",
                Senha = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy",
                PessoaId = 1
            });
        }
    }
}