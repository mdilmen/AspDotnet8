using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using WiredBrainCoffee.MinApi;
using WiredBrainCoffee.MinApi.Services;
using WiredBrainCoffee.MinApi.Services.Interfaces;
using WiredBrainCoffee.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddKeyedScoped<IOrderService, OrderService>("consumer");
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddOutputCache();
builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = "your_connection_string";
    options.InstanceName = "dotnet8redis";
});
builder.Services.AddRequestTimeouts();
builder.Services.AddHttpClient();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain
    .Insert(0, AotJsonSerializerContext.Default);
});
builder.Services.AddCors();
builder.Services.AddAntiforgery();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseOutputCache();
app.UseHttpsRedirection();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseRequestTimeouts();
app.UseAntiforgery();

app.MapGet("/liveness", () =>
{
    return "Alive";
}).ShortCircuit();

app.MapShortCircuit(400, "robots.txt", "sitemap.xml");

app.MapGet("/orders", (
    [FromKeyedServices("consumer")]IOrderService orderService) =>
{
    return orderService.GetOrders();
});

app.MapGet("/orders/{id}", (
    [FromKeyedServices("consumer")] IOrderService orderService, int id) =>
{
    return orderService.GetOrderById(id);
});

app.MapPost("/contact-collection", (IFormCollection collection) =>
{
    var name = collection["name"];

    // TODO: Save to db
});

app.MapPost("/contact", (HttpContext context, [FromForm]Contact contact) =>
{
    contact.SubmittedTime = DateTime.Now;

    return contact;
});

app.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
{
    return antiforgery.GetAndStoreTokens(context);
});

app.MapGet("/menu", (IMenuService menuService) =>
{
    return menuService.GetMenuItems();
});

app.MapGet("/inventory", async (HttpContext context, IHttpClientFactory factory) => {

    var client = factory.CreateClient();
    var stockCount = await client
    .GetStringAsync("https://localhost:7210/inventory", context.RequestAborted);

    return stockCount;
})
.WithRequestTimeout(TimeSpan.FromSeconds(5));

app.Run();

[JsonSerializable(typeof(List<MenuItem>))]
[JsonSerializable(typeof(List<Order>))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Contact))]
public partial class AotJsonSerializerContext : JsonSerializerContext
{

}

class MyUser : IdentityUser { }

class AppDbContext : IdentityDbContext<MyUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}