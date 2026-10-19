using System.ComponentModel.DataAnnotations;

namespace OEletronico.Models.DTOs
{
    public class MovimentacaoDTO
    {
        public int ProdutoId { get; set; }

        [Required(ErrorMessage = "Selecione se é Entrada ou Saída")]
        public string Tipo { get; set; } = string.Empty; // "Entrada" ou "Saída"

        [Required(ErrorMessage = "Informe a quantidade")]
        [Range(1, 10000, ErrorMessage = "A quantidade deve ser maior que zero")]
        public int Quantidade { get; set; }
    }
}