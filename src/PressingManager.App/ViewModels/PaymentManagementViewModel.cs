using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    }

    public ObservableCollection<PaymentHistoryItem> OrderPayments { get; } = new();

    public ObservableCollection<ExpenseDto> RecentExpenses { get; } = new();

    public IReadOnlyList<PaymentMethodOption> ExpenseMethods { get; } = Enum.GetValues<PaymentMethod>()
        .Select(m => new PaymentMethodOption(m, PaymentReceiptGenerator.GetMethodLabel(m)))
        .ToList();

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
        ? $"Caisse ouverte (#{CurrentCashSession!.Id}) • Fonds initial : {CurrentCashSession.OpeningAmount:N2}"
        : "Caisse fermée — ouvrez une session pour encaisser en espèces";

    public bool HasLatestReceipt => LatestReceipt is not null;

    public string LatestReceiptSummary => LatestReceipt is null
        ? string.Empty
        : $"{LatestReceipt.ReceiptNumber} • {LatestReceipt.AmountPaid:N2} via {LatestReceipt.MethodLabel}";

    /// <summary>Declenche l'ouverture de la fenetre de confirmation et d'impression du recu.</summary>
    public event EventHandler<PaymentReceiptDto>? ReceiptReady;

    [RelayCommand]
    private void SelectMethod(PaymentMethod method) => SelectedMethod = method;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        CurrentCashSession = await _cash.GetCurrentAsync();
        await RefreshExpensesAsync();
    }, successMessage: null);

    [RelayCommand]
    private Task LoadOrderAsync() => RunAsync(async () =>
    {
        if (!int.TryParse(OrderIdText.Trim(), out var orderId) || orderId <= 0)
        {
            throw new InvalidOperationException("Saisissez un identifiant de commande valide (nombre positif).");
        }

        await RefreshOrderAsync(orderId);
        PaymentAmountText = RemainingBalance > 0m
            ? RemainingBalance.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
    }, successMessage: null);

    [RelayCommand]
    private Task RegisterPaymentAsync() => RunAsync(async () =>
    {
        var orderId = RequireLoadedOrderId();
        var amount = ParsePositiveAmount(PaymentAmountText, "montant du paiement");
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
    }, "Paiement validé et reçu généré avec succès.");

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
            Message = "Sélectionnez un paiement valide dans l'historique.";
            return;
        }

        if (!SelectedPayment.IsValid)
        {
            IsError = true;
            Message = "Un reçu officiel ne peut être généré que pour un paiement validé.";
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
    }, "Paiement annulé.");

    [RelayCommand]
    private Task RefundSelectedPaymentAsync() => RunAsync(async () =>
    {
        var payment = RequireSelectedPayment();
        var userId = _session.CurrentUser?.Id ?? 0;

        await _payments.RefundAsync(new CancelPaymentDto(payment.Id, CancelReason, userId));
        CancelReason = string.Empty;
        await RefreshOrderAsync(payment.OrderId);
    }, "Paiement remboursé.");

    [RelayCommand]
    private Task OpenCashSessionAsync() => RunAsync(async () =>
    {
        var openingAmount = ParseNonNegativeAmount(OpeningAmountText, "fonds d'ouverture");
        var userId = _session.CurrentUser?.Id ?? 0;
        CurrentCashSession = await _cash.OpenAsync(new OpenCashSessionDto(openingAmount, userId));
    }, "Session de caisse ouverte.");

    [RelayCommand]
    private Task CloseCashSessionAsync() => RunAsync(async () =>
    {
        if (CurrentCashSession is null || CurrentCashSession.Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException("Aucune session de caisse ouverte à clôturer.");
        }

        var counted = ParseNonNegativeAmount(CountedAmountText, "montant compté");
        var userId = _session.CurrentUser?.Id ?? 0;

        var closed = await _cash.CloseAsync(new CloseCashSessionDto(
            CurrentCashSession.Id,
            counted,
            userId,
            CloseComment));

        CurrentCashSession = null;
        CountedAmountText = string.Empty;
        CloseComment = string.Empty;
        Message = $"Caisse clôturée — Attendu : {closed.ExpectedAmount:N2}, Écart : {closed.Discrepancy:N2}.";
    }, successMessage: null);

    [RelayCommand]
    private Task CreateExpenseAsync() => RunAsync(async () =>
    {
        var amount = ParsePositiveAmount(ExpenseAmountText, "montant de la dépense");
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
    }, "Dépense enregistrée.");

    private PaymentReceiptDto BuildReceiptFromDto(PaymentDto payment)
    {
        var totalPaid = OrderPayments
            .Where(p => p.IsValid)
            .Sum(p => p.Amount);

        var cashierName = _session.CurrentUser?.FullName ?? "Caissier";
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
            ?? throw new InvalidOperationException($"Commande #{orderId} introuvable.");

        var payments = await _payments.GetByOrderAsync(orderId);
        var balance = await _payments.GetBalanceAsync(orderId);

        LoadedOrderId = order.OrderId;
        OrderNumber = string.IsNullOrWhiteSpace(order.OrderNumber) ? $"CMD-{order.OrderId:D4}" : order.OrderNumber;
        CustomerName = string.IsNullOrWhiteSpace(order.CustomerName) ? "Client" : order.CustomerName;
        OrderTotal = order.TotalAmount;
        RemainingBalance = balance;
        IsOrderLoaded = true;

        OrderPayments.Clear();
        foreach (var p in payments)
        {
            OrderPayments.Add(new PaymentHistoryItem(
                p.Id,
                p.OrderId,
                p.Amount,
                p.PaidAt,
                p.PaidAt.ToLocalTime(),
                p.Method,
                PaymentReceiptGenerator.GetMethodLabel(p.Method),
                p.Reference,
                p.Status,
                PaymentReceiptGenerator.GetStatusLabel(p.Status),
                p.CancelReason));
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
        LoadedOrderId ?? throw new InvalidOperationException("Chargez d'abord une commande avant d'enregistrer un paiement.");

    private PaymentHistoryItem RequireSelectedPayment() =>
        SelectedPayment ?? throw new InvalidOperationException("Sélectionnez d'abord un paiement dans la liste.");

    private static decimal ParsePositiveAmount(string text, string fieldName)
    {
        var value = ParseNonNegativeAmount(text, fieldName);
        if (value <= 0m)
        {
            throw new InvalidOperationException($"Le {fieldName} doit être strictement supérieur à zéro.");
        }

        return value;
    }

    private static decimal ParseNonNegativeAmount(string text, string fieldName)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0m)
        {
            throw new InvalidOperationException($"Saisissez un {fieldName} valide.");
        }

        return value;
    }

    private async Task RunAsync(Func<Task> action, string? successMessage)
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
            if (successMessage is not null)
            {
                Message = successMessage;
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
