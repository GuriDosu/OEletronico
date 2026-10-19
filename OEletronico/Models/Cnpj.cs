using System.ComponentModel.DataAnnotations;

namespace OEletronico.Models
{
    public class Cnpj
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O número do CNPJ é obrigatório.")]
        [Display(Name = "Número do CNPJ")]
        [StringLength(18, ErrorMessage = "O CNPJ deve ter no máximo 18 caracteres.")]
        public string Numero { get; set; } = string.Empty;

        [Required(ErrorMessage = "A razão social é obrigatória.")]
        [Display(Name = "Razão Social")]
        [StringLength(200)]
        public string RazaoSocial { get; set; } = string.Empty;

        [Display(Name = "Nome Fantasia")]
        [StringLength(150)]
        public string? NomeFantasia { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // Um CNPJ (contrato) pode ter vários colaboradores
        public ICollection<Pessoa>? Pessoas { get; set; }
    }
}