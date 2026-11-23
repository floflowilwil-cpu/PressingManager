namespace PressingManager.Core.Enums;

/// <summary>
/// Modes de paiement acceptes par le pressing (paiements clients et depenses).
/// Les valeurs numeriques sont explicites car elles seront stockees en base :
/// ne jamais les renumeroter, ajouter uniquement a la fin.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Paiement en especes (lie a une session de caisse ouverte).</summary>
    Cash = 0,

    /// <summary>Paiement via MTN Mobile Money (reference de transaction obligatoire).</summary>
    MtnMobileMoney = 1,

    /// <summary>Paiement via Orange Money (reference de transaction obligatoire).</summary>
    OrangeMoney = 2,

    /// <summary>Autre moyen de paiement (virement, carte, etc.).</summary>
    Other = 3
}
