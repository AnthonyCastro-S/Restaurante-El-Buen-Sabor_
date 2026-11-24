using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Insumo
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Unidad { get; set; } = "kg";
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal CostoUnitario { get; set; }
        public bool Activo { get; set; } = true;
        public string EstadoStock => StockActual <= StockMinimo ? "Bajo" : "Normal";

        public static List<Insumo> ObtenerTodos(string buscar = "", bool incluirInactivos = false)
        {
            var lista = new List<Insumo>();
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var cmd = new SqlCommand(
                "SELECT id,nombre,unidad,stock_actual,stock_minimo,costo_unitario,activo FROM insumo " +
                "WHERE nombre LIKE @buscar AND (@inactivos=1 OR activo=1) ORDER BY nombre", con);
            cmd.Parameters.AddWithValue("@buscar", "%" + (buscar ?? "") + "%");
            cmd.Parameters.AddWithValue("@inactivos", incluirInactivos);
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(Leer(r));
            return lista;
        }

        public static Insumo ObtenerPorId(long id)
        {
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var cmd = new SqlCommand("SELECT id,nombre,unidad,stock_actual,stock_minimo,costo_unitario,activo FROM insumo WHERE id=@id", con);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Leer(r) : null;
        }

        private static Insumo Leer(SqlDataReader r) => new Insumo
        {
            Id = Convert.ToInt64(r["id"]),
            Nombre = r["nombre"].ToString(),
            Unidad = r["unidad"].ToString(),
            StockActual = Convert.ToDecimal(r["stock_actual"]),
            StockMinimo = Convert.ToDecimal(r["stock_minimo"]),
            CostoUnitario = Convert.ToDecimal(r["costo_unitario"]),
            Activo = Convert.ToBoolean(r["activo"])
        };

        public void Guardar()
        {
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var cmd = new SqlCommand("INSERT INTO insumo(nombre,unidad,stock_actual,stock_minimo,costo_unitario,activo) VALUES(@nombre,@unidad,@stock,@minimo,@costo,@activo)", con);
            Parametros(cmd);
            cmd.ExecuteNonQuery();
        }

        public void Actualizar()
        {
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var cmd = new SqlCommand("UPDATE insumo SET nombre=@nombre,unidad=@unidad,stock_minimo=@minimo,costo_unitario=@costo,activo=@activo WHERE id=@id", con);
            Parametros(cmd);
            cmd.Parameters.AddWithValue("@id", Id);
            cmd.ExecuteNonQuery();
        }

        private void Parametros(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@nombre", (Nombre ?? "").Trim());
            cmd.Parameters.AddWithValue("@unidad", (Unidad ?? "unidad").Trim());
            cmd.Parameters.AddWithValue("@stock", StockActual);
            cmd.Parameters.AddWithValue("@minimo", StockMinimo);
            cmd.Parameters.AddWithValue("@costo", CostoUnitario);
            cmd.Parameters.AddWithValue("@activo", Activo);
        }

        public static bool RegistrarMovimiento(long id, string tipo, decimal cantidad, string motivo, string usuario, long usuarioId, out string error)
        {
            error = "";
            if (cantidad <= 0) { error = "La cantidad debe ser mayor que cero."; return false; }
            if (tipo != "ENTRADA" && tipo != "SALIDA" && tipo != "AJUSTE") { error = "Tipo de movimiento no válido."; return false; }
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var tx = con.BeginTransaction();
            try
            {
                decimal anterior = 0;
                string nombre = "";
                bool encontrado = false;
                using (var cmd = new SqlCommand("SELECT nombre,stock_actual FROM insumo WITH (UPDLOCK,ROWLOCK) WHERE id=@id AND activo=1", con, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            encontrado = true;
                            nombre = r["nombre"].ToString();
                            anterior = Convert.ToDecimal(r["stock_actual"]);
                        }
                    }
                }
                if (!encontrado) { error = "El insumo no existe o está inactivo."; tx.Rollback(); return false; }
                decimal nuevo = tipo == "ENTRADA" ? anterior + cantidad : tipo == "SALIDA" ? anterior - cantidad : cantidad;
                if (nuevo < 0) { error = "No hay stock suficiente para registrar esa salida."; tx.Rollback(); return false; }
                using (var upd = new SqlCommand("UPDATE insumo SET stock_actual=@stock WHERE id=@id", con, tx))
                {
                    upd.Parameters.AddWithValue("@stock", nuevo);
                    upd.Parameters.AddWithValue("@id", id);
                    upd.ExecuteNonQuery();
                }
                using (var mov = new SqlCommand("INSERT INTO movimiento_inventario(insumo_id,usuario_id,insumo_nombre,tipo,cantidad,stock_anterior,stock_nuevo,motivo,usuario_nombre,fecha) VALUES(@id,@usuarioId,@nombre,@tipo,@cantidad,@anterior,@nuevo,@motivo,@usuario,GETDATE())", con, tx))
                {
                    mov.Parameters.AddWithValue("@id", id);
                    mov.Parameters.AddWithValue("@usuarioId", usuarioId);
                    mov.Parameters.AddWithValue("@nombre", nombre);
                    mov.Parameters.AddWithValue("@tipo", tipo);
                    mov.Parameters.AddWithValue("@cantidad", cantidad);
                    mov.Parameters.AddWithValue("@anterior", anterior);
                    mov.Parameters.AddWithValue("@nuevo", nuevo);
                    mov.Parameters.AddWithValue("@motivo", Conexion.V(motivo));
                    mov.Parameters.AddWithValue("@usuario", usuario ?? "Sistema");
                    mov.ExecuteNonQuery();
                }
                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static List<MovimientoInsumo> ObtenerMovimientos(int limite = 100)
        {
            var lista = new List<MovimientoInsumo>();
            using var con = new SqlConnection(Conexion.Cadena);
            con.Open();
            using var cmd = new SqlCommand("SELECT TOP (@limite) id,insumo_nombre,tipo,cantidad,stock_anterior,stock_nuevo,motivo,usuario_nombre,fecha FROM movimiento_inventario ORDER BY fecha DESC,id DESC", con);
            cmd.Parameters.AddWithValue("@limite", limite);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new MovimientoInsumo {
                    Id = Convert.ToInt64(r["id"]), Insumo = r["insumo_nombre"].ToString(),
                    Tipo = r["tipo"].ToString(), Cantidad = Convert.ToDecimal(r["cantidad"]),
                    StockAnterior = Convert.ToDecimal(r["stock_anterior"]), StockNuevo = Convert.ToDecimal(r["stock_nuevo"]),
                    Motivo = r["motivo"] as string, Usuario = r["usuario_nombre"].ToString(),
                    Fecha = Convert.ToDateTime(r["fecha"])
                });
            }
            return lista;
        }
    }

    public class MovimientoInsumo
    {
        public long Id { get; set; }
        public string Insumo { get; set; }
        public string Tipo { get; set; }
        public decimal Cantidad { get; set; }
        public decimal StockAnterior { get; set; }
        public decimal StockNuevo { get; set; }
        public string Motivo { get; set; }
        public string Usuario { get; set; }
        public DateTime Fecha { get; set; }
    }
}
