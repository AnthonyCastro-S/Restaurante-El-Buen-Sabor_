using ElBuenSabor.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class InventarioController : Controller
    {
        public IActionResult Index(string buscar = "")
        {
            ViewBag.Buscar = buscar;
            ViewBag.Movimientos = Insumo.ObtenerMovimientos(30);
            return View(Insumo.ObtenerTodos(buscar));
        }

        public IActionResult Create() => View("Form", new Insumo());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Insumo insumo)
        {
            Validar(insumo);
            if (!ModelState.IsValid) return View("Form", insumo);
            if (Insumo.ObtenerTodos("", true).Any(x => x.Nombre.Equals(insumo.Nombre?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Nombre", "Ya existe un insumo con ese nombre.");
                return View("Form", insumo);
            }
            insumo.Guardar();
            TempData["Ok"] = "Insumo registrado correctamente.";
            return RedirectToAction("Index");
        }

        public IActionResult Edit(long id)
        {
            var insumo = Insumo.ObtenerPorId(id);
            return insumo == null ? RedirectToAction("Index") : View("Form", insumo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Insumo insumo)
        {
            Validar(insumo);
            if (!ModelState.IsValid) return View("Form", insumo);
            insumo.Actualizar();
            TempData["Ok"] = "Insumo actualizado.";
            return RedirectToAction("Index");
        }

        public IActionResult Movimiento(long id)
        {
            var insumo = Insumo.ObtenerPorId(id);
            if (insumo == null) return RedirectToAction("Index");
            return View(insumo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Movimiento(long id, string tipo, decimal cantidad, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 255)
                ModelState.AddModelError("", "Ingresa un motivo de hasta 255 caracteres.");
            string error = "";
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long usuarioId);
            bool registrado = ModelState.IsValid && Insumo.RegistrarMovimiento(id, tipo, cantidad, motivo?.Trim(), User.Identity?.Name, usuarioId, out error);
            if (registrado)
            {
                TempData["Ok"] = "Movimiento registrado y stock actualizado.";
                return RedirectToAction("Index");
            }
            if (ModelState.IsValid) ModelState.AddModelError("", error);
            var insumo = Insumo.ObtenerPorId(id);
            return View(insumo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(long id)
        {
            var insumo = Insumo.ObtenerPorId(id);
            if (insumo != null)
            {
                insumo.Activo = !insumo.Activo;
                insumo.Actualizar();
                TempData["Ok"] = insumo.Activo ? "Insumo activado." : "Insumo desactivado.";
            }
            return RedirectToAction("Index");
        }

        private void Validar(Insumo insumo)
        {
            if (string.IsNullOrWhiteSpace(insumo.Nombre)) ModelState.AddModelError("Nombre", "El nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(insumo.Unidad)) ModelState.AddModelError("Unidad", "La unidad es obligatoria.");
            if (insumo.StockActual < 0 || insumo.StockMinimo < 0 || insumo.CostoUnitario < 0)
                ModelState.AddModelError("", "El stock y el costo no pueden ser negativos.");
        }
    }
}
