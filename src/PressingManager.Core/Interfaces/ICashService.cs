using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Gestion des sessions de caisse (ouverture, suivi de la session courante et cloture avec ecart).
/// </summary>
public interface ICashService
{
    /// <summary>Ouvre une nouvelle session de caisse (une seule ouverte a la fois).</summary>
    Task<CashSessionDto> OpenAsync(
        OpenCashSessionDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Retourne la session de caisse actuellement ouverte, ou null si aucune.</summary>
    Task<CashSessionDto?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cloture la caisse en calculant le montant attendu et l'ecart par rapport au montant compte.
    /// </summary>
    Task<CashSessionDto> CloseAsync(
        CloseCashSessionDto request,
        CancellationToken cancellationToken = default);
}
