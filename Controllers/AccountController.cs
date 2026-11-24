using System.Security.Claims;
using ElBuenSabor.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElBuenSabor.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public IActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string returnUrl)
        {
            var usuario = Usuario.Validar((email ?? "").Trim(), password);
            if (usuario == null)
            {
                ViewBag.Error = "Correo o contraseña incorrectos, o usuario inactivo.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NombreCompleto),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol)
            };
            var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            switch (usuario.Rol)
            {
                case "MESERO": return RedirectToAction("Index", "Mesa");
                case "COCINERO": return RedirectToAction("Index", "Cocina");
                case "CAJERO": return RedirectToAction("Index", "Caja");
                default: return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult AccesoDenegado()
        {
            return View();
        }
    }
}
