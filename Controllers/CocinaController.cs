using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN,COCINERO")]
    public class CocinaController : Controller
    {

        public IActionResult Index()
        {
            return View(Pedido.ObtenerParaCocina());
        }

        public IActionResult Comenzar(long id)
        {
            if (!Pedido.CambiarEstado(id, "PENDIENTE", "EN_PREPARACION"))
                TempData["Error"] = "El pedido ya no está pendiente (pudo ser cancelado).";
            return RedirectToAction("Index");
        }

        public IActionResult MarcarListo(long id)
        {
            if (Pedido.CambiarEstado(id, "EN_PREPARACION", "LISTO")) TempData["Ok"] = "Pedido listo. El mesero ya puede verlo para entregarlo.";
            else TempData["Error"] = "Solo se puede marcar como listo un pedido en preparación.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Observacion(long id, string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) TempData["Error"] = "Escribe la observación.";
            else if (texto.Length > 200) TempData["Error"] = "La observación no puede superar 200 caracteres.";
            else
            {
                Pedido.AgregarObservacion(id, texto.Trim());
                TempData["Ok"] = "Observación registrada.";
            }
            return RedirectToAction("Index");
        }
    }
}
