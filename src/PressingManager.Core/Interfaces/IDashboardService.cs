using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>
/// Fournit les indicateurs du tableau de bord (Personne 1 les affiche).
/// Permission requise : ViewDashboard. L'implementation EF Core viendra avec ReportService ;
/// les calculs sont deja dans <see cref="Common.DashboardCalculator"/>.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
