using System;
using System.Collections.Generic;
using System.Linq;

namespace LogiTrack.Models
{
    public class Order
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public DateTime DatePlaced { get; set; }
        public List<InventoryItem> Items { get; set; }

        public Order()
        {
            Items = new List<InventoryItem>();
        }

        public void AddItem(InventoryItem item)
        {
            if (item != null)
            {
                Items.Add(item);
            }
        }

        public void RemoveItem(int itemId)
        {
            Items.RemoveAll(item => item.ItemId == itemId);
        }

        public string GetOrderSummary()
        {
            return $"Order #{OrderId} for {CustomerName} | Items: {Items.Count} | Placed: {DatePlaced:M/d/yyyy}";
        }
    }
}