using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OEletronico.Models.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ───────────────────────────────────────────────────────────
// CRIA/RESETA O ADMIN PADRÃO NA INICIALIZAÇÃO
// ───────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OEletronico.Models.Data.AppDbContext>();

    context.Database.EnsureCreated();

    var adminUser = context.Usuarios.FirstOrDefault(u => u.Login == "admin");

    if (adminUser == null)
    {
        var pessoaAdmin = new OEletronico.Models.Pessoa
        {
            Nome = "Administrador",
            Email = "admin@oeletronico.com",
            Cargo = OEletronico.Models.Enums.CargoEnum.Admin,
            Setor = "Administração",
            DataAdmissao = DateTime.SpecifyKind(new DateTime(2024, 1, 1), DateTimeKind.Utc)
        };
        context.Pessoas.Add(pessoaAdmin);
        context.SaveChanges();

        var novoAdmin = new OEletronico.Models.Usuario
        {
            Login = "admin",
            Senha = BCrypt.Net.BCrypt.HashPassword("admin123"),
            PessoaId = pessoaAdmin.Id
        };
        context.Usuarios.Add(novoAdmin);
        context.SaveChanges();
    }
    else
    {
        adminUser.Senha = BCrypt.Net.BCrypt.HashPassword("admin123");
        context.SaveChanges();
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();