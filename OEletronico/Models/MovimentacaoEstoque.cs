using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OEletronico.Models
{
    public class MovimentacaoEstoque
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O tipo de movimentação é obrigatório.")]
        [StringLength(20)]
        public string Tipo { get; set; } = string.Empty; // "Entrada" ou "Saida"

        public int Quantidade { get; set; }

        [Required]
        public DateTime Data { get; set; } 

        public int PessoaId { get; set; }

        public Pessoa Pessoa { get; set; } = null!;

        public int ProdutoId { get; set; }

        public Produto Produto { get; set; } = null!;
    }
}