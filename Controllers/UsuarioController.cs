using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UsuarioController : Controller
    {

        public IActionResult Index(string buscar, string rol)
        {
            ViewBag.Buscar = buscar;
            ViewBag.Rol = rol;
            return View(Usuario.ObtenerTodos(buscar, rol));
        }

        public IActionResult Create()
        {
            return View("Form", new Usuario());
        }

        [HttpPost]
        public IActionResult Create(Usuario usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario.Nombre) || string.IsNullOrWhiteSpace(usuario.Apellido) || string.IsNullOrWhiteSpace(usuario.Email))
                ModelState.AddModelError("", "Nombre, apellido y correo son obligatorios.");
            else if (string.IsNullOrWhiteSpace(usuario.Password) || usuario.Password.Length < 6)
                ModelState.AddModelError("", "La contraseña es obligatoria (mínimo 6 caracteres).");
            else if (Usuario.ExisteEmail(usuario.Email.Trim(), 0))
                ModelState.AddModelError("", "Ya existe un usuario con ese correo.");

            if (!ModelState.IsValid) return View("Form", usuario);

            usuario.Email = usuario.Email.Trim();
            usuario.Guardar();
            TempData["Ok"] = "Usuario registrado correctamente.";
            return RedirectToAction("Index");
        }

        public IActionResult Edit(long id)
        {
            var usuario = Usuario.ObtenerPorId(id);
            if (usuario == null) return RedirectToAction("Index");
            return View("Form", usuario);
        }

        [HttpPost]
        public IActionResult Edit(Usuario usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario.Nombre) || string.IsNullOrWhiteSpace(usuario.Apellido) || string.IsNullOrWhiteSpace(usuario.Email))
                ModelState.AddModelError("", "Nombre, apellido y correo son obligatorios.");
            else if (!string.IsNullOrEmpty(usuario.Password) && usuario.Password.Length < 6)
                ModelState.AddModelError("", "La nueva contraseña debe tener mínimo 6 caracteres.");
            else if (Usuario.ExisteEmail(usuario.Email.Trim(), usuario.Id))
                ModelState.AddModelError("", "Ya existe un usuario con ese correo.");

            if (!ModelState.IsValid) return View("Form", usuario);

            usuario.Email = usuario.Email.Trim();
            usuario.Actualizar();
            TempData["Ok"] = "Usuario actualizado correctamente.";
            return RedirectToAction("Index");
        }

        public IActionResult CambiarEstado(long id)
        {
            Usuario.CambiarEstado(id);
            return RedirectToAction("Index");
        }
    }
}
