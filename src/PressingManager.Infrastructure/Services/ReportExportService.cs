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
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _timeZone;

    public ReportExportService(
        ICurrentUserContext session,
        IAuditService audit,
        TimeProvider? time = null,
        TimeZoneInfo? timeZone = null)
    {
        _session = session;
        _audit = audit;
        _time = time ?? TimeProvider.System;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task ExportOrdersAsync(
        IReadOnlyList<OrderReportRow> rows,
        string filePath,
        string description,
        CancellationToken cancellationToken = default)
    {
        CheckExportAllowed(rows, filePath);

        var csv = CsvExporter.OrdersToCsv(ToLocal(rows));

        // BOM UTF-8 : sans lui, Excel affiche mal les accents.
        await File.WriteAllTextAsync(filePath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);

        await AuditAsync("CSV", description, rows.Count, cancellationToken);
    }

    public async Task ExportOrdersPdfAsync(
        IReadOnlyList<OrderReportRow> rows,
        string filePath,
        string description,
        CancellationToken cancellationToken = default)
    {
        CheckExportAllowed(rows, filePath);

        var localRows = ToLocal(rows);
        var generatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(_time.GetUtcNow().UtcDateTime, _timeZone);

        // QuestPDF est synchrone : on le sort du thread de l'interface.
        await Task.Run(
            () => PdfReportBuilder.BuildOrdersReport(localRows, description, generatedAtLocal, filePath),
            cancellationToken);

        await AuditAsync("PDF", description, rows.Count, cancellationToken);
    }

    private void CheckExportAllowed(IReadOnlyList<OrderReportRow> rows, string filePath)
    {
        _session.Require(Permission.ExportReports);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (rows.Count == 0)
        {
            throw new BusinessRuleException("Aucune donnée à exporter pour cette sélection.");
        }
    }

    private List<OrderReportRow> ToLocal(IReadOnlyList<OrderReportRow> rows) =>
        rows.Select(r => r with { CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), _timeZone) })
            .ToList();

    private Task AuditAsync(string format, string description, int count, CancellationToken cancellationToken) =>
        _audit.LogAsync(
            AuditActions.ReportExported,
            "Report",
            details: $"{format} — {description} : {count} ligne(s)",
            cancellationToken: cancellationToken);
}
