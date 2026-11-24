using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Entities;

namespace PressingManager.Tests.Integration;

/// <summary>Une ligne de commande doit referencer un service actif du catalogue (cle etrangere OrderItems -> Services).</summary>
public class OrderItemServiceRelationTests
{
    private static List<CreateOrderItemDto> OneLine(int serviceId) =>
        new() { new CreateOrderItemDto(serviceId, "Chemise", 1, 1500m) };

    [Fact]
    public async Task CreateOrder_WithUnknownService_IsRefusedWithAClearMessage()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ctx.Orders.CreateAsync(new CreateOrderDto(customer.Id, OneLine(999))));

        Assert.Contains("service", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrder_WithDeactivatedService_IsRefused()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        using (var db = ctx.Db.CreateDbContext())
        {
            db.Services.Single(s => s.Id == 1).IsActive = false;
            db.SaveChanges();
        }

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ctx.Orders.CreateAsync(new CreateOrderDto(customer.Id, OneLine(1))));
    }

    [Fact]
    public async Task CreateOrder_WithActiveService_Succeeds()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        var order = await ctx.Orders.CreateAsync(new CreateOrderDto(customer.Id, OneLine(1)));

        Assert.Equal(1, Assert.Single(order.Items).ServiceId);
    }

    [Fact]
    public async Task DeletingAServiceUsedByAnOrder_IsRefusedByTheDatabase()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();
        await ctx.Orders.CreateAsync(new CreateOrderDto(customer.Id, OneLine(1)));

        using var db = ctx.Db.CreateDbContext();
        db.Services.Remove(db.Services.Single(s => s.Id == 1));

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task InsertingAnOrderLineWithUnknownService_IsRefusedByTheDatabase()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        using var db = ctx.Db.CreateDbContext();
        db.Orders.Add(new Order
        {
            OrderNumber = "CMD-FK-0001",
            CustomerId = customer.Id,
            Items = { new OrderItem { ServiceId = 999, ItemType = "Chemise", Quantity = 1, UnitPrice = 1000m } }
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
