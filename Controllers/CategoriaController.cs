using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class CategoriaController : Controller
    {

        public IActionResult Index()
        {
            return View(Categoria.ObtenerTodas(false));
        }

        public IActionResult Create()
        {
            return View("Form", new Categoria());
        }

        [HttpPost]
        public IActionResult Create(Categoria categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
                ModelState.AddModelError("", "El nombre es obligatorio.");
            else if (Categoria.ExisteNombre(categoria.Nombre.Trim(), 0))
                ModelState.AddModelError("", "Ya existe una categoría con ese nombre.");

            if (!ModelState.IsValid) return View("Form", categoria);

            categoria.Nombre = categoria.Nombre.Trim();
            categoria.Guardar();
            TempData["Ok"] = "Categoría registrada.";
            return RedirectToAction("Index");
        }

        public IActionResult Edit(long id)
        {
            var categoria = Categoria.ObtenerPorId(id);
            if (categoria == null) return RedirectToAction("Index");
            return View("Form", categoria);
        }

        [HttpPost]
        public IActionResult Edit(Categoria categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
                ModelState.AddModelError("", "El nombre es obligatorio.");
            else if (Categoria.ExisteNombre(categoria.Nombre.Trim(), categoria.Id))
                ModelState.AddModelError("", "Ya existe una categoría con ese nombre.");

            if (!ModelState.IsValid) return View("Form", categoria);

            categoria.Nombre = categoria.Nombre.Trim();
            categoria.Actualizar();
            TempData["Ok"] = "Categoría actualizada.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(long id)
        {
            if (Categoria.Eliminar(id)) TempData["Ok"] = "Categoría eliminada.";
            else TempData["Error"] = "No se puede eliminar: la categoría tiene platos. Puedes desactivarla editándola.";
            return RedirectToAction("Index");
        }
    }
}
