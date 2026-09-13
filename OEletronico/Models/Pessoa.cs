using OEletronico.Models.Enums;

namespace OEletronico.Models

{
    public class Pessoa
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public CargoEnum Cargo { get; set; }
        public string Setor { get; set; } = string.Empty;
        public DateTime DataAdmissao { get; set; }
        public Usuario? Usuario { get; set; }

        public BancoHoras? BancoHoras { get; set; }

        public ICollection<RegistroPonto> RegistrosPonto { get; set; } = new List<RegistroPonto>();


    }
}