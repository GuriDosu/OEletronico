namespace OEletronico.Models
{
    public class BancoHoras
    {
        public int Id { get; set; }
        public double HorasNormais { get; set; }
        public double HorasExtras { get; set; }
        public double Saldo { get; set; }

        public int PessoaId { get; set; }
        public Pessoa Pessoa { get; set; } = null!;
    }
}