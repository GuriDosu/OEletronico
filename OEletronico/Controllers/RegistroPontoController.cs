using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.Enums;

namespace OEletronico.Controllers
{
    public class RegistroPontoController : Controller
    {
        private readonly AppDbContext _context;

        public RegistroPontoController(AppDbContext context)
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

        // ─── INDEX: Tela de bater ponto (só colaboradores) ──────────
        public async Task<IActionResult> Index()
        {
            if (!EstaLogado()) return RedirecionarLogin();

            // Admin não bate ponto
            if (EhAdmin())
            {
                TempData["Erro"] = "Administradores não batem ponto.";
                return RedirectToAction("Index", "Pessoa");
            }

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var hoje = DateTime.UtcNow.Date;

            // Busca o registro de HOJE (se existir)
            var pontoHoje = await _context.RegistrosPonto
                .Where(r => r.PessoaId == pessoaId &&
                            r.Data.Date == hoje)
                .OrderByDescending(r => r.HoraEntrada)
                .FirstOrDefaultAsync();

            // Histórico dos últimos 30 dias
            var dataLimite = DateTime.UtcNow.AddDays(-30);
            var historico = await _context.RegistrosPonto
                .Where(r => r.PessoaId == pessoaId && r.Data >= dataLimite)
                .OrderByDescending(r => r.Data)
                .Take(30)
                .ToListAsync();

            ViewBag.PontoHoje = pontoHoje;
            ViewBag.Historico = historico;

            return View();
        }

        // ─── BATER ENTRADA ──────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BaterEntrada()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (EhAdmin()) return Forbid();

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var hoje = DateTime.UtcNow.Date;

            // Verifica se já bateu ponto hoje
            var pontoHoje = await _context.RegistrosPonto
                .FirstOrDefaultAsync(r => r.PessoaId == pessoaId && r.Data.Date == hoje);

            if (pontoHoje != null)
            {
                TempData["Erro"] = "Você já bateu entrada hoje!";
                return RedirectToAction("Index");
            }

            // Registra a entrada
            var agora = DateTime.UtcNow;
            var ponto = new RegistroPonto
            {
                Data = DateTime.SpecifyKind(hoje, DateTimeKind.Utc),
                HoraEntrada = agora.TimeOfDay,
                HoraSaida = null,
                PessoaId = pessoaId
            };

            _context.RegistrosPonto.Add(ponto);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"✓ Entrada registrada às {agora.ToLocalTime():HH:mm}";
            return RedirectToAction("Index");
        }

        // ─── BATER SAÍDA ────────────────────────────────────────────
        // ─── BATER SAÍDA ────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BaterSaida()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (EhAdmin()) return Forbid();

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);
            var hoje = DateTime.UtcNow.Date;

            // Busca o ponto de hoje COM a pessoa pra saber o cargo
            var ponto = await _context.RegistrosPonto
                .Include(r => r.Pessoa)
                .FirstOrDefaultAsync(r => r.PessoaId == pessoaId && r.Data.Date == hoje);

            if (ponto == null)
            {
                TempData["Erro"] = "Você precisa bater entrada primeiro!";
                return RedirectToAction("Index");
            }

            if (ponto.HoraSaida.HasValue)
            {
                TempData["Erro"] = "Você já bateu saída hoje!";
                return RedirectToAction("Index");
            }

            // Registra a saída
            var agora = DateTime.UtcNow;
            ponto.HoraSaida = agora.TimeOfDay;

            // ⭐ DEFINIR JORNADA E ALMOÇO POR CARGO
            double jornadaPadrao;
            double horasAlmoco;

            if (ponto.Pessoa.Cargo == CargoEnum.Estagiario)
            {
                jornadaPadrao = 6.0;   // Estagiário: 6h
                horasAlmoco = 0.0;     // Sem almoço descontado
            }
            else // CLT
            {
                jornadaPadrao = 8.0;   // CLT: 8h
                horasAlmoco = 1.0;     // 1h de almoço descontada
            }

            // Calcula horas líquidas
            var horasTrabalhadas = (ponto.HoraSaida.Value - ponto.HoraEntrada).TotalHours;
            var horasLiquidas = horasTrabalhadas - horasAlmoco;

            // Atualiza banco de horas
            var bancoHoras = await _context.BancosHoras
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
                _context.BancosHoras.Add(bancoHoras);
            }

            if (horasLiquidas > 0)
            {
                bancoHoras.HorasNormais += Math.Min(horasLiquidas, jornadaPadrao);

                if (horasLiquidas > jornadaPadrao)
                    bancoHoras.HorasExtras += (horasLiquidas - jornadaPadrao);

                bancoHoras.Saldo += (horasLiquidas - jornadaPadrao);
            }

            await _context.SaveChangesAsync();

            var cargoLabel = ponto.Pessoa.Cargo == CargoEnum.Estagiario ? "Estagiário (6h)" : "CLT (8h)";
            TempData["Sucesso"] = $"✓ Saída registrada às {agora.ToLocalTime():HH:mm} | Trabalhou {horasLiquidas:F2}h líquidas ({cargoLabel})";
            return RedirectToAction("Index");
        }
    }
}