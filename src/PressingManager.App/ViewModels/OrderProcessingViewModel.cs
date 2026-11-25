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

/// <summary>Filtre de la file de traitement : a traiter, en retard, toutes les commandes ou un statut precis.</summary>
public sealed record ProcessingFilterOption(string Key, OrderStatus? Status, bool OverdueOnly, string Label);

/// <summary>Une ligne resumee d'article dans le panneau de details de la commande.</summary>
public sealed record OrderItemDisplayRow(string Summary, string LineTotalText, string? Observations);

/// <summary>Une ligne de la file des commandes.</summary>
public sealed record ProcessingRow(ProcessingOrderDto Source, decimal TotalAmount = 0m)
{
    public int OrderId => Source.OrderId;

    public string OrderIdText => $"#{OrderId}";

    public string OrderNumber => Source.OrderNumber;

    public string CustomerName => Source.CustomerName;

    public OrderStatus Status => Source.Status;

    public string StatusText => Loc.Instance.Status(Source.Status);

    public string TotalText => MoneyFormatter.FormatFcfa(TotalAmount);

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
/// Onglet Commandes & Traitement : creation de commandes, recherche, details, paiement direct,
/// changements de statut et historique.
/// </summary>
public partial class OrderProcessingViewModel : ModuleViewModelBase
{
    private readonly IOrderProcessingService _processing;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _payments;

    private IReadOnlyList<ProcessingOrderDto> _orders = Array.Empty<ProcessingOrderDto>();
    private IReadOnlyDictionary<int, OrderDetailsDto> _orderDetailsById = new Dictionary<int, OrderDetailsDto>();
    private IReadOnlyList<OrderStatusHistoryDto> _history = Array.Empty<OrderStatusHistoryDto>();
    private bool _suppressFilterReload;

