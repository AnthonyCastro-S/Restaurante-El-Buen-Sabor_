namespace ElBuenSabor.Models
{
    public class DetallePedido
    {
        public long Id { get; set; }
        public long PedidoId { get; set; }
        public long PlatoId { get; set; }
        public string PlatoNombre { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public string Notas { get; set; }
    }
}
