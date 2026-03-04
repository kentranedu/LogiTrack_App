using LogiTrack.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

Console.WriteLine("=== InventoryItem DisplayInfo Test ===");
var sampleItem = new InventoryItem
{
    ItemId = 1,
    Name = "Wireless Mouse",
    Quantity = 25,
    Location = "Aisle B3"
};
sampleItem.DisplayInfo();

Console.WriteLine();
Console.WriteLine("=== Order Add/Remove/Summary Test ===");
var order = new Order
{
    OrderId = 1001,
    CustomerName = "Acme Corp",
    DatePlaced = DateTime.Now
};

var keyboard = new InventoryItem
{
    ItemId = 2,
    Name = "Mechanical Keyboard",
    Quantity = 10,
    Location = "Aisle C1"
};

var monitor = new InventoryItem
{
    ItemId = 3,
    Name = "24-inch Monitor",
    Quantity = 5,
    Location = "Aisle D2"
};

order.AddItem(sampleItem);
order.AddItem(keyboard);
order.AddItem(monitor);

order.RemoveItem(2);

Console.WriteLine(order.GetOrderSummary());

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
