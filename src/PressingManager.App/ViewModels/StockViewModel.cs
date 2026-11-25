using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record MovementTypeOption(StockMovementType Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Une ligne du tableau des produits (stock deja calcule par le service a partir des mouvements).</summary>
public sealed record ProductRow(ProductDto Source)
{
    public int Id => Source.Id;

    public string Name => Source.Name;

    public string Unit => Source.Unit;

    public bool IsBelowAlertThreshold => Source.IsBelowAlertThreshold;

    public string StockText => DecimalInput.Format(Source.CurrentStock);

    public string ThresholdText => DecimalInput.Format(Source.AlertThreshold);

    public string StatusText => Source.IsBelowAlertThreshold
        ? Loc.Instance["Stock_StatusLow"]
        : Loc.Instance["Stock_StatusOk"];
}

/// <summary>Une ligne de l'historique des mouvements d'un produit.</summary>
public sealed record MovementRow(StockMovementDto Source, string UserText)
{
    public DateTime DateLocal => DateTime.SpecifyKind(Source.MovementDate, DateTimeKind.Utc).ToLocalTime();

    public string TypeText => Loc.Instance[$"Move_{Source.Type}"];

    public bool IsIncoming => Source.Type is StockMovementType.Entry or StockMovementType.AdjustmentIncrease;

    public string QuantityText => (IsIncoming ? "+" : "−") + DecimalInput.Format(Source.Quantity);

    public string Reason => Source.Reason ?? string.Empty;
}

/// <summary>
/// Onglet Stocks (documentation, section 4.8). Lecture : permission ViewStock ;
/// creation de produits et mouvements : permission ManageStock.
/// </summary>
public partial class StockViewModel : ModuleViewModelBase
{
    private readonly IStockService _stock;

    private IReadOnlyList<ProductDto> _all = Array.Empty<ProductDto>();

    public StockViewModel(IStockService stock, ICurrentUserContext session)
        : base(session)
    {
        _stock = stock ?? throw new ArgumentNullException(nameof(stock));
        _movementTypes = BuildMovementTypes();

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<ProductRow> Products { get; } = new();

    public ObservableCollection<MovementRow> Movements { get; } = new();

    public bool CanView => Session.HasPermission(Permission.ViewStock);

    public bool IsAccessDenied => !CanView;

    public bool CanManage => Session.HasPermission(Permission.ManageStock);

    public bool HasAlerts => _all.Any(p => p.IsBelowAlertThreshold);

    public string AlertText
    {
        get
        {
            var count = _all.Count(p => p.IsBelowAlertThreshold);

            return count == 0
                ? Loc.Instance["Stock_NoAlert"]
                : Loc.Instance.Format("Stock_AlertsBanner", count);
        }
    }

    [ObservableProperty]
    private IReadOnlyList<MovementTypeOption> _movementTypes;

    [ObservableProperty]
    private ProductRow? _selectedProduct;

    [ObservableProperty]
    private bool _showOnlyAlerts;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newUnit = string.Empty;

    [ObservableProperty]
    private string _newThresholdText = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editUnit = string.Empty;

    [ObservableProperty]
    private string _editThresholdText = string.Empty;

    [ObservableProperty]
    private StockMovementType _selectedMovementType = StockMovementType.Entry;

    [ObservableProperty]
    private string _quantityText = string.Empty;

    [ObservableProperty]
    private string _reasonText = string.Empty;

    partial void OnSelectedProductChanged(ProductRow? value)
    {
        EditName = value?.Name ?? string.Empty;
        EditUnit = value?.Unit ?? string.Empty;
        EditThresholdText = value is null ? string.Empty : DecimalInput.Format(value.Source.AlertThreshold);

        _ = LoadMovementsAsync(value);
    }

    partial void OnShowOnlyAlertsChanged(bool value) => RebuildRows(SelectedProduct?.Id);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(() => RefreshAsync(SelectedProduct?.Id), successKey: null);

    [RelayCommand]
    private Task CreateProductAsync() => RunAsync(async () =>
    {
        var threshold = ParseThreshold(NewThresholdText);
        var created = await _stock.CreateProductAsync(new CreateProductDto(NewName, NewUnit, threshold));

        NewName = string.Empty;
        NewUnit = string.Empty;
        NewThresholdText = string.Empty;
        await RefreshAsync(created.Id);
    }, "Stock_ProductCreated");

    [RelayCommand]
    private Task UpdateProductAsync() => RunAsync(async () =>
    {
        var product = RequireSelection();
        var threshold = ParseThreshold(EditThresholdText);

        await _stock.UpdateProductAsync(new UpdateProductDto(product.Id, EditName, EditUnit, threshold));
        await RefreshAsync(product.Id);
    }, "Stock_ProductUpdated");

    [RelayCommand]
    private Task RecordMovementAsync() => RunAsync(async () =>
    {
        var product = RequireSelection();

        if (!DecimalInput.TryParse(QuantityText, out var quantity))
        {
            throw new BusinessRuleException(Loc.Instance["Common_InvalidNumber"]);
        }

        await _stock.RecordMovementAsync(new RecordStockMovementDto(
            product.Id,
            SelectedMovementType,
            quantity,
            CurrentUserId,
            NullIfEmpty(ReasonText)));

        QuantityText = string.Empty;
        ReasonText = string.Empty;
        await RefreshAsync(product.Id);
    }, "Stock_MovementRecorded");

    private static decimal ParseThreshold(string text)
    {
        // Seuil laisse vide = 0 (pas d'alerte tant que le stock n'est pas vide).
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        return DecimalInput.TryParse(text, out var value)
            ? value
            : throw new BusinessRuleException(Loc.Instance["Common_InvalidNumber"]);
    }

    private ProductRow RequireSelection() =>
        SelectedProduct ?? throw new BusinessRuleException(Loc.Instance["Stock_SelectFirst"]);

    private async Task RefreshAsync(int? keepSelectionId)
    {
        _all = await _stock.GetProductsAsync();
        RebuildRows(keepSelectionId);
    }

    private void RebuildRows(int? keepSelectionId)
    {
        Products.Clear();

        var visible = ShowOnlyAlerts ? _all.Where(p => p.IsBelowAlertThreshold) : _all;

        foreach (var product in visible)
        {
            Products.Add(new ProductRow(product));
        }

        SelectedProduct = Products.FirstOrDefault(p => p.Id == keepSelectionId);

        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(AlertText));
    }

    /// <summary>Charge l'historique du produit selectionne ; une erreur est affichee sans effacer le message en cours.</summary>
    private async Task LoadMovementsAsync(ProductRow? product)
    {
        Movements.Clear();

        if (product is null || !CanView)
        {
            return;
        }

        try
        {
            var movements = await _stock.GetMovementsAsync(product.Id);

            // La selection a pu changer pendant le chargement.
            if (SelectedProduct?.Id != product.Id)
            {
                return;
            }

            foreach (var movement in movements)
            {
                Movements.Add(new MovementRow(movement, UserLabel(movement.UserId)));
            }
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            IsError = true;
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            IsError = true;
            Message = Loc.Instance.Format("Common_Unexpected", ex.Message);
        }
    }

    /// <summary>Le nom n'est connu que pour l'utilisateur connecte (la liste des comptes est reservee a l'administrateur).</summary>
    private string UserLabel(int userId) =>
        Session.CurrentUser is { } current && current.Id == userId
            ? current.FullName
            : Loc.Instance.Format("Common_UserNumber", userId);

    private static List<MovementTypeOption> BuildMovementTypes() =>
        Enum.GetValues<StockMovementType>()
            .Select(type => new MovementTypeOption(type, Loc.Instance[$"Move_{type}"]))
            .ToList();

    protected override void OnSessionChanged()
    {
        OnPropertyChanged(nameof(CanView));
        OnPropertyChanged(nameof(IsAccessDenied));
        OnPropertyChanged(nameof(CanManage));

        _all = Array.Empty<ProductDto>();
        RebuildRows(null);
        Message = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        MovementTypes = BuildMovementTypes();
        OnPropertyChanged(nameof(SelectedMovementType)); // le ComboBox re-selectionne le type en cours
        RebuildRows(SelectedProduct?.Id);
    }
}
