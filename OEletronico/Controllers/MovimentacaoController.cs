using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;

namespace OEletronico.Controllers
{
    public class MovimentacaoController : Controller
    {
        private readonly AppDbContext _context;

        public MovimentacaoController(AppDbContext context)
        {
            _context = context;
        }

        // ─── Segurança ────────────────────────────────────
        private bool EstaLogado() =>
            HttpContext.Session.GetString("UserId") != null;

        private IActionResult RedirecionarLogin() =>
            RedirectToAction("Login", "Auth");

        // ─── INDEX: Histórico de movimentações ───────────────────────
        public async Task<IActionResult> Index(string? tipoFiltro, int? produtoId)
        {
            if (!EstaLogado()) return RedirecionarLogin();

            var query = _context.MovimentacoesEstoque
                .Include(m => m.Pessoa)
                .Include(m => m.Produto)
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrEmpty(tipoFiltro))
                query = query.Where(m => m.Tipo == tipoFiltro);

            if (produtoId.HasValue && produtoId.Value > 0)
                query = query.Where(m => m.ProdutoId == produtoId.Value);

            var movimentacoes = await query
                .OrderByDescending(m => m.Data)
                .Take(100)
                .ToListAsync();

            ViewBag.Produtos = await _context.Produtos.OrderBy(p => p.Nome).ToListAsync();
            ViewBag.TipoFiltro = tipoFiltro;
            ViewBag.ProdutoId = produtoId;

            return View(movimentacoes);
        }

        // ─── CADASTRAR PRODUTO (GET) ─────────────────────────────────
        public IActionResult Cadastrar()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            return View();
        }

        // ─── CADASTRAR PRODUTO (POST) ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastrar(Produto model)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!ModelState.IsValid) return View(model);

            // Padroniza
            var codigoNormalizado = model.Codigo.Trim().ToUpper();
            var nomeNormalizado = model.Nome.Trim();

            // ⚠️ VALIDAÇÃO 1: Código já existe?
            if (await _context.Produtos.AnyAsync(p => p.Codigo == codigoNormalizado))
            {
                ModelState.AddModelError("Codigo", $"O código \"{codigoNormalizado}\" já está em uso por outro produto.");
                return View(model);
            }

            // ⚠️ VALIDAÇÃO 2: Nome já existe (sem distinguir maiúsculo/minúsculo)?
            var nomeExiste = await _context.Produtos
                .AnyAsync(p => p.Nome.ToLower() == nomeNormalizado.ToLower());

            if (nomeExiste)
            {
                ModelState.AddModelError("Nome", $"Já existe um produto com o nome \"{nomeNormalizado}\". Verifique se não é o mesmo produto.");
                return View(model);
            }

            // Cria o produto
            var produto = new Produto
            {
                Nome = nomeNormalizado,
                Codigo = codigoNormalizado,
                Preco = model.Preco,
                Quantidade = model.Quantidade
            };

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            // Se cadastrou com quantidade > 0, cria movimentação inicial de entrada
            if (produto.Quantidade > 0)
            {
                var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
                var movimentacaoInicial = new MovimentacaoEstoque
                {
                    Tipo = "Entrada",
                    Quantidade = produto.Quantidade,
                    Data = DateTime.UtcNow,
                    PessoaId = pessoaId,
                    ProdutoId = produto.Id
                };
                _context.MovimentacoesEstoque.Add(movimentacaoInicial);
                await _context.SaveChangesAsync();
            }

            var nomeUsuario = HttpContext.Session.GetString("UserName") ?? "Usuário";
            TempData["Sucesso"] = $"✓ Produto \"{produto.Nome}\" cadastrado com sucesso por {nomeUsuario}!";
            return RedirectToAction("Index");
        }

        // ─── MOVIMENTAR (GET): Tela de entrada/saída ─────────────────
        public async Task<IActionResult> Movimentar()
        {
            if (!EstaLogado()) return RedirecionarLogin();

            ViewBag.Produtos = await _context.Produtos
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return View();
        }

        // ─── MOVIMENTAR (POST): Salva entrada/saída ──────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Movimentar(int ProdutoId, string Tipo, int Quantidade)
        {
            if (!EstaLogado()) return RedirecionarLogin();

            // Validações
            if (ProdutoId <= 0)
            {
                TempData["Erro"] = "Selecione um produto.";
                return RedirectToAction("Movimentar");
            }

            if (Tipo != "Entrada" && Tipo != "Saida")
            {
                TempData["Erro"] = "Tipo de movimentação inválido.";
                return RedirectToAction("Movimentar");
            }

            if (Quantidade <= 0)
            {
                TempData["Erro"] = "A quantidade deve ser maior que zero.";
                return RedirectToAction("Movimentar");
            }

            var produto = await _context.Produtos.FindAsync(ProdutoId);
            if (produto == null)
            {
                TempData["Erro"] = "Produto não encontrado.";
                return RedirectToAction("Movimentar");
            }

            // Se for saída, verifica estoque
            if (Tipo == "Saida" && Quantidade > produto.Quantidade)
            {
                TempData["Erro"] = $"❌ Estoque insuficiente! Disponível: {produto.Quantidade} unidades de {produto.Nome}.";
                return RedirectToAction("Movimentar");
            }

            // Atualiza estoque
            if (Tipo == "Entrada")
                produto.Quantidade += Quantidade;
            else
                produto.Quantidade -= Quantidade;

            // Registra movimentação
            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var movimentacao = new MovimentacaoEstoque
            {
                Tipo = Tipo,
                Quantidade = Quantidade,
                Data = DateTime.UtcNow,
                PessoaId = pessoaId,
                ProdutoId = produto.Id
            };
            _context.MovimentacoesEstoque.Add(movimentacao);

            await _context.SaveChangesAsync();

            var nomeUsuario = HttpContext.Session.GetString("UserName") ?? "Usuário";
            var tipoLabel = Tipo == "Entrada" ? "📥 Entrada" : "📤 Saída";
            TempData["Sucesso"] = $"{tipoLabel} de {Quantidade} un. em \"{produto.Nome}\" registrada por {nomeUsuario}.";

            return RedirectToAction("Index");
        }
    }
}