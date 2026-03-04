namespace LogiTrack.Models
{
    public class InventoryItem
    {
        [System.ComponentModel.DataAnnotations.Key]
        public int ItemId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public string Location { get; set; }

        public void DisplayInfo()
        {
            Console.WriteLine($"Item: {Name} | Quantity: {Quantity} | Location: {Location}");
        }
    }
}