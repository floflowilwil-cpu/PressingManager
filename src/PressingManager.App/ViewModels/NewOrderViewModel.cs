using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record CustomerOption(int Id, string FullName, string Phone, string CustomerNumber)
{
    public string DisplayLabel => $"{FullName} ({Phone}) — {CustomerNumber}";
}

public sealed record ServiceOption(int Id, string Name, decimal DefaultPrice, string DisplayLabel);

public sealed record DraftOrderItem(
    int ServiceId,
    string ServiceName,
    string ItemType,
    int Quantity,
    decimal UnitPrice,
    string? Observations)
{
    public decimal LineTotal => Quantity * UnitPrice;

    public string UnitPriceText => MoneyFormatter.FormatFcfa(UnitPrice);

    public string LineTotalText => MoneyFormatter.FormatFcfa(LineTotal);
}

/// <summary>
/// ViewModel du dialogue de creation d'une commande : selection ou creation rapide de client,
/// ajout d'articles depuis le catalogue de services, remise/frais et validation.
/// </summary>
public partial class NewOrderViewModel : ModuleViewModelBase
{
    private readonly ICustomerService _customers;
    private readonly IServiceCatalogService _services;
    private readonly IOrderService _orders;

    public NewOrderViewModel(
        ICustomerService customers,
        IServiceCatalogService services,
        IOrderService orders,
        ICurrentUserContext session)
        : base(session)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
    }

    public ObservableCollection<CustomerOption> Customers { get; } = new();

    public ObservableCollection<ServiceOption> Services { get; } = new();

    public ObservableCollection<DraftOrderItem> Items { get; } = new();

    public bool CanManageCustomers => Session.HasPermission(Permission.ManageCustomers);

    public bool CanRegisterPayment => Session.HasPermission(Permission.RegisterPayments);

    public bool HasNoCustomers => Customers.Count == 0;

    public bool HasNoServices => Services.Count == 0;

    public string SubtotalText => MoneyFormatter.FormatFcfa(Subtotal);

    public string TotalText => MoneyFormatter.FormatFcfa(ComputeTotal());

    public OrderDetailsDto? CreatedOrder { get; private set; }

    public bool OpenPaymentAfterCreate { get; private set; }

    public event EventHandler? OrderCreated;

    [ObservableProperty]
    private CustomerOption? _selectedCustomer;

    [ObservableProperty]
    private ServiceOption? _selectedService;

    [ObservableProperty]
    private bool _isQuickCustomerVisible;

    [ObservableProperty]
    private string _quickCustomerName = string.Empty;

    [ObservableProperty]
    private string _quickCustomerPhone = string.Empty;

    [ObservableProperty]
    private string _itemTypeText = string.Empty;

    [ObservableProperty]
    private string _quantityText = "1";

    [ObservableProperty]
    private string _unitPriceText = string.Empty;

    [ObservableProperty]
    private string _observationsText = string.Empty;

    [ObservableProperty]
    private DateTime? _expectedDate = DateTime.Today.AddDays(2);

    [ObservableProperty]
    private string _discountText = "0";

    [ObservableProperty]
    private string _feesText = "0";

    [ObservableProperty]
    private string _notesText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    private decimal Subtotal => Items.Sum(i => i.LineTotal);

    partial void OnSelectedServiceChanged(ServiceOption? value)
    {
        UnitPriceText = value is { DefaultPrice: > 0m }
            ? DecimalInput.Format(value.DefaultPrice)
            : string.Empty;
    }

    partial void OnDiscountTextChanged(string value) => RefreshTotals();

    partial void OnFeesTextChanged(string value) => RefreshTotals();

    public Task LoadAsync() => RunAsync(async () =>
    {
        Customers.Clear();
        if (CanManageCustomers)
        {
            var customerList = await _customers.SearchAsync(string.Empty);
            foreach (var c in customerList)
            {
                Customers.Add(new CustomerOption(c.Id, c.FullName, c.Phone, c.CustomerNumber));
            }
        }

        SelectedCustomer = Customers.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoCustomers));

        if (HasNoCustomers && CanManageCustomers)
        {
            IsQuickCustomerVisible = true;
        }

        Services.Clear();
        var serviceList = await _services.GetServicesAsync(includeInactive: false);
        foreach (var s in serviceList)
        {
            var activePrice = s.Prices
                .Where(p => p.ValidTo is null)
                .OrderByDescending(p => p.ValidFrom)
                .Select(p => p.Amount)
                .FirstOrDefault();

            var label = activePrice > 0m
                ? $"{s.Name} ({MoneyFormatter.FormatFcfa(activePrice)})"
                : s.Name;

            Services.Add(new ServiceOption(s.Id, s.Name, activePrice, label));
        }

        SelectedService = Services.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoServices));
        RefreshTotals();
    }, successKey: null);

    [RelayCommand]
    private void ToggleQuickCustomer() => IsQuickCustomerVisible = !IsQuickCustomerVisible;

    [RelayCommand]
    private Task CreateQuickCustomerAsync() => RunAsync(async () =>
    {
        var created = await _customers.CreateAsync(
            new CreateCustomerDto(QuickCustomerName, QuickCustomerPhone));

        var option = new CustomerOption(created.Id, created.FullName, created.Phone, created.CustomerNumber);
        Customers.Insert(0, option);
        SelectedCustomer = option;

        QuickCustomerName = string.Empty;
        QuickCustomerPhone = string.Empty;
        IsQuickCustomerVisible = false;
        OnPropertyChanged(nameof(HasNoCustomers));

        Message = Loc.Instance.Format("Ord_Dlg_QuickCreated", created.FullName);
        IsError = false;
    }, successKey: null);

    [RelayCommand]
    private Task AddItemAsync() => RunAsync(() =>
    {
        var item = BuildAndValidateItem();
        Items.Add(item);

        ItemTypeText = string.Empty;
        QuantityText = "1";
        UnitPriceText = SelectedService is { DefaultPrice: > 0m } s
            ? DecimalInput.Format(s.DefaultPrice)
            : string.Empty;
        ObservationsText = string.Empty;

        RefreshTotals();
        return Task.CompletedTask;
    }, successKey: null);

    [RelayCommand]
    private void RemoveItem(DraftOrderItem? item)
    {
        if (item is null)
        {
            return;
        }

        Items.Remove(item);
        RefreshTotals();
    }

    [RelayCommand]
    private Task CreateOrderAsync() => SaveOrderAsync(openPayment: false);

    [RelayCommand]
    private Task CreateAndPayAsync() => SaveOrderAsync(openPayment: true);

    private Task SaveOrderAsync(bool openPayment) => RunConfirmedAsync(async () =>
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var customer = SelectedCustomer
                ?? throw new BusinessRuleException(Loc.Instance["Ord_Err_SelectCustomer"]);

            // Confort UX : si l'utilisateur a saisi un article sans cliquer sur "+ Ajouter", on l'ajoute avant de valider.
            if (Items.Count == 0)
            {
                if (SelectedService is not null && !string.IsNullOrWhiteSpace(ItemTypeText))
                {
                    Items.Add(BuildAndValidateItem());
                    ItemTypeText = string.Empty;
                    ObservationsText = string.Empty;
                    RefreshTotals();
                }
                else
                {
                    throw new BusinessRuleException(Loc.Instance["Ord_Err_NoItems"]);
                }
            }

            var discount = ParseNonNegativeMoney(DiscountText, "Ord_Err_InvalidDiscount");
            var fees = ParseNonNegativeMoney(FeesText, "Ord_Err_InvalidFees");

            var request = new CreateOrderDto(
                customer.Id,
                Items.Select(i => new CreateOrderItemDto(
                    i.ServiceId,
                    i.ItemType,
                    i.Quantity,
                    i.UnitPrice,
                    i.Observations)).ToList(),
                ExpectedDate,
                discount,
                fees,
                NullIfEmpty(NotesText));

            var created = await _orders.CreateAsync(request);
            CreatedOrder = created;
            OpenPaymentAfterCreate = openPayment;
            OrderCreated?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }, successKey: null);

    private DraftOrderItem BuildAndValidateItem()
    {
        var service = SelectedService
            ?? throw new BusinessRuleException(Loc.Instance["Ord_Err_SelectService"]);

        var itemType = (ItemTypeText ?? string.Empty).Trim();
        if (itemType.Length == 0)
        {
            throw new BusinessRuleException(Loc.Instance["Ord_Err_ItemTypeRequired"]);
        }

        if (!int.TryParse((QuantityText ?? string.Empty).Trim(), out var quantity) || quantity < 1)
        {
            throw new BusinessRuleException(Loc.Instance["Ord_Err_InvalidQty"]);
        }

        if (!DecimalInput.TryParse(UnitPriceText, out var unitPrice) || unitPrice < 0m)
        {
            throw new BusinessRuleException(Loc.Instance["Ord_Err_InvalidPrice"]);
        }

        return new DraftOrderItem(
            service.Id,
            service.Name,
            itemType,
            quantity,
            Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            NullIfEmpty(ObservationsText));
    }

    private decimal ComputeTotal()
    {
        var discount = TryParseMoneyOrZero(DiscountText);
        var fees = TryParseMoneyOrZero(FeesText);
        return Math.Max(0m, Subtotal + fees - discount);
    }

    private static decimal TryParseMoneyOrZero(string? text) =>
        DecimalInput.TryParse(text, out var value) && value >= 0m ? value : 0m;

    private static decimal ParseNonNegativeMoney(string? text, string errorKey)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (!DecimalInput.TryParse(text, out var value) || value < 0m)
        {
            throw new BusinessRuleException(Loc.Instance[errorKey]);
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(TotalText));
    }
}
