using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

/// <summary>Assemble les vrais services clients/commandes sur une base en mémoire.</summary>
public sealed class CustomersOrdersContext : IDisposable
{
    public CustomersOrdersContext()
    {
        Customers = new CustomerService(Db, Session, Time);
        Orders = new OrderService(Db, Session, Time);
        SignInAs(UserRole.FrontDesk);
    }

    public TestDb Db { get; } = new();

    public TestTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero));

    public CurrentUserContext Session { get; } = new();

    public CustomerService Customers { get; }

    public OrderService Orders { get; }

    public DateTime Today => Time.GetLocalNow().Date;

    public void SignInAs(UserRole role) =>
        Session.SignIn(new UserDto(1, "Utilisateur Test", "test", role, true, null));

    public Task<CustomerDto> NewCustomerAsync(string name = "Awa Diallo", string phone = "0601020304") =>
        Customers.CreateAsync(new CreateCustomerDto(name, phone));

    public static List<CreateOrderItemDto> TwoLines() =>
        new() { new(1, "Chemise", 2, 1500m), new(2, "Costume", 1, 2500m) };

    public Task<OrderDetailsDto> NewOrderAsync(int customerId, decimal discount = 0m, decimal fees = 0m) =>
        Orders.CreateAsync(new CreateOrderDto(customerId, TwoLines(), Discount: discount, Fees: fees));

    public void Dispose() => Db.Dispose();
}

public class CustomersOrdersServiceTests
{
    // ---------- CLIENTS ----------

    [Fact]
    public async Task Customer_Create_AssignsSequentialNumbers()
    {
        using var ctx = new CustomersOrdersContext();

        var first = await ctx.NewCustomerAsync("Awa Diallo", "0601020304");
        var second = await ctx.NewCustomerAsync("Moussa Ba", "0605060708");

        Assert.Equal("CLI-0001", first.CustomerNumber);
        Assert.Equal("CLI-0002", second.CustomerNumber);
        Assert.True(first.IsActive);
    }

