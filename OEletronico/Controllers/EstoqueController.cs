using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.DTOs;

namespace OEletronico.Controllers
{
    public class EstoqueController : Controller
    {
        private readonly AppDbContext _context;

        public EstoqueController(AppDbContext context)
        {
            _context = context;
        }

        private bool EstaLogado() => HttpContext.Session.GetString("UserId") != null;

        private bool TemAcessoEstoque()
        {
            var cargo = HttpContext.Session.GetString("UserCargo");
            if (cargo == "Admin") return true;

            var pessoaIdStr = HttpContext.Session.GetString("UserPessoaId");
            if (string.IsNullOrEmpty(pessoaIdStr)) return false;

            var pessoaId = int.Parse(pessoaIdStr);
            var pessoa = _context.Pessoas.Find(pessoaId);

            return pessoa != null && (pessoa.Setor == "Estoque" || cargo == "Estoquista");
        }

        // ─── TELA PRINCIPAL DO ESTOQUE FÍSICO ─────────────────────────────
        public async Task<IActionResult> Index(string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            var query = _context.Produtos
                .Include(p => p.Estoque)
                .Where(p => p.Ativo);

            if (categoria == "LED")
            {
                query = query.Where(p => p.Categoria == "LED" || p.Nome.ToUpper().Contains("LED"));
                ViewBag.TituloEstoque = "💡 Estoque de Painéis LED";
            }
            else
            {
                query = query.Where(p => p.Categoria != "LED" && !p.Nome.ToUpper().Contains("LED"));
                ViewBag.TituloEstoque = "📦 Estoque de Produtos Gerais";
            }

            ViewBag.CategoriaAtual = categoria;
            ViewBag.UserCargo = HttpContext.Session.GetString("UserCargo");

            var produtos = await query.OrderBy(p => p.Nome).ToListAsync();
            return View(produtos);
        }

