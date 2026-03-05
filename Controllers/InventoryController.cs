using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LogiTrack.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;

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
        private static readonly TimeSpan InventoryCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly SemaphoreSlim InventoryCacheLock = new(1, 1);

        private static string InventoryItemCacheKey(int id) => $"inventory_item_{id}";

        private static MemoryCacheEntryOptions BuildCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = InventoryCacheDuration,
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };
        }

        private async Task<(List<InventoryItem> Inventory, bool WasCacheHit)> GetOrRehydrateInventoryAsync()
        {
            if (_cache.TryGetValue(InventoryCacheKey, out List<InventoryItem>? cachedInventory) && cachedInventory is not null)
            {
                return (cachedInventory, true);
            }

            await InventoryCacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(InventoryCacheKey, out cachedInventory) && cachedInventory is not null)
                {
                    return (cachedInventory, true);
                }

                var rehydratedInventory = await _context.InventoryItems
                    .AsNoTracking()
                    .ToListAsync();

                _cache.Set(InventoryCacheKey, rehydratedInventory, BuildCacheOptions());

                foreach (var inventoryItem in rehydratedInventory)
                {
                    _cache.Set(InventoryItemCacheKey(inventoryItem.ItemId), inventoryItem, BuildCacheOptions());
                }

                return (rehydratedInventory, false);
            }
            finally
            {
                InventoryCacheLock.Release();
            }
        }

        private async Task<List<InventoryItem>> ForceRehydrateInventoryAsync()
        {
            await InventoryCacheLock.WaitAsync();
            try
            {
                var rehydratedInventory = await _context.InventoryItems
                    .AsNoTracking()
                    .ToListAsync();

                _cache.Set(InventoryCacheKey, rehydratedInventory, BuildCacheOptions());

                foreach (var inventoryItem in rehydratedInventory)
                {
                    _cache.Set(InventoryItemCacheKey(inventoryItem.ItemId), inventoryItem, BuildCacheOptions());
                }

                return rehydratedInventory;
            }
            finally
            {
                InventoryCacheLock.Release();
            }
        }

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

            var (inventory, wasCacheHit) = await GetOrRehydrateInventoryAsync();

            stopwatch.Stop();
            Response.Headers["X-Inventory-Cache"] = wasCacheHit ? "HIT" : "REHYDRATED";
            Response.Headers["X-Inventory-Elapsed-Ms"] = stopwatch.ElapsedMilliseconds.ToString();
            _logger.LogInformation("Inventory cache {CacheState} in {ElapsedMs} ms", wasCacheHit ? "HIT" : "REHYDRATED", stopwatch.ElapsedMilliseconds);

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

            if (_cache.TryGetValue(InventoryCacheKey, out List<InventoryItem>? cachedInventory) && cachedInventory is not null)
            {
                var itemFromInventoryCache = cachedInventory.FirstOrDefault(currentItem => currentItem.ItemId == id);
                if (itemFromInventoryCache is not null)
                {
                    _cache.Set(itemCacheKey, itemFromInventoryCache, BuildCacheOptions());
                    return itemFromInventoryCache;
                }
            }

            var (rehydratedInventory, _) = await GetOrRehydrateInventoryAsync();
            var rehydratedItem = rehydratedInventory.FirstOrDefault(currentItem => currentItem.ItemId == id);
            if (rehydratedItem is not null)
            {
                return rehydratedItem;
            }

            return NotFound(ApiError.Create("NotFound", $"Inventory item with id {id} was not found.", HttpContext.TraceIdentifier));
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

            var itemCacheKey = InventoryItemCacheKey(item.ItemId);
            _cache.Set(itemCacheKey, item, BuildCacheOptions());

            await ForceRehydrateInventoryAsync();
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

            _cache.Remove(InventoryItemCacheKey(id));

            await ForceRehydrateInventoryAsync();
            return NoContent();
        }
    }
}