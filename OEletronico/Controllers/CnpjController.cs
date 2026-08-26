using Microsoft.AspNetCore.Mvc;

namespace OEletronico.Controllers
{
    public class CnpjController : Controller
    {

           private bool EstaLogado() =>
    HttpContext.Session.GetString("UserId") != null;

        private bool EhAdmin() =>
            HttpContext.Session.GetString("UserCargo") == "Admin";
    }

}

