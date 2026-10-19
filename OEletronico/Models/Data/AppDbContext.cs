using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Enums;
using System;

namespace OEletronico.Models.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Pessoa> Pessoas { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<RegistroPonto> RegistrosPonto { get; set; }
        public DbSet<BancoHoras> BancosHoras { get; set; }
        public DbSet<MovimentacaoEstoque> MovimentacoesEstoque { get; set; }
        public DbSet<Relatorio> Relatorios { get; set; }
        public DbSet<Cnpj> Cnpjs { get; set; }

        // ⭐ NOVO — Tabela do Estoque Físico (Prateleiras)
        public DbSet<Estoque> Estoques { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ─── ÍNDICES ÚNICOS ──────────────────────────────────────────────
            modelBuilder.Entity<Usuario>().HasIndex(u => u.Login).IsUnique();
            modelBuilder.Entity<Pessoa>().HasIndex(p => p.Email).IsUnique();
            modelBuilder.Entity<Produto>().HasIndex(p => p.Codigo).IsUnique();
            modelBuilder.Entity<Cnpj>().HasIndex(c => c.Numero).IsUnique();

            // O número identificador físico do estoque não pode se repetir
            modelBuilder.Entity<Estoque>().HasIndex(e => e.NumeroIdentificador).IsUnique();

            // ─── RELACIONAMENTOS ORIGINAIS ───────────────────────────────────
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

            // 1 CNPJ para N colaboradores
            modelBuilder.Entity<Pessoa>()
                .HasOne(p => p.Cnpj)
                .WithMany(c => c.Pessoas)
                .HasForeignKey(p => p.CnpjId)
                .OnDelete(DeleteBehavior.SetNull);

            // ─── ⭐ NOVOS RELACIONAMENTOS (ESTOQUE E MOVIMENTAÇÃO) ⭐ ─────────

            // 1) Produto <-> Estoque (1 para 1)
            // Isso resolve permanentemente aquele erro chato do "ProdutoId1"
            modelBuilder.Entity<Produto>()
                .HasOne(p => p.Estoque)
                .WithOne(e => e.Produto)
                .HasForeignKey<Estoque>(e => e.ProdutoId)
                .OnDelete(DeleteBehavior.Cascade);

            // 2) Estoque <-> Pessoa (Quem fez a última atualização na prateleira)
            modelBuilder.Entity<Estoque>()
                .HasOne(e => e.PessoaAtualizacao)
                .WithMany()
                .HasForeignKey(e => e.PessoaAtualizacaoId)
                .OnDelete(DeleteBehavior.Restrict); // Restrict evita erro no SQL Server/Postgres

            // 3) Movimentacao <-> Pessoa (Quem deu entrada/saída)
            modelBuilder.Entity<MovimentacaoEstoque>()
                .HasOne(m => m.Pessoa)
                .WithMany()
                .HasForeignKey(m => m.PessoaId)
                .OnDelete(DeleteBehavior.Restrict);

            // 4) Movimentacao <-> Produto (O produto que foi movimentado)
            modelBuilder.Entity<MovimentacaoEstoque>()
                .HasOne(m => m.Produto)
                .WithMany(p => p.Movimentacoes)
                .HasForeignKey(m => m.ProdutoId)
                .OnDelete(DeleteBehavior.Cascade);


            // ─── OUTRAS CONFIGURAÇÕES ────────────────────────────────────────
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