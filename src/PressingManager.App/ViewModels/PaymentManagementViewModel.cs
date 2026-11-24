
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressingManager.App.Services;
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
    private readonly LanguageService _languageService;

    public PaymentManagementViewModel(
        IPaymentService payments,
        ICashService cash,
        IExpenseService expenses,
        IOrderBillingInfoProvider orderBilling,
        ICurrentUserContext session,
        LanguageService languageService)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _cash = cash ?? throw new ArgumentNullException(nameof(cash));
        _expenses = expenses ?? throw new ArgumentNullException(nameof(expenses));
        _orderBilling = orderBilling ?? throw new ArgumentNullException(nameof(orderBilling));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
    }

    public ObservableCollection<PaymentHistoryItem> OrderPayments { get; } = new();

    public ObservableCollection<ExpenseDto> RecentExpenses { get; } = new();

    public IReadOnlyList<PaymentMethodOption> ExpenseMethods =>
        Enum.GetValues<PaymentMethod>()
            .Select(m => new PaymentMethodOption(m, GetMethodLabel(m)))
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
        ? Translate(
            $"Caisse ouverte (#{CurrentCashSession!.Id}) • Fonds initial : {CurrentCashSession.OpeningAmount:N2}",
            $"Cash register open (#{CurrentCashSession!.Id}) • Opening balance: {CurrentCashSession.OpeningAmount:N2}")
        : Translate(
            "Caisse fermée — ouvrez une session pour encaisser en espèces",
            "Cash register closed — open a session to accept cash payments");

    public bool HasLatestReceipt => LatestReceipt is not null;

    public string LatestReceiptSummary => LatestReceipt is null
        ? string.Empty
        : $"{LatestReceipt.ReceiptNumber} • {LatestReceipt.AmountPaid:N2} via {TranslateMethodLabel(LatestReceipt.MethodLabel)}";

    public event EventHandler<PaymentReceiptDto>? ReceiptReady;

    // Actualise les propriétés dépendantes de la langue sélectionnée.
    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(CashStatusLabel));
        OnPropertyChanged(nameof(LatestReceiptSummary));
        OnPropertyChanged(nameof(ExpenseMethods));

        // Actualisation des libellés de l'historique sans perdre les paiements.
        var payments = OrderPayments.ToList();
        OrderPayments.Clear();

        foreach (var payment in payments)
        {
            OrderPayments.Add(payment with
            {
                MethodLabel = GetMethodLabel(payment.Method),
                StatusLabel = GetStatusLabel(payment.Status)
            });
        }

        if (SelectedPayment is not null)
        {
            SelectedPayment = OrderPayments.FirstOrDefault(
                p => p.Id == SelectedPayment.Id);
        }

        if (!string.IsNullOrWhiteSpace(Message))
        {
            Message = TranslateKnownMessage(Message);
        }
    }

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
            throw new InvalidOperationException(Translate(
                "Saisissez un identifiant de commande valide (nombre positif).",
                "Enter a valid order ID (positive number)."));
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
        var amount = ParsePositiveAmount(PaymentAmountText, "montant du paiement", "payment amount");
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
            ReceiptReady?.Invoke(this, LatestReceipt);
    }

    [RelayCommand]
    private void OpenSelectedPaymentReceipt()
    {
        if (SelectedPayment is null)
        {
            IsError = true;
            Message = Translate(
                "Sélectionnez un paiement valide dans l'historique.",
                "Select a valid payment from the history.");
            return;
        }

        if (!SelectedPayment.IsValid)
        {
            IsError = true;
            Message = Translate(
                "Un reçu officiel ne peut être généré que pour un paiement validé.",
                "An official receipt can only be generated for a validated payment.");
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
        var openingAmount = ParseNonNegativeAmount(
            OpeningAmountText, "fonds d'ouverture", "opening balance");

        var userId = _session.CurrentUser?.Id ?? 0;
        CurrentCashSession = await _cash.OpenAsync(
            new OpenCashSessionDto(openingAmount, userId));
    }, "Session de caisse ouverte.");

    [RelayCommand]
    private Task CloseCashSessionAsync() => RunAsync(async () =>
    {
        if (CurrentCashSession is null ||
            CurrentCashSession.Status != CashSessionStatus.Open)
        {
            throw new InvalidOperationException(Translate(
                "Aucune session de caisse ouverte à clôturer.",
                "There is no open cash register session to close."));
        }

        var counted = ParseNonNegativeAmount(
            CountedAmountText, "montant compté", "counted amount");

        var userId = _session.CurrentUser?.Id ?? 0;

        var closed = await _cash.CloseAsync(new CloseCashSessionDto(
            CurrentCashSession.Id,
            counted,
            userId,
            CloseComment));

        CurrentCashSession = null;
        CountedAmountText = string.Empty;
        CloseComment = string.Empty;

        Message = Translate(
            $"Caisse clôturée — Attendu : {closed.ExpectedAmount:N2}, Écart : {closed.Discrepancy:N2}.",
            $"Cash register closed — Expected: {closed.ExpectedAmount:N2}, Difference: {closed.Discrepancy:N2}.");
    }, successMessage: null);

    [RelayCommand]
    private Task CreateExpenseAsync() => RunAsync(async () =>
    {
        var amount = ParsePositiveAmount(
            ExpenseAmountText, "montant de la dépense", "expense amount");

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
            ?? throw new InvalidOperationException(
                Translate(
                    $"Commande #{orderId} introuvable.",
                    $"Order #{orderId} was not found."));

        var payments = await _payments.GetByOrderAsync(orderId);
        var balance = await _payments.GetBalanceAsync(orderId);

        LoadedOrderId = order.OrderId;
        OrderNumber = string.IsNullOrWhiteSpace(order.OrderNumber)
            ? $"CMD-{order.OrderId:D4}"
            : order.OrderNumber;

        CustomerName = string.IsNullOrWhiteSpace(order.CustomerName)
            ? Translate("Client", "Customer")
            : order.CustomerName;

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
                GetMethodLabel(p.Method),
                p.Reference,
                p.Status,
                GetStatusLabel(p.Status),
                p.CancelReason));
        }
    }

    private async Task RefreshExpensesAsync()
    {
        var now = DateTime.UtcNow;
        var list = await _expenses.SearchAsync(now.AddDays(-30), now.AddDays(1));

        RecentExpenses.Clear();

        foreach (var expense in list)
            RecentExpenses.Add(expense);
    }

    private int RequireLoadedOrderId() =>
        LoadedOrderId ?? throw new InvalidOperationException(
            Translate(
                "Chargez d'abord une commande avant d'enregistrer un paiement.",
                "Load an order before registering a payment."));

    private PaymentHistoryItem RequireSelectedPayment() =>
        SelectedPayment ?? throw new InvalidOperationException(
            Translate(
                "Sélectionnez d'abord un paiement dans la liste.",
                "Select a payment from the list first."));

    private decimal ParsePositiveAmount(
        string text,
        string frenchFieldName,
        string englishFieldName)
    {
        var value = ParseNonNegativeAmount(text, frenchFieldName, englishFieldName);

        if (value <= 0m)
        {
            throw new InvalidOperationException(Translate(
                $"Le {frenchFieldName} doit être strictement supérieur à zéro.",
                $"The {englishFieldName} must be greater than zero."));
        }

        return value;
    }

    private decimal ParseNonNegativeAmount(
        string text,
        string frenchFieldName,
        string englishFieldName)
    {
        var normalized = (text ?? string.Empty).Trim().Replace(',', '.');

        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value) || value < 0m)
        {
            throw new InvalidOperationException(Translate(
                $"Saisissez un {frenchFieldName} valide.",
                $"Enter a valid {englishFieldName}."));
        }

        return value;
    }

    private async Task RunAsync(Func<Task> action, string? successMessage)
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            await action();

            IsError = false;

            if (successMessage is not null)
                Message = TranslateKnownMessage(successMessage);
        }
        catch (Exception ex) when (
            ex is BusinessRuleException or
            PermissionDeniedException or
            InvalidOperationException)
        {
            IsError = true;
            Message = TranslateKnownMessage(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string GetMethodLabel(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => Translate("Espèces", "Cash"),
            PaymentMethod.MtnMobileMoney => "MTN Mobile Money",
            PaymentMethod.OrangeMoney => "Orange Money",
            PaymentMethod.Other => Translate("Autre", "Other"),
            _ => method.ToString()
        };
    }

    private string GetStatusLabel(PaymentStatus status)
    {
        var label = PaymentReceiptGenerator.GetStatusLabel(status);
        return TranslateKnownMessage(label);
    }

    private string TranslateMethodLabel(string label)
    {
        return label switch
        {
            "Espèces" => Translate("Espèces", "Cash"),
            "Autre" => Translate("Autre", "Other"),
            _ => label
        };
    }

    private string TranslateKnownMessage(string message)
    {
        return message switch
        {
            "Paiement validé et reçu généré avec succès." =>
                Translate("Paiement validé et reçu généré avec succès.",
                    "Payment validated and receipt generated successfully."),

            "Paiement annulé." =>
                Translate("Paiement annulé.", "Payment cancelled."),

            "Paiement remboursé." =>
                Translate("Paiement remboursé.", "Payment refunded."),

            "Session de caisse ouverte." =>
                Translate("Session de caisse ouverte.", "Cash register session opened."),

            "Dépense enregistrée." =>
                Translate("Dépense enregistrée.", "Expense saved."),

            "Valide" => Translate("Valide", "Valid"),
            "Annulé" => Translate("Annulé", "Cancelled"),
            "Remboursé" => Translate("Remboursé", "Refunded"),

            _ => message
        };
    }

    private string Translate(string french, string english)
    {
        return _languageService.IsEnglish ? english : french;
    }
}
