using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;

public class LogiTrackContext : DbContext
{
	public LogiTrackContext()
	{
	}

	public LogiTrackContext(DbContextOptions<LogiTrackContext> options)
		: base(options)
	{
	}

	public DbSet<InventoryItem> InventoryItems { get; set; }
	public DbSet<Order> Orders { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		if (!options.IsConfigured)
		{
			options.UseSqlite("Data Source=logitrack.db");
		}
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Order>()
			.HasMany(order => order.Items)
			.WithOne(item => item.Order)
			.HasForeignKey(item => item.OrderId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}