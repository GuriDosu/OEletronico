namespace OEletronico.Models
{
    public class RegistroPonto
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public TimeSpan HoraEntrada { get; set; }
        public TimeSpan? HoraSaida { get; set; }

        public int PessoaId { get; set; }

        public Pessoa Pessoa { get; set; } = null!;
    }
}
