using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OEletronico.Models.Data;


QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

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
            DataAdmissao = new DateTime(2024, 1, 1)
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

    // ⭐ NOVO — CNPJs padrão (só insere os que ainda não existem)
    try
    {
        var cnpjsPadrao = new[]
        {
            new OEletronico.Models.Cnpj { Numero = "44.281.097/0001-23", RazaoSocial = "Duartec",      NomeFantasia = "Duartec" },
            new OEletronico.Models.Cnpj { Numero = "41.921.281/0001-00", RazaoSocial = "Ollintek",     NomeFantasia = "Ollintek" },
            new OEletronico.Models.Cnpj { Numero = "40.316.508/0001-18", RazaoSocial = "O Eletronico", NomeFantasia = "O Eletronico" },
            new OEletronico.Models.Cnpj { Numero = "61.301.782/0001-93", RazaoSocial = "Drive Shop",   NomeFantasia = "Drive Shop" }
        };

        foreach (var cnpj in cnpjsPadrao)
        {
            if (!context.Cnpjs.Any(c => c.Numero == cnpj.Numero))
                context.Cnpjs.Add(cnpj);
        }
        context.SaveChanges();
    }
    catch (Exception ex)
    {
        // Se a tabela Cnpjs ainda não existir, o sistema continua abrindo normalmente
        Console.WriteLine($"[AVISO] CNPJs padrão não foram inseridos: {ex.Message}");
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