    public OrderProcessingViewModel(
        IOrderProcessingService processing,
        IOrderService orders,
        IPaymentService payments,
        ICurrentUserContext session)
        : base(session)
    {
        _processing = processing ?? throw new ArgumentNullException(nameof(processing));
        _orderService = orders ?? throw new ArgumentNullException(nameof(orders));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _filters = BuildFilters();
        _selectedFilter = _filters[0];

        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<ProcessingRow> Orders { get; } = new();

    public ObservableCollection<StatusOption> NextStatuses { get; } = new();

    public ObservableCollection<StatusHistoryRow> History { get; } = new();

    public ObservableCollection<OrderItemDisplayRow> SelectedOrderItems { get; } = new();

    /// <summary>Confirmation avant une action definitive (documentation, 10.3). Branchee par la vue ; accepte par defaut.</summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    /// <summary>Declenche la navigation vers l'onglet Paiements pour l'identifiant de commande demande.</summary>
    public event EventHandler<int>? PayOrderRequested;

    public bool CanView => Session.HasPermission(Permission.ViewOrders);

    public bool IsAccessDenied => !CanView;

    public bool CanCreateOrder => Session.HasPermission(Permission.ManageOrders);

    public bool CanPayOrder => Session.HasPermission(Permission.RegisterPayments);

    public bool CanChangeStatus => Session.HasPermission(Permission.UpdateOrderStatus);

    public bool CanCorrect => Session.HasPermission(Permission.CorrectOrderStatus);

    public bool IsOrderSelected => SelectedOrder is not null;

    public bool CanPaySelectedOrder => CanPayOrder && SelectedOrder is { } s && s.Status != OrderStatus.Cancelled;

    public bool HasNoTransition => SelectedOrder is not null && NextStatuses.Count == 0;

    public string SummaryText => Loc.Instance.Format("Proc_Summary", Orders.Count, Orders.Count(o => o.IsOverdue));

    [ObservableProperty]
    private IReadOnlyList<ProcessingFilterOption> _filters;

    [ObservableProperty]
    private ProcessingFilterOption? _selectedFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrderSelected), nameof(CanPaySelectedOrder))]
    private ProcessingRow? _selectedOrder;

    [ObservableProperty]
    private string _selectedOrderBillingText = string.Empty;

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

    partial void OnSearchTextChanged(string value)
    {
        RebuildOrders(SelectedOrder?.OrderId);
    }

    partial void OnSelectedOrderChanged(ProcessingRow? value)
    {
        CommentText = string.Empty;

        if (IsCorrectionMode)
        {
            IsCorrectionMode = false; // relance BuildNextStatuses via OnIsCorrectionModeChanged
        }

        BuildNextStatuses();
        _ = LoadSelectedOrderDetailsAsync(value);
        _ = LoadHistoryAsync(value);
    }

    partial void OnIsCorrectionModeChanged(bool value) => BuildNextStatuses();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(() => RefreshAsync(SelectedOrder?.OrderId), successKey: null);

    [RelayCommand]
    private void PaySelectedOrder()
    {
        if (!CanPaySelectedOrder || SelectedOrder is null)
        {
            return;
        }

        PayOrderRequested?.Invoke(this, SelectedOrder.OrderId);
    }

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

    /// <summary>Appele apres creation d'une commande dans NewOrderWindow.</summary>
    public async Task HandleOrderCreatedAsync(OrderDetailsDto created, bool openPayment)
    {
        ArgumentNullException.ThrowIfNull(created);

        _suppressFilterReload = true;
        try
        {
            SelectedFilter = Filters.FirstOrDefault(f => f.Key == "queue") ?? Filters[0];
            SearchText = string.Empty;
        }
        finally
        {
            _suppressFilterReload = false;
        }

        await RunAsync(() => RefreshAsync(keepOrderId: created.Id), successKey: null);

        IsError = false;
        Message = Loc.Instance.Format(
            "Ord_CreatedMsg",
            created.OrderNumber,
            created.Id,
            MoneyFormatter.FormatFcfa(created.Total));

        if (openPayment && CanPayOrder)
        {
            PayOrderRequested?.Invoke(this, created.Id);
        }
    }

    private async Task RefreshAsync(int? keepOrderId)
    {
        var filter = SelectedFilter;
        var allDetails = await _orderService.SearchAsync(new OrderSearchDto());
        _orderDetailsById = allDetails.ToDictionary(o => o.Id);

        if (filter?.Key == "all")
        {
            var nowUtc = DateTime.UtcNow;
            _orders = allDetails
                .Select(o => new ProcessingOrderDto(
                    o.Id,
                    o.OrderNumber,
                    o.CustomerName,
                    o.Status,
                    o.CreatedAt,
                    o.ExpectedDate,
                    o.Items.Sum(i => i.Quantity),
                    OrderStatusRules.IsOverdue(o.Status, o.ExpectedDate, nowUtc)))
                .ToList();
        }
        else
        {
            _orders = filter switch
            {
                { OverdueOnly: true } => await _processing.GetOverdueOrdersAsync(),
                { Status: { } status } => await _processing.GetOrdersAsync(status),
                _ => await _processing.GetOrdersAsync()
            };
        }

        RebuildOrders(keepOrderId);
        await LoadSelectedOrderDetailsAsync(SelectedOrder);
    }

    private void RebuildOrders(int? keepOrderId)
    {
        var search = (SearchText ?? string.Empty).Trim();
        var normalizedIdSearch = search.TrimStart('#');

        IEnumerable<ProcessingOrderDto> filtered = _orders;
        if (search.Length > 0)
        {
            filtered = filtered.Where(o =>
                o.OrderNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || o.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || o.OrderId.ToString().Contains(normalizedIdSearch, StringComparison.OrdinalIgnoreCase));
        }

        Orders.Clear();

        foreach (var order in filtered)
        {
            var total = _orderDetailsById.TryGetValue(order.OrderId, out var details)
                ? details.Total
                : 0m;

            Orders.Add(new ProcessingRow(order, total));
        }

        // Une commande devenue livree ou annulee peut sortir de la file : alors rien n'est selectionne.
        SelectedOrder = Orders.FirstOrDefault(o => o.OrderId == keepOrderId);

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(CanPaySelectedOrder));
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

    private async Task LoadSelectedOrderDetailsAsync(ProcessingRow? order)
    {
        SelectedOrderItems.Clear();

        if (order is null || !CanView)
        {
            SelectedOrderBillingText = string.Empty;
            return;
        }

        OrderDetailsDto? details = _orderDetailsById.TryGetValue(order.OrderId, out var cached)
            ? cached
            : await _orderService.GetByNumberAsync(order.OrderNumber);

        if (SelectedOrder?.OrderId != order.OrderId)
        {
            return;
        }

        if (details is not null)
        {
            foreach (var item in details.Items)
            {
                SelectedOrderItems.Add(new OrderItemDisplayRow(
                    $"{item.Quantity} × {item.ItemType}",
                    MoneyFormatter.FormatFcfa(item.LineTotal),
                    item.Observations));
            }
        }

        try
        {
            var balance = await _payments.GetBalanceAsync(order.OrderId);
            if (SelectedOrder?.OrderId != order.OrderId)
            {
                return;
            }

            var totalAmount = details?.Total ?? order.TotalAmount;
            var totalFormatted = MoneyFormatter.FormatFcfa(totalAmount);
            var balanceFormatted = balance <= 0m
                ? Loc.Instance["Ord_FullyPaid"]
                : MoneyFormatter.FormatFcfa(balance);

            SelectedOrderBillingText = Loc.Instance.Format(
                "Ord_BillingSummary",
                totalFormatted,
                balanceFormatted);
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException or InvalidOperationException)
        {
            if (SelectedOrder?.OrderId == order.OrderId)
            {
                SelectedOrderBillingText = MoneyFormatter.FormatFcfa(details?.Total ?? order.TotalAmount);
            }
        }
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
            new("overdue", null, true, Loc.Instance["Proc_FilterOverdue"]),
            new("all", null, false, Loc.Instance["Ord_FilterAll"])
        };

        filters.AddRange(Enum.GetValues<OrderStatus>()
            .Select(s => new ProcessingFilterOption($"status:{s}", s, false, Loc.Instance.Status(s))));

        return filters;
    }

    protected override void OnSessionChanged()
    {
        OnPropertyChanged(nameof(CanView));
        OnPropertyChanged(nameof(IsAccessDenied));
        OnPropertyChanged(nameof(CanCreateOrder));
        OnPropertyChanged(nameof(CanPayOrder));
        OnPropertyChanged(nameof(CanPaySelectedOrder));
        OnPropertyChanged(nameof(CanChangeStatus));
        OnPropertyChanged(nameof(CanCorrect));

        _orders = Array.Empty<ProcessingOrderDto>();
        _orderDetailsById = new Dictionary<int, OrderDetailsDto>();
        SearchText = string.Empty;
        SelectedOrderItems.Clear();
        SelectedOrderBillingText = string.Empty;
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
        _ = LoadSelectedOrderDetailsAsync(SelectedOrder);

        // RebuildOrders reselectionne une nouvelle ligne : on retablit le statut choisi dans la nouvelle liste.
        SelectedNextStatus = NextStatuses.FirstOrDefault(s => s.Value == keepStatusValue) ?? NextStatuses.FirstOrDefault();
    }
}
