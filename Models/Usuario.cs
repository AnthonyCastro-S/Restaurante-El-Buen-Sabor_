using Microsoft.Data.SqlClient;

namespace ElBuenSabor.Models
{
    public class Usuario
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Rol { get; set; } = "MESERO";
        public bool Activo { get; set; } = true;
        public string NombreCompleto { get { return Nombre + " " + Apellido; } }

        private const string Select = "SELECT id, nombre, apellido, email, rol, activo FROM usuario ";

        private static Usuario Leer(SqlDataReader r)
        {
            return new Usuario
            {
                Id = (long)r["id"],
                Nombre = r["nombre"].ToString(),
                Apellido = r["apellido"].ToString(),
                Email = r["email"].ToString(),
                Rol = r["rol"].ToString(),
                Activo = (bool)r["activo"]
            };
        }

        public static Usuario Validar(string email, string password)
        {
            Usuario usuario = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT id, nombre, apellido, email, rol, activo, password FROM usuario WHERE email=@Email AND activo=1", con);
                cmd.Parameters.AddWithValue("@Email", email ?? "");
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read() && Conexion.Verificar(password, reader["password"].ToString()))
                    usuario = Leer(reader);
            }
            return usuario;
        }

        public static List<Usuario> ObtenerTodos(string buscar, string rol)
        {
            var lista = new List<Usuario>();
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select +
                    "WHERE (nombre + ' ' + apellido LIKE @Buscar OR email LIKE @Buscar) " +
                    "AND (@Rol = '' OR rol = @Rol) ORDER BY nombre", con);
                cmd.Parameters.AddWithValue("@Buscar", "%" + (buscar ?? "") + "%");
                cmd.Parameters.AddWithValue("@Rol", rol ?? "");
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) lista.Add(Leer(reader));
            }
            return lista;
        }

        public static Usuario ObtenerPorId(long id)
        {
            Usuario usuario = null;
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand(Select + "WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) usuario = Leer(reader);
            }
            return usuario;
        }

        public static bool ExisteEmail(string email, long idExcluir)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM usuario WHERE email=@Email AND id<>@Id", con);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@Id", idExcluir);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public void Guardar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("INSERT INTO usuario (nombre, apellido, email, password, rol, activo) VALUES (@Nombre, @Apellido, @Email, @Password, @Rol, @Activo)", con);
                cmd.Parameters.AddWithValue("@Nombre", Nombre);
                cmd.Parameters.AddWithValue("@Apellido", Apellido);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Password", Conexion.Hash(Password));
                cmd.Parameters.AddWithValue("@Rol", Rol);
                cmd.Parameters.AddWithValue("@Activo", Activo);
                cmd.ExecuteNonQuery();
            }
        }

        public void Actualizar()
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                string sql = "UPDATE usuario SET nombre=@Nombre, apellido=@Apellido, email=@Email, rol=@Rol, activo=@Activo" +
                             (string.IsNullOrEmpty(Password) ? "" : ", password=@Password") + " WHERE id=@Id";
                SqlCommand cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@Nombre", Nombre);
                cmd.Parameters.AddWithValue("@Apellido", Apellido);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Rol", Rol);
                cmd.Parameters.AddWithValue("@Activo", Activo);
                cmd.Parameters.AddWithValue("@Id", Id);
                if (!string.IsNullOrEmpty(Password)) cmd.Parameters.AddWithValue("@Password", Conexion.Hash(Password));
                cmd.ExecuteNonQuery();
            }
        }
        public static void CambiarEstado(long id)
        {
            using (SqlConnection con = new SqlConnection(Conexion.Cadena))
            {
                con.Open();
                SqlCommand cmd = new SqlCommand("UPDATE usuario SET activo = CASE WHEN activo=1 THEN 0 ELSE 1 END WHERE id=@Id", con);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
