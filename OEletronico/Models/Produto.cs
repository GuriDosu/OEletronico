using OEletronico.Models.Enums;

namespace OEletronico.Models
{
    public class Produto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public int Quantidade { get; set; }
        public TipoProdutoEnum Tipo { get; set; }
        public string Categoria { get; set; } = string.Empty; // <-- Adicionado da Tabela 6

        // Navegação
        public Estoque? Estoque { get; set; }
        public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = new List<MovimentacaoEstoque>();
        public bool Ativo { get; set; } = true; // <-- NOVO CAMPO PARA INATIVAR
    }
}