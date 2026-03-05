using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LogiTrack.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;
        private const string InventoryCacheKey = "inventory_all";

        public InventoryController(LogiTrackContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            if (_cache.TryGetValue(InventoryCacheKey, out List<InventoryItem>? cachedInventory) && cachedInventory is not null)
            {
                return cachedInventory;
            }

            var inventory = await _context.InventoryItems
                .AsNoTracking()
                .ToListAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            };

            _cache.Set(InventoryCacheKey, inventory, cacheEntryOptions);

            return inventory;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(int id)
        {
            var item = await _context.InventoryItems
                .AsNoTracking()
                .FirstOrDefaultAsync(currentItem => currentItem.ItemId == id);
            if (item == null)
            {
                return NotFound(ApiError.Create("NotFound", $"Inventory item with id {id} was not found.", HttpContext.TraceIdentifier));
            }

            return item;
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<InventoryItem>> AddInventoryItem([FromBody] InventoryItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Location))
            {
                return BadRequest(ApiError.Create("ValidationError", "Name and Location are required.", HttpContext.TraceIdentifier));
            }

            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            _cache.Remove(InventoryCacheKey);
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.ItemId }, item);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var deletedCount = await _context.InventoryItems
                .Where(item => item.ItemId == id)
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return NotFound(ApiError.Create("NotFound", $"Inventory item with id {id} was not found.", HttpContext.TraceIdentifier));

            _cache.Remove(InventoryCacheKey);
            return NoContent();
        }
    }
}