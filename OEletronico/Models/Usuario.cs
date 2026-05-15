namespace OEletronico.Models
{
        public class Usuario
        {
            public int Id { get; set; }
            public string Login { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;

            public int PessoaId { get; set; }
            public Pessoa Pessoa { get; set; } = null!;
        }
    }

