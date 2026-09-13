using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OEletronico.Models
{
    public class Estoque
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe a prateleira.")]
        [StringLength(20)]
        public string Prateleira { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a numeração identificadora.")]
        [StringLength(30)]
        public string NumeroIdentificador { get; set; } = string.Empty;

        public DateTime DataUltimaAtualizacao { get; set; } = DateTime.UtcNow;

        public int ProdutoId { get; set; }
        public Produto? Produto { get; set; }
        public int PessoaAtualizacaoId { get; set; }

        [ForeignKey("PessoaAtualizacaoId")] 
        public Pessoa? PessoaAtualizacao { get; set; }
    }
}