using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Pedido
    {
        public long Id { get; set; }
        public long MesaId { get; set; }
        public int MesaNumero { get; set; }
        public long MeseroId { get; set; }
        public string MeseroNombre { get; set; }
        public string Estado { get; set; } = "PENDIENTE";
        public decimal Total { get; set; }
        public string Observaciones { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int CantidadPlatos { get; set; }
        public List<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();

        private const string Select =
            "SELECT p.id, p.mesa_id, m.numero AS mesa_numero, p.mesero_id, u.nombre + ' ' + u.apellido AS mesero, " +
            "p.estado, p.total, p.observaciones, p.fecha_creacion, p.fecha_cierre, " +
            "(SELECT ISNULL(SUM(d.cantidad),0) FROM pedido_plato d WHERE d.pedido_id = p.id) AS platos " +
            "FROM pedido p INNER JOIN mesa m ON p.mesa_id = m.id INNER JOIN usuario u ON p.mesero_id = u.id ";

        private static Pedido Leer(SqlDataReader r)
        {
            return new Pedido
            {
                Id = (long)r["id"],
                MesaId = (long)r["mesa_id"],
                MesaNumero = (int)r["mesa_numero"],
                MeseroId = (long)r["mesero_id"],
                MeseroNombre = r["mesero"].ToString(),
                Estado = r["estado"] == DBNull.Value ? "PENDIENTE" : r["estado"].ToString(),
                Total = r["total"] == DBNull.Value ? 0 : (decimal)r["total"],
                Observaciones = r["observaciones"] as string,
                FechaCreacion = r["fecha_creacion"] == DBNull.Value ? DateTime.Now : (DateTime)r["fecha_creacion"],
                FechaCierre = r["fecha_cierre"] == DBNull.Value ? (DateTime?)null : (DateTime)r["fecha_cierre"],
                CantidadPlatos = Convert.ToInt32(r["platos"])
            };
        }

        public static List<Pedido> ObtenerTodos(string buscar, string estado, DateTime? fecha)
        {
            var lista = new List<Pedido>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select +
                    "WHERE (@Estado = '' OR p.estado = @Estado) " +
                    "AND (@Fecha IS NULL OR CAST(p.fecha_creacion AS DATE) = @Fecha) " +
                    "AND (@Buscar = '' OR CAST(p.id AS NVARCHAR(20)) = @Buscar OR CAST(m.numero AS NVARCHAR(10)) = @Buscar) " +
                    "ORDER BY p.id DESC", con);
                cmd.Parameters.AddWithValue("@Estado", estado ?? "");
                cmd.Parameters.AddWithValue("@Fecha", fecha.HasValue ? (object)fecha.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@Buscar", (buscar ?? "").Trim());
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(Leer(reader));
            }
            return lista;
        }

        public static Pedido ObtenerPorId(long id)
        {
            Pedido pedido = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE p.id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) pedido = Leer(reader);
                reader.Close();
                if (pedido != null) pedido.Detalles = ObtenerDetalles(con, id);
            }
            return pedido;
        }

        private static List<DetallePedido> ObtenerDetalles(SqlConnection con, long pedidoId)
        {
            var lista = new List<DetallePedido>();
            SqlCommand cmd = new SqlCommand(
                "SELECT d.id, d.pedido_id, d.plato_id, p.nombre, d.cantidad, d.precio_unitario, d.subtotal, d.notas " +
                "FROM pedido_plato d INNER JOIN plato p ON d.plato_id = p.id WHERE d.pedido_id=@Id ORDER BY d.id", con);
            cmd.Parameters.AddWithValue("@Id", pedidoId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new DetallePedido
                {
                    Id = (long)reader["id"],
                    PedidoId = (long)reader["pedido_id"],
                    PlatoId = (long)reader["plato_id"],
                    PlatoNombre = reader["nombre"].ToString(),
                    Cantidad = (int)reader["cantidad"],
                    PrecioUnitario = (decimal)reader["precio_unitario"],
                    Subtotal = (decimal)reader["subtotal"],
                    Notas = reader["notas"] as string
                });
            }
            reader.Close();
            return lista;
        }
        public static List<Pedido> ObtenerParaCocina()
        {
            var lista = new List<Pedido>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE p.estado IN ('PENDIENTE','EN_PREPARACION','LISTO') ORDER BY p.fecha_creacion", con);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(Leer(reader));
                reader.Close();
                foreach (var p in lista) p.Detalles = ObtenerDetalles(con, p.Id);
            }
            return lista;
        }
        public void Crear()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlTransaction tx = con.BeginTransaction();
                try
                {
                    SqlCommand cmd = new SqlCommand(
                        "INSERT INTO pedido (mesa_id, mesero_id, estado, total, observaciones) OUTPUT INSERTED.id VALUES (@MesaId, @MeseroId, 'PENDIENTE', 0, @Obs)", con, tx);
                    cmd.Parameters.AddWithValue("@MesaId", MesaId);
                    cmd.Parameters.AddWithValue("@MeseroId", MeseroId);
                    cmd.Parameters.AddWithValue("@Obs", Conexion.V(string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones));
                    Id = Convert.ToInt64(cmd.ExecuteScalar());

                    decimal total = 0;
                    foreach (var d in Detalles)
                    {
                        SqlCommand precioCmd = new SqlCommand("SELECT precio FROM plato WHERE id=@PlatoId AND disponible=1", con, tx);
                        precioCmd.Parameters.AddWithValue("@PlatoId", d.PlatoId);
                        object precio = precioCmd.ExecuteScalar();
                        if (precio == null) throw new Exception("Uno de los platos ya no está disponible.");

                        d.PrecioUnitario = (decimal)precio;
                        d.Subtotal = d.PrecioUnitario * d.Cantidad;
                        total += d.Subtotal;

                        SqlCommand det = new SqlCommand(
                            "INSERT INTO pedido_plato (pedido_id, plato_id, cantidad, precio_unitario, subtotal, notas) VALUES (@PedidoId, @PlatoId, @Cantidad, @Precio, @Subtotal, @Notas)", con, tx);
                        det.Parameters.AddWithValue("@PedidoId", Id);
                        det.Parameters.AddWithValue("@PlatoId", d.PlatoId);
                        det.Parameters.AddWithValue("@Cantidad", d.Cantidad);
                        det.Parameters.AddWithValue("@Precio", d.PrecioUnitario);
                        det.Parameters.AddWithValue("@Subtotal", d.Subtotal);
                        det.Parameters.AddWithValue("@Notas", Conexion.V(string.IsNullOrWhiteSpace(d.Notas) ? null : d.Notas));
                        det.ExecuteNonQuery();
                    }

                    SqlCommand upd = new SqlCommand("UPDATE pedido SET total=@Total WHERE id=@Id; UPDATE mesa SET estado='OCUPADA' WHERE id=@MesaId", con, tx);
                    upd.Parameters.AddWithValue("@Total", total);
                    upd.Parameters.AddWithValue("@Id", Id);
                    upd.Parameters.AddWithValue("@MesaId", MesaId);
                    upd.ExecuteNonQuery();

                    Total = total;
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }
        public static bool AgregarPlatos(long pedidoId, List<DetallePedido> detalles, out string error)
        {
            error = null;
            if (detalles == null || detalles.Count == 0)
            {
                error = "Selecciona al menos un plato con cantidad mayor a cero.";
                return false;
            }

            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlTransaction tx = con.BeginTransaction();
                try
                {
                    SqlCommand estado = new SqlCommand("SELECT estado FROM pedido WITH (UPDLOCK, ROWLOCK) WHERE id=@Id", con, tx);
                    estado.Parameters.AddWithValue("@Id", pedidoId);
                    object valorEstado = estado.ExecuteScalar();
                    if (valorEstado == null || valorEstado.ToString() != "EN_PREPARACION")
                    {
                        tx.Rollback();
                        error = "Solo se pueden agregar platos a pedidos que estén en preparación.";
                        return false;
                    }

                    decimal adicional = 0;
                    foreach (var d in detalles.Where(x => x.Cantidad > 0))
                    {
                        SqlCommand precioCmd = new SqlCommand("SELECT precio FROM plato WHERE id=@PlatoId AND disponible=1", con, tx);
                        precioCmd.Parameters.AddWithValue("@PlatoId", d.PlatoId);
                        object precio = precioCmd.ExecuteScalar();
                        if (precio == null) throw new Exception("Uno de los platos seleccionados ya no está disponible.");

                        decimal precioUnitario = Convert.ToDecimal(precio);
                        decimal subtotal = precioUnitario * d.Cantidad;
                        adicional += subtotal;
                        SqlCommand det = new SqlCommand(
                            "INSERT INTO pedido_plato (pedido_id, plato_id, cantidad, precio_unitario, subtotal, notas) VALUES (@PedidoId, @PlatoId, @Cantidad, @Precio, @Subtotal, @Notas)", con, tx);
                        det.Parameters.AddWithValue("@PedidoId", pedidoId);
                        det.Parameters.AddWithValue("@PlatoId", d.PlatoId);
                        det.Parameters.AddWithValue("@Cantidad", d.Cantidad);
                        det.Parameters.AddWithValue("@Precio", precioUnitario);
                        det.Parameters.AddWithValue("@Subtotal", subtotal);
                        det.Parameters.AddWithValue("@Notas", Conexion.V(string.IsNullOrWhiteSpace(d.Notas) ? null : d.Notas));
                        det.ExecuteNonQuery();
                    }

                    if (adicional <= 0)
                    {
                        tx.Rollback();
                        error = "Agrega al menos un plato con cantidad mayor a cero.";
                        return false;
                    }

                    SqlCommand actualizar = new SqlCommand("UPDATE pedido SET total=total+@Adicional WHERE id=@Id AND estado='EN_PREPARACION'", con, tx);
                    actualizar.Parameters.AddWithValue("@Adicional", adicional);
                    actualizar.Parameters.AddWithValue("@Id", pedidoId);
                    if (actualizar.ExecuteNonQuery() == 0) throw new Exception("El pedido cambió de estado. Actualiza la pantalla e inténtalo nuevamente.");
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
        public static bool CambiarMesa(long pedidoId, long nuevaMesaId, out string error)
        {
            error = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlTransaction tx = con.BeginTransaction();
                try
                {
                    SqlCommand pedidoCmd = new SqlCommand("SELECT mesa_id, estado FROM pedido WITH (UPDLOCK, ROWLOCK) WHERE id=@Id", con, tx);
                    pedidoCmd.Parameters.AddWithValue("@Id", pedidoId);
                    long mesaActual;
                    string estado;
                    using (var r = pedidoCmd.ExecuteReader())
                    {
                        if (!r.Read()) { tx.Rollback(); error = "No se encontró el pedido."; return false; }
                        mesaActual = Convert.ToInt64(r["mesa_id"]);
                        estado = r["estado"].ToString();
                    }
                    if (!new[] { "PENDIENTE", "EN_PREPARACION", "LISTO", "ENTREGADO" }.Contains(estado))
                    {
                        tx.Rollback(); error = "No se puede cambiar la mesa de un pedido cerrado o cancelado."; return false;
                    }
                    if (mesaActual == nuevaMesaId)
                    {
                        tx.Rollback(); error = "El pedido ya pertenece a esa mesa."; return false;
                    }

                    SqlCommand destino = new SqlCommand(
                        "SELECT COUNT(*) FROM mesa m WITH (UPDLOCK, HOLDLOCK) WHERE m.id=@Nueva AND m.estado='DISPONIBLE' " +
                        "AND NOT EXISTS (SELECT 1 FROM pedido p WHERE p.mesa_id=m.id AND p.estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO'))", con, tx);
                    destino.Parameters.AddWithValue("@Nueva", nuevaMesaId);
                    if (Convert.ToInt32(destino.ExecuteScalar()) == 0)
                    {
                        tx.Rollback(); error = "La mesa seleccionada no está disponible."; return false;
                    }

                    SqlCommand mover = new SqlCommand("UPDATE pedido SET mesa_id=@Nueva WHERE id=@Id", con, tx);
                    mover.Parameters.AddWithValue("@Nueva", nuevaMesaId);
                    mover.Parameters.AddWithValue("@Id", pedidoId);
                    mover.ExecuteNonQuery();

                    SqlCommand ocupar = new SqlCommand("UPDATE mesa SET estado='OCUPADA' WHERE id=@Nueva; " +
                        "UPDATE mesa SET estado='DISPONIBLE' WHERE id=@Actual AND NOT EXISTS " +
                        "(SELECT 1 FROM pedido WHERE mesa_id=@Actual AND estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO'))", con, tx);
                    ocupar.Parameters.AddWithValue("@Nueva", nuevaMesaId);
                    ocupar.Parameters.AddWithValue("@Actual", mesaActual);
                    ocupar.ExecuteNonQuery();
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
        public static bool CambiarEstado(long id, string actual, string nuevo)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE pedido SET estado=@Nuevo WHERE id=@Id AND estado=@Actual", con);
                cmd.Parameters.AddWithValue("@Nuevo", nuevo);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Actual", actual);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public static void AgregarObservacion(long id, string texto)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(
                    "UPDATE pedido SET observaciones = CASE WHEN observaciones IS NULL OR observaciones = '' THEN @Texto ELSE observaciones + CHAR(13) + CHAR(10) + @Texto END WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Texto", "[Cocina] " + texto);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
        public static bool Cancelar(long id, string motivo)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlTransaction tx = con.BeginTransaction();
                try
                {
                    SqlCommand cmd = new SqlCommand(
                        "UPDATE pedido SET estado='CANCELADO', fecha_cierre=SYSDATETIME(), " +
                        "observaciones = ISNULL(observaciones + CHAR(13) + CHAR(10), '') + @Motivo " +
                        "WHERE id=@Id AND estado IN ('PENDIENTE','EN_PREPARACION','LISTO')", con, tx);
                    cmd.Parameters.AddWithValue("@Motivo", "[CANCELADO] " + motivo);
                    cmd.Parameters.AddWithValue("@Id", id);
                    bool ok = cmd.ExecuteNonQuery() > 0;

                    if (ok)
                    {
                        SqlCommand mesa = new SqlCommand(
                            "UPDATE mesa SET estado='DISPONIBLE' WHERE id=(SELECT mesa_id FROM pedido WHERE id=@Id) AND estado='OCUPADA' " +
                            "AND NOT EXISTS (SELECT 1 FROM pedido WHERE mesa_id=(SELECT mesa_id FROM pedido WHERE id=@Id) AND estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO'))", con, tx);
                        mesa.Parameters.AddWithValue("@Id", id);
                        mesa.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return ok;
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
