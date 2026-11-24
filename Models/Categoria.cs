using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Categoria
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activa { get; set; } = true;
        public int CantidadPlatos { get; set; }

        public static List<Categoria> ObtenerTodas(bool soloActivas)
        {
            var lista = new List<Categoria>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(
                    "SELECT c.id, c.nombre, c.descripcion, c.activa, (SELECT COUNT(*) FROM plato p WHERE p.categoria_id=c.id) AS cantidad " +
                    "FROM categoria c " + (soloActivas ? "WHERE c.activa=1 " : "") + "ORDER BY c.id", con);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new Categoria
                    {
                        Id = (long)reader["id"],
                        Nombre = reader["nombre"].ToString(),
                        Descripcion = reader["descripcion"] as string,
                        Activa = (bool)reader["activa"],
                        CantidadPlatos = (int)reader["cantidad"]
                    });
                }
            }
            return lista;
        }

        public static Categoria ObtenerPorId(long id)
        {
            Categoria c = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT id, nombre, descripcion, activa FROM categoria WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    c = new Categoria
                    {
                        Id = (long)reader["id"],
                        Nombre = reader["nombre"].ToString(),
                        Descripcion = reader["descripcion"] as string,
                        Activa = (bool)reader["activa"]
                    };
                }
            }
            return c;
        }

        public static bool ExisteNombre(string nombre, long idExcluir)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM categoria WHERE nombre=@Nombre AND id<>@Id", con);
                cmd.Parameters.AddWithValue("@Nombre", nombre);
                cmd.Parameters.AddWithValue("@Id", idExcluir);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public void Guardar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO categoria (nombre, descripcion, activa) VALUES (@Nombre, @Descripcion, @Activa)", con);
                cmd.Parameters.AddWithValue("@Nombre", Nombre);
                cmd.Parameters.AddWithValue("@Descripcion", Conexion.V(Descripcion));
                cmd.Parameters.AddWithValue("@Activa", Activa);
                cmd.ExecuteNonQuery();
            }
        }

        public void Actualizar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE categoria SET nombre=@Nombre, descripcion=@Descripcion, activa=@Activa WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Nombre", Nombre);
                cmd.Parameters.AddWithValue("@Descripcion", Conexion.V(Descripcion));
                cmd.Parameters.AddWithValue("@Activa", Activa);
                cmd.Parameters.AddWithValue("@Id", Id);
                cmd.ExecuteNonQuery();
            }
        }
        public static bool Eliminar(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM categoria WHERE id=@Id AND NOT EXISTS (SELECT 1 FROM plato WHERE categoria_id=@Id)", con);
                cmd.Parameters.AddWithValue("@Id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }
}
