namespace OEletronico.Models
{
    public class MovimentacaoEstoque
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public DateTime Data { get; set; }

        public int PessoaId { get; set; }
        public Pessoa Pessoa { get; set; } = null!;

        public int ProdutoId { get; set; }
        public Produto Produto { get; set; } = null!;
    }
}