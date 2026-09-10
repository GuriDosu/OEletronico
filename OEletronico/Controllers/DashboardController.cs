using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
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
        public IActionResult Index(string filtro = "mes")
        {
            try
            {
                string? userCargo = HttpContext.Session.GetString("UserCargo");
                if (string.IsNullOrEmpty(userCargo) || userCargo != "Admin")
                {
                    TempData["Erro"] = "Acesso restrito a Administradores.";
                    return RedirectToAction("Login", "Auth");
                }

                // 1. Indicadores Básicos
                ViewBag.TotalProdutos = _context.Produtos.Count();
                ViewBag.TotalColaboradores = _context.Pessoas.Count();

                decimal valorPatrimonioEstoque = _context.Produtos.Sum(p => (decimal?)(p.Quantidade * p.Preco)) ?? 0;
                ViewBag.ValorPatrimonioEstoque = valorPatrimonioEstoque;

                // 2. Busca segura das movimentações
                var todasMovimentacoes = _context.MovimentacoesEstoque
                    .Include(m => m.Produto)
                    .ToList();

                DateTime agora = DateTime.UtcNow;

                // 3. Filtro seguro na memória
                var movimentacoesFiltradas = todasMovimentacoes.Where(m => {
                    if (filtro == "dia")
                        return m.Data.Date == agora.Date;
                    if (filtro == "semana")
                        return m.Data >= agora.AddDays(-7);
                    if (filtro == "mes")
                        return m.Data.Month == agora.Month && m.Data.Year == agora.Year;
                    if (filtro == "ano")
                        return m.Data.Year == agora.Year;
                    return true;
                }).ToList();

                // 4. Cálculos
                int totalEntradas = movimentacoesFiltradas.Where(m => m.Tipo == "Entrada").Sum(m => m.Quantidade);
                int totalSaidas = movimentacoesFiltradas.Where(m => m.Tipo == "Saida").Sum(m => m.Quantidade);

                decimal dinheiroEntrada = movimentacoesFiltradas
                    .Where(m => m.Tipo == "Entrada" && m.Produto != null)
                    .Sum(m => m.Quantidade * m.Produto.Preco);

                ViewBag.TotalMovimentacoes = movimentacoesFiltradas.Count;
                ViewBag.TotalEntradas = totalEntradas;
                ViewBag.TotalSaidas = totalSaidas;
                ViewBag.DinheiroEntrada = dinheiroEntrada;
                ViewBag.FiltroAtual = filtro;

                var estoqueBaixo = _context.Produtos.Where(p => p.Quantidade <= 5).ToList();

                ViewBag.UltimasMovimentacoes = todasMovimentacoes
                    .OrderByDescending(m => m.Id)
                    .Take(5)
                    .ToList();

                return View(estoqueBaixo);
            }
            catch (Exception ex)
            {
                // ISSO VAI MOSTRAR O ERRO EXATO NA TELA EM VEZ DE DAR HTTP 500
                return Content($"<h1 style='color:red;'>Erro Crítico no Dashboard:</h1><pre>{ex.Message}\n\n{ex.StackTrace}</pre>", "text/html");
            }
        }
    }
}