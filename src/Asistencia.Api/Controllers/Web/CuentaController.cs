using System.Globalization;
using System.Security.Claims;
using Asistencia.Api.Servicios;
using Asistencia.Api.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistencia.Api.Controllers.Web;

[AllowAnonymous]
public sealed class CuentaController(AuthService auth) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        var u = await auth.ValidarDocenteAsync(m.Usuario, m.Password);
        if (u is null)
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            return View(m);
        }

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, u.UsuarioId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, u.NombreCompleto),
            new Claim(ClaimTypes.Role, u.Rol)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
        return Url.IsLocalUrl(m.ReturnUrl) ? LocalRedirect(m.ReturnUrl!) : RedirectToAction("Index", "Cursos");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Error() => Content("Ocurrió un error. Intenta nuevamente.");
}
