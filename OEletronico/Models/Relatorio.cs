namespace OEletronico.Models
{
    public class Relatorio
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateTime DataGeracao { get; set; }

        public int PessoaId { get; set; }
        public Pessoa Pessoa { get; set; } = null!;
    }
}