    [Fact]
    public async Task Customer_Create_RejectsMissingFieldsAndDuplicates()
    {
        using var ctx = new CustomersOrdersContext();
        await ctx.NewCustomerAsync("Awa Diallo", "0601020304");

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ctx.Customers.CreateAsync(new CreateCustomerDto("", "0601020304")));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ctx.Customers.CreateAsync(new CreateCustomerDto("Nouveau Client", "  ")));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ctx.Customers.CreateAsync(new CreateCustomerDto("awa diallo", "0601020304")));
    }

    [Fact]
    public async Task Customer_Search_FindsByNamePhoneAndNumber_AndHidesDeactivated()
    {
        using var ctx = new CustomersOrdersContext();
        var awa = await ctx.NewCustomerAsync("Awa Diallo", "0601020304");
        await ctx.NewCustomerAsync("Moussa Ba", "0605060708");

        Assert.Single(await ctx.Customers.SearchAsync("awa"));
        Assert.Single(await ctx.Customers.SearchAsync("0605"));
        Assert.Single(await ctx.Customers.SearchAsync("CLI-0002"));
        Assert.Equal(2, (await ctx.Customers.SearchAsync("")).Count);
        Assert.Empty(await ctx.Customers.SearchAsync("%"));

        await ctx.Customers.DeactivateAsync(awa.Id);

        Assert.Empty(await ctx.Customers.SearchAsync("awa"));
        var stored = await ctx.Customers.GetByIdAsync(awa.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
    }

    [Fact]
    public async Task Customer_Update_ChangesFields()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        var updated = await ctx.Customers.UpdateAsync(
            new UpdateCustomerDto(customer.Id, "Awa Diallo-Sow", "0699999999", "Dakar", "Cliente fidèle"));

        Assert.Equal("Awa Diallo-Sow", updated.FullName);
        Assert.Equal("0699999999", updated.Phone);
        Assert.Equal("Dakar", updated.Address);
        Assert.Equal(customer.CustomerNumber, updated.CustomerNumber);
    }

    // ---------- COMMANDES ----------

    [Fact]
    public async Task Order_Create_ComputesTotals_AndNumbers()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        var first = await ctx.NewOrderAsync(customer.Id, discount: 500m, fees: 200m);
        var second = await ctx.NewOrderAsync(customer.Id);

        Assert.Equal(NumberGenerator.OrderNumber(ctx.Today, 1), first.OrderNumber);
        Assert.Equal(NumberGenerator.OrderNumber(ctx.Today, 2), second.OrderNumber);
        Assert.Equal(5500m, first.Subtotal);
        Assert.Equal(5200m, first.Total);
        Assert.Equal(OrderStatus.Received, first.Status);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(customer.FullName, first.CustomerName);
    }

    [Fact]
    public async Task Order_Create_RejectsInvalidRequests()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();

        async Task AssertRejected(CreateOrderDto request) =>
            await Assert.ThrowsAsync<BusinessRuleException>(() => ctx.Orders.CreateAsync(request));

        await AssertRejected(new CreateOrderDto(customer.Id, new List<CreateOrderItemDto>()));
        await AssertRejected(new CreateOrderDto(
            customer.Id, new List<CreateOrderItemDto> { new(1, "Chemise", 0, 1500m) }));
        await AssertRejected(new CreateOrderDto(
            customer.Id, new List<CreateOrderItemDto> { new(1, "Chemise", 1, -5m) }));
        await AssertRejected(new CreateOrderDto(
            customer.Id, CustomersOrdersContext.TwoLines(), Discount: 99999m));
        await AssertRejected(new CreateOrderDto(
            customer.Id, CustomersOrdersContext.TwoLines(), ExpectedDate: ctx.Today.AddDays(-1)));
        await AssertRejected(new CreateOrderDto(
            customer.Id + 100, CustomersOrdersContext.TwoLines()));
    }

    [Fact]
    public async Task Order_Create_RejectsDeactivatedCustomer()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();
        await ctx.Customers.DeactivateAsync(customer.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => ctx.NewOrderAsync(customer.Id));
    }

    [Fact]
    public async Task Order_GetByNumber_FindsOrder_AndReturnsNullIfUnknown()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();
        var order = await ctx.NewOrderAsync(customer.Id);

        var found = await ctx.Orders.GetByNumberAsync(order.OrderNumber.ToLowerInvariant());

        Assert.NotNull(found);
        Assert.Equal(order.Id, found!.Id);
        Assert.Equal(2, found.Items.Count);
        Assert.Null(await ctx.Orders.GetByNumberAsync("CMD-00000000-0001"));
        Assert.Null(await ctx.Orders.GetByNumberAsync("   "));
    }

    [Fact]
    public async Task Order_GetByCustomer_ListsNewestFirst()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();
        var other = await ctx.NewCustomerAsync("Moussa Ba", "0605060708");

        var first = await ctx.NewOrderAsync(customer.Id);
        ctx.Time.Advance(TimeSpan.FromMinutes(5));
        var second = await ctx.NewOrderAsync(customer.Id);
        await ctx.NewOrderAsync(other.Id);

        var orders = await ctx.Orders.GetByCustomerAsync(customer.Id);

        Assert.Equal(2, orders.Count);
        Assert.Equal(second.OrderNumber, orders[0].OrderNumber);
        Assert.Equal(first.OrderNumber, orders[1].OrderNumber);
    }

    // ---------- LISTE DES COMMANDES (onglet Commandes) ----------

    [Fact]
    public async Task Order_Search_ListsNewestFirstAndFiltersByNumberOrCustomer()
    {
        using var ctx = new CustomersOrdersContext();
        var awa = await ctx.NewCustomerAsync("Awa Diallo", "0601020304");
        var moussa = await ctx.NewCustomerAsync("Moussa Ba", "0605060708");
        var first = await ctx.NewOrderAsync(awa.Id);
        ctx.Time.Advance(TimeSpan.FromMinutes(5));
        var second = await ctx.NewOrderAsync(moussa.Id);

        var all = await ctx.Orders.SearchAsync(string.Empty);
        Assert.Equal(2, all.Count);
        Assert.Equal(second.OrderNumber, all[0].OrderNumber);

        var byCustomer = await ctx.Orders.SearchAsync("awa");
        Assert.Single(byCustomer);
        Assert.Equal(first.OrderNumber, byCustomer[0].OrderNumber);

        var byNumber = await ctx.Orders.SearchAsync(second.OrderNumber);
        Assert.Single(byNumber);
        Assert.Equal("Moussa Ba", byNumber[0].CustomerName);
    }

    [Fact]
    public async Task Order_Search_RequiresViewOrdersPermission()
    {
        using var ctx = new CustomersOrdersContext();
        ctx.SignInAs(UserRole.StockManager);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => ctx.Orders.SearchAsync(string.Empty));
    }

    // ---------- DROITS ----------

    [Fact]
    public async Task Permissions_AreChecked()
    {
        using var ctx = new CustomersOrdersContext();
        var customer = await ctx.NewCustomerAsync();
        var order = await ctx.NewOrderAsync(customer.Id);

        // Un caissier peut consulter les commandes, mais pas créer clients ni commandes
        ctx.SignInAs(UserRole.Cashier);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => ctx.NewCustomerAsync("Autre Client", "0600000000"));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => ctx.NewOrderAsync(customer.Id));
        Assert.NotNull(await ctx.Orders.GetByNumberAsync(order.OrderNumber));

        // Personne de connecté : tout est refusé
        ctx.Session.SignOut();
        await Assert.ThrowsAsync<PermissionDeniedException>(() => ctx.Customers.SearchAsync("awa"));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => ctx.Orders.GetByNumberAsync(order.OrderNumber));
    }
}