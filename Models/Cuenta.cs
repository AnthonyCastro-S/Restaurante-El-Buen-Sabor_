using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Cuenta
    {
        public const decimal TasaIgv = 0.18m;
        public static readonly string[] MetodosPago = { "EFECTIVO", "TARJETA", "YAPE_PLIN" };

        public long MesaId { get; set; }
        public int MesaNumero { get; set; }
        public int CantidadPedidos { get; set; }
        public decimal Subtotal { get; set; }
        public List<Pedido> Pedidos { get; set; } = new List<Pedido>();
        public decimal Igv { get { return Math.Round(Subtotal * TasaIgv, 2, MidpointRounding.AwayFromZero); } }
        public decimal Total { get { return Subtotal + Igv; } }
        public static List<Cuenta> ObtenerPendientes()
        {
            var lista = new List<Cuenta>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(
                    "SELECT m.id, m.numero, SUM(p.total) AS subtotal, COUNT(*) AS pedidos " +
                    "FROM pedido p INNER JOIN mesa m ON p.mesa_id = m.id " +
                    "WHERE p.estado = 'ENTREGADO' AND p.pago_id IS NULL " +
                    "GROUP BY m.id, m.numero ORDER BY m.numero", con);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new Cuenta
                    {
                        MesaId = (long)reader["id"],
                        MesaNumero = (int)reader["numero"],
                        Subtotal = (decimal)reader["subtotal"],
                        CantidadPedidos = (int)reader["pedidos"]
                    });
                }
            }
            return lista;
        }
        public static Cuenta ObtenerPorMesa(long mesaId)
        {
            Cuenta cuenta = null;
            var ids = new List<long>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand mesa = new SqlCommand("SELECT id, numero FROM mesa WHERE id=@Id", con);
                mesa.Parameters.AddWithValue("@Id", mesaId);
                SqlDataReader r = mesa.ExecuteReader();
                if (r.Read()) cuenta = new Cuenta { MesaId = (long)r["id"], MesaNumero = (int)r["numero"] };
                r.Close();
                if (cuenta == null) return null;

                SqlCommand pedidos = new SqlCommand("SELECT id FROM pedido WHERE mesa_id=@Id AND estado='ENTREGADO' AND pago_id IS NULL ORDER BY id", con);
                pedidos.Parameters.AddWithValue("@Id", mesaId);
                r = pedidos.ExecuteReader();
                while (r.Read()) ids.Add((long)r["id"]);
                r.Close();
            }
            foreach (long id in ids)
            {
                var p = Pedido.ObtenerPorId(id);
                if (p != null) cuenta.Pedidos.Add(p);
            }
            cuenta.CantidadPedidos = cuenta.Pedidos.Count;
            cuenta.Subtotal = cuenta.Pedidos.Sum(p => p.Total);
            return cuenta;
        }

        public static bool RegistrarPago(long mesaId, decimal efectivo, decimal tarjeta, decimal yapePlin,
            decimal efectivoRecibido, long cajeroId, out string error, out long pagoId)
        {
            error = null;
            pagoId = 0;
            if (efectivo < 0 || tarjeta < 0 || yapePlin < 0 || efectivoRecibido < 0)
            {
                error = "Los importes no pueden ser negativos.";
                return false;
            }

            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlTransaction tx = con.BeginTransaction();
                try
                {
                    SqlCommand suma = new SqlCommand(
                        "SELECT ISNULL(SUM(total),0) FROM pedido WHERE mesa_id=@M AND estado='ENTREGADO' AND pago_id IS NULL", con, tx);
                    suma.Parameters.AddWithValue("@M", mesaId);
                    decimal subtotal = Convert.ToDecimal(suma.ExecuteScalar());
                    if (subtotal <= 0)
                    {
                        tx.Rollback();
                        error = "La mesa no tiene pedidos entregados por cobrar.";
                        return false;
                    }

                    var cuenta = new Cuenta { Subtotal = subtotal };
                    decimal total = cuenta.Total;
                    decimal distribuido = Math.Round(efectivo + tarjeta + yapePlin, 2, MidpointRounding.AwayFromZero);

                    if (distribuido != total)
                    {
                        tx.Rollback();
                        error = "La suma de los medios de pago debe ser exactamente S/ " + total.ToString("0.00") + ".";
                        return false;
                    }
                    if (efectivo > 0 && efectivoRecibido < efectivo)
                    {
                        tx.Rollback();
                        error = "El efectivo recibido no puede ser menor al importe asignado al efectivo.";
                        return false;
                    }
                    if (efectivo == 0 && efectivoRecibido > 0)
                    {
                        tx.Rollback();
                        error = "No ingreses efectivo recibido si no se está pagando en efectivo.";
                        return false;
                    }

                    string metodoGeneral = (new[] { efectivo, tarjeta, yapePlin }.Count(x => x > 0) > 1)
                        ? "MIXTO"
                        : efectivo > 0 ? "EFECTIVO" : tarjeta > 0 ? "TARJETA" : "YAPE_PLIN";
                    decimal vuelto = efectivo > 0 ? efectivoRecibido - efectivo : 0;

                    SqlCommand pago = new SqlCommand(
                        "INSERT INTO pago (mesa_id,cajero_id,subtotal,igv,total,metodo,monto_recibido,vuelto) " +
                        "OUTPUT INSERTED.id VALUES (@M,@C,@Sub,@Igv,@Tot,@Met,@Rec,@Vue)", con, tx);
                    pago.Parameters.AddWithValue("@M", mesaId);
                    pago.Parameters.AddWithValue("@C", cajeroId);
                    pago.Parameters.AddWithValue("@Sub", subtotal);
                    pago.Parameters.AddWithValue("@Igv", cuenta.Igv);
                    pago.Parameters.AddWithValue("@Tot", total);
                    pago.Parameters.AddWithValue("@Met", metodoGeneral);
                    pago.Parameters.AddWithValue("@Rec", total);
                    pago.Parameters.AddWithValue("@Vue", vuelto);
                    pagoId = Convert.ToInt64(pago.ExecuteScalar());

                    SqlCommand detalle = new SqlCommand(
                        "INSERT INTO pago_detalle(pago_id,metodo,importe) VALUES " +
                        "(@P,'EFECTIVO',@E),(@P,'TARJETA',@T),(@P,'YAPE_PLIN',@Y)", con, tx);
                    detalle.Parameters.AddWithValue("@P", pagoId);
                    detalle.Parameters.AddWithValue("@E", efectivo);
                    detalle.Parameters.AddWithValue("@T", tarjeta);
                    detalle.Parameters.AddWithValue("@Y", yapePlin);
                    detalle.ExecuteNonQuery();

                    SqlCommand pedidos = new SqlCommand(
                        "UPDATE pedido SET estado='PAGADO', pago_id=@P, fecha_cierre=SYSDATETIME() " +
                        "WHERE mesa_id=@M AND estado='ENTREGADO' AND pago_id IS NULL", con, tx);
                    pedidos.Parameters.AddWithValue("@P", pagoId);
                    pedidos.Parameters.AddWithValue("@M", mesaId);
                    pedidos.ExecuteNonQuery();

                    SqlCommand liberarMesa = new SqlCommand(
                        "UPDATE mesa SET estado='DISPONIBLE' WHERE id=@M AND NOT EXISTS " +
                        "(SELECT 1 FROM pedido WHERE mesa_id=@M AND estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO'))", con, tx);
                    liberarMesa.Parameters.AddWithValue("@M", mesaId);
                    liberarMesa.ExecuteNonQuery();

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }
    }
}
