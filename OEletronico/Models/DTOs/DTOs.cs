using System.ComponentModel.DataAnnotations;

namespace OEletronico.Models.DTOs
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "Informe o login")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = string.Empty;
    }

    public class PessoaDTO
    {
        [Required(ErrorMessage = "Informe o nome completo")]
        [StringLength(150, MinimumLength = 3)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o email")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecione o cargo")]
        public string Cargo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o setor")]
        public string Setor { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a data de admissão")]
        [DataType(DataType.Date)]
        public DateTime DataAdmissao { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Defina um login")]
        [StringLength(50, MinimumLength = 3)]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Defina uma senha")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mínimo de 6 caracteres")]
        [DataType(DataType.Password)]
        public string Senha { get; set; } = string.Empty;
    }
}