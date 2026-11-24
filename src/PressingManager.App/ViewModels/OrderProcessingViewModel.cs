using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record StatusOption(OrderStatus Value, string Label);

/// <summary>Filtre de la file de traitement : a traiter, en retard, ou un statut precis.</summary>
public sealed record ProcessingFilterOption(string Key, OrderStatus? Status, bool OverdueOnly, string Label);

/// <summary>Une ligne de la file de traitement.</summary>
public sealed record ProcessingRow(ProcessingOrderDto Source)
{
    public int OrderId => Source.OrderId;

    public string OrderNumber => Source.OrderNumber;

    public string CustomerName => Source.CustomerName;

    public OrderStatus Status => Source.Status;

    public string StatusText => Loc.Instance.Status(Source.Status);

    public DateTime CreatedLocal => DateTime.SpecifyKind(Source.CreatedAt, DateTimeKind.Utc).ToLocalTime();

    /// <summary>La date prevue est un jour calendaire : pas de conversion de fuseau.</summary>
    public string ExpectedText => Source.ExpectedDate is { } date
        ? date.ToString("dd/MM/yyyy", Loc.Instance.Culture)
        : "—";

    public int ItemCount => Source.ItemCount;

    public bool IsOverdue => Source.IsOverdue;

    public bool IsReadyForPickup => Source.IsReadyForPickup;

    public string FlagText =>
        IsOverdue ? Loc.Instance["Proc_Overdue"]
        : IsReadyForPickup ? Loc.Instance["Proc_Ready"]
        : string.Empty;
}

/// <summary>Une ligne de l'historique des statuts d'une commande.</summary>
public sealed record StatusHistoryRow(OrderStatusHistoryDto Source, string UserText)
{
    public DateTime ChangedAtLocal => DateTime.SpecifyKind(Source.ChangedAt, DateTimeKind.Utc).ToLocalTime();

    public string FromText => Source.FromStatus is { } previous ? Loc.Instance.Status(previous) : "—";

    public string ToText => Loc.Instance.Status(Source.ToStatus);

    public string CorrectionText => Source.IsCorrection ? Loc.Instance["Proc_HistCorrection"] : string.Empty;

    public string Comment => Source.Comment ?? string.Empty;
}

/// <summary>
/// Onglet Traitement (documentation, section 4.4). Lecture : permission ViewOrders ;
/// changement de statut : UpdateOrderStatus ; retour en arriere : CorrectOrderStatus (verifie par le service).
/// </summary>
public partial class OrderProcessingViewModel : ModuleViewModelBase
{
    private readonly IOrderProcessingService _processing;

    private IReadOnlyList<ProcessingOrderDto> _orders = Array.Empty<ProcessingOrderDto>();
    private IReadOnlyList<OrderStatusHistoryDto> _history = Array.Empty<OrderStatusHistoryDto>();
    private bool _suppressFilterReload;

