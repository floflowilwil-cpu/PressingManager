using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Localization;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.App.ViewModels;

public sealed record PaymentHistoryItem(
    int Id,
    int OrderId,
    decimal Amount,
    DateTime PaidAtUtc,
    DateTime PaidAtLocal,
    PaymentMethod Method,
    string MethodLabel,
    string? Reference,
    PaymentStatus Status,
    string StatusLabel,
    string? CancelReason)
{
    public bool IsValid => Status == PaymentStatus.Valid;
}

public sealed record PaymentMethodOption(PaymentMethod Value, string Label);

public partial class PaymentManagementViewModel : ObservableObject
{
    private readonly IPaymentService _payments;
    private readonly ICashService _cash;
    private readonly IExpenseService _expenses;
    private readonly IOrderBillingInfoProvider _orderBilling;
    private readonly ICurrentUserContext _session;

    private IReadOnlyList<PaymentDto> _loadedPayments = Array.Empty<PaymentDto>();

    public PaymentManagementViewModel(
        IPaymentService payments,
        ICashService cash,
        IExpenseService expenses,
        IOrderBillingInfoProvider orderBilling,
        ICurrentUserContext session)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _cash = cash ?? throw new ArgumentNullException(nameof(cash));
        _expenses = expenses ?? throw new ArgumentNullException(nameof(expenses));
        _orderBilling = orderBilling ?? throw new ArgumentNullException(nameof(orderBilling));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        _expenseMethods = BuildExpenseMethods();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<PaymentHistoryItem> OrderPayments { get; } = new();

