namespace ProductionOrderAPI.Models
{
    public class Apontamento
    {
        public int OrderNumber { get; set; }
        public int OperationNumber { get; set; }
        public double Quantity { get; set; }
        public DateTime? ProductionDateTime { get; set; }
    }
}
