using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Export des rapports en CSV (s'ouvre dans Excel). Permission requise : ExportReports.
/// L'écran (Personne 1) choisit le chemin du fichier puis appelle ce service : le contrôle
/// de permission et la trace dans le journal d'audit sont faits ici, pas dans l'interface.
/// </summary>
public interface IReportExportService
{
    /// <param name="rows">Lignes issues de IReportService (dates en UTC : elles sont converties en heure locale).</param>
    /// <param name="filePath">Chemin complet du fichier .csv à écrire (écrasé s'il existe).</param>
    /// <param name="description">Texte court pour l'audit, ex. « Commandes 01/11/2026 - 30/11/2026 ».</param>
    Task ExportOrdersAsync(
        IReadOnlyList<OrderReportRow> rows,
        string filePath,
        string description,
        CancellationToken cancellationToken = default);
}
