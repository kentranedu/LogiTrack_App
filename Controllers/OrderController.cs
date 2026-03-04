using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _context;

        public OrderController(LogiTrackContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(order => order.Items)
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderDetailsResponse>> GetOrderById(int id)
        {
            var order = await _context.Orders
                .Where(currentOrder => currentOrder.OrderId == id)
                .Select(currentOrder => new OrderDetailsResponse
                {
                    OrderId = currentOrder.OrderId,
                    CustomerName = currentOrder.CustomerName,
                    DatePlaced = currentOrder.DatePlaced,
                    Items = currentOrder.Items
                        .Select(item => new OrderItemDetailsResponse
                        {
                            ItemId = item.ItemId,
                            Name = item.Name,
                            Quantity = item.Quantity,
                            Location = item.Location
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return Ok(order);
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName))
            {
                return BadRequest("CustomerName is required.");
            }

            var order = new Order
            {
                CustomerName = request.CustomerName,
                DatePlaced = request.DatePlaced ?? System.DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            if (request.ItemIds is { Count: > 0 })
            {
                var itemsToAttach = await _context.InventoryItems
                    .Where(item => request.ItemIds.Contains(item.ItemId))
                    .ToListAsync();

                foreach (var item in itemsToAttach)
                {
                    item.OrderId = order.OrderId;
                }

                await _context.SaveChangesAsync();
            }

            var createdOrder = await _context.Orders
                .Include(currentOrder => currentOrder.Items)
                .FirstAsync(currentOrder => currentOrder.OrderId == order.OrderId);

            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.OrderId }, createdOrder);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateOrderRequest
    {
        public string CustomerName { get; set; } = string.Empty;
        public System.DateTime? DatePlaced { get; set; }
        public List<int>? ItemIds { get; set; }
    }

    public class OrderDetailsResponse
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public System.DateTime DatePlaced { get; set; }
        public List<OrderItemDetailsResponse> Items { get; set; } = new();
    }

    public class OrderItemDetailsResponse
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Location { get; set; } = string.Empty;
    }
}