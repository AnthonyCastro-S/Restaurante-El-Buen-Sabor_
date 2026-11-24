using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN,MESERO,CAJERO")]
    public class PedidoController : Controller
    {

        public IActionResult Index(string buscar, string estado, DateTime? fecha)
        {
            ViewBag.Buscar = buscar;
            ViewBag.Estado = estado;
            ViewBag.Fecha = fecha;
            return View(Pedido.ObtenerTodos(buscar, estado, fecha));
        }

        public IActionResult Details(long id)
        {
            var pedido = Pedido.ObtenerPorId(id);
            if (pedido == null) return RedirectToAction("Index");
            return View(pedido);
        }
        [Authorize(Roles = "ADMIN,MESERO")]
        public IActionResult Create()
        {
            CargarListas();
            return View(new Pedido { MeseroId = IdMeseroActual() });
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        [HttpPost]
        public IActionResult Create(Pedido pedido)
        {
            if (User.IsInRole("MESERO")) pedido.MeseroId = IdMeseroActual();
            pedido.Detalles = (pedido.Detalles ?? new List<DetallePedido>()).Where(d => d.Cantidad > 0).ToList();

            if ((pedido.Observaciones ?? "").Length > 255)
                ModelState.AddModelError("", "Las observaciones no pueden superar los 255 caracteres.");
            else if (pedido.MesaId == 0)
                ModelState.AddModelError("", "Selecciona una mesa.");
            else if (pedido.MeseroId == 0)
                ModelState.AddModelError("", "Selecciona el mesero que atiende.");
            else if (!pedido.Detalles.Any())
                ModelState.AddModelError("", "Agrega al menos un plato con cantidad mayor a 0.");
            else
            {
                var mesa = Mesa.ObtenerPorId(pedido.MesaId);
                if (mesa == null || mesa.Estado == "RESERVADA")
                    ModelState.AddModelError("", "La mesa seleccionada no está disponible para atender pedidos.");
                else if (Mesa.TienePedidosActivos(mesa.Id))
                    ModelState.AddModelError("", "La mesa " + mesa.Numero + " está ocupada: ya tiene un pedido en curso. Cóbralo o cancélalo antes de registrar otro.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                            pedido.Crear();
                    TempData["Ok"] = "Pedido enviado a cocina (PED-" + pedido.Id + ").";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            CargarListas();
            return View(pedido);
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        public IActionResult AgregarPlatos(long id)
        {
            var pedido = Pedido.ObtenerPorId(id);
            if (pedido == null || pedido.Estado != "EN_PREPARACION")
            {
                TempData["Error"] = "Solo puedes agregar platos a un pedido que esté en preparación.";
                return RedirectToAction("Index");
            }
            CargarPlatos();
            pedido.Detalles = new List<DetallePedido>();
            return View(pedido);
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AgregarPlatos(long id, Pedido pedido)
        {
            var detalles = (pedido.Detalles ?? new List<DetallePedido>()).Where(d => d.Cantidad > 0).ToList();
            if (detalles.Any(d => (d.Notas ?? "").Length > 255))
                ModelState.AddModelError("", "Las notas no pueden superar los 255 caracteres.");
            else if (!detalles.Any())
                ModelState.AddModelError("", "Selecciona al menos un plato con cantidad mayor a cero.");

            if (ModelState.IsValid)
            {
                try
                {
                    if (Pedido.AgregarPlatos(id, detalles, out string error))
                    {
                        TempData["Ok"] = "Se agregaron los nuevos platos al pedido PED-" + id + " y cocina puede visualizarlos.";
                        return RedirectToAction("Details", new { id });
                    }
                    ModelState.AddModelError("", error);
                }
                catch (Exception ex) { ModelState.AddModelError("", ex.Message); }
            }

            pedido.Id = id;
            pedido.Detalles = pedido.Detalles ?? new List<DetallePedido>();
            CargarPlatos();
            return View(pedido);
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        public IActionResult CambiarMesa(long id)
        {
            var pedido = Pedido.ObtenerPorId(id);
            if (pedido == null || !new[] { "PENDIENTE", "EN_PREPARACION", "LISTO", "ENTREGADO" }.Contains(pedido.Estado))
            {
                TempData["Error"] = "Este pedido no permite cambiar de mesa.";
                return RedirectToAction("Index");
            }
            ViewBag.Mesas = Mesa.ObtenerTodas("DISPONIBLE");
            return View(pedido);
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarMesa(long id, long nuevaMesaId)
        {
            if (Pedido.CambiarMesa(id, nuevaMesaId, out string error))
                TempData["Ok"] = "El pedido PED-" + id + " fue trasladado a la nueva mesa.";
            else
                TempData["Error"] = error;
            return RedirectToAction("Details", new { id });
        }

        private long IdMeseroActual()
        {
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long id);
            return User.IsInRole("MESERO") ? id : 0;
        }

        private void CargarListas()
        {
            ViewBag.Mesas = Mesa.ObtenerParaPedido();
            ViewBag.Meseros = Usuario.ObtenerTodos("", "MESERO").Where(u => u.Activo && (!User.IsInRole("MESERO") || u.Id == IdMeseroActual())).ToList();
            CargarPlatos();
        }

        private void CargarPlatos()
        {
            ViewBag.Platos = Plato.ObtenerTodos("", 0, true);
        }
        [Authorize(Roles = "ADMIN,MESERO")]
        public IActionResult Entregar(long id)
        {
            if (Pedido.CambiarEstado(id, "LISTO", "ENTREGADO")) TempData["Ok"] = "Pedido entregado al cliente.";
            else TempData["Error"] = "Solo se pueden entregar pedidos que estén en estado Listo.";
            return RedirectToAction("Index");
        }
        [Authorize(Roles = "ADMIN,MESERO")]
        public IActionResult Cancelar(long id)
        {
            var pedido = Pedido.ObtenerPorId(id);
            if (pedido == null) return RedirectToAction("Index");
            return View(pedido);
        }

        [Authorize(Roles = "ADMIN,MESERO")]
        [HttpPost]
        public IActionResult Cancelar(long id, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                ViewBag.Error = "Debes registrar el motivo de la cancelación.";
                return View(Pedido.ObtenerPorId(id));
            }
            if (Pedido.Cancelar(id, motivo.Trim())) TempData["Ok"] = "Pedido cancelado.";
            else TempData["Error"] = "El pedido ya fue entregado o cerrado y no puede cancelarse.";
            return RedirectToAction("Index");
        }
    }
}
