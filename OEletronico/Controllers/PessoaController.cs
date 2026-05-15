using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.DTOs;
using OEletronico.Models.Enums;

namespace OEletronico.Controllers
{
    public class PessoaController : Controller
    {
        private readonly AppDbContext _context;

        public PessoaController(AppDbContext context)
        {
            _context = context;
        }

        // ─── MÉTODOS AUXILIARES DE SEGURANÇA ──────────────────────────
        private bool EstaLogado() =>
            HttpContext.Session.GetString("UserId") != null;

        private bool EhAdmin() =>
            HttpContext.Session.GetString("UserCargo") == "Admin";

        private IActionResult RedirecionarLogin() =>
            RedirectToAction("Login", "Auth");

        // ─── INDEX: Lista de colaboradores (somente admin) ────────────
        public async Task<IActionResult> Index()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var pessoas = await _context.Pessoas
                .Include(p => p.Usuario)
                .Where(p => p.Cargo != CargoEnum.Admin)
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return View(pessoas);
        }

        // ─── CREATE (GET): Tela de cadastro ───────────────────────────
        public IActionResult Create()
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            return View();
        }

        // ─── CREATE (POST): Salva novo colaborador ────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PessoaDTO dto)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();
            if (!ModelState.IsValid) return View(dto);

            // Verifica se login já existe
            if (await _context.Usuarios.AnyAsync(u => u.Login == dto.Login))
            {
                ModelState.AddModelError("Login", "Este login já está em uso.");
                return View(dto);
            }

            // Verifica se email já existe
            if (await _context.Pessoas.AnyAsync(p => p.Email == dto.Email))
            {
                ModelState.AddModelError("Email", "Este email já está cadastrado.");
                return View(dto);
            }

            // Cria a Pessoa
            var pessoa = new Pessoa
            {
                Nome = dto.Nome,
                Email = dto.Email,
                Cargo = Enum.Parse<CargoEnum>(dto.Cargo),
                Setor = dto.Setor,
                DataAdmissao = DateTime.SpecifyKind(dto.DataAdmissao, DateTimeKind.Utc)
            };
            _context.Pessoas.Add(pessoa);
            await _context.SaveChangesAsync();

            // Cria o Usuário vinculado
            var usuario = new Usuario
            {
                Login = dto.Login,
                Senha = BCrypt.Net.BCrypt.HashPassword(dto.Senha),
                PessoaId = pessoa.Id
            };
            _context.Usuarios.Add(usuario);

            // Cria o Banco de Horas zerado
            var banco = new BancoHoras
            {
                HorasNormais = 0,
                HorasExtras = 0,
                Saldo = 0,
                PessoaId = pessoa.Id
            };
            _context.BancosHoras.Add(banco);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Colaborador {pessoa.Nome} cadastrado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── EDIT (GET): Formulário de edição ──────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var pessoa = await _context.Pessoas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pessoa == null) return NotFound();
            return View(pessoa);
        }

        // ─── EDIT (POST): Salva alterações ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Pessoa model, string? NovaSenha)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var pessoa = await _context.Pessoas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pessoa == null) return NotFound();

            pessoa.Nome = model.Nome;
            pessoa.Email = model.Email;
            pessoa.Cargo = model.Cargo;
            pessoa.Setor = model.Setor;
            pessoa.DataAdmissao = DateTime.SpecifyKind(model.DataAdmissao, DateTimeKind.Utc);

            // Se foi informada nova senha, atualiza
            if (!string.IsNullOrEmpty(NovaSenha) && pessoa.Usuario != null)
                pessoa.Usuario.Senha = BCrypt.Net.BCrypt.HashPassword(NovaSenha);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Colaborador {pessoa.Nome} atualizado com sucesso!";
            return RedirectToAction("Index");
        }

        // ─── DELETE (POST): Exclui colaborador ────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!EstaLogado()) return RedirecionarLogin();
            if (!EhAdmin()) return Forbid();

            var pessoa = await _context.Pessoas
                .Include(p => p.Usuario)
                .Include(p => p.BancoHoras)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pessoa == null) return NotFound();

            if (pessoa.Usuario != null)
                _context.Usuarios.Remove(pessoa.Usuario);

            if (pessoa.BancoHoras != null)
                _context.BancosHoras.Remove(pessoa.BancoHoras);

            _context.Pessoas.Remove(pessoa);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Colaborador {pessoa.Nome} excluído com sucesso!";
            return RedirectToAction("Index");
        }
    
    // ─── PERFIL (GET): Visualizar e editar próprio perfil ──────────
public async Task<IActionResult> Perfil()
        {
            if (!EstaLogado()) return RedirecionarLogin();

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);

            var pessoa = await _context.Pessoas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == pessoaId);

            if (pessoa == null) return NotFound();
            return View(pessoa);
        }

        // ─── PERFIL (POST): Salva alterações do próprio perfil ─────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Perfil(Pessoa model, string? SenhaAtual, string? NovaSenha)
        {
            if (!EstaLogado()) return RedirecionarLogin();

            var pessoaId = int.Parse(HttpContext.Session.GetString("UserPessoaId")!);

            var pessoa = await _context.Pessoas
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == pessoaId);

            if (pessoa == null) return NotFound();

            // Atualiza dados básicos
            pessoa.Nome = model.Nome;
            pessoa.Email = model.Email;
            pessoa.Setor = model.Setor;

            // Atualiza sessão com novo nome/iniciais
            HttpContext.Session.SetString("UserName", pessoa.Nome);
            var iniciais = string.Join("",
                pessoa.Nome.Split(' ').Take(2).Select(p => p[0])).ToUpper();
            HttpContext.Session.SetString("UserInitials", iniciais);

            // Alteração de senha (opcional)
            if (!string.IsNullOrEmpty(NovaSenha) && pessoa.Usuario != null)
            {
                if (string.IsNullOrEmpty(SenhaAtual) ||
                    !BCrypt.Net.BCrypt.Verify(SenhaAtual, pessoa.Usuario.Senha))
                {
                    TempData["Erro"] = "Senha atual incorreta.";
                    return View(pessoa);
                }
                pessoa.Usuario.Senha = BCrypt.Net.BCrypt.HashPassword(NovaSenha);
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Perfil atualizado com sucesso!";
            return RedirectToAction("Perfil");
        }
    }
}