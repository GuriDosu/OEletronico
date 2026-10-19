using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.Enums;

namespace OEletronico.Controllers
{
    public class CnpjController : Controller
    {
        private readonly AppDbContext _context;

        public CnpjController(AppDbContext context)
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

        // ─── Formata para 00.000.000/0000-00 (null se não tiver 14 dígitos) ──
        private static string? FormatarCnpj(string? valor)
        {
            var d = new string((valor ?? "").Where(char.IsDigit).ToArray());
            if (d.Length != 14) return null;
            return $"{d.Substring(0, 2)}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
        }

        // ─── INDEX: lista os CNPJs (só Admin) ─────────────────────────
        public async Task<IActionResult> Index(string? busca)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var query = _context.Cnpjs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                query = query.Where(c =>
                    c.Numero.ToLower().Contains(termo) ||
                    c.RazaoSocial.ToLower().Contains(termo) ||
                    (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(termo)));
            }

            var cnpjs = await query
                .OrderByDescending(c => c.Ativo)
                .ThenBy(c => c.RazaoSocial)
                .ToListAsync();

            // Quantos colaboradores cada CNPJ tem
            var vinculos = await _context.Pessoas
                .Where(p => p.CnpjId != null)
                .GroupBy(p => p.CnpjId)
                .Select(g => new { CnpjId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.CnpjId!.Value, g => g.Total);

            ViewBag.Vinculos = vinculos;
            ViewBag.Busca = busca;

            return View(cnpjs);
        }

        // ─── CREATE (GET) ─────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            return View();
        }

        // ─── CREATE (POST) ────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cnpj model)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(model);

            var numero = FormatarCnpj(model.Numero);
            if (numero == null)
            {
                ModelState.AddModelError("Numero", "Informe um CNPJ com 14 dígitos.");
                return View(model);
            }

            if (await _context.Cnpjs.AnyAsync(c => c.Numero == numero))
            {
                ModelState.AddModelError("Numero", "Este CNPJ já está cadastrado.");
                return View(model);
            }

            var cnpj = new Cnpj
            {
                Numero = numero,
                RazaoSocial = model.RazaoSocial.Trim(),
                NomeFantasia = string.IsNullOrWhiteSpace(model.NomeFantasia) ? null : model.NomeFantasia.Trim(),
                Ativo = true,
                DataCadastro = DateTime.Now
            };

            _context.Cnpjs.Add(cnpj);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"CNPJ \"{cnpj.RazaoSocial}\" cadastrado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── EDIT (GET) ───────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var cnpj = await _context.Cnpjs.FindAsync(id);
            if (cnpj == null) return NotFound();

            return View(cnpj);
        }

        // ─── EDIT (POST) ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cnpj model)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(model);

            var cnpj = await _context.Cnpjs.FindAsync(id);
            if (cnpj == null) return NotFound();

            var numero = FormatarCnpj(model.Numero);
            if (numero == null)
            {
                ModelState.AddModelError("Numero", "Informe um CNPJ com 14 dígitos.");
                return View(model);
            }

            if (await _context.Cnpjs.AnyAsync(c => c.Numero == numero && c.Id != id))
            {
                ModelState.AddModelError("Numero", "Já existe outro CNPJ com este número.");
                return View(model);
            }

            cnpj.Numero = numero;
            cnpj.RazaoSocial = model.RazaoSocial.Trim();
            cnpj.NomeFantasia = string.IsNullOrWhiteSpace(model.NomeFantasia) ? null : model.NomeFantasia.Trim();
            cnpj.Ativo = model.Ativo;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"CNPJ \"{cnpj.RazaoSocial}\" atualizado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── ATIVAR / DESATIVAR ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarStatus(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var cnpj = await _context.Cnpjs.FindAsync(id);
            if (cnpj == null) return NotFound();

            cnpj.Ativo = !cnpj.Ativo;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"CNPJ \"{cnpj.RazaoSocial}\" {(cnpj.Ativo ? "ativado" : "desativado")}!";
            return RedirectToAction("Index");
        }

        // ─── DELETE ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var cnpj = await _context.Cnpjs.FindAsync(id);
            if (cnpj == null) return NotFound();

            if (await _context.Pessoas.AnyAsync(p => p.CnpjId == id))
            {
                TempData["Erro"] = $"O CNPJ \"{cnpj.RazaoSocial}\" não pode ser excluído porque há colaboradores vinculados a ele.";
                return RedirectToAction("Index");
            }

            _context.Cnpjs.Remove(cnpj);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "CNPJ excluído com sucesso!";
            return RedirectToAction("Index");
        }

        // ═══════════════════════════════════════════════════════════
        // VÍNCULOS — o Admin escolhe o CNPJ de cada colaborador
        // ═══════════════════════════════════════════════════════════

        public async Task<IActionResult> Vinculos()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var colaboradores = await _context.Pessoas
                .Include(p => p.Cnpj)
                .Where(p => p.Cargo != CargoEnum.Admin)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            ViewBag.Cnpjs = await _context.Cnpjs
                .Where(c => c.Ativo)
                .OrderBy(c => c.RazaoSocial)
                .ToListAsync();

            return View(colaboradores);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarVinculo(int pessoaId, int? cnpjId)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var pessoa = await _context.Pessoas.FindAsync(pessoaId);
            if (pessoa == null)
            {
                TempData["Erro"] = "Colaborador não encontrado.";
                return RedirectToAction("Vinculos");
            }

            // Regra: o Admin não recebe CNPJ
            if (pessoa.Cargo == CargoEnum.Admin)
            {
                TempData["Erro"] = "Administradores não são vinculados a CNPJ.";
                return RedirectToAction("Vinculos");
            }

            string? razao = null;
            if (cnpjId.HasValue)
            {
                razao = await _context.Cnpjs
                    .Where(c => c.Id == cnpjId.Value)
                    .Select(c => c.RazaoSocial)
                    .FirstOrDefaultAsync();

                if (razao == null)
                {
                    TempData["Erro"] = "O CNPJ selecionado não existe.";
                    return RedirectToAction("Vinculos");
                }
            }

            pessoa.CnpjId = cnpjId;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = razao != null
                ? $"{pessoa.Nome} agora está vinculado(a) ao CNPJ \"{razao}\"."
                : $"O vínculo de CNPJ de {pessoa.Nome} foi removido.";

            return RedirectToAction("Vinculos");
        }
    }
}