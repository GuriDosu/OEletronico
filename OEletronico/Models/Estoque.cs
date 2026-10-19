using System.ComponentModel.DataAnnotations;

namespace OEletronico.Models
{
    public class Estoque
    {
        public int Id { get; set; }

        public int ProdutoId { get; set; }
        public Produto? Produto { get; set; }

        [StringLength(20)]
        public string Prateleira { get; set; } = string.Empty;

        [StringLength(30)]
        public string NumeroIdentificador { get; set; } = string.Empty;

        public DateTime DataUltimaAtualizacao { get; set; } = DateTime.UtcNow;
        public int PessoaAtualizacaoId { get; set; }
        public Pessoa? PessoaAtualizacao { get; set; }
    }
}