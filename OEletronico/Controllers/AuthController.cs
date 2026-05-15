using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OEletronico.Models;
using OEletronico.Models.Data;
using OEletronico.Models.DTOs;
using OEletronico.Models.Enums;

namespace OEletronico.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ─── TELA DE LOGIN (GET) ───────────────────────────────────────
        [HttpGet]
        public IActionResult Login()
        {
            // Se já está logado, redireciona
            if (HttpContext.Session.GetString("UserId") != null)
                return RedirectToAction("Index", "Pessoa");

            return View();
        }

        // ─── FAZER LOGIN (POST) ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            // Busca o usuário com sua pessoa associada
            var usuario = await _context.Usuarios
                .Include(u => u.Pessoa)
                .FirstOrDefaultAsync(u => u.Login == dto.Login);

            // Valida senha com BCrypt
            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.Senha))
            {
                ViewBag.Erro = "Login ou senha incorretos.";
                return View(dto);
            }

            // Gera o token JWT
            var token = GerarToken(usuario);

            // Guarda dados na sessão
            HttpContext.Session.SetString("UserId", usuario.Id.ToString());
            HttpContext.Session.SetString("UserName", usuario.Pessoa.Nome);
            HttpContext.Session.SetString("UserCargo", usuario.Pessoa.Cargo.ToString());
            HttpContext.Session.SetString("UserPessoaId", usuario.PessoaId.ToString());
            HttpContext.Session.SetString("JwtToken", token);

            // Gera iniciais para o avatar
            var iniciais = string.Join("",
                usuario.Pessoa.Nome.Split(' ').Take(2).Select(p => p[0])).ToUpper();
            HttpContext.Session.SetString("UserInitials", iniciais);

            // Admin vai para a lista de colaboradores
            if (usuario.Pessoa.Cargo == CargoEnum.Admin)
                return RedirectToAction("Index", "Pessoa");

            // Colaboradores (CLT/Estagiário) vão para o Perfil
            return RedirectToAction("Perfil", "Pessoa");
        }

        // ─── LOGOUT ────────────────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ─── MÉTODO PRIVADO PARA GERAR JWT ─────────────────────────────
        private string GerarToken(Usuario usuario)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Login),
                new Claim(ClaimTypes.Role, usuario.Pessoa.Cargo.ToString()),
                new Claim("PessoaId", usuario.PessoaId.ToString()),
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        // 🔧 MÉTODO TEMPORÁRIO — APAGAR DEPOIS DE USAR
        [HttpGet]
        public IActionResult GerarHash(string senha)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(senha);
            return Content(hash);
        }
    }
}