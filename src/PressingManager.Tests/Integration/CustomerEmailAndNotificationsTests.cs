using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Infrastructure.Data.Seed;
using PressingManager.Infrastructure.Repositories;
using PressingManager.Infrastructure.Services;
using PressingManager.Tests.Integration.Support;

namespace PressingManager.Tests.Integration;

public sealed class CustomerEmailAndNotificationsTests : IDisposable
{
    private readonly ProcessingTestDb _db = new();
    private readonly ProcessingTestClock _clock = new(new DateTimeOffset(2026, 11, 25, 14, 0, 0, TimeSpan.Zero));
    private readonly string _tempDir;
    private readonly int _userId;

    public CustomerEmailAndNotificationsTests()
    {
        _userId = _db.AddUser();
        using var context = _db.CreateDbContext();
        ProcessingSeed.Seed(context, _clock);

        _tempDir = Path.Combine(Path.GetTempPath(), "pm-notif-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CustomerService_CreatesSearchesUpdatesEmail_AndRejectsInvalidEmail()
    {
        var session = ProcessingTestSession.SignedIn(UserRole.Administrator, _userId);
        var audit = new AuditService(_db, session, _clock);
        var customers = new CustomerService(_db, session, _clock);
        var catalog = new ServiceCatalogService(_db, session, audit, _clock);
        var orders = new OrderService(_db, session, _clock);
        var billing = new EfOrderBillingInfoProvider(_db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            customers.CreateAsync(new CreateCustomerDto("John Test", "650784369", Email: "email-invalide")));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            customers.CreateAsync(new CreateCustomerDto("John Test", "650784369", Email: "john@nodot")));

        var created = await customers.CreateAsync(
            new CreateCustomerDto("John Test", "650784369", Email: "  john.client@pressing.cm  "));

        Assert.Equal("john.client@pressing.cm", created.Email);

        var byEmail = await customers.SearchAsync("john.client@pressing.cm");
        Assert.Single(byEmail);

        var updated = await customers.UpdateAsync(
            new UpdateCustomerDto(created.Id, "John Test", "650784369", Email: "john.updated@pressing.cm"));
        Assert.Equal("john.updated@pressing.cm", updated.Email);

        var services = await catalog.GetServicesAsync();
        var order = await orders.CreateAsync(new CreateOrderDto(
            updated.Id,
            [new CreateOrderItemDto(services[0].Id, "Chemise", 2, 500m)],
            ExpectedDate: _clock.GetLocalNow().Date.AddDays(2)));

        Assert.Equal("650784369", order.CustomerPhone);
        Assert.Equal("john.updated@pressing.cm", order.CustomerEmail);

        var billingInfo = await billing.GetOrderBillingInfoAsync(order.Id);
        Assert.NotNull(billingInfo);
        Assert.Equal("650784369", billingInfo.CustomerPhone);
        Assert.Equal("john.updated@pressing.cm", billingInfo.CustomerEmail);
    }

    [Theory]
    [InlineData("650784369", "237650784369")]
    [InlineData("+237 650-78-43-69", "237650784369")]
    [InlineData("00237690112233", "237690112233")]
    public void CampayGateway_NormalizesCameroonNumbers(string input, string expected)
    {
        var store = new JsonIntegrationSettingsStore(Path.Combine(_tempDir, "settings.json"));
        var gateway = new CampayMobileMoneyGateway(store);

        Assert.Equal(expected, gateway.NormalizeCameroonPhone(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("237222112233")]
    public void CampayGateway_RejectsInvalidPhoneNumbers(string input)
    {
        var store = new JsonIntegrationSettingsStore(Path.Combine(_tempDir, "settings.json"));
        var gateway = new CampayMobileMoneyGateway(store);

        Assert.Throws<BusinessRuleException>(() => gateway.NormalizeCameroonPhone(input));
    }

    [Fact]
    public async Task CampayGateway_SimulationMode_ProducesValidOperatorReference()
    {
        var settingsPath = Path.Combine(_tempDir, "sim-settings.json");
        var store = new JsonIntegrationSettingsStore(settingsPath);
        var gateway = new CampayMobileMoneyGateway(store);

        Assert.False(gateway.IsLiveMode);

        var mtnResult = await gateway.RequestPaymentAsync(new MobileMoneyChargeRequest(
            PaymentMethod.MtnMobileMoney,
            "650784369",
            2500m,
            "CMD-20261125-0001",
            "Test MTN"));

        Assert.True(mtnResult.Success);
        Assert.True(mtnResult.IsSimulated);
        Assert.StartsWith("MTN-SIM-", mtnResult.OperatorReference);

        var omResult = await gateway.RequestPaymentAsync(new MobileMoneyChargeRequest(
            PaymentMethod.OrangeMoney,
            "690112233",
            3000m,
            "CMD-20261125-0002",
            "Test OM"));

        Assert.True(omResult.Success);
        Assert.True(omResult.IsSimulated);
        Assert.StartsWith("OM-SIM-", omResult.OperatorReference);
    }

    [Fact]
    public async Task EmailNotificationService_SavesReceiptPdfAndStatusEmailsLocallyWhenSmtpDisabled()
    {
        var store = new JsonIntegrationSettingsStore(Path.Combine(_tempDir, "mail-settings.json"));
        var outboxDir = Path.Combine(_tempDir, "outbox");
        var emailService = new SmtpEmailNotificationService(store, outboxDir);

        var payment = new PaymentDto(
            Id: 42,
            OrderId: 10,
            Amount: 5000m,
            PaidAt: _clock.GetUtcNow().UtcDateTime,
            Method: PaymentMethod.MtnMobileMoney,
            Reference: "MTN-SIM-123456",
            Status: PaymentStatus.Valid,
            ReceivedByUserId: _userId,
            CashSessionId: null,
            CancelReason: null,
            CancelledByUserId: null,
            CancelledAt: null);

        var receipt = PaymentReceiptGenerator.BuildReceipt(
            payment,
            "CMD-20261125-0010",
            "John Client",
            5000m,
            5000m,
            "Caissier");

        var pdfBytes = PaymentReceiptPdfBuilder.BuildReceiptPdfBytes(receipt, isEnglish: false);
        var receiptDelivery = await emailService.SendPaymentReceiptAsync(
            "john.client@pressing.cm",
            receipt,
            pdfBytes,
            isEnglish: false);

        Assert.False(receiptDelivery.SentViaSmtp);
        Assert.True(receiptDelivery.SavedLocally);
        Assert.NotNull(receiptDelivery.LocalFilePath);
        Assert.True(File.Exists(receiptDelivery.LocalFilePath));

        var receiptEml = await File.ReadAllTextAsync(receiptDelivery.LocalFilePath);
        Assert.Contains("REC-20261125-0042.pdf", receiptEml);
        Assert.Contains("application/pdf", receiptEml);

        var statusDelivery = await emailService.SendOrderStatusChangedAsync(
            "john.client@pressing.cm",
            new OrderStatusEmailDto(
                "CMD-20261125-0010",
                "John Client",
                OrderStatus.Cleaning,
                OrderStatus.ReadyForPickup,
                _clock.GetLocalNow().Date.AddDays(1),
                "Prêt au comptoir",
                ["2 × Chemise (1 000 FCFA)"]),
            isEnglish: true);

        Assert.True(statusDelivery.SavedLocally);
        Assert.NotNull(statusDelivery.LocalFilePath);
        Assert.True(File.Exists(statusDelivery.LocalFilePath));

        var statusEml = await File.ReadAllTextAsync(statusDelivery.LocalFilePath);
        Assert.Contains("CMD-20261125-0010", statusEml);
        Assert.Contains("Ready for pickup", statusEml);
    }

    [Fact]
    public async Task EmailNotificationService_UsesTimeOfDayGreetingWithCustomerName()
    {
        Assert.Equal("Bonjour Marie,", SmtpEmailNotificationService.GetGreeting(false, new DateTime(2026, 11, 26, 5, 0, 0), "Marie"));
        Assert.Equal("Bonjour Marie,", SmtpEmailNotificationService.GetGreeting(false, new DateTime(2026, 11, 26, 17, 59, 0), "Marie"));
        Assert.Equal("Bonsoir Marie,", SmtpEmailNotificationService.GetGreeting(false, new DateTime(2026, 11, 26, 18, 0, 0), "Marie"));
        Assert.Equal("Bonsoir Marie,", SmtpEmailNotificationService.GetGreeting(false, new DateTime(2026, 11, 26, 2, 0, 0), "Marie"));
        Assert.Equal("Bonsoir,", SmtpEmailNotificationService.GetGreeting(false, new DateTime(2026, 11, 26, 2, 0, 0)));

        Assert.Equal("Good morning John,", SmtpEmailNotificationService.GetGreeting(true, new DateTime(2026, 11, 26, 9, 0, 0), "John"));
        Assert.Equal("Good afternoon John,", SmtpEmailNotificationService.GetGreeting(true, new DateTime(2026, 11, 26, 15, 0, 0), "John"));
        Assert.Equal("Good evening John,", SmtpEmailNotificationService.GetGreeting(true, new DateTime(2026, 11, 26, 20, 0, 0), "John"));

        var store = new JsonIntegrationSettingsStore(Path.Combine(_tempDir, "greeting-settings.json"));
        var outboxDir = Path.Combine(_tempDir, "greeting-outbox");
        var eveningService = new SmtpEmailNotificationService(store, outboxDir, () => new DateTime(2026, 11, 26, 20, 0, 0));

        var payment = new PaymentDto(
            Id: 43,
            OrderId: 11,
            Amount: 2000m,
            PaidAt: _clock.GetUtcNow().UtcDateTime,
            Method: PaymentMethod.MtnMobileMoney,
            Reference: "MTN-SIM-654321",
            Status: PaymentStatus.Valid,
            ReceivedByUserId: _userId,
            CashSessionId: null,
            CancelReason: null,
            CancelledByUserId: null,
            CancelledAt: null);

        var receipt = PaymentReceiptGenerator.BuildReceipt(
            payment,
            "CMD-20261126-0011",
            "Marie",
            2000m,
            2000m,
            "Caissier");

        var pdfBytes = PaymentReceiptPdfBuilder.BuildReceiptPdfBytes(receipt, isEnglish: false);
        var receiptResult = await eveningService.SendPaymentReceiptAsync("marie@pressing.cm", receipt, pdfBytes, isEnglish: false);
        var eveningReceiptHtml = DecodeHtmlBodyFromEml(await File.ReadAllTextAsync(receiptResult.LocalFilePath!));

        Assert.Contains("Bonsoir Marie,", eveningReceiptHtml);

        var statusResult = await eveningService.SendOrderStatusChangedAsync(
            "marie@pressing.cm",
            new OrderStatusEmailDto(
                "CMD-20261126-0011",
                "Marie",
                OrderStatus.Received,
                OrderStatus.Cleaning,
                null,
                null,
                ["1 × Chemise (500 FCFA)"]),
            isEnglish: false);
        var eveningStatusHtml = DecodeHtmlBodyFromEml(await File.ReadAllTextAsync(statusResult.LocalFilePath!));

        Assert.Contains("Bonsoir Marie,", eveningStatusHtml);

        var morningService = new SmtpEmailNotificationService(store, outboxDir, () => new DateTime(2026, 11, 26, 10, 0, 0));
        var morningStatusResult = await morningService.SendOrderStatusChangedAsync(
            "john@pressing.cm",
            new OrderStatusEmailDto(
                "CMD-20261126-0011",
                "John",
                OrderStatus.Cleaning,
                OrderStatus.ReadyForPickup,
                null,
                null,
                ["1 × Chemise (500 FCFA)"]),
            isEnglish: false);
        var morningStatusHtml = DecodeHtmlBodyFromEml(await File.ReadAllTextAsync(morningStatusResult.LocalFilePath!));

        Assert.Contains("Bonjour John,", morningStatusHtml);
    }

    private static string DecodeHtmlBodyFromEml(string emlContent)
    {
        var lines = emlContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var inHtmlHeaders = false;
        var inHtmlBody = false;
        var isBase64 = false;
        var bodyLines = new List<string>();

        foreach (var line in lines)
        {
            if (!inHtmlHeaders && !inHtmlBody)
            {
                if (line.StartsWith("Content-Type: text/html", StringComparison.OrdinalIgnoreCase))
                {
                    inHtmlHeaders = true;
                }

                continue;
            }

            if (inHtmlHeaders)
            {
                if (line.StartsWith("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
                {
                    isBase64 = true;
                }
                else if (line.Length == 0)
                {
                    inHtmlHeaders = false;
                    inHtmlBody = true;
                }

                continue;
            }

            if (line.StartsWith("--", StringComparison.Ordinal) && bodyLines.Count > 0)
            {
                break;
            }

            bodyLines.Add(line);
        }

        if (isBase64)
        {
            var compact = string.Concat(bodyLines.Select(l => l.Trim()));
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(compact));
        }

        return string.Join('\n', bodyLines).Trim();
    }
}
