using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

/// <summary>Un service proposable (un tarif en vigueur d'un service, pour une unite de facturation).</summary>
public sealed record ServiceOption(int ServiceId, string Label, decimal UnitPrice);

/// <summary>Une ligne de la commande en cours de saisie.</summary>
public sealed record OrderLine(
    int ServiceId,
    string ServiceName,
    string ItemType,
    int Quantity,
    decimal UnitPrice,
    string? Observations)
{
    public decimal LineTotal => Quantity * UnitPrice;

    public string UnitPriceText => UiFormat.Money(UnitPrice);

    public string LineTotalText => UiFormat.Money(LineTotal);
}

/// <summary>Fenetre « Nouvelle commande » : client, articles, remise, frais, date prevue.</summary>
public partial class NewOrderViewModel : ObservableObject
{
    private readonly ICustomerService _customers;
    private readonly IOrderService _orders;
    private readonly IServiceScopeFactory _scopeFactory;

    private CancellationTokenSource? _searchCts;

    public NewOrderViewModel(
        ICustomerService customers,
        IOrderService orders,
        IServiceScopeFactory scopeFactory)
    {
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public ObservableCollection<CustomerDto> Customers { get; } = new();

    public ObservableCollection<ServiceOption> ServiceOptions { get; } = new();

    public ObservableCollection<OrderLine> Lines { get; } = new();

    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerDto? _selectedCustomer;

    [ObservableProperty]
    private ServiceOption? _selectedService;

    [ObservableProperty]
    private string _newItemType = string.Empty;

    [ObservableProperty]
    private string _newQuantityText = "1";

    [ObservableProperty]
    private string _newUnitPriceText = string.Empty;

    [ObservableProperty]
    private string _newObservation = string.Empty;

    [ObservableProperty]
    private DateTime? _expectedDate;

    [ObservableProperty]
    private string _discountText = "0";

    [ObservableProperty]
    private string _feesText = "0";

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _subtotalText = UiFormat.Money(0);

    [ObservableProperty]
    private string _totalText = UiFormat.Money(0);

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public bool HasNoLines => Lines.Count == 0;

    partial void OnCustomerSearchChanged(string value) => _ = DebouncedCustomerSearchAsync();

    partial void OnSelectedServiceChanged(ServiceOption? value)
    {
        if (value is not null)
        {
            NewUnitPriceText = value.UnitPrice.ToString("0.##", CultureInfo.CurrentCulture);
        }
    }

    partial void OnDiscountTextChanged(string value) => Recalculate();

    partial void OnFeesTextChanged(string value) => Recalculate();

    public async Task LoadAsync()
    {
        ErrorMessage = null;

        try
        {
            await LoadCustomersAsync();
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = CustomersViewModel.ToUserMessage(ex);
        }
    }

    [RelayCommand]
    private void AddLine()
    {
        ErrorMessage = null;

        if (SelectedService is null)
        {
            ErrorMessage = "Choisissez d'abord un service.";
            return;
        }

        var itemType = NewItemType.Trim();
        if (itemType.Length == 0)
        {
            ErrorMessage = "Indiquez le type d'article (ex. chemise, pantalon).";
            return;
        }

        if (!int.TryParse(NewQuantityText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) || quantity < 1)
        {
            ErrorMessage = "La quantité doit être un nombre entier d'au moins 1.";
            return;
        }

        if (!TryParseMoney(NewUnitPriceText, out var price) || price < 0)
        {
            ErrorMessage = "Le prix unitaire est invalide.";
            return;
        }

        var observation = NewObservation.Trim();
        Lines.Add(new OrderLine(
            SelectedService.ServiceId,
            SelectedService.Label,
            itemType,
            quantity,
            price,
            observation.Length == 0 ? null : observation));

        NewItemType = string.Empty;
        NewQuantityText = "1";
        NewObservation = string.Empty;

        OnPropertyChanged(nameof(HasNoLines));
        Recalculate();
    }

    [RelayCommand]
    private void RemoveLine(OrderLine? line)
    {
        if (line is null)
        {
            return;
        }

        Lines.Remove(line);
        OnPropertyChanged(nameof(HasNoLines));
        Recalculate();
    }

    /// <summary>Cree la commande. Renvoie null (et remplit ErrorMessage) si elle est refusee.</summary>
    public async Task<OrderDetailsDto?> SubmitAsync()
    {
        ErrorMessage = null;

        if (SelectedCustomer is null)
        {
            ErrorMessage = "Choisissez le client de la commande.";
            return null;
        }

        if (Lines.Count == 0)
        {
            ErrorMessage = "Ajoutez au moins un article à la commande.";
            return null;
        }

        if (!TryParseMoney(DiscountText, out var discount) || discount < 0)
        {
            ErrorMessage = "La remise est invalide.";
            return null;
        }

        if (!TryParseMoney(FeesText, out var fees) || fees < 0)
        {
            ErrorMessage = "Les frais sont invalides.";
            return null;
        }

        var items = Lines
            .Select(l => new CreateOrderItemDto(l.ServiceId, l.ItemType, l.Quantity, l.UnitPrice, l.Observations))
            .ToList();

        var notes = Notes.Trim();

        try
        {
            IsBusy = true;
            return await _orders.CreateAsync(new CreateOrderDto(
                SelectedCustomer.Id,
                items,
                ExpectedDate,
                discount,
                fees,
                notes.Length == 0 ? null : notes));
        }
        catch (Exception ex)
        {
            ErrorMessage = CustomersViewModel.ToUserMessage(ex);
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DebouncedCustomerSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            await Task.Delay(300, cts.Token);
            await LoadCustomersAsync();
        }
        catch (OperationCanceledException)
        {
            // Une frappe plus recente a pris le relais.
        }
        catch (Exception ex)
        {
            ErrorMessage = CustomersViewModel.ToUserMessage(ex);
        }
    }

    private async Task LoadCustomersAsync()
    {
        var previousId = SelectedCustomer?.Id;
        var found = await _customers.SearchAsync(CustomerSearch);

        Customers.Clear();
        foreach (var customer in found)
        {
            Customers.Add(customer);
        }

        // On garde le client deja choisi si la recherche le contient encore.
        SelectedCustomer = previousId is { } id ? Customers.FirstOrDefault(c => c.Id == id) : null;
    }

    private async Task LoadServicesAsync()
    {
        // Le catalogue (module de la personne 3) vit dans un scope : un DbContext par operation.
        using var scope = _scopeFactory.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var services = await catalog.GetServicesAsync();

        var now = DateTime.UtcNow;
        ServiceOptions.Clear();

        foreach (var service in services)
        {
            foreach (var price in service.Prices
                         .Where(p => p.ValidFrom <= now && (p.ValidTo is null || p.ValidTo > now))
                         .OrderBy(p => p.Unit))
            {
                var unit = price.Unit == BillingUnit.Kilogram ? "au kg" : "à la pièce";
                ServiceOptions.Add(new ServiceOption(service.Id, $"{service.Name} ({unit})", price.Amount));
            }
        }

        if (ServiceOptions.Count == 0)
        {
            ErrorMessage = "Aucun service avec tarif n'est configuré : ajoutez-en avant de créer une commande.";
        }
    }

    private void Recalculate()
    {
        var subtotal = Lines.Sum(l => l.LineTotal);
        TryParseMoney(DiscountText, out var discount);
        TryParseMoney(FeesText, out var fees);

        SubtotalText = UiFormat.Money(subtotal);
        TotalText = UiFormat.Money(subtotal - discount + fees);
    }

    /// <summary>Accepte « 1500,50 » comme « 1500.50 » et les espaces de milliers ; vide = 0.</summary>
    private static bool TryParseMoney(string? text, out decimal value)
    {
        var clean = (text ?? string.Empty).Replace(" ", string.Empty).Replace("\u00A0", string.Empty).Trim();
        if (clean.Length == 0)
        {
            value = 0m;
            return true;
        }

        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
