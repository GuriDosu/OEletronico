using OEletronico.Models;
using System.ComponentModel.DataAnnotations;

public class Cnpj
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O número do CNPJ é obrigatório.")]
    [StringLength(18)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "A razão social é obrigatória.")]
    [StringLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    [StringLength(150)]
    public string? NomeFantasia { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCadastro { get; set; } = DateTime.Now;

    public ICollection<Pessoa>? Pessoas { get; set; }
}