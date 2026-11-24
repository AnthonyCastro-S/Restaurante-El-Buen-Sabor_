using ElBuenSabor.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;

namespace ElBuenSabor.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                ViewBag.PedidosHoy = Escalar(con, "SELECT COUNT(*) FROM pedido WHERE CAST(fecha_creacion AS DATE)=CAST(GETDATE() AS DATE)");
                ViewBag.VentasHoy = EscalarDecimal(con, "SELECT ISNULL(SUM(total),0) FROM pago WHERE CAST(fecha AS DATE)=CAST(GETDATE() AS DATE)");
                ViewBag.MesasOcupadas = Escalar(con, "SELECT COUNT(*) FROM mesa WHERE estado='OCUPADA'");
                ViewBag.MesasTotal = Escalar(con, "SELECT COUNT(*) FROM mesa");
                ViewBag.PedidosPendientes = Escalar(con, "SELECT COUNT(*) FROM pedido WHERE estado IN ('PENDIENTE','EN_PREPARACION','LISTO')");
                ViewBag.CuentasPendientes = Escalar(con, "SELECT COUNT(DISTINCT mesa_id) FROM pedido WHERE estado='ENTREGADO' AND pago_id IS NULL");
                ViewBag.UltimosPagos = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 6 p.id,p.total,p.fecha,m.numero AS mesa FROM pago p INNER JOIN mesa m ON p.mesa_id=m.id ORDER BY p.fecha DESC", con))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    var pagos = new List<object>();
                    while (r.Read()) pagos.Add(new { Id = r["id"], Total = r["total"], Fecha = r["fecha"], Mesa = r["mesa"] });
                    ViewBag.UltimosPagos = pagos;
                }
                ViewBag.CajaAbierta = false;
                if (long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out long usuarioId))
                {
                    ViewBag.CajaAbierta = Escalar(con, "SELECT COUNT(*) FROM caja_sesion WHERE cajero_id=@Id AND estado='ABIERTA'", usuarioId) > 0;
                }
            }
            return View();
        }

        private static int Escalar(SqlConnection con, string sql, long id = 0)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con))
            {
                if (sql.Contains("@Id")) cmd.Parameters.AddWithValue("@Id", id);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static decimal EscalarDecimal(SqlConnection con, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, con)) return Convert.ToDecimal(cmd.ExecuteScalar());
        }

        [AllowAnonymous]
        public IActionResult Error() { return View("~/Views/Shared/Error.cshtml"); }
    }
}
