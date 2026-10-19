using System.ComponentModel.DataAnnotations;

namespace OEletronico.Models.DTOs
{
    public class AlocarDTO
    {
        public int ProdutoId { get; set; }

        [Required(ErrorMessage = "O campo Prateleira é obrigatório.")]
        public string Prateleira { get; set; } = string.Empty;

        [Required(ErrorMessage = "O campo Caixa / Lote é obrigatório.")]
        public string NumeroIdentificador { get; set; } = string.Empty;
    }
}