        // ─── EXPORTAR PLANILHA EM EXCEL (.xlsx) - APENAS ADMIN ────────────
        public async Task<IActionResult> ExportarExcel(string categoria = "Produtos")
        {
            if (!EstaLogado()) return RedirectToAction("Login", "Auth");

            var cargo = HttpContext.Session.GetString("UserCargo");
            if (cargo != "Admin")
            {
                return Forbid();
            }

            var query = _context.Produtos
                .Include(p => p.Estoque)
                .Where(p => p.Ativo);

            if (categoria == "LED")
            {
                query = query.Where(p => p.Categoria == "LED" || p.Nome.ToUpper().Contains("LED"));
            }
            else
            {
                query = query.Where(p => p.Categoria != "LED" && !p.Nome.ToUpper().Contains("LED"));
            }

            var produtos = await query.OrderBy(p => p.Nome).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Produtos");

            // TÍTULO
            worksheet.Cell("A1").Value = "⚡ O ELETRÔNICO";
            worksheet.Cell("A1").Style.Font.FontSize = 18;
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1a3a5c");
            worksheet.Range("A1:F1").Merge();
            worksheet.Range("A1:F1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A2").Value = categoria == "LED" ? "Catálogo de Painéis LED" : "Catálogo de Produtos Gerais";
            worksheet.Cell("A2").Style.Font.FontSize = 13;
            worksheet.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#6b7280");
            worksheet.Range("A2:F2").Merge();
            worksheet.Range("A2:F2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A3").Value = $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}";
            worksheet.Cell("A3").Style.Font.FontSize = 10;
            worksheet.Cell("A3").Style.Font.FontColor = XLColor.Gray;
            worksheet.Range("A3:F3").Merge();
            worksheet.Range("A3:F3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // CABEÇALHO
            int linhaInicio = 5;
            string[] cabecalho = { "Código", "Nome", "Preço (R$)", "Quantidade", "Valor Total (R$)", "Status" };

            for (int i = 0; i < cabecalho.Length; i++)
            {
                var celula = worksheet.Cell(linhaInicio, i + 1);
                celula.Value = cabecalho[i];
                celula.Style.Font.Bold = true;
                celula.Style.Font.FontColor = XLColor.White;
                celula.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a3a5c");
                celula.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // DADOS
            int linha = linhaInicio + 1;
            foreach (var p in produtos)
            {
                worksheet.Cell(linha, 1).Value = p.Codigo;
                worksheet.Cell(linha, 2).Value = p.Nome;
                worksheet.Cell(linha, 3).Value = (double)p.Preco;
                worksheet.Cell(linha, 4).Value = p.Quantidade;
                worksheet.Cell(linha, 5).Value = (double)(p.Preco * p.Quantidade);

                string status;
                XLColor corStatus;
                if (p.Quantidade == 0)
                {
                    status = "Esgotado";
                    corStatus = XLColor.FromHtml("#dc2626");
                }
                else if (p.Quantidade < 10)
                {
                    status = "Estoque baixo";
                    corStatus = XLColor.FromHtml("#d97706");
                }
                else
                {
                    status = "Em estoque";
                    corStatus = XLColor.FromHtml("#16a34a");
                }

                worksheet.Cell(linha, 6).Value = status;
                worksheet.Cell(linha, 6).Style.Font.FontColor = corStatus;
                worksheet.Cell(linha, 6).Style.Font.Bold = true;

                worksheet.Cell(linha, 3).Style.NumberFormat.Format = "R$ #,##0.00";
                worksheet.Cell(linha, 5).Style.NumberFormat.Format = "R$ #,##0.00";

                if (linha % 2 == 0)
                {
                    worksheet.Range(linha, 1, linha, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");
                }

                linha++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            var fileName = $"Estoque_{categoria}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ─── 1. ALOCAR EM PRATELEIRA (ESTOQUISTA E ADMIN) ─────────────────
        [HttpGet]
        public async Task<IActionResult> Alocar(int id, string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            var produto = await _context.Produtos.Include(p => p.Estoque).FirstOrDefaultAsync(p => p.Id == id);
            if (produto == null) return NotFound();

            ViewBag.ProdutoNome = produto.Nome;
            ViewBag.CategoriaAtual = categoria;

            var dto = new AlocarDTO
            {
                ProdutoId = produto.Id,
                Prateleira = produto.Estoque?.Prateleira ?? "",
                NumeroIdentificador = produto.Estoque?.NumeroIdentificador ?? ""
            };
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Alocar(AlocarDTO dto, string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            if (!ModelState.IsValid)
            {
                var produtoObj = await _context.Produtos.FindAsync(dto.ProdutoId);
                ViewBag.ProdutoNome = produtoObj?.Nome ?? "Produto";
                ViewBag.CategoriaAtual = categoria;
                return View(dto);
            }

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var estoque = await _context.Estoques.FirstOrDefaultAsync(e => e.ProdutoId == dto.ProdutoId);

            if (estoque == null)
            {
                estoque = new Estoque
                {
                    ProdutoId = dto.ProdutoId,
                    Prateleira = dto.Prateleira,
                    NumeroIdentificador = dto.NumeroIdentificador,
                    PessoaAtualizacaoId = pessoaId,
                    DataUltimaAtualizacao = DateTime.UtcNow
                };
                _context.Estoques.Add(estoque);
            }
            else
            {
                estoque.Prateleira = dto.Prateleira;
                estoque.NumeroIdentificador = dto.NumeroIdentificador;
                estoque.PessoaAtualizacaoId = pessoaId;
                estoque.DataUltimaAtualizacao = DateTime.UtcNow;
                _context.Estoques.Update(estoque);
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Localização física salva na prateleira com sucesso!";
            return RedirectToAction("Index", new { categoria });
        }

        // ─── 2. MOVIMENTAR QUANTIDADE (ESTOQUISTA E ADMIN) ────────────────
        [HttpGet]
        public async Task<IActionResult> Movimentar(int id, string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();

            ViewBag.ProdutoNome = produto.Nome;
            ViewBag.QuantidadeAtual = produto.Quantidade;
            ViewBag.CategoriaAtual = categoria; // Mantém a categoria na view GET

            return View(new MovimentacaoDTO { ProdutoId = produto.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Movimentar(MovimentacaoDTO dto, string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            var produto = await _context.Produtos.FindAsync(dto.ProdutoId);
            if (produto == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ProdutoNome = produto.Nome;
                ViewBag.QuantidadeAtual = produto.Quantidade;
                ViewBag.CategoriaAtual = categoria;
                return View(dto);
            }

            if (dto.Tipo == "Saída" && produto.Quantidade < dto.Quantidade)
            {
                ModelState.AddModelError("Quantidade", "Estoque insuficiente para esta saída!");
                ViewBag.ProdutoNome = produto.Nome;
                ViewBag.QuantidadeAtual = produto.Quantidade;
                ViewBag.CategoriaAtual = categoria;
                return View(dto);
            }

            if (dto.Tipo == "Entrada") produto.Quantidade += dto.Quantidade;
            else if (dto.Tipo == "Saída") produto.Quantidade -= dto.Quantidade;

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            _context.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                ProdutoId = produto.Id,
                Tipo = dto.Tipo,
                Quantidade = dto.Quantidade,
                Data = DateTime.UtcNow,
                PessoaId = pessoaId
            });

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Quantidade movimentada no estoque com sucesso!";

            // Retorna exatamente para a aba correta (LED ou Produtos)
            return RedirectToAction("Index", new { categoria });
        }

        // ─── 3. INATIVAR PRODUTO (ESTOQUISTA E ADMIN) ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inativar(int id, string categoria = "Produtos")
        {
            if (!EstaLogado() || !TemAcessoEstoque()) return Forbid();

            var produto = await _context.Produtos.FindAsync(id);
            if (produto != null)
            {
                produto.Ativo = false;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"O produto {produto.Nome} foi inativado e removido do estoque ativo.";
            }
            return RedirectToAction("Index", new { categoria });
        }
    }
}