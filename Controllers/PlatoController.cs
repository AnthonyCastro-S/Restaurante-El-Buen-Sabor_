using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ElBuenSabor.Models;

namespace ElBuenSabor.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class PlatoController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long TamanoMaximo = 2 * 1024 * 1024;

        public PlatoController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult Index(string buscar, long categoriaId = 0)
        {
            ViewBag.Buscar = buscar;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.Categorias = Categoria.ObtenerTodas(false);
            return View(Plato.ObtenerTodos(buscar, categoriaId, false));
        }

        public IActionResult Create()
        {
            ViewBag.Categorias = Categoria.ObtenerTodas(true);
            return View("Form", new Plato());
        }

        [HttpPost]
        public IActionResult Create(Plato plato, IFormFile imagenArchivo)
        {
            Validar(plato);
            ValidarImagen(imagenArchivo);
            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = Categoria.ObtenerTodas(true);
                return View("Form", plato);
            }
            plato.Nombre = plato.Nombre.Trim();
            plato.Imagen = GuardarImagen(imagenArchivo);
            plato.Guardar();
            TempData["Ok"] = "Plato registrado en la carta.";
            return RedirectToAction("Index");
        }

        public IActionResult Edit(long id)
        {
            var plato = Plato.ObtenerPorId(id);
            if (plato == null) return RedirectToAction("Index");
            ViewBag.Categorias = Categoria.ObtenerTodas(false);
            return View("Form", plato);
        }

        [HttpPost]
        public IActionResult Edit(Plato plato, IFormFile imagenArchivo, bool quitarImagen)
        {
            Validar(plato);
            ValidarImagen(imagenArchivo);
            var actual = Plato.ObtenerPorId(plato.Id);
            if (actual == null) return RedirectToAction("Index");
            plato.Imagen = actual.Imagen;

            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = Categoria.ObtenerTodas(false);
                return View("Form", plato);
            }
            plato.Nombre = plato.Nombre.Trim();

            if (imagenArchivo != null && imagenArchivo.Length > 0)
            {
                plato.Imagen = GuardarImagen(imagenArchivo);
                BorrarImagen(actual.Imagen);
            }
            else if (quitarImagen)
            {
                plato.Imagen = null;
                BorrarImagen(actual.Imagen);
            }
            plato.Actualizar();
            TempData["Ok"] = "Plato actualizado.";
            return RedirectToAction("Index");
        }

        private void ValidarImagen(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0) return;
            string ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!ExtensionesPermitidas.Contains(ext) || !(archivo.ContentType ?? "").StartsWith("image/"))
                ModelState.AddModelError("", "La imagen debe ser JPG, PNG o WEBP.");
            else if (archivo.Length > TamanoMaximo)
                ModelState.AddModelError("", "La imagen no puede superar los 2 MB.");
        }
        private string GuardarImagen(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0) return null;
            string carpeta = Path.Combine(_env.WebRootPath, "uploads", "platos");
            Directory.CreateDirectory(carpeta);
            string nombre = Guid.NewGuid().ToString("N") + Path.GetExtension(archivo.FileName).ToLowerInvariant();
            using (var stream = new FileStream(Path.Combine(carpeta, nombre), FileMode.Create))
            {
                archivo.CopyTo(stream);
            }
            return "/uploads/platos/" + nombre;
        }

        private void BorrarImagen(string ruta)
        {
            if (string.IsNullOrEmpty(ruta) || !ruta.StartsWith("/uploads/platos/")) return;
            string fisica = Path.Combine(_env.WebRootPath, "uploads", "platos", Path.GetFileName(ruta));
            if (System.IO.File.Exists(fisica)) System.IO.File.Delete(fisica);
        }

        private void Validar(Plato plato)
        {
            if (string.IsNullOrWhiteSpace(plato.Nombre))
                ModelState.AddModelError("", "El nombre del plato es obligatorio.");
            else if (plato.Precio <= 0)
                ModelState.AddModelError("", "El precio debe ser mayor que 0.");
            else if (plato.CategoriaId == 0)
                ModelState.AddModelError("", "Selecciona una categoría.");
            else if (Plato.ExisteNombre(plato.Nombre.Trim(), plato.Id))
                ModelState.AddModelError("", "Ya existe un plato con ese nombre.");
        }

        public IActionResult CambiarDisponibilidad(long id)
        {
            Plato.CambiarDisponibilidad(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(long id)
        {
            var plato = Plato.ObtenerPorId(id);
            if (Plato.Eliminar(id))
            {
                if (plato != null) BorrarImagen(plato.Imagen);
                TempData["Ok"] = "Plato eliminado.";
            }
            else TempData["Ok"] = "El plato ya fue usado en pedidos: se marcó como no disponible en lugar de eliminarlo.";
            return RedirectToAction("Index");
        }
    }
}
