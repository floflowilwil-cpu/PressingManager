using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

// Fiche client
public class Customer
{
    public int Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Un client peut avoir plusieurs commandes
    public List<Order> Orders { get; set; } = new();
}

// Commande
public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpectedDate { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Received;

    public decimal Discount { get; set; }   // remise
    public decimal Fees { get; set; }       // frais éventuels
    public string? Notes { get; set; }

    // Une commande contient plusieurs lignes
    public List<OrderItem> Items { get; set; } = new();

    // Sous-total = somme des lignes
    public decimal Subtotal => Items.Sum(i => i.LineTotal);

    // Total = sous-total - remise + frais
    public decimal Total => Subtotal - Discount + Fees;
}

// Ligne de commande (un article / une prestation)
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ServiceId { get; set; }       // lien vers le catalogue (personne 3)
    public string ItemType { get; set; } = string.Empty;  // ex : chemise, pantalon
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }   // prix appliqué, figé dans la commande
    public string? Observations { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}