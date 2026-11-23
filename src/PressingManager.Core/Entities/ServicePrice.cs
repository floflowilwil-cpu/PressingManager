using PressingManager.Core.Enums;

namespace PressingManager.Core.Entities;

/// <summary>
/// Tarif d'un service pour une unite de facturation.
/// Une ligne de commande copie le montant applique au moment de la commande :
/// modifier un tarif ne doit jamais changer les anciennes commandes.
/// </summary>
public class ServicePrice
{
    public int Id { get; set; }

    public int ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public BillingUnit Unit { get; set; }

    /// <summary>Montant en type decimal (jamais float/double pour l'argent).</summary>
    public decimal Amount { get; set; }

    /// <summary>Debut de validite du tarif.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Fin de validite ; null = tarif toujours en vigueur.</summary>
    public DateTime? ValidTo { get; set; }
}
