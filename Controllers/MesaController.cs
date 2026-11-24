using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN,MESERO")]
    public class MesaController : Controller
    {

        public IActionResult Index(string estado)
        {
            ViewBag.Estado = estado;
            return View(Mesa.ObtenerTodas(estado));
        }
        public IActionResult CambiarEstado(long id, string estado)
        {
            var mesa = Mesa.ObtenerPorId(id);
            if (mesa == null || !new[] { "DISPONIBLE", "OCUPADA", "RESERVADA" }.Contains(estado))
                return RedirectToAction("Index");

            if (mesa.Estado == estado)
                TempData["Error"] = "La mesa " + mesa.Numero + " ya se encuentra " + estado.ToLower() + ".";
            else if (estado == "DISPONIBLE" && Mesa.TienePedidosActivos(id))
                TempData["Error"] = "La mesa " + mesa.Numero + " tiene pedidos en curso o una cuenta por cobrar. No se puede liberar todavía.";
            else
            {
                Mesa.CambiarEstado(id, estado);
                TempData["Ok"] = "Mesa " + mesa.Numero + " ahora está " + estado.ToLower() + ".";
            }
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "ADMIN")]
        public IActionResult Create()
        {
            return View("Form", new Mesa());
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Create(Mesa mesa)
        {
            Validar(mesa);
            if (!ModelState.IsValid) return View("Form", mesa);
            mesa.Estado = "DISPONIBLE";
            mesa.Guardar();
            TempData["Ok"] = "Mesa registrada.";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "ADMIN")]
        public IActionResult Edit(long id)
        {
            var mesa = Mesa.ObtenerPorId(id);
            if (mesa == null) return RedirectToAction("Index");
            return View("Form", mesa);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Edit(Mesa mesa)
        {
            Validar(mesa);
            if (!ModelState.IsValid) return View("Form", mesa);
            mesa.Actualizar();
            TempData["Ok"] = "Mesa actualizada.";
            return RedirectToAction("Index");
        }

        private void Validar(Mesa mesa)
        {
            if (mesa.Numero <= 0 || mesa.Capacidad <= 0)
                ModelState.AddModelError("", "El número y la capacidad deben ser mayores que 0.");
            else if (Mesa.ExisteNumero(mesa.Numero, mesa.Id))
                ModelState.AddModelError("", "Ya existe una mesa con ese número.");
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Delete(long id)
        {
            if (Mesa.Eliminar(id)) TempData["Ok"] = "Mesa eliminada.";
            else TempData["Error"] = "No se puede eliminar: la mesa tiene pedidos registrados.";
            return RedirectToAction("Index");
        }
    }
}
