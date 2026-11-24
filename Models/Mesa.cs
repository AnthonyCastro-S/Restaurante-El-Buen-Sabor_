using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Mesa
    {
        public long Id { get; set; }
        public int Numero { get; set; }
        public int Capacidad { get; set; }
        public string Ubicacion { get; set; }
        public string Estado { get; set; } = "DISPONIBLE";

        private const string Select = "SELECT id, numero, capacidad, ubicacion, estado FROM mesa ";

        private static Mesa Leer(SqlDataReader r)
        {
            return new Mesa
            {
                Id = (long)r["id"],
                Numero = (int)r["numero"],
                Capacidad = (int)r["capacidad"],
                Ubicacion = r["ubicacion"] as string,
                Estado = r["estado"] == DBNull.Value ? "DISPONIBLE" : r["estado"].ToString()
            };
        }

        public static List<Mesa> ObtenerTodas(string estado)
        {
            var lista = new List<Mesa>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE (@Estado = '' OR estado = @Estado) ORDER BY numero", con);
                cmd.Parameters.AddWithValue("@Estado", estado ?? "");
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(Leer(reader));
            }
            return lista;
        }
        public bool EnUso { get; set; }
        public static List<Mesa> ObtenerParaPedido()
        {
            var lista = new List<Mesa>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(
                    "SELECT id, numero, capacidad, ubicacion, estado, " +
                    "CASE WHEN EXISTS (SELECT 1 FROM pedido p WHERE p.mesa_id = mesa.id AND p.estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO')) THEN 1 ELSE 0 END AS en_uso " +
                    "FROM mesa ORDER BY numero", con);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var m = Leer(reader);
                    m.EnUso = Convert.ToInt32(reader["en_uso"]) == 1;
                    lista.Add(m);
                }
            }
            return lista;
        }

        public static Mesa ObtenerPorId(long id)
        {
            Mesa mesa = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) mesa = Leer(reader);
            }
            return mesa;
        }

        public static bool ExisteNumero(int numero, long idExcluir)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM mesa WHERE numero=@Numero AND id<>@Id", con);
                cmd.Parameters.AddWithValue("@Numero", numero);
                cmd.Parameters.AddWithValue("@Id", idExcluir);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public static bool TienePedidosActivos(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM pedido WHERE mesa_id=@Id AND estado IN ('PENDIENTE','EN_PREPARACION','LISTO','ENTREGADO')", con);
                cmd.Parameters.AddWithValue("@Id", id);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public void Guardar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO mesa (numero, capacidad, ubicacion, estado) VALUES (@Numero, @Capacidad, @Ubicacion, @Estado)", con);
                cmd.Parameters.AddWithValue("@Numero", Numero);
                cmd.Parameters.AddWithValue("@Capacidad", Capacidad);
                cmd.Parameters.AddWithValue("@Ubicacion", Conexion.V(Ubicacion));
                cmd.Parameters.AddWithValue("@Estado", Estado);
                cmd.ExecuteNonQuery();
            }
        }

        public void Actualizar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE mesa SET numero=@Numero, capacidad=@Capacidad, ubicacion=@Ubicacion WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Numero", Numero);
                cmd.Parameters.AddWithValue("@Capacidad", Capacidad);
                cmd.Parameters.AddWithValue("@Ubicacion", Conexion.V(Ubicacion));
                cmd.Parameters.AddWithValue("@Id", Id);
                cmd.ExecuteNonQuery();
            }
        }

        public static void CambiarEstado(long id, string estado)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE mesa SET estado=@Estado WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Estado", estado);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
        public static bool Eliminar(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM mesa WHERE id=@Id AND NOT EXISTS (SELECT 1 FROM pedido WHERE mesa_id=@Id)", con);
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }
}
