using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public enum ReportViewKind
{
    Orders,
    Outstanding,
    AwaitingPickup
}

public enum ReportPeriodKind
{
    Week,
    Month,
    Quarter,
    Year
}

public sealed record StatusFilterOption(OrderStatus? Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Une paire de barres du graphique (hauteurs deja converties en pixels).</summary>
public sealed record BarItem(string Label, double CollectedHeight, double ExpensesHeight, string Tooltip);

/// <summary>Une part de l'anneau « Repartition des services ».</summary>
public sealed record ServiceSliceItem(string Name, string PercentText, string AmountText, Brush Color, Geometry Shape)
{
    public string Tooltip => $"{Name} : {AmountText}";
}

/// <summary>Une ligne du tableau, avec les textes deja formates pour l'affichage (dates en heure locale).</summary>
public sealed record ReportRowItem(OrderReportRow Source)
{
    public string OrderNumber => Source.OrderNumber;

    public string DateText => TimeZoneInfo
        .ConvertTimeFromUtc(DateTime.SpecifyKind(Source.CreatedAt, DateTimeKind.Utc), TimeZoneInfo.Local)
        .ToString("dd/MM/yyyy HH:mm");

    public string CustomerName => Source.CustomerName;

    public string StatusLabel => Loc.Instance.Status(Source.Status);

    public string TotalText => MoneyFormatter.FormatFcfa(Source.Total, withCurrency: false);

    public string PaidText => MoneyFormatter.FormatFcfa(Source.AmountPaid, withCurrency: false);

    public string BalanceText => MoneyFormatter.FormatFcfa(Source.Balance, withCurrency: false);

    /// <summary>Vrai si le client doit encore de l'argent (commande non annulee).</summary>
    public bool HasBalance => Source.Status != OrderStatus.Cancelled && Source.Balance > 0;
}

/// <summary>Onglet Rapports &amp; Statistiques (reserve a l'administrateur : permission ViewReports).</summary>
public partial class ReportsViewModel : ObservableObject
{
    /// <summary>Hauteur utile (pixels) de la zone des barres ; doit correspondre a la vue.</summary>
    public const double PlotHeight = 170;

    private const double DonutCenter = 80;
    private const double DonutOuterRadius = 76;
    private const double DonutInnerRadius = 50;

    private static readonly string[] SlicePalette = { "#08A6B5", "#0864D9", "#F59E0B", "#8B5CF6", "#94A3B8" };

    private readonly IReportService _reports;
    private readonly IReportExportService _exports;
    private readonly ICurrentUserContext _session;

    private IReadOnlyList<OrderReportRow> _periodOrders = Array.Empty<OrderReportRow>();
    private IReadOnlyList<OrderReportRow> _outstanding = Array.Empty<OrderReportRow>();
    private IReadOnlyList<OrderReportRow> _awaitingPickup = Array.Empty<OrderReportRow>();
    private IReadOnlyList<OrderReportRow> _displayed = Array.Empty<OrderReportRow>();
    private FinancialSummary? _previousSummary;
    private IReadOnlyList<TrendPoint> _trend = Array.Empty<TrendPoint>();
    private IReadOnlyList<ServiceShare> _shares = Array.Empty<ServiceShare>();
    private decimal _serviceTotal;
    private decimal _chartMax;
    private bool _refreshPending;

    public ReportsViewModel(IReportService reports, IReportExportService exports, ICurrentUserContext session)
    {
        _reports = reports;
        _exports = exports;
        _session = session;

        _statusFilters = BuildStatusFilters();
        _selectedStatus = _statusFilters[0];

        Loc.Instance.LanguageChanged += OnLanguageChanged;

        (_fromDate, _toDate) = PeriodDates(ReportPeriodKind.Month);
    }

    [ObservableProperty]
    private IReadOnlyList<StatusFilterOption> _statusFilters;

    public ObservableCollection<ReportRowItem> Rows { get; } = new();

    public ObservableCollection<BarItem> Bars { get; } = new();

    public ObservableCollection<ServiceSliceItem> ServiceSlices { get; } = new();

    public bool CanViewReports => _session.HasPermission(Permission.ViewReports);

    public bool IsAccessDenied => !CanViewReports;

    /// <summary>La vue est creee avant la connexion : a rappeler a chaque ouverture de l'onglet pour relire les droits.</summary>
    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(IsAccessDenied));
        OnPropertyChanged(nameof(CanExport));
    }

    public bool CanExport => _session.HasPermission(Permission.ExportReports);

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private StatusFilterOption? _selectedStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeekPeriod), nameof(IsMonthPeriod), nameof(IsQuarterPeriod), nameof(IsYearPeriod))]
    private ReportPeriodKind _selectedPeriod = ReportPeriodKind.Month;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrdersView), nameof(IsOutstandingView), nameof(IsAwaitingView), nameof(ShowStatusFilter))]
    private ReportViewKind _selectedView = ReportViewKind.Orders;

    [ObservableProperty]
    private FinancialSummary? _summary;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    public bool IsWeekPeriod
    {
        get => SelectedPeriod == ReportPeriodKind.Week;
        set { if (value) SelectedPeriod = ReportPeriodKind.Week; }
    }

    public bool IsMonthPeriod
    {
        get => SelectedPeriod == ReportPeriodKind.Month;
        set { if (value) SelectedPeriod = ReportPeriodKind.Month; }
    }

    public bool IsQuarterPeriod
    {
        get => SelectedPeriod == ReportPeriodKind.Quarter;
        set { if (value) SelectedPeriod = ReportPeriodKind.Quarter; }
    }

    public bool IsYearPeriod
    {
        get => SelectedPeriod == ReportPeriodKind.Year;
        set { if (value) SelectedPeriod = ReportPeriodKind.Year; }
    }

    public bool IsOrdersView
    {
        get => SelectedView == ReportViewKind.Orders;
        set { if (value) SelectedView = ReportViewKind.Orders; }
    }

    public bool IsOutstandingView
    {
        get => SelectedView == ReportViewKind.Outstanding;
        set { if (value) SelectedView = ReportViewKind.Outstanding; }
    }

    public bool IsAwaitingView
    {
        get => SelectedView == ReportViewKind.AwaitingPickup;
        set { if (value) SelectedView = ReportViewKind.AwaitingPickup; }
    }

    /// <summary>Le filtre par statut n'a de sens que pour la liste des commandes de la periode.</summary>
    public bool ShowStatusFilter => SelectedView == ReportViewKind.Orders;

    // --- Cartes (Recettes / Depenses / Benefice) -------------------------------------------------

    public string CollectedText => MoneyFormatter.FormatFcfa(Summary?.TotalCollected ?? 0m);

    public string ExpensesText => MoneyFormatter.FormatFcfa(Summary?.TotalExpenses ?? 0m);

    public string NetResultText => MoneyFormatter.FormatFcfa(Summary?.NetResult ?? 0m);

    public bool IsNetNegative => (Summary?.NetResult ?? 0m) < 0m;

    public string CollectedTrendText => Delta(Summary?.TotalCollected ?? 0m, _previousSummary?.TotalCollected ?? 0m);

    public string ExpensesTrendText => Delta(Summary?.TotalExpenses ?? 0m, _previousSummary?.TotalExpenses ?? 0m);

    public string NetTrendText => Delta(Summary?.NetResult ?? 0m, _previousSummary?.NetResult ?? 0m);

    // --- Graphiques -----------------------------------------------------------------------------

    public bool HasChartData => _chartMax > 0m;

    public bool HasNoChartData => !HasChartData;

    public string AxisTopText => Compact(_chartMax);

    public string AxisMiddleText => Compact(_chartMax / 2m);

    public bool HasServiceData => _serviceTotal > 0m;

    public bool HasNoServiceData => !HasServiceData;

    public string ServiceTotalText => Compact(_serviceTotal);

    public string DisplayedCountText => Loc.Instance.Format("Stats_Rows", Rows.Count);

    partial void OnSummaryChanged(FinancialSummary? value) => OnPropertyChanged(string.Empty);

    partial void OnSelectedViewChanged(ReportViewKind value) => ApplyView();

    partial void OnSelectedStatusChanged(StatusFilterOption? value) => ApplyView();

    partial void OnSelectedPeriodChanged(ReportPeriodKind value)
    {
        (FromDate, ToDate) = PeriodDates(value);
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            _refreshPending = true;
            return;
        }

        do
        {
            _refreshPending = false;
            await RunAsync(LoadAsync, successKey: null);
        }
        while (_refreshPending);
    }

    /// <summary>Recharge les chiffres (appele par l'ecran a chaque ouverture de l'onglet).</summary>
    public Task RefreshDataAsync() => RefreshAsync();

    /// <summary>Exporte les lignes actuellement affichees. Le chemin est choisi par la vue (boite de dialogue).</summary>
    public Task ExportAsync(string filePath, bool asPdf) => RunAsync(async () =>
    {
        var description = BuildDescription();

        if (asPdf)
        {
            await _exports.ExportOrdersPdfAsync(_displayed, filePath, description);
        }
        else
        {
            await _exports.ExportOrdersAsync(_displayed, filePath, description);
        }
    }, "Stats_ExportDone", filePath);

    private static (DateTime From, DateTime To) PeriodDates(ReportPeriodKind period)
    {
        var today = DateTime.Today;
        var days = period switch
        {
            ReportPeriodKind.Week => 7,
            ReportPeriodKind.Month => 30,
            ReportPeriodKind.Quarter => 90,
            _ => 365
        };

        return (today.AddDays(-(days - 1)), today);
    }

    private async Task LoadAsync()
    {
        if (FromDate is not { } from || ToDate is not { } to)
        {
            throw new BusinessRuleException(Loc.Instance["Stats_NoDates"]);
        }

        var (fromUtc, toUtc) = ReportPeriod.ToUtcRange(from, to, TimeZoneInfo.Local);

        // Periode precedente de meme duree, pour les pourcentages d'evolution des cartes.
        var length = (to.Date - from.Date).Days + 1;
        var previousTo = from.Date.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(length - 1));
        var (previousFromUtc, previousToUtc) = ReportPeriod.ToUtcRange(previousFrom, previousTo, TimeZoneInfo.Local);

        _periodOrders = await _reports.GetOrdersAsync(fromUtc, toUtc);
        _outstanding = await _reports.GetOutstandingBalancesAsync();
        _awaitingPickup = await _reports.GetAwaitingPickupAsync();
        _previousSummary = await _reports.GetFinancialSummaryAsync(previousFromUtc, previousToUtc);

        _trend = await _reports.GetTrendAsync(fromUtc, toUtc);
        _shares = await _reports.GetServiceBreakdownAsync(fromUtc, toUtc);

        BuildBars();
        BuildServiceSlices();
        ApplyView();

        // En dernier : declenche le rafraichissement de toutes les valeurs affichees.
        Summary = await _reports.GetFinancialSummaryAsync(fromUtc, toUtc);

        // Les records se comparent par valeur : si le resume est identique, Summary ne notifie pas.
        OnPropertyChanged(string.Empty);
    }

    private void BuildBars()
    {
        var trend = _trend;
        var culture = Loc.Instance.Culture;
        _chartMax = trend.Count == 0 ? 0m : trend.Max(t => Math.Max(t.Collected, t.Expenses));
        var scale = _chartMax > 0m ? PlotHeight / (double)_chartMax : 0d;
        var labelStep = trend.Count <= 12 ? 1 : trend.Count <= 16 ? 2 : 5;

        Bars.Clear();
        for (var i = 0; i < trend.Count; i++)
        {
            var point = trend[i];
            var label = point.IsMonthly
                ? point.Period.ToString("MMM", culture)
                : point.Period.ToString("dd/MM", culture);

            Bars.Add(new BarItem(
                i % labelStep == 0 ? label : string.Empty,
                (double)point.Collected * scale,
                (double)point.Expenses * scale,
                Loc.Instance.Format(
                    "Stats_BarTooltip",
                    label,
                    MoneyFormatter.FormatFcfa(point.Collected),
                    MoneyFormatter.FormatFcfa(point.Expenses))));
        }
    }

    private void BuildServiceSlices()
    {
        var shares = _shares;
        // Les 4 plus gros services, le reste regroupe dans « Autres ».
        var main = shares.Take(4).ToList();
        var restAmount = shares.Skip(4).Sum(s => s.Amount);
        if (restAmount > 0m)
        {
            main.Add(new ServiceShare(Loc.Instance["Stats_Others"], restAmount));
        }

        _serviceTotal = main.Sum(s => s.Amount);

        ServiceSlices.Clear();
        if (_serviceTotal <= 0m)
        {
            return;
        }

        var angle = 0d;
        for (var i = 0; i < main.Count; i++)
        {
            var share = main[i];
            var fraction = (double)(share.Amount / _serviceTotal);
            var sweep = fraction * 360d;

            ServiceSlices.Add(new ServiceSliceItem(
                share.ServiceName,
                $"{Math.Round(fraction * 100d, 0, MidpointRounding.AwayFromZero):0}%",
                MoneyFormatter.FormatFcfa(share.Amount),
                FrozenBrush(SlicePalette[i % SlicePalette.Length]),
                BuildSlice(angle, sweep)));

            angle += sweep;
        }
    }

    /// <summary>Secteur d'anneau : l'angle 0 est en haut, le sens est horaire.</summary>
    private static Geometry BuildSlice(double startAngle, double sweep)
    {
        if (sweep >= 359.99)
        {
            sweep = 359.99;
        }

        var endAngle = startAngle + sweep;
        var isLarge = sweep > 180d;

        var figure = new PathFigure { StartPoint = PointAt(DonutOuterRadius, startAngle), IsClosed = true };
        figure.Segments.Add(new ArcSegment(
            PointAt(DonutOuterRadius, endAngle),
            new Size(DonutOuterRadius, DonutOuterRadius),
            0, isLarge, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(PointAt(DonutInnerRadius, endAngle), true));
        figure.Segments.Add(new ArcSegment(
            PointAt(DonutInnerRadius, startAngle),
            new Size(DonutInnerRadius, DonutInnerRadius),
            0, isLarge, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointAt(double radius, double angleDegrees)
    {
        var radians = (angleDegrees - 90d) * Math.PI / 180d;
        return new Point(
            DonutCenter + radius * Math.Cos(radians),
            DonutCenter + radius * Math.Sin(radians));
    }

    private static Brush FrozenBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    /// <summary>« ↑ +12% » / « ↓ -5% » par rapport a la periode precedente.</summary>
    private static string Delta(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            return current == 0m ? "—" : Loc.Instance["Stats_New"];
        }

        var percent = Math.Round((current - previous) / Math.Abs(previous) * 100m, 0, MidpointRounding.AwayFromZero);
        var arrow = percent >= 0m ? "↑" : "↓";
        var sign = percent >= 0m ? "+" : string.Empty;

        return $"{arrow} {sign}{percent.ToString("0", CultureInfo.InvariantCulture)}%";
    }

    /// <summary>Montant court pour l'axe et le centre de l'anneau : 12 500 -> « 12,5 k ».</summary>
    private static string Compact(decimal amount)
    {
        if (amount >= 1_000_000m)
        {
            return (amount / 1_000_000m).ToString("0.#", Loc.Instance.Culture) + " M";
        }

        if (amount >= 1_000m)
        {
            return (amount / 1_000m).ToString("0.#", Loc.Instance.Culture) + " k";
        }

        return amount.ToString("0", Loc.Instance.Culture);
    }

    private static List<StatusFilterOption> BuildStatusFilters() =>
        new[] { new StatusFilterOption(null, Loc.Instance["Stats_AllStatuses"]) }
            .Concat(Enum.GetValues<OrderStatus>().Select(s => new StatusFilterOption(s, Loc.Instance.Status(s))))
            .ToList();

    /// <summary>Le choix FR/EN se fait sur l'ecran de connexion ; on rafraichit quand meme si la langue change.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        var selectedValue = SelectedStatus?.Value;

        StatusFilters = BuildStatusFilters();
        SelectedStatus = StatusFilters.First(f => f.Value == selectedValue);

        BuildBars();
        BuildServiceSlices();
        ApplyView();
        OnPropertyChanged(string.Empty);
    }

    private void ApplyView()
    {
        _displayed = SelectedView switch
        {
            ReportViewKind.Outstanding => _outstanding,
            ReportViewKind.AwaitingPickup => _awaitingPickup,
            _ => SelectedStatus?.Value is { } status
                ? _periodOrders.Where(o => o.Status == status).ToList()
                : _periodOrders
        };

        Rows.Clear();
        foreach (var row in _displayed)
        {
            Rows.Add(new ReportRowItem(row));
        }

        OnPropertyChanged(nameof(DisplayedCountText));
    }

    private string BuildDescription() => SelectedView switch
    {
        ReportViewKind.Outstanding => Loc.Instance.Format("Stats_DescDue", $"{DateTime.Today:dd/MM/yyyy}"),
        ReportViewKind.AwaitingPickup => Loc.Instance.Format("Stats_DescPickup", $"{DateTime.Today:dd/MM/yyyy}"),
        _ => Loc.Instance.Format("Stats_DescOrders", $"{FromDate:dd/MM/yyyy}", $"{ToDate:dd/MM/yyyy}")
    };

    private async Task RunAsync(Func<Task> action, string? successKey, params object[] successArgs)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await action();
            IsError = false;
            Message = successKey is null ? null : Loc.Instance.Format(successKey, successArgs);
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException)
        {
            IsError = true;
            Message = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IsError = true;
            Message = Loc.Instance.Format("Stats_FileError", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
