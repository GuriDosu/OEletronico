namespace OEletronico.Models
{
    public class MovimentacaoEstoque
    {
        public int Id { get; set; }

        public int ProdutoId { get; set; } // Obrigatório para saber qual produto moveu
        public Produto? Produto { get; set; }

        public string Tipo { get; set; } = string.Empty; // Ex: "Entrada" ou "Saída"
        public int Quantidade { get; set; }
        public DateTime Data { get; set; } = DateTime.UtcNow;

        // Quem fez a movimentação
        public int PessoaId { get; set; }
        public Pessoa? Pessoa { get; set; }
    }
}