using System.Security.Claims;
using ElBuenSabor.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN,CAJERO")]
    public class CajaController : Controller
    {
        public IActionResult Index()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                long usuarioId = UsuarioActual();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 id,fecha_apertura,monto_inicial FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA' ORDER BY id DESC", con))
                {
                    cmd.Parameters.AddWithValue("@U", usuarioId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            ViewBag.SesionId = Convert.ToInt64(r["id"]);
                            ViewBag.FechaApertura = Convert.ToDateTime(r["fecha_apertura"]);
                            ViewBag.MontoInicial = Convert.ToDecimal(r["monto_inicial"]);
                        }
                    }
                }
            }
            return View(Cuenta.ObtenerPendientes());
        }

        public IActionResult GenerarCuenta(long id)
        {
            var cuenta = Cuenta.ObtenerPorMesa(id);
            if (cuenta == null || !cuenta.Pedidos.Any())
            {
                TempData["Error"] = "La mesa no tiene pedidos entregados por cobrar.";
                return RedirectToAction("Index");
            }
            return View(cuenta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Pagar(long id, decimal efectivo, decimal tarjeta, decimal yapePlin, decimal efectivoRecibido)
        {
            long cajeroId = UsuarioActual();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand caja = new SqlCommand("SELECT COUNT(*) FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA'", con))
                {
                    caja.Parameters.AddWithValue("@U", cajeroId);
                    if (Convert.ToInt32(caja.ExecuteScalar()) == 0)
                    {
                        TempData["Error"] = "Debes abrir caja antes de registrar pagos.";
                        return RedirectToAction("Index");
                    }
                }
            }
            var cuenta = Cuenta.ObtenerPorMesa(id);
            if (cuenta == null || !cuenta.Pedidos.Any())
            {
                TempData["Error"] = "La mesa no tiene pedidos entregados por cobrar.";
                return RedirectToAction("Index");
            }

            if (Cuenta.RegistrarPago(id, efectivo, tarjeta, yapePlin, efectivoRecibido, cajeroId, out string error, out long pagoId))
            {
                return RedirectToAction("Comprobante", new { id = pagoId });
            }

            TempData["Error"] = error;
            return RedirectToAction("GenerarCuenta", new { id });
        }

        public IActionResult Comprobante(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                var datos = new Dictionary<string, object>();
                using (SqlCommand cmd = new SqlCommand("SELECT p.id,p.mesa_id,m.numero,p.subtotal,p.igv,p.total,p.metodo,p.monto_recibido,p.vuelto,p.fecha,u.nombre+' '+u.apellido AS cajero FROM pago p INNER JOIN mesa m ON m.id=p.mesa_id INNER JOIN usuario u ON u.id=p.cajero_id WHERE p.id=@Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return NotFound();
                        foreach (var key in new[] { "id","mesa_id","numero","subtotal","igv","total","metodo","monto_recibido","vuelto","fecha","cajero" }) datos[key] = r[key];
                    }
                }
                ViewBag.Pago = datos;
                var detalles = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand("SELECT d.metodo,d.importe FROM pago_detalle d WHERE d.pago_id=@Id AND d.importe>0 ORDER BY d.id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) detalles.Add(new { Metodo = r["metodo"].ToString(), Importe = Convert.ToDecimal(r["importe"]) });
                }
                ViewBag.MediosPago = detalles;
                var pedidos = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand("SELECT pp.cantidad,pl.nombre,pp.precio_unitario,pp.subtotal,p.id AS pedido FROM pedido p INNER JOIN pedido_plato pp ON pp.pedido_id=p.id INNER JOIN plato pl ON pl.id=pp.plato_id WHERE p.pago_id=@Id ORDER BY p.id,pp.id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) pedidos.Add(new { Cantidad = r["cantidad"], Plato = r["nombre"].ToString(), Precio = r["precio_unitario"], Subtotal = r["subtotal"], Pedido = r["pedido"] });
                }
                ViewBag.Detalles = pedidos;
            }
            return View();
        }

        [HttpGet]
        public IActionResult Apertura() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Apertura(decimal montoInicial)
        {
            if (montoInicial < 0) { TempData["Error"] = "El monto inicial no puede ser negativo."; return RedirectToAction("Apertura"); }
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand("IF EXISTS(SELECT 1 FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA') THROW 50001,'Ya tienes una caja abierta.',1; INSERT INTO caja_sesion(cajero_id,monto_inicial) VALUES(@U,@M)", con))
                {
                    cmd.Parameters.AddWithValue("@U", UsuarioActual());
                    cmd.Parameters.AddWithValue("@M", montoInicial);
                    try { cmd.ExecuteNonQuery(); }
                    catch (SqlException ex) { TempData["Error"] = ex.Message; return RedirectToAction("Index"); }
                }
            }
            TempData["Ok"] = "Caja abierta correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Movimiento(string tipo, string concepto, decimal importe)
        {
            if (importe <= 0 || string.IsNullOrWhiteSpace(concepto) || (tipo != "INGRESO" && tipo != "EGRESO"))
            {
                TempData["Error"] = "Completa correctamente los datos del movimiento.";
                return RedirectToAction("Index");
            }
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand("INSERT INTO movimiento_caja(sesion_id,usuario_id,tipo,concepto,importe) SELECT id,@U,@T,@C,@I FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA'", con))
                {
                    cmd.Parameters.AddWithValue("@U", UsuarioActual());
                    cmd.Parameters.AddWithValue("@T", tipo);
                    cmd.Parameters.AddWithValue("@C", concepto.Trim());
                    cmd.Parameters.AddWithValue("@I", importe);
                    if (cmd.ExecuteNonQuery() == 0) TempData["Error"] = "No tienes una caja abierta.";
                    else TempData["Ok"] = "Movimiento registrado.";
                }
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Cierre()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 id,monto_inicial,fecha_apertura FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA' ORDER BY id DESC", con))
                {
                    cmd.Parameters.AddWithValue("@U", UsuarioActual());
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) { TempData["Error"] = "No tienes una caja abierta."; return RedirectToAction("Index"); }
                        ViewBag.SesionId = r["id"];
                        ViewBag.MontoInicial = r["monto_inicial"];
                        ViewBag.FechaApertura = r["fecha_apertura"];
                    }
                }
                long sesion = Convert.ToInt64(ViewBag.SesionId);
                using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(CASE WHEN pc.metodo='EFECTIVO' THEN pc.importe ELSE 0 END),0) FROM pago_detalle pc INNER JOIN pago p ON p.id=pc.pago_id INNER JOIN caja_sesion cs ON cs.cajero_id=p.cajero_id WHERE cs.id=@S AND p.fecha>=cs.fecha_apertura", con))
                {
                    cmd.Parameters.AddWithValue("@S", sesion);
                    ViewBag.VentasEfectivo = Convert.ToDecimal(cmd.ExecuteScalar());
                }
                using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(CASE WHEN pc.metodo='TARJETA' THEN pc.importe ELSE 0 END),0),ISNULL(SUM(CASE WHEN pc.metodo='YAPE_PLIN' THEN pc.importe ELSE 0 END),0) FROM pago_detalle pc INNER JOIN pago p ON p.id=pc.pago_id INNER JOIN caja_sesion cs ON cs.cajero_id=p.cajero_id WHERE cs.id=@S AND p.fecha>=cs.fecha_apertura", con))
                {
                    cmd.Parameters.AddWithValue("@S", sesion);
                    using (SqlDataReader r = cmd.ExecuteReader()) { r.Read(); ViewBag.VentasTarjeta = r.GetDecimal(0); ViewBag.VentasDigital = r.GetDecimal(1); }
                }
                using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(SUM(CASE WHEN tipo='INGRESO' THEN importe ELSE -importe END),0) FROM movimiento_caja WHERE sesion_id=@S", con))
                {
                    cmd.Parameters.AddWithValue("@S", sesion);
                    ViewBag.Movimientos = Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cierre(decimal efectivoContado, string observaciones)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand(@"DECLARE @S BIGINT=(SELECT TOP 1 id FROM caja_sesion WHERE cajero_id=@U AND estado='ABIERTA' ORDER BY id DESC);
                    IF @S IS NULL THROW 50002,'No hay una caja abierta.',1;
                    DECLARE @A DECIMAL(10,2)=(SELECT monto_inicial FROM caja_sesion WHERE id=@S);
                    DECLARE @V DECIMAL(10,2)=(SELECT ISNULL(SUM(pd.importe),0) FROM pago_detalle pd INNER JOIN pago p ON p.id=pd.pago_id WHERE p.cajero_id=@U AND p.fecha>=(SELECT fecha_apertura FROM caja_sesion WHERE id=@S) AND pd.metodo='EFECTIVO');
                    DECLARE @M DECIMAL(10,2)=(SELECT ISNULL(SUM(CASE WHEN tipo='INGRESO' THEN importe ELSE -importe END),0) FROM movimiento_caja WHERE sesion_id=@S);
                    UPDATE caja_sesion SET fecha_cierre=SYSDATETIME(),ventas_efectivo=@V,efectivo_contado=@C,diferencia=@C-(@A+@V+@M),observaciones=@O,estado='CERRADA' WHERE id=@S;", con))
                {
                    cmd.Parameters.AddWithValue("@U", UsuarioActual());
                    cmd.Parameters.AddWithValue("@C", efectivoContado);
                    cmd.Parameters.AddWithValue("@O", (object)(observaciones ?? "") );
                    cmd.ExecuteNonQuery();
                }
            }
            TempData["Ok"] = "Caja cerrada. El arqueo quedó registrado.";
            return RedirectToAction("Historial");
        }

        public IActionResult Historial()
        {
            var rows = new List<dynamic>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 30 c.id,c.fecha_apertura,c.fecha_cierre,c.monto_inicial,c.ventas_efectivo,c.efectivo_contado,c.diferencia,c.estado,u.nombre+' '+u.apellido AS cajero FROM caja_sesion c INNER JOIN usuario u ON u.id=c.cajero_id ORDER BY c.id DESC", con))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) rows.Add(new { Id=r["id"], Apertura=r["fecha_apertura"], Cierre=r["fecha_cierre"], Inicial=r["monto_inicial"], Ventas=r["ventas_efectivo"], Contado=r["efectivo_contado"], Diferencia=r["diferencia"], Estado=r["estado"], Cajero=r["cajero"] });
            }
            return View(rows);
        }

        private long UsuarioActual()
        {
            long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long id);
            return id;
        }
    }
}
