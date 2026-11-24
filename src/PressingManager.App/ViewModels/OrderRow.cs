using System.Windows.Media;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;

namespace PressingManager.App.ViewModels;

/// <summary>Formatage d'affichage commun aux ecrans (devise a changer ici uniquement).</summary>
public static class UiFormat
{
    public const string Currency = "FCFA";

    public static string Money(decimal amount) => $"{MoneyFormatter.Format(amount)} {Currency}";

    /// <summary>La base stocke l'UTC : on affiche en heure locale.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
}

/// <summary>Une ligne du tableau des commandes (ou de l'historique d'un client).</summary>
public sealed class OrderRow
{
    public OrderRow(OrderDetailsDto details)
    {
        Details = details;

        (Background, Foreground) = details.Status switch
        {
            OrderStatus.Received or OrderStatus.AwaitingProcessing => (MakeBrush("#FEF3C7"), MakeBrush("#B45309")),
            OrderStatus.Cleaning or OrderStatus.Ironing => (MakeBrush("#DBEAFE"), MakeBrush("#1D4ED8")),
            OrderStatus.ReadyForPickup => (MakeBrush("#CCFBF1"), MakeBrush("#0F766E")),
            OrderStatus.Delivered => (MakeBrush("#DCFCE7"), MakeBrush("#15803D")),
            OrderStatus.Cancelled => (MakeBrush("#FEE2E2"), MakeBrush("#B91C1C")),
            _ => (MakeBrush("#E2E8F0"), MakeBrush("#334155"))
        };
    }

    public OrderDetailsDto Details { get; }

    public string Number => Details.OrderNumber;

    public string CustomerName => Details.CustomerName;

    public string DateText => UiFormat.ToLocal(Details.CreatedAt).ToString("dd/MM/yyyy");

    public string ExpectedDateText => Details.ExpectedDate?.ToString("dd/MM/yyyy") ?? "—";

    public string StatusLabel => OrderStatusLabels.ToFrench(Details.Status);

    public string AmountText => UiFormat.Money(Details.Total);

    public Brush Background { get; }

    public Brush Foreground { get; }

    private static SolidColorBrush MakeBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}

/// <summary>Contenu de la fenetre « detail client » : fiche + historique des commandes.</summary>
public sealed record CustomerDetailsModel(CustomerDto Customer, IReadOnlyList<OrderRow> Orders)
{
    public string CreatedText => UiFormat.ToLocal(Customer.CreatedAt).ToString("dd/MM/yyyy");

    public string AddressText => string.IsNullOrWhiteSpace(Customer.Address) ? "—" : Customer.Address!;

    public string NotesText => string.IsNullOrWhiteSpace(Customer.Notes) ? "—" : Customer.Notes!;

    public bool HasOrders => Orders.Count > 0;

    public bool HasNoOrders => Orders.Count == 0;
}
