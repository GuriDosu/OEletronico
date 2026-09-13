using ClosedXML.Excel;
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

        // ─── Segurança ────────────────────────────────────
        private bool EstaLogado() =>
            HttpContext.Session.GetString("UserId") != null;

        private bool EhAdmin() =>
            HttpContext.Session.GetString("UserCargo") == "Admin";

        private IActionResult RedirecionarLogin() =>
            RedirectToAction("Login", "Auth");

        // ─── INDEX: Lista produtos (TODOS podem ver) ─────────────────
        public async Task<IActionResult> Index(string? busca, string? filtro)
        {
            if (!EstaLogado()) return RedirecionarLogin();

            var query = _context.Produtos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                busca = busca.Trim().ToLower();
                query = query.Where(p =>
                    p.Nome.ToLower().Contains(busca) ||
                    p.Codigo.ToLower().Contains(busca));
            }

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

        // ─── DELETE (POST): Exclui produto (SÓ ADMIN) ────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();

            var nomeProduto = produto.Nome;

            try
            {
                // ⭐ Exclui o produto mesmo com movimentações registradas.
                // O histórico de movimentações desse produto é apagado
                // automaticamente em cascata (configuração do EF Core).
                _context.Produtos.Remove(produto);
                await _context.SaveChangesAsync();

                TempData["Sucesso"] = $"Produto {nomeProduto} e seu histórico de movimentações foram excluídos com sucesso!";
            }
            catch (DbUpdateException)
            {
                TempData["Erro"] = $"Não foi possível excluir o produto {nomeProduto}. Tente novamente.";
            }

            return RedirectToAction("Index");
        }

        // ─── EXPORTAR EXCEL (SÓ ADMIN) ───────────────────────────────
        public async Task<IActionResult> ExportarExcel()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var produtos = await _context.Produtos.OrderBy(p => p.Nome).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Produtos");

            // TÍTULO
            worksheet.Cell("A1").Value = "⚡ O ELETRÔNICO";
            worksheet.Cell("A1").Style.Font.FontSize = 18;
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1a3a5c");
            worksheet.Range("A1:F1").Merge();
            worksheet.Range("A1:F1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A2").Value = "Catálogo de Produtos";
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

            // TOTAIS
            int linhaTotal = linha + 1;
            worksheet.Cell(linhaTotal, 1).Value = "TOTAIS:";
            worksheet.Cell(linhaTotal, 1).Style.Font.Bold = true;
            worksheet.Cell(linhaTotal, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8f0fb");

            worksheet.Cell(linhaTotal, 4).Value = produtos.Sum(p => p.Quantidade);
            worksheet.Cell(linhaTotal, 4).Style.Font.Bold = true;
            worksheet.Cell(linhaTotal, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8f0fb");

            worksheet.Cell(linhaTotal, 5).Value = (double)produtos.Sum(p => p.Preco * p.Quantidade);
            worksheet.Cell(linhaTotal, 5).Style.Font.Bold = true;
            worksheet.Cell(linhaTotal, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8f0fb");
            worksheet.Cell(linhaTotal, 5).Style.NumberFormat.Format = "R$ #,##0.00";

            worksheet.Columns().AdjustToContents();
            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 40;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var conteudo = stream.ToArray();

            var nomeArquivo = $"Produtos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }
    }
}