using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OEletronico.Controllers
{
    public class BancoHorasController : Controller
    {
        private readonly AppDbContext _context;

        public BancoHorasController(AppDbContext context)
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

        // ─── INDEX: Banco de horas do colaborador (próprio) ─────────
        public async Task<IActionResult> Index()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (EhAdmin()) return RedirectToAction("Admin");

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);

            var bancoHoras = await _context.BancosHoras
                .Include(b => b.Pessoa)
                .FirstOrDefaultAsync(b => b.PessoaId == pessoaId);

            if (bancoHoras == null)
            {
                bancoHoras = new BancoHoras
                {
                    HorasNormais = 0,
                    HorasExtras = 0,
                    Saldo = 0,
                    PessoaId = pessoaId
                };
            }

            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            inicioMes = DateTime.SpecifyKind(inicioMes, DateTimeKind.Utc);

            var pontosMes = await _context.RegistrosPonto
                .Where(r => r.PessoaId == pessoaId && r.Data >= inicioMes)
                .OrderByDescending(r => r.Data)
                .ToListAsync();

            ViewBag.PontosMes = pontosMes;
            return View(bancoHoras);
        }

        // ─── ADMIN: Banco de horas de todos os colaboradores ─────────
        public async Task<IActionResult> Admin()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var colaboradores = await _context.Pessoas
                .Include(p => p.BancoHoras)
                .Where(p => p.Cargo != CargoEnum.Admin)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return View(colaboradores);
        }

        // ─── REGISTRAR ATESTADO (GET) ────────────────────────────────
        public async Task<IActionResult> RegistrarAtestado()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var colaboradores = await _context.Pessoas
                .Where(p => p.Cargo != CargoEnum.Admin)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            ViewBag.Colaboradores = colaboradores;
            return View();
        }

        // ─── REGISTRAR ATESTADO (POST) ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarAtestado(int PessoaId, DateTime DataAtestado, string Observacao)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            if (PessoaId <= 0)
            {
                TempData["Erro"] = "Selecione um colaborador.";
                return RedirectToAction("RegistrarAtestado");
            }

            var pessoa = await _context.Pessoas.FindAsync(PessoaId);
            if (pessoa == null)
            {
                TempData["Erro"] = "Colaborador não encontrado.";
                return RedirectToAction("RegistrarAtestado");
            }

            var dataUtc = DateTime.SpecifyKind(DataAtestado.Date, DateTimeKind.Utc);

            // Verifica se já existe registro nesse dia
            var existente = await _context.RegistrosPonto
                .FirstOrDefaultAsync(r => r.PessoaId == PessoaId && r.Data.Date == dataUtc.Date);

            if (existente != null)
            {
                TempData["Erro"] = $"Já existe um registro de ponto para {pessoa.Nome} no dia {DataAtestado:dd/MM/yyyy}.";
                return RedirectToAction("RegistrarAtestado");
            }

            // Define jornada por cargo
            double jornadaHoras = pessoa.Cargo == CargoEnum.Estagiario ? 6.0 : 8.0;

            // Cria o registro de atestado (conta como jornada completa)
            var registro = new RegistroPonto
            {
                Data = dataUtc,
                HoraEntrada = TimeSpan.Zero,
                HoraSaida = TimeSpan.FromHours(jornadaHoras),
                EhAtestado = true,
                ObservacaoAtestado = Observacao,
                PessoaId = PessoaId
            };

            _context.RegistrosPonto.Add(registro);

            // Atualiza banco de horas (atestado conta como jornada cumprida)
            var bancoHoras = await _context.BancosHoras
                .FirstOrDefaultAsync(b => b.PessoaId == PessoaId);

            if (bancoHoras == null)
            {
                bancoHoras = new BancoHoras
                {
                    HorasNormais = jornadaHoras,
                    HorasExtras = 0,
                    Saldo = 0,
                    PessoaId = PessoaId
                };
                _context.BancosHoras.Add(bancoHoras);
            }
            else
            {
                bancoHoras.HorasNormais += jornadaHoras;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"✓ Atestado registrado para {pessoa.Nome} em {DataAtestado:dd/MM/yyyy}.";
            return RedirectToAction("Admin");
        }

        // ─── EXPORTAR PDF (SOMENTE ADMIN) ────────────────────────────
        public async Task<IActionResult> ExportarPdf()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var colaboradores = await _context.Pessoas
                .Include(p => p.BancoHoras)
                .Where(p => p.Cargo != CargoEnum.Admin)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            inicioMes = DateTime.SpecifyKind(inicioMes, DateTimeKind.Utc);

            var atestadosPorPessoa = await _context.RegistrosPonto
                .Where(r => r.EhAtestado && r.Data >= inicioMes)
                .GroupBy(r => r.PessoaId)
                .Select(g => new { PessoaId = g.Key, Quantidade = g.Count() })
                .ToDictionaryAsync(x => x.PessoaId, x => x.Quantidade);

            var pdfBytes = GerarPdfBancoHoras(colaboradores, atestadosPorPessoa);

            // ⭐ REGISTRA NO HISTÓRICO DE RELATÓRIOS
            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var relatorio = new Relatorio
            {
                Tipo = "Banco de Horas",
                DataGeracao = DateTime.UtcNow,
                PessoaId = pessoaId
            };
            _context.Relatorios.Add(relatorio);
            await _context.SaveChangesAsync();

            var nomeArquivo = $"BancoHoras_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(pdfBytes, "application/pdf", nomeArquivo);
        }
        // ─── MÉTODO PRIVADO: Gera o PDF ──────────────────────────────
        private byte[] GerarPdfBancoHoras(List<Pessoa> colaboradores, Dictionary<int, int> atestados)
        {
            var totalHorasNormais = colaboradores.Sum(c => c.BancoHoras?.HorasNormais ?? 0);
            var totalHorasExtras = colaboradores.Sum(c => c.BancoHoras?.HorasExtras ?? 0);
            var totalSaldo = colaboradores.Sum(c => c.BancoHoras?.Saldo ?? 0);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // ─── CABEÇALHO ───
                    page.Header().Element(header =>
                    {
                        header.Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("⚡ O ELETRÔNICO")
                                        .FontSize(20).Bold().FontColor("#1a3a5c");
                                    c.Item().Text("Sistema de Gestão Empresarial")
                                        .FontSize(10).FontColor("#6b7280");
                                });

                                row.ConstantItem(120).AlignRight().Column(c =>
                                {
                                    c.Item().AlignRight().Text("RELATÓRIO")
                                        .FontSize(9).FontColor("#6b7280");
                                    c.Item().AlignRight().Text("Banco de Horas")
                                        .FontSize(13).Bold().FontColor("#1a3a5c");
                                });
                            });

                            col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#1a3a5c");
                        });
                    });

                    // ─── CONTEÚDO ───
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        // INFORMAÇÕES DO RELATÓRIO
                        col.Item().PaddingBottom(15).Background("#f3f4f6").Padding(12).Column(info =>
                        {
                            info.Item().Row(r =>
                            {
                                r.RelativeItem().Text(t =>
                                {
                                    t.Span("Gerado em: ").SemiBold();
                                    t.Span($"{DateTime.Now:dd/MM/yyyy 'às' HH:mm}");
                                });
                                r.RelativeItem().AlignRight().Text(t =>
                                {
                                    t.Span("Total de colaboradores: ").SemiBold();
                                    t.Span($"{colaboradores.Count}");
                                });
                            });
                        });

                        // CARDS DE RESUMO
                        col.Item().PaddingBottom(15).Row(row =>
                        {
                            row.RelativeItem().Padding(2).Background("#dbeafe").Padding(10).Column(c =>
                            {
                                c.Item().Text("HORAS NORMAIS").FontSize(8).Bold().FontColor("#1e40af");
                                c.Item().PaddingTop(4).Text($"{totalHorasNormais:F2}h").FontSize(16).Bold().FontColor("#2563eb");
                            });

                            row.RelativeItem().Padding(2).Background("#fef3c7").Padding(10).Column(c =>
                            {
                                c.Item().Text("HORAS EXTRAS").FontSize(8).Bold().FontColor("#92400e");
                                c.Item().PaddingTop(4).Text($"{totalHorasExtras:F2}h").FontSize(16).Bold().FontColor("#d97706");
                            });

                            row.RelativeItem().Padding(2).Background(totalSaldo >= 0 ? "#dcfce7" : "#fee2e2").Padding(10).Column(c =>
                            {
                                c.Item().Text("SALDO TOTAL").FontSize(8).Bold().FontColor(totalSaldo >= 0 ? "#166534" : "#991b1b");
                                c.Item().PaddingTop(4).Text($"{(totalSaldo >= 0 ? "+" : "")}{totalSaldo:F2}h").FontSize(16).Bold().FontColor(totalSaldo >= 0 ? "#16a34a" : "#dc2626");
                            });
                        });

                        // TÍTULO TABELA
                        col.Item().PaddingBottom(8).Text("Detalhamento por Colaborador")
                            .FontSize(13).Bold().FontColor("#1a3a5c");

                        // TABELA DE COLABORADORES
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3); // Nome
                                c.RelativeColumn(1.5f); // Cargo
                                c.RelativeColumn(1); // Jornada
                                c.RelativeColumn(1.5f); // Setor
                                c.RelativeColumn(1.3f); // H. Normais
                                c.RelativeColumn(1.3f); // H. Extras
                                c.RelativeColumn(1.3f); // Saldo
                                c.RelativeColumn(1); // Atestados
                            });

                            // CABEÇALHO
                            table.Header(header =>
                            {
                                header.Cell().Background("#1a3a5c").Padding(8).Text("Colaborador").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).Text("Cargo").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).Text("Jornada").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).Text("Setor").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).AlignRight().Text("H. Normais").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).AlignRight().Text("H. Extras").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).AlignRight().Text("Saldo").FontColor(Colors.White).Bold().FontSize(9);
                                header.Cell().Background("#1a3a5c").Padding(8).AlignCenter().Text("Atestados").FontColor(Colors.White).Bold().FontSize(9);
                            });

                            // LINHAS
                            int contador = 0;
                            foreach (var p in colaboradores)
                            {
                                var bg = contador % 2 == 0 ? "#ffffff" : "#f9fafb";
                                var hNormais = p.BancoHoras?.HorasNormais ?? 0;
                                var hExtras = p.BancoHoras?.HorasExtras ?? 0;
                                var saldo = p.BancoHoras?.Saldo ?? 0;
                                var corSaldo = saldo > 0 ? "#16a34a" : (saldo < 0 ? "#dc2626" : "#6b7280");
                                var sinalSaldo = saldo > 0 ? "+" : "";
                                var qtdAtestados = atestados.ContainsKey(p.Id) ? atestados[p.Id] : 0;
                                var jornada = p.Cargo == CargoEnum.Estagiario ? "6h/dia" : "8h/dia";
                                var cargoLabel = p.Cargo == CargoEnum.Estagiario ? "Estagiário" : "CLT";

                                table.Cell().Background(bg).Padding(7).Column(c =>
                                {
                                    c.Item().Text(p.Nome).SemiBold().FontSize(9);
                                    c.Item().Text(p.Email).FontSize(7).FontColor("#6b7280");
                                });
                                table.Cell().Background(bg).Padding(7).AlignMiddle().Text(cargoLabel).FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignMiddle().Text(jornada).FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignMiddle().Text(p.Setor).FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignRight().AlignMiddle().Text($"{hNormais:F2}h").FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignRight().AlignMiddle().Text($"{hExtras:F2}h").FontColor("#d97706").SemiBold().FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignRight().AlignMiddle().Text($"{sinalSaldo}{saldo:F2}h").FontColor(corSaldo).Bold().FontSize(9);
                                table.Cell().Background(bg).Padding(7).AlignCenter().AlignMiddle().Text($"{qtdAtestados}").FontSize(9);

                                contador++;
                            }

                            // TOTAIS
                            table.Cell().ColumnSpan(4).Background("#e8f0fb").Padding(8).Text("TOTAIS:").Bold().FontColor("#1a3a5c");
                            table.Cell().Background("#e8f0fb").Padding(8).AlignRight().Text($"{totalHorasNormais:F2}h").Bold().FontColor("#1a3a5c");
                            table.Cell().Background("#e8f0fb").Padding(8).AlignRight().Text($"{totalHorasExtras:F2}h").Bold().FontColor("#d97706");
                            table.Cell().Background("#e8f0fb").Padding(8).AlignRight().Text($"{(totalSaldo >= 0 ? "+" : "")}{totalSaldo:F2}h").Bold().FontColor(totalSaldo >= 0 ? "#16a34a" : "#dc2626");
                            table.Cell().Background("#e8f0fb").Padding(8).AlignCenter().Text($"{atestados.Values.Sum()}").Bold();
                        });

                        // LEGENDA
                        col.Item().PaddingTop(15).Background("#fef3c7").Padding(10).Column(legend =>
                        {
                            legend.Item().Text("ℹ Informações importantes:").FontSize(9).Bold().FontColor("#92400e");
                            legend.Item().PaddingTop(4).Text("• Jornada CLT: 8h/dia (1h de almoço descontada)").FontSize(8).FontColor("#92400e");
                            legend.Item().Text("• Jornada Estagiário: 6h/dia (sem intervalo de almoço)").FontSize(8).FontColor("#92400e");
                            legend.Item().Text("• Atestados contam como jornada cumprida (não afetam o saldo negativamente)").FontSize(8).FontColor("#92400e");
                        });
                    });

                    // ─── RODAPÉ ───
                    page.Footer().Element(footer =>
                    {
                        footer.Column(col =>
                        {
                            col.Item().LineHorizontal(0.5f).LineColor("#e5e7eb");
                            col.Item().PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text("O Eletrônico — Sistema de Gestão Empresarial").FontSize(8).FontColor("#9ca3af");
                                r.RelativeItem().AlignRight().Text(t =>
                                {
                                    t.Span("Página ").FontSize(8).FontColor("#9ca3af");
                                    t.CurrentPageNumber().FontSize(8).FontColor("#9ca3af");
                                    t.Span(" de ").FontSize(8).FontColor("#9ca3af");
                                    t.TotalPages().FontSize(8).FontColor("#9ca3af");
                                });
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}