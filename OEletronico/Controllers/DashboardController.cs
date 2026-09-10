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

                // Indicadores Básicos
                ViewBag.TotalProdutos = _context.Produtos.Count();
                ViewBag.TotalColaboradores = _context.Pessoas.Count();

                decimal valorPatrimonioEstoque = _context.Produtos.Sum(p => (decimal?)(p.Quantidade * p.Preco)) ?? 0;
                ViewBag.ValorPatrimonioEstoque = valorPatrimonioEstoque;

                // PREPARAÇÃO DO FILTRO DE TEMPO
                var query = _context.MovimentacoesEstoque
                    .Include(m => m.Produto)
                    .AsQueryable();

                DateTime agora = DateTime.UtcNow;

                if (filtro == "dia")
                {
                    DateTime inicioDia = agora.Date;
                    DateTime fimDia = inicioDia.AddDays(1);
                    query = query.Where(m => m.Data >= inicioDia && m.Data < fimDia);
                }
                else if (filtro == "semana")
                {
                    DateTime inicioSemana = agora.AddDays(-7);
                    query = query.Where(m => m.Data >= inicioSemana);
                }
                else if (filtro == "mes")
                {
                    int mes = agora.Month;
                    int ano = agora.Year;
                    query = query.Where(m => m.Data.Month == mes && m.Data.Year == ano);
                }
                else if (filtro == "ano")
                {
                    int ano = agora.Year;
                    query = query.Where(m => m.Data.Year == ano);
                }
                // Se o filtro for "tudo", nenhuma restrição de data é aplicada.

                var movimentacoesFiltradas = query.ToList();

                // Cálculos baseados no período filtrado
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

                // Listas de apoio
                var estoqueBaixo = _context.Produtos.Where(p => p.Quantidade <= 5).ToList();

                ViewBag.UltimasMovimentacoes = _context.MovimentacoesEstoque
                    .Include(m => m.Produto)
                    .OrderByDescending(m => m.Id)
                    .Take(5)
                    .ToList();

                return View(estoqueBaixo);
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro interno no Dashboard: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }
    }
}