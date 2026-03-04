using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;

public class LogiTrackContext : DbContext
{
	public DbSet<InventoryItem> InventoryItems { get; set; }
	public DbSet<Order> Orders { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder options)
		=> options.UseSqlite("Data Source=logitrack.db");

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Order>()
			.HasMany(order => order.Items)
			.WithOne(item => item.Order)
			.HasForeignKey(item => item.OrderId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}