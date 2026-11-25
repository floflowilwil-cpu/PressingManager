using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record BillingUnitOption(BillingUnit Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Une ligne du catalogue (tarifs en vigueur traduits selon la langue courante).</summary>
public sealed record ServiceRow(ServiceDto Source)
{
    public int Id => Source.Id;

    public string Name => Source.Name;

    public string Description => Source.Description ?? string.Empty;

    public bool IsActive => Source.IsActive;

    public string CurrentPrices
    {
        get
        {
            var current = Source.Prices
                .Where(p => p.ValidTo is null)
                .OrderBy(p => p.Unit)
                .ToList();

            return current.Count == 0
                ? Loc.Instance["Svc_NoPrice"]
                : string.Join(
                    " · ",
                    current.Select(p => $"{Loc.Instance[$"Unit_{p.Unit}"]} : {MoneyFormatter.FormatFcfa(p.Amount)}"));
        }
    }
}

/// <summary>Une ligne de l'historique des tarifs d'un service.</summary>
public sealed record PriceRow(ServicePriceDto Source)
{
    public string UnitText => Loc.Instance[$"Unit_{Source.Unit}"];

    public string AmountText => MoneyFormatter.FormatFcfa(Source.Amount, withCurrency: false);

    public DateTime ValidFromLocal => DateTime.SpecifyKind(Source.ValidFrom, DateTimeKind.Utc).ToLocalTime();

    public string ValidToText => Source.ValidTo is { } end
        ? DateTime.SpecifyKind(end, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Loc.Instance.Culture)
        : Loc.Instance["Svc_InForce"];

    public bool IsCurrent => Source.ValidTo is null;
}

/// <summary>Onglet Services et tarifs (documentation, section 4.5). Ecriture : permission ManageServices.</summary>
public partial class ServiceCatalogViewModel : ModuleViewModelBase
{
    private readonly IServiceCatalogService _catalog;

    private IReadOnlyList<ServiceDto> _all = Array.Empty<ServiceDto>();

    public ServiceCatalogViewModel(IServiceCatalogService catalog, ICurrentUserContext session)
        : base(session)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _units = BuildUnits();

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<ServiceRow> Services { get; } = new();

    public ObservableCollection<PriceRow> PriceHistory { get; } = new();

    /// <summary>La lecture du catalogue est ouverte a tous ; la modification demande ManageServices.</summary>
    public bool CanManage => Session.HasPermission(Permission.ManageServices);

    public bool IsReadOnly => !CanManage;

    [ObservableProperty]
    private IReadOnlyList<BillingUnitOption> _units;

    [ObservableProperty]
    private ServiceRow? _selectedService;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newDescription = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private BillingUnit _selectedUnit = BillingUnit.Piece;

    [ObservableProperty]
    private string _priceAmountText = string.Empty;

    [ObservableProperty]
    private bool _includeInactive = true;

    partial void OnSelectedServiceChanged(ServiceRow? value)
    {
        EditName = value?.Name ?? string.Empty;
        EditDescription = value?.Description ?? string.Empty;

        PriceHistory.Clear();

        if (value is null)
        {
            return;
        }

        foreach (var price in value.Source.Prices.OrderByDescending(p => p.ValidFrom).ThenByDescending(p => p.Id))
        {
            PriceHistory.Add(new PriceRow(price));
        }
    }

    partial void OnIncludeInactiveChanged(bool value) =>
        _ = RunAsync(() => RefreshAsync(SelectedService?.Id), successKey: null);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(() => RefreshAsync(SelectedService?.Id), successKey: null);

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        var created = await _catalog.CreateAsync(new CreateServiceDto(NewName, NullIfEmpty(NewDescription)));

        NewName = string.Empty;
        NewDescription = string.Empty;
        await RefreshAsync(created.Id);
    }, "Svc_Created");

    [RelayCommand]
    private Task UpdateAsync() => RunAsync(async () =>
    {
        var service = RequireSelection();

        await _catalog.UpdateAsync(new UpdateServiceDto(service.Id, EditName, NullIfEmpty(EditDescription)));
        await RefreshAsync(service.Id);
    }, "Svc_Updated");

    [RelayCommand]
    private Task ToggleActiveAsync() => RunAsync(async () =>
    {
        var service = RequireSelection();

        await _catalog.SetActiveAsync(service.Id, !service.IsActive);
        await RefreshAsync(service.Id);
    }, "Svc_StatusUpdated");

    [RelayCommand]
    private Task SetPriceAsync() => RunAsync(async () =>
    {
        var service = RequireSelection();

        if (!DecimalInput.TryParse(PriceAmountText, out var amount))
        {
            throw new BusinessRuleException(Loc.Instance["Common_InvalidNumber"]);
        }

        await _catalog.SetPriceAsync(new SetServicePriceDto(service.Id, SelectedUnit, amount, CurrentUserId));

        PriceAmountText = string.Empty;
        await RefreshAsync(service.Id);
    }, "Svc_PriceSet");

    private ServiceRow RequireSelection() =>
        SelectedService ?? throw new BusinessRuleException(Loc.Instance["Svc_SelectFirst"]);

    private async Task RefreshAsync(int? keepSelectionId)
    {
        _all = await _catalog.GetServicesAsync(IncludeInactive);
        RebuildRows(keepSelectionId);
    }

    private void RebuildRows(int? keepSelectionId)
    {
        Services.Clear();

        foreach (var service in _all)
        {
            Services.Add(new ServiceRow(service));
        }

        SelectedService = Services.FirstOrDefault(s => s.Id == keepSelectionId);
    }

    private static List<BillingUnitOption> BuildUnits() =>
        Enum.GetValues<BillingUnit>()
            .Select(unit => new BillingUnitOption(unit, Loc.Instance[$"Unit_{unit}"]))
            .ToList();

    protected override void OnSessionChanged()
    {
        OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(IsReadOnly));

        // Autre utilisateur : on ne garde pas l'ecran du precedent.
        _all = Array.Empty<ServiceDto>();
        RebuildRows(null);
        Message = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Units = BuildUnits();
        OnPropertyChanged(nameof(SelectedUnit)); // le ComboBox re-selectionne l'unite en cours
        RebuildRows(SelectedService?.Id);
    }
}
