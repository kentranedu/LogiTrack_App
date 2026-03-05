using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LogiTrack.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LogiTrack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly LogiTrackContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<InventoryController> _logger;
        private const string InventoryCacheKey = "inventory_all";
        private static readonly TimeSpan InventoryCacheDuration = TimeSpan.FromSeconds(30);

        private static string InventoryItemCacheKey(int id) => $"inventory_item_{id}";

        public InventoryController(LogiTrackContext context, IMemoryCache cache, ILogger<InventoryController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            var stopwatch = Stopwatch.StartNew();

            if (_cache.TryGetValue(InventoryCacheKey, out List<InventoryItem>? cachedInventory) && cachedInventory is not null)
            {
                stopwatch.Stop();
                Response.Headers["X-Inventory-Cache"] = "HIT";
                Response.Headers["X-Inventory-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
                _logger.LogInformation("Inventory cache HIT in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
                return cachedInventory;
            }

            var inventory = await _context.InventoryItems
                .AsNoTracking()
                .ToListAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InventoryCacheDuration
            };

            _cache.Set(InventoryCacheKey, inventory, cacheEntryOptions);

            stopwatch.Stop();
            Response.Headers["X-Inventory-Cache"] = "MISS";
            Response.Headers["X-Inventory-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
            _logger.LogInformation("Inventory cache MISS in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);

            return inventory;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(int id)
        {
            var itemCacheKey = InventoryItemCacheKey(id);

            if (_cache.TryGetValue(itemCacheKey, out InventoryItem? cachedItem) && cachedItem is not null)
            {
                return cachedItem;
            }

            var item = await _context.InventoryItems
                .AsNoTracking()
                .FirstOrDefaultAsync(currentItem => currentItem.ItemId == id);
            if (item == null)
            {
                return NotFound(ApiError.Create("NotFound", $"Inventory item with id {id} was not found.", HttpContext.TraceIdentifier));
            }

            _cache.Set(itemCacheKey, item, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InventoryCacheDuration
            });

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
            _cache.Set(InventoryItemCacheKey(item.ItemId), item, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InventoryCacheDuration
            });
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
            _cache.Remove(InventoryItemCacheKey(id));
            return NoContent();
        }
    }
}