    public OrderProcessingViewModel(IOrderProcessingService processing, ICurrentUserContext session)
        : base(session)
    {
        _processing = processing ?? throw new ArgumentNullException(nameof(processing));
        _filters = BuildFilters();
        _selectedFilter = _filters[0];

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<ProcessingRow> Orders { get; } = new();

    public ObservableCollection<StatusOption> NextStatuses { get; } = new();

    public ObservableCollection<StatusHistoryRow> History { get; } = new();

    /// <summary>Confirmation avant une action definitive (documentation, 10.3). Branchee par la vue ; accepte par defaut.</summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    public bool CanView => Session.HasPermission(Permission.ViewOrders);

    public bool IsAccessDenied => !CanView;

    public bool CanChangeStatus => Session.HasPermission(Permission.UpdateOrderStatus);

    public bool CanCorrect => Session.HasPermission(Permission.CorrectOrderStatus);

    public bool IsOrderSelected => SelectedOrder is not null;

    public bool HasNoTransition => SelectedOrder is not null && NextStatuses.Count == 0;

    public string SummaryText => Loc.Instance.Format("Proc_Summary", Orders.Count, Orders.Count(o => o.IsOverdue));

    [ObservableProperty]
    private IReadOnlyList<ProcessingFilterOption> _filters;

    [ObservableProperty]
    private ProcessingFilterOption? _selectedFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrderSelected))]
    private ProcessingRow? _selectedOrder;

    [ObservableProperty]
    private StatusOption? _selectedNextStatus;

    [ObservableProperty]
    private bool _isCorrectionMode;

    [ObservableProperty]
    private string _commentText = string.Empty;

    partial void OnSelectedFilterChanged(ProcessingFilterOption? value)
    {
        if (_suppressFilterReload || value is null)
        {
            return;
        }

        _ = RunAsync(() => RefreshAsync(keepOrderId: null), successKey: null);
    }

    partial void OnSelectedOrderChanged(ProcessingRow? value)
    {
        CommentText = string.Empty;

        if (IsCorrectionMode)
        {
            IsCorrectionMode = false; // relance BuildNextStatuses via OnIsCorrectionModeChanged
        }

        BuildNextStatuses();
        _ = LoadHistoryAsync(value);
    }

    partial void OnIsCorrectionModeChanged(bool value) => BuildNextStatuses();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(() => RefreshAsync(SelectedOrder?.OrderId), successKey: null);

    [RelayCommand]
    private Task ApplyStatusAsync() => RunConfirmedAsync(async () =>
    {
        var order = SelectedOrder ?? throw new BusinessRuleException(Loc.Instance["Proc_SelectOrder"]);
        var target = SelectedNextStatus ?? throw new BusinessRuleException(Loc.Instance["Proc_SelectStatus"]);

        // Annulation : action sensible, confirmation obligatoire.
        if (target.Value == OrderStatus.Cancelled
            && !Confirm(Loc.Instance.Format("Proc_ConfirmCancel", order.OrderNumber)))
        {
            return false;
        }

        await _processing.ChangeStatusAsync(new ChangeOrderStatusDto(
            order.OrderId,
            target.Value,
            CurrentUserId,
            IsCorrectionMode,
            NullIfEmpty(CommentText)));

        await RefreshAsync(order.OrderId);
        return true;
    }, "Proc_StatusChanged");

    private async Task RefreshAsync(int? keepOrderId)
    {
        var filter = SelectedFilter;

        _orders = filter switch
        {
            { OverdueOnly: true } => await _processing.GetOverdueOrdersAsync(),
            { Status: { } status } => await _processing.GetOrdersAsync(status),
            _ => await _processing.GetOrdersAsync()
        };

        RebuildOrders(keepOrderId);
    }

    private void RebuildOrders(int? keepOrderId)
    {
        Orders.Clear();

        foreach (var order in _orders)
        {
            Orders.Add(new ProcessingRow(order));
        }

        // Une commande devenue livree ou annulee peut sortir de la file : alors rien n'est selectionne.
        SelectedOrder = Orders.FirstOrDefault(o => o.OrderId == keepOrderId);

        OnPropertyChanged(nameof(SummaryText));
    }

    private void BuildNextStatuses()
    {
        NextStatuses.Clear();
        SelectedNextStatus = null;

        if (SelectedOrder is { } order)
        {
            var current = order.Status;

            IEnumerable<OrderStatus> candidates = IsCorrectionMode
                // Corrections = retours en arriere que le flux normal n'autorise pas.
                ? Enum.GetValues<OrderStatus>().Where(s =>
                    s != current
                    && !OrderStatusRules.GetNextStatuses(current).Contains(s)
                    && OrderStatusRules.CanTransition(current, s, isAuthorizedCorrection: true))
                : _processing.GetAllowedNextStatuses(current);

            foreach (var status in candidates)
            {
                NextStatuses.Add(new StatusOption(status, Loc.Instance.Status(status)));
            }

            SelectedNextStatus = NextStatuses.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HasNoTransition));
    }

    /// <summary>Charge l'historique de la commande selectionnee ; une erreur est affichee sans effacer le message en cours.</summary>
    private async Task LoadHistoryAsync(ProcessingRow? order)
    {
        _history = Array.Empty<OrderStatusHistoryDto>();
        History.Clear();

        if (order is null || !CanView)
        {
            return;
        }

        try
        {
            var events = await _processing.GetStatusHistoryAsync(order.OrderId);

            // La selection a pu changer pendant le chargement.
            if (SelectedOrder?.OrderId != order.OrderId)
            {
                return;
            }

            _history = events;
            RebuildHistory();
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

    private void RebuildHistory()
    {
        History.Clear();

        foreach (var entry in _history)
        {
            History.Add(new StatusHistoryRow(entry, UserLabel(entry.ChangedByUserId)));
        }
    }

    /// <summary>Le nom n'est connu que pour l'utilisateur connecte (la liste des comptes est reservee a l'administrateur).</summary>
    private string UserLabel(int userId) =>
        Session.CurrentUser is { } current && current.Id == userId
            ? current.FullName
            : Loc.Instance.Format("Common_UserNumber", userId);

    private static List<ProcessingFilterOption> BuildFilters()
    {
        var filters = new List<ProcessingFilterOption>
        {
            new("queue", null, false, Loc.Instance["Proc_FilterQueue"]),
            new("overdue", null, true, Loc.Instance["Proc_FilterOverdue"])
        };

        filters.AddRange(Enum.GetValues<OrderStatus>()
            .Select(s => new ProcessingFilterOption($"status:{s}", s, false, Loc.Instance.Status(s))));

        return filters;
    }

    protected override void OnSessionChanged()
    {
        OnPropertyChanged(nameof(CanView));
        OnPropertyChanged(nameof(IsAccessDenied));
        OnPropertyChanged(nameof(CanChangeStatus));
        OnPropertyChanged(nameof(CanCorrect));

        _orders = Array.Empty<ProcessingOrderDto>();
        RebuildOrders(null);
        Message = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        var key = SelectedFilter?.Key;

        _suppressFilterReload = true;
        try
        {
            Filters = BuildFilters();
            SelectedFilter = Filters.FirstOrDefault(f => f.Key == key) ?? Filters[0];
        }
        finally
        {
            _suppressFilterReload = false;
        }

        var keepStatusValue = SelectedNextStatus?.Value;

        RebuildOrders(SelectedOrder?.OrderId);

        // RebuildOrders reselectionne une nouvelle ligne : on retablit le statut choisi dans la nouvelle liste.
        SelectedNextStatus = NextStatuses.FirstOrDefault(s => s.Value == keepStatusValue) ?? NextStatuses.FirstOrDefault();
    }
}
