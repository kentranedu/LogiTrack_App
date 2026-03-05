using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly LogiTrackContext _context;

        public OrderController(LogiTrackContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders([FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            var ordersQuery = _context.Orders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(order => order.Items);

            if (page.HasValue || pageSize.HasValue)
            {
                var currentPage = Math.Max(page ?? 1, 1);
                var currentPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
                var totalCount = await ordersQuery.CountAsync();

                var pagedOrders = await ordersQuery
                    .OrderByDescending(order => order.DatePlaced)
                    .Skip((currentPage - 1) * currentPageSize)
                    .Take(currentPageSize)
                    .ToListAsync();

                Response.Headers["X-Pagination-Page"] = currentPage.ToString();
                Response.Headers["X-Pagination-PageSize"] = currentPageSize.ToString();
                Response.Headers["X-Pagination-TotalCount"] = totalCount.ToString();

                return Ok(pagedOrders);
            }

            var orders = await ordersQuery.ToListAsync();

            return Ok(orders);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderDetailsResponse>> GetOrderById(int id)
        {
            var order = await _context.Orders
                .AsNoTracking()
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
                return NotFound(ApiError.Create("NotFound", $"Order with id {id} was not found.", HttpContext.TraceIdentifier));
            }

            return Ok(order);
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerName))
            {
                return BadRequest(ApiError.Create("ValidationError", "CustomerName is required.", HttpContext.TraceIdentifier));
            }

            var order = new Order
            {
                CustomerName = request.CustomerName,
                DatePlaced = request.DatePlaced ?? System.DateTime.UtcNow
            };

            _context.Orders.Add(order);

            if (request.ItemIds is { Count: > 0 })
            {
                var itemsToAttach = await _context.InventoryItems
                    .Where(item => request.ItemIds.Contains(item.ItemId))
                    .ToListAsync();

                foreach (var item in itemsToAttach)
                {
                    order.Items.Add(item);
                }
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrderById), new { id = order.OrderId }, order);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult> DeleteOrder(int id)
        {
            var deletedCount = await _context.Orders
                .Where(order => order.OrderId == id)
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
            {
                return NotFound(ApiError.Create("NotFound", $"Order with id {id} was not found.", HttpContext.TraceIdentifier));
            }

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