    public ObservableCollection<ExpenseDto> RecentExpenses { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<PaymentMethodOption> _expenseMethods;

    [ObservableProperty]
    private string _orderIdText = string.Empty;

    [ObservableProperty]
    private int? _loadedOrderId;

    [ObservableProperty]
    private string _orderNumber = "-";

    [ObservableProperty]
    private string _customerName = "-";

    [ObservableProperty]
    private decimal _orderTotal;

    [ObservableProperty]
    private decimal _remainingBalance;

    [ObservableProperty]
    private bool _isOrderLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsCashSelected),
        nameof(IsMtnSelected),
        nameof(IsOrangeSelected),
        nameof(IsOtherSelected),
        nameof(IsMobileMoneySelected))]
    private PaymentMethod _selectedMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string _paymentAmountText = string.Empty;

    [ObservableProperty]
    private string _transactionReference = string.Empty;

    [ObservableProperty]
    private string _cancelReason = string.Empty;

    [ObservableProperty]
    private PaymentHistoryItem? _selectedPayment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLatestReceipt), nameof(LatestReceiptSummary))]
    private PaymentReceiptDto? _latestReceipt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCashSessionOpen), nameof(CashStatusLabel))]
    private CashSessionDto? _currentCashSession;

    [ObservableProperty]
    private string _openingAmountText = "0";

    [ObservableProperty]
    private string _countedAmountText = string.Empty;

    [ObservableProperty]
    private string _closeComment = string.Empty;

    [ObservableProperty]
    private string _expenseCategory = string.Empty;

    [ObservableProperty]
    private string _expenseAmountText = string.Empty;

    [ObservableProperty]
    private PaymentMethod _expenseMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string _expenseComment = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsCashSelected => SelectedMethod == PaymentMethod.Cash;

    public bool IsMtnSelected => SelectedMethod == PaymentMethod.MtnMobileMoney;

    public bool IsOrangeSelected => SelectedMethod == PaymentMethod.OrangeMoney;

    public bool IsOtherSelected => SelectedMethod == PaymentMethod.Other;

    public bool IsMobileMoneySelected => PaymentRules.RequiresReference(SelectedMethod);

    public bool IsCashSessionOpen => CurrentCashSession is { Status: CashSessionStatus.Open };

    public string CashStatusLabel => IsCashSessionOpen
        ? Loc.Instance.Format(
            "Pay_CashOpenStatus",
            CurrentCashSession!.Id,
            CurrentCashSession.OpeningAmount.ToString("N2", Loc.Instance.Culture))
        : Loc.Instance["Pay_CashClosedStatus"];

    public bool HasLatestReceipt => LatestReceipt is not null;

    public string LatestReceiptSummary => LatestReceipt is null
        ? string.Empty
        : Loc.Instance.Format(
            "Pay_LatestReceiptSummary",
            LatestReceipt.ReceiptNumber,
            LatestReceipt.AmountPaid.ToString("N2", Loc.Instance.Culture),
            Loc.Instance.PaymentMethod(LatestReceipt.Method));

    /// <summary>Declenche l'ouverture de la fenetre de confirmation et d'impression du recu.</summary>
    public event EventHandler<PaymentReceiptDto>? ReceiptReady;

    [RelayCommand]
    private void SelectMethod(PaymentMethod method) => SelectedMethod = method;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        CurrentCashSession = await _cash.GetCurrentAsync();
        await RefreshExpensesAsync();
    }, successKey: null);

    [RelayCommand]
    private Task LoadOrderAsync() => RunAsync(async () =>
    {
        if (!int.TryParse(OrderIdText.Trim(), out var orderId) || orderId <= 0)
        {
            throw new InvalidOperationException(Loc.Instance["Pay_Err_InvalidOrderId"]);
        }

        await RefreshOrderAsync(orderId);
        PaymentAmountText = RemainingBalance > 0m
            ? RemainingBalance.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
    }, successKey: null);

    [RelayCommand]
    private Task RegisterPaymentAsync() => RunAsync(async () =>
    {
        var orderId = RequireLoadedOrderId();
        var amount = ParsePositiveAmount(PaymentAmountText, "Pay_Field_PaymentAmount");
        var userId = _session.CurrentUser?.Id ?? 0;

        var created = await _payments.RegisterAsync(new RegisterPaymentDto(
            orderId,
            amount,
            SelectedMethod,
            userId,
            IsMobileMoneySelected ? TransactionReference : null));

        TransactionReference = string.Empty;
        await RefreshOrderAsync(orderId);

        PaymentAmountText = RemainingBalance > 0m
            ? RemainingBalance.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;

        var receipt = BuildReceiptFromDto(created);
        LatestReceipt = receipt;
        ReceiptReady?.Invoke(this, receipt);
    }, "Pay_Msg_PaymentSaved");

    [RelayCommand]
    private void OpenLatestReceipt()
    {
        if (LatestReceipt is not null)
        {
            ReceiptReady?.Invoke(this, LatestReceipt);
        }
    }

    [RelayCommand]
    private void OpenSelectedPaymentReceipt()
    {
        if (SelectedPayment is null)
        {
            IsError = true;
            Message = Loc.Instance["Pay_Err_SelectValidPayment"];
            return;
        }

        if (!SelectedPayment.IsValid)
        {
            IsError = true;
            Message = Loc.Instance["Pay_Err_ReceiptOnlyForValid"];
            return;
        }

        var dto = new PaymentDto(
            SelectedPayment.Id,
            SelectedPayment.OrderId,
            SelectedPayment.Amount,
            SelectedPayment.PaidAtUtc,
            SelectedPayment.Method,
            SelectedPayment.Reference,
            SelectedPayment.Status,
            _session.CurrentUser?.Id ?? 0,
            null,
            SelectedPayment.CancelReason,
            null,
            null);

        var receipt = BuildReceiptFromDto(dto);
        LatestReceipt = receipt;
        ReceiptReady?.Invoke(this, receipt);
    }

    [RelayCommand]
    private Task CancelSelectedPaymentAsync() => RunAsync(async () =>
    {
        var payment = RequireSelectedPayment();
        var userId = _session.CurrentUser?.Id ?? 0;

        await _payments.CancelAsync(new CancelPaymentDto(payment.Id, CancelReason, userId));
        CancelReason = string.Empty;
        await RefreshOrderAsync(payment.OrderId);
    }, "Pay_Msg_PaymentCancelled");

    [RelayCommand]
    private Task RefundSelectedPaymentAsync() => RunAsync(async () =>
    {
        var payment = RequireSelectedPayment();
        var userId = _session.CurrentUser?.Id ?? 0;

        await _payments.RefundAsync(new CancelPaymentDto(payment.Id, CancelReason, userId));
        CancelReason = string.Empty;
        await RefreshOrderAsync(payment.OrderId);
    }, "Pay_Msg_PaymentRefunded");

    [RelayCommand]
    private Task OpenCashSessionAsync() => RunAsync(async () =>
    {
        var openingAmount = ParseNonNegativeAmount(OpeningAmountText, "Pay_Field_OpeningAmount");
        var userId = _session.CurrentUser?.Id ?? 0;
        CurrentCashSession = await _cash.OpenAsync(new OpenCashSessionDto(openingAmount, userId));
    }, "Pay_Msg_CashOpened");

    [RelayCommand]
    private Task CloseCashSessionAsync() => RunAsync(async () =>
    {
        if (CurrentCashSession is null || CurrentCashSession.Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException(Loc.Instance["Pay_Err_NoOpenCash"]);
        }

        var counted = ParseNonNegativeAmount(CountedAmountText, "Pay_Field_CountedAmount");
        var userId = _session.CurrentUser?.Id ?? 0;

        var closed = await _cash.CloseAsync(new CloseCashSessionDto(
            CurrentCashSession.Id,
            counted,
            userId,
            CloseComment));

        CurrentCashSession = null;
        CountedAmountText = string.Empty;
        CloseComment = string.Empty;
        Message = Loc.Instance.Format(
            "Pay_Msg_CashClosed",
            (closed.ExpectedAmount ?? 0m).ToString("N2", Loc.Instance.Culture),
            (closed.Discrepancy ?? 0m).ToString("N2", Loc.Instance.Culture));
    }, successKey: null);

    [RelayCommand]
    private Task CreateExpenseAsync() => RunAsync(async () =>
    {
        var amount = ParsePositiveAmount(ExpenseAmountText, "Pay_Field_ExpenseAmount");
        var userId = _session.CurrentUser?.Id ?? 0;

        await _expenses.CreateAsync(new CreateExpenseDto(
            DateTime.UtcNow,
            ExpenseCategory,
            amount,
            ExpenseMethod,
            userId,
            ExpenseComment));

        ExpenseCategory = string.Empty;
        ExpenseAmountText = string.Empty;
        ExpenseComment = string.Empty;
        await RefreshExpensesAsync();
    }, "Pay_Msg_ExpenseSaved");

    private PaymentReceiptDto BuildReceiptFromDto(PaymentDto payment)
    {
        var totalPaid = OrderPayments
            .Where(p => p.IsValid)
            .Sum(p => p.Amount);

        var cashierName = _session.CurrentUser?.FullName ?? Loc.Instance["Pay_DefaultCashier"];
        return PaymentReceiptGenerator.BuildReceipt(
            payment,
            OrderNumber,
            CustomerName,
            OrderTotal,
            totalPaid,
            cashierName);
    }

    private async Task RefreshOrderAsync(int orderId)
    {
        var order = await _orderBilling.GetOrderBillingInfoAsync(orderId)
            ?? throw new InvalidOperationException(Loc.Instance.Format("Pay_Err_OrderNotFound", orderId));

        _loadedPayments = await _payments.GetByOrderAsync(orderId);
        var balance = await _payments.GetBalanceAsync(orderId);

        LoadedOrderId = order.OrderId;
        OrderNumber = string.IsNullOrWhiteSpace(order.OrderNumber) ? $"CMD-{order.OrderId:D4}" : order.OrderNumber;
        CustomerName = string.IsNullOrWhiteSpace(order.CustomerName) ? Loc.Instance["Pay_DefaultCustomer"] : order.CustomerName;
        OrderTotal = order.TotalAmount;
        RemainingBalance = balance;
        IsOrderLoaded = true;

        RebuildPaymentRows(SelectedPayment?.Id);
    }

    private void RebuildPaymentRows(int? keepSelectionId)
    {
        OrderPayments.Clear();
        foreach (var p in _loadedPayments)
        {
            OrderPayments.Add(new PaymentHistoryItem(
                p.Id,
                p.OrderId,
                p.Amount,
                p.PaidAt,
                p.PaidAt.ToLocalTime(),
                p.Method,
                Loc.Instance.PaymentMethod(p.Method),
                p.Reference,
                p.Status,
                Loc.Instance.PaymentStatus(p.Status),
                p.CancelReason));
        }

        SelectedPayment = OrderPayments.FirstOrDefault(p => p.Id == keepSelectionId);
    }

    private static List<PaymentMethodOption> BuildExpenseMethods() =>
        Enum.GetValues<PaymentMethod>()
            .Select(m => new PaymentMethodOption(m, Loc.Instance.PaymentMethod(m)))
            .ToList();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ExpenseMethods = BuildExpenseMethods();
        OnPropertyChanged(nameof(ExpenseMethod));
        OnPropertyChanged(nameof(CashStatusLabel));
        OnPropertyChanged(nameof(LatestReceiptSummary));
        RebuildPaymentRows(SelectedPayment?.Id);

        if (IsOrderLoaded && CustomerName is "Client" or "Customer")
        {
            CustomerName = Loc.Instance["Pay_DefaultCustomer"];
        }
    }

    private async Task RefreshExpensesAsync()
    {
        var now = DateTime.UtcNow;
        var list = await _expenses.SearchAsync(now.AddDays(-30), now.AddDays(1));
        RecentExpenses.Clear();
        foreach (var e in list)
        {
            RecentExpenses.Add(e);
        }
    }

    private int RequireLoadedOrderId() =>
        LoadedOrderId ?? throw new InvalidOperationException(Loc.Instance["Pay_Err_LoadOrderFirst"]);

    private PaymentHistoryItem RequireSelectedPayment() =>
        SelectedPayment ?? throw new InvalidOperationException(Loc.Instance["Pay_Err_SelectPaymentFirst"]);

    private static decimal ParsePositiveAmount(string text, string fieldKey)
    {
        var value = ParseNonNegativeAmount(text, fieldKey);
        if (value <= 0m)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Pay_Err_PositiveAmount", Loc.Instance[fieldKey]));
        }

        return value;
    }

    private static decimal ParseNonNegativeAmount(string text, string fieldKey)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0m)
        {
            throw new InvalidOperationException(Loc.Instance.Format("Pay_Err_ValidAmount", Loc.Instance[fieldKey]));
        }

        return value;
    }

    private async Task RunAsync(Func<Task> action, string? successKey)
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
            if (successKey is not null)
            {
                Message = Loc.Instance[successKey];
            }
        }
        catch (Exception ex) when (ex is BusinessRuleException or PermissionDeniedException or InvalidOperationException)
        {
            IsError = true;
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
