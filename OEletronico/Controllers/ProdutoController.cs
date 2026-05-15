using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;

namespace OEletronico.Controllers
{
    public class ProdutoController : Controller
    {
        private readonly AppDbContext _context;

        public ProdutoController(AppDbContext context)
        {
            _context = context;
        }

        // ─── Métodos de segurança ────────────────────────────────────
        private bool EstaLogado() =>
            HttpContext.Session.GetString("UserId") != null;

        private bool EhAdmin() =>
            HttpContext.Session.GetString("UserCargo") == "Admin";

        private IActionResult RedirecionarLogin() =>
            RedirectToAction("Login", "Auth");

        // ─── INDEX: Lista produtos (todos veem) ──────────────────────
        public async Task<IActionResult> Index(string? busca, string? filtro)
        {
            if (!EstaLogado()) return RedirecionarLogin();

            var query = _context.Produtos.AsQueryable();

            // Busca por nome ou código
            if (!string.IsNullOrWhiteSpace(busca))
            {
                busca = busca.Trim().ToLower();
                query = query.Where(p =>
                    p.Nome.ToLower().Contains(busca) ||
                    p.Codigo.ToLower().Contains(busca));
            }

            // Filtro de estoque
            if (filtro == "baixo")
                query = query.Where(p => p.Quantidade > 0 && p.Quantidade < 10);
            else if (filtro == "esgotado")
                query = query.Where(p => p.Quantidade == 0);
            else if (filtro == "normal")
                query = query.Where(p => p.Quantidade >= 10);

            var produtos = await query.OrderBy(p => p.Nome).ToListAsync();

            ViewBag.Busca = busca;
            ViewBag.Filtro = filtro;
            return View(produtos);
        }

        // ─── CREATE (GET): Formulário de cadastro ────────────────────
        public IActionResult Create()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            return View();
        }

        // ─── CREATE (POST): Salva novo produto ───────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Produto model)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            if (!ModelState.IsValid) return View(model);

            // Verifica se código já existe
            if (await _context.Produtos.AnyAsync(p => p.Codigo == model.Codigo.ToUpper()))
            {
                ModelState.AddModelError("Codigo", "Este código já está em uso.");
                return View(model);
            }

            var produto = new Produto
            {
                Nome = model.Nome,
                Codigo = model.Codigo.ToUpper(),
                Preco = model.Preco,
                Quantidade = model.Quantidade
            };

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Produto {produto.Nome} cadastrado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── EDIT (GET): Formulário de edição ────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();

            return View(produto);
        }

        // ─── EDIT (POST): Salva alterações ───────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Produto model)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(model);

            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();

            // Verifica se outro produto tem o mesmo código
            if (await _context.Produtos.AnyAsync(p => p.Codigo == model.Codigo && p.Id != id))
            {
                ModelState.AddModelError("Codigo", "Este código já está em uso por outro produto.");
                return View(model);
            }

            produto.Nome = model.Nome;
            produto.Codigo = model.Codigo.ToUpper();
            produto.Preco = model.Preco;
            produto.Quantidade = model.Quantidade;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Produto {produto.Nome} atualizado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── DELETE (POST): Exclui produto ───────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();

            // Verifica se há movimentações vinculadas
            var temMovimentacoes = await _context.MovimentacoesEstoque
                .AnyAsync(m => m.ProdutoId == id);

            if (temMovimentacoes)
            {
                TempData["Erro"] = $"O produto {produto.Nome} não pode ser excluído pois possui movimentações de estoque registradas.";
                return RedirectToAction("Index");
            }

            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Produto {produto.Nome} excluído com sucesso!";
            return RedirectToAction("Index");
        }
    }
}