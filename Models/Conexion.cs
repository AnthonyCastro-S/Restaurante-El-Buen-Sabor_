
using System.Security.Cryptography;

namespace ElBuenSabor.Models
{
    public static class Conexion
    {
        // Cadena de conexión a SQL Server
        public static string Cadena =
            @"Server=.\SQLEXPRESS;Database=restaurante_db;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;";

        private const int Iteraciones = 100000;
        private const int TamanoSal = 16;
        private const int TamanoHash = 32;

        // Hash seguro con sal aleatoria (PBKDF2-SHA256)
        public static string Hash(string texto)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(TamanoSal);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                texto ?? "",
                sal,
                Iteraciones,
                HashAlgorithmName.SHA256,
                TamanoHash
            );

            return "PBKDF2$" + Iteraciones + "$" +
                   Convert.ToBase64String(sal) + "$" +
                   Convert.ToBase64String(hash);
        }

        public static bool Verificar(string texto, string almacenado)
        {
            try
            {
                if (string.IsNullOrEmpty(almacenado))
                    return false;

                var p = almacenado.Split('$');

                if (p.Length != 4 || p[0] != "PBKDF2" ||
                    !int.TryParse(p[1], out int iter))
                    return false;

                byte[] sal = Convert.FromBase64String(p[2]);
                byte[] esperado = Convert.FromBase64String(p[3]);

                byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(
                    texto ?? "",
                    sal,
                    iter,
                    HashAlgorithmName.SHA256,
                    esperado.Length
                );

                return CryptographicOperations.FixedTimeEquals(
                    calculado, esperado
                );
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static object V(object valor)
        {
            return valor ?? DBNull.Value;
        }
    }
}
