using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Unit;

namespace PressingManager.Tests.Integration;

public sealed class ReportExportServiceTests : IDisposable
{
    private readonly ReportTestContext _ctx = new();
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pressing-test-{Guid.NewGuid():N}.csv");
    private readonly ReportExportService _export;

    public ReportExportServiceTests()
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.Zero));
        var audit = new AuditService(_ctx.Db, _ctx.Session, time);
        var utcPlusOne = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");

        _export = new ReportExportService(_ctx.Session, audit, time, utcPlusOne);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static OrderReportRow Row() =>
        new("CMD-001", new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc), "Awa Nkodo", OrderStatus.Delivered, 5000m, 3000m);

    [Fact]
    public async Task Export_WritesAnExcelFriendlyCsv_WithLocalTimes_AndAuditsIt()
    {
        await _export.ExportOrdersAsync(new[] { Row() }, _path, "Commandes de novembre");

        var bytes = await File.ReadAllBytesAsync(_path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // BOM UTF-8

        var text = await File.ReadAllTextAsync(_path);
        Assert.Contains("Numéro;Date;Client;Statut;Total;Payé;Solde", text);
        Assert.Contains("CMD-001;2026-11-05 10:00;Awa Nkodo;Livré;5000,00;3000,00;2000,00", text); // 9h UTC = 10h locale

        await using var db = _ctx.Db.CreateDbContext();
        var log = await db.AuditLogs.SingleAsync(a => a.Action == AuditActions.ReportExported);
        Assert.Contains("Commandes de novembre", log.Details);
        Assert.Contains("1 ligne", log.Details);
    }

    [Fact]
    public async Task ExportPdf_WritesAPdfFile_AndAuditsIt()
    {
        await _export.ExportOrdersPdfAsync(new[] { Row() }, _path, "Commandes de novembre");

        var bytes = await File.ReadAllBytesAsync(_path);
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        await using var db = _ctx.Db.CreateDbContext();
        var log = await db.AuditLogs.SingleAsync(a => a.Action == AuditActions.ReportExported);
        Assert.Contains("PDF", log.Details);
        Assert.Contains("Commandes de novembre", log.Details);
    }

    [Fact]
    public async Task ExportPdf_HandlesManyRowsOverSeveralPages()
    {
        var rows = Enumerable.Range(1, 120)
            .Select(i => new OrderReportRow($"CMD-{i:000}", new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc), "Awa Nkodo", OrderStatus.Cleaning, 1000m + i, 0m))
            .ToList();

        await _export.ExportOrdersPdfAsync(rows, _path, "Beaucoup de commandes");

        Assert.True(new FileInfo(_path).Length > 2000);
    }

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    [InlineData(UserRole.StockManager)]
    public async Task ExportPdf_RequiresTheExportReportsPermission_AndWritesNothing(UserRole role)
    {
        _ctx.SignInAs(role);

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _export.ExportOrdersPdfAsync(new[] { Row() }, _path, "Test"));

        Assert.False(File.Exists(_path));
    }

    [Fact]
    public async Task ExportPdf_WithNoRows_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _export.ExportOrdersPdfAsync(Array.Empty<OrderReportRow>(), _path, "Vide"));

        Assert.False(File.Exists(_path));
    }

    [Theory]
    [InlineData(UserRole.FrontDesk)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Processor)]
    [InlineData(UserRole.StockManager)]
    public async Task Export_RequiresTheExportReportsPermission_AndWritesNothing(UserRole role)
    {
        _ctx.SignInAs(role);

        await Assert.ThrowsAsync<PermissionDeniedException>(() =>
            _export.ExportOrdersAsync(new[] { Row() }, _path, "Test"));

        Assert.False(File.Exists(_path));
    }

    [Fact]
    public async Task Export_WithNoRows_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _export.ExportOrdersAsync(Array.Empty<OrderReportRow>(), _path, "Vide"));

        Assert.False(File.Exists(_path));
    }
}
