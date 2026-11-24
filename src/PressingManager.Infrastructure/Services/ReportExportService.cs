using System.Text;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

public sealed class ReportExportService : IReportExportService
{
    private readonly ICurrentUserContext _session;
    private readonly IAuditService _audit;
    private readonly TimeZoneInfo _timeZone;

    public ReportExportService(ICurrentUserContext session, IAuditService audit, TimeZoneInfo? timeZone = null)
    {
        _session = session;
        _audit = audit;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task ExportOrdersAsync(
        IReadOnlyList<OrderReportRow> rows,
        string filePath,
        string description,
        CancellationToken cancellationToken = default)
    {
        _session.Require(Permission.ExportReports);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (rows.Count == 0)
        {
            throw new BusinessRuleException("Aucune donnée à exporter pour cette sélection.");
        }

        var localRows = rows.Select(r => r with { CreatedAt = ToLocal(r.CreatedAt) });
        var csv = CsvExporter.OrdersToCsv(localRows);

        // BOM UTF-8 : sans lui, Excel affiche mal les accents.
        await File.WriteAllTextAsync(filePath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

        await _audit.LogAsync(
            AuditActions.ReportExported,
            "Report",
            details: $"{description} : {rows.Count} ligne(s)",
            cancellationToken: cancellationToken);
    }

    private DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
}
