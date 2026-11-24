using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Plato
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public decimal Precio { get; set; }
        public bool Disponible { get; set; } = true;
        public string Imagen { get; set; }
        public long CategoriaId { get; set; }
        public string Categoria { get; set; }

        private const string Select =
            "SELECT p.id, p.nombre, p.descripcion, p.precio, p.disponible, p.imagen, p.categoria_id, c.nombre AS categoria " +
            "FROM plato p INNER JOIN categoria c ON p.categoria_id = c.id ";

        private static Plato Leer(SqlDataReader r)
        {
            return new Plato
            {
                Id = (long)r["id"],
                Nombre = r["nombre"].ToString(),
                Descripcion = r["descripcion"] as string,
                Precio = (decimal)r["precio"],
                Disponible = (bool)r["disponible"],
                Imagen = r["imagen"] as string,
                CategoriaId = (long)r["categoria_id"],
                Categoria = r["categoria"].ToString()
            };
        }

        public static List<Plato> ObtenerTodos(string buscar, long categoriaId, bool soloDisponibles)
        {
            var lista = new List<Plato>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select +
                    "WHERE p.nombre LIKE @Buscar AND (@Cat = 0 OR p.categoria_id = @Cat) " +
                    (soloDisponibles ? "AND p.disponible = 1 AND c.activa = 1 " : "") +
                    "ORDER BY c.id, p.nombre", con);
                cmd.Parameters.AddWithValue("@Buscar", "%" + (buscar ?? "") + "%");
                cmd.Parameters.AddWithValue("@Cat", categoriaId);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(Leer(reader));
            }
            return lista;
        }

        public static Plato ObtenerPorId(long id)
        {
            Plato plato = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE p.id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) plato = Leer(reader);
            }
            return plato;
        }

        public static bool ExisteNombre(string nombre, long idExcluir)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM plato WHERE nombre=@Nombre AND id<>@Id", con);
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
                SqlCommand cmd = new SqlCommand("INSERT INTO plato (nombre, descripcion, precio, disponible, categoria_id, imagen) VALUES (@Nombre, @Descripcion, @Precio, @Disponible, @CategoriaId, @Imagen)", con);
                Parametros(cmd);
                cmd.ExecuteNonQuery();
            }
        }

        public void Actualizar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE plato SET nombre=@Nombre, descripcion=@Descripcion, precio=@Precio, disponible=@Disponible, categoria_id=@CategoriaId, imagen=@Imagen, updated_at=GETDATE() WHERE id=@Id", con);
                Parametros(cmd);
                cmd.Parameters.AddWithValue("@Id", Id);
                cmd.ExecuteNonQuery();
            }
        }

        private void Parametros(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Nombre", Nombre);
            cmd.Parameters.AddWithValue("@Descripcion", Conexion.V(Descripcion));
            cmd.Parameters.AddWithValue("@Precio", Precio);
            cmd.Parameters.AddWithValue("@Disponible", Disponible);
            cmd.Parameters.AddWithValue("@CategoriaId", CategoriaId);
            cmd.Parameters.AddWithValue("@Imagen", Conexion.V(Imagen));
        }

        public static void CambiarDisponibilidad(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE plato SET disponible = CASE WHEN disponible=1 THEN 0 ELSE 1 END WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
        public static bool Eliminar(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("DELETE FROM plato WHERE id=@Id AND NOT EXISTS (SELECT 1 FROM pedido_plato WHERE plato_id=@Id)", con);
                cmd.Parameters.AddWithValue("@Id", id);
                if (cmd.ExecuteNonQuery() > 0) return true;

                SqlCommand desactivar = new SqlCommand("UPDATE plato SET disponible=0 WHERE id=@Id", con);
                desactivar.Parameters.AddWithValue("@Id", id);
                desactivar.ExecuteNonQuery();
                return false;
            }
        }
    }
}
