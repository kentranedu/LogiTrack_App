using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        // In-memory storage for demonstration
        private static List<Order> _orders = new List<Order>();
        private static int _nextId = 1;

        [HttpGet]
        public ActionResult<IEnumerable<Order>> GetAllOrders()
        {
            return Ok(_orders);
        }

        [HttpGet("{id}")]
        public ActionResult<Order> GetOrderById(int id)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            return Ok(order);
        }

        [HttpPost]
        public ActionResult<Order> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return BadRequest("Order must contain at least one item");

            var order = new Order
            {
                Id = _nextId++,
                Items = request.Items,
                CreatedDate = System.DateTime.UtcNow
            };

            _orders.Add(order);
            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
        }

        [HttpDelete("{id}")]
        public ActionResult DeleteOrder(int id)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
                return NotFound();

            _orders.Remove(order);
            return NoContent();
        }
    }

    public class Order
    {
        public int Id { get; set; }
        public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();
        public System.DateTime CreatedDate { get; set; }
    }

    public class InventoryItem
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class CreateOrderRequest
    {
        public List<InventoryItem> Items { get; set; }
    }
}