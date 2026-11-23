namespace PressingManager.Core.Enums;

/// <summary>
/// Etat d'une session de caisse journaliere.
/// Une seule session peut etre ouverte a la fois.
/// </summary>
public enum CashSessionStatus
{
    /// <summary>Caisse actuellement ouverte (encaissements et depenses en especes autorises).</summary>
    Open = 0,

    /// <summary>Caisse cloturee (montant attendu et ecart calcules).</summary>
    Closed = 1
}
