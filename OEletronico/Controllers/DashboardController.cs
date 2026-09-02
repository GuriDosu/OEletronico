using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models.Data;

namespace OEletronico.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            string userCargo = HttpContext.Session.GetString("UserCargo");

            if (string.IsNullOrEmpty(userCargo) || userCargo != "Admin")
            {
                TempData["Erro"] = "Acesso negado. Faça login com uma conta de Administrador.";
                return RedirectToAction("Login", "Auth");
            }

            // Indicadores Básicos
            ViewBag.TotalProdutos = _context.Produtos.Count();
            ViewBag.TotalMovimentacoes = _context.MovimentacoesEstoque.Count();
            ViewBag.TotalColaboradores = _context.Pessoas.Count();

            // Patrimônio Total em Estoque (Quantidade * Preço de cada produto)
            decimal valorPatrimonioEstoque = _context.Produtos
                .Sum(p => p.Quantidade * p.Preco);
            ViewBag.ValorPatrimonioEstoque = valorPatrimonioEstoque;

            // Movimentações e cálculos financeiros
            var movimentacoes = _context.MovimentacoesEstoque
                .Include(m => m.Produto)
                .ToList();

            int totalEntradas = movimentacoes.Where(m => m.Tipo == "Entrada").Sum(m => m.Quantidade);
            int totalSaidas = movimentacoes.Where(m => m.Tipo == "Saida").Sum(m => m.Quantidade);

            decimal dinheiroEntrada = movimentacoes
                .Where(m => m.Tipo == "Entrada" && m.Produto != null)
                .Sum(m => m.Quantidade * m.Produto.Preco);

            ViewBag.TotalEntradas = totalEntradas;
            ViewBag.TotalSaidas = totalSaidas;
            ViewBag.DinheiroEntrada = dinheiroEntrada;

            // Alerta de Estoque Baixo (≤ 5 unidades)
            var estoqueBaixo = _context.Produtos
                .Where(p => p.Quantidade <= 5)
                .ToList();

            // Últimas 5 Movimentações Registradas no Sistema
            var ultimasMovimentacoes = _context.MovimentacoesEstoque
                .Include(m => m.Produto)
                .OrderByDescending(m => m.Id)
                .Take(5)
                .ToList();

            ViewBag.UltimasMovimentacoes = ultimasMovimentacoes;

            return View(estoqueBaixo);
        }
    }
}