using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PressingManager.Core.Common;
using PressingManager.Core.DTOs;
using PressingManager.Core.Enums;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Passerelle Mobile Money MTN / Orange : utilise l'API Campay lorsque configuree (Provider = "Campay")
/// ou le mode Simulation interactif par defaut pour les tests locaux sans cle marchand.
/// </summary>
public sealed class CampayMobileMoneyGateway : IMobileMoneyGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IIntegrationSettingsStore _settingsStore;
    private readonly HttpClient _httpClient;

    [ActivatorUtilitiesConstructor]
    public CampayMobileMoneyGateway(IIntegrationSettingsStore settingsStore)
        : this(settingsStore, new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
    {
    }

    public CampayMobileMoneyGateway(IIntegrationSettingsStore settingsStore, HttpClient httpClient)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public bool IsLiveMode
    {
        get
        {
            var cfg = _settingsStore.Load().MobileMoney;
            return string.Equals(cfg.Provider?.Trim(), "Campay", StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(cfg.ApiToken)
                    || (!string.IsNullOrWhiteSpace(cfg.Username) && !string.IsNullOrWhiteSpace(cfg.Password)));
        }
    }

    public string ActiveProviderName => IsLiveMode ? "Campay" : "Simulation";

    public string NormalizeCameroonPhone(string rawPhone)
    {
        var input = (rawPhone ?? string.Empty).Trim();
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("00237", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length == 9 && digits.StartsWith('6'))
        {
            digits = "237" + digits;
        }

        if (digits.Length != 12 || !digits.StartsWith("2376", StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Numéro Mobile Money invalide (format camerounais attendu : 6XXXXXXXX ou +2376XXXXXXXX).");
        }

        return digits;
    }

    public async Task<MobileMoneyChargeResult> RequestPaymentAsync(
        MobileMoneyChargeRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Method is not (PaymentMethod.MtnMobileMoney or PaymentMethod.OrangeMoney))
        {
            throw new BusinessRuleException("Le paiement USSD s'applique uniquement à MTN Mobile Money ou Orange Money.");
        }

        if (request.Amount <= 0m)
        {
            throw new BusinessRuleException("Le montant à encaisser doit être strictement positif.");
        }

        var normalizedPhone = NormalizeCameroonPhone(request.PhoneNumber);
        var cfg = _settingsStore.Load().MobileMoney;

        if (!IsLiveMode)
        {
            progress?.Report($"Demande USSD envoyée sur +{normalizedPhone}...");
            await Task.Delay(150, cancellationToken);

            var prefix = request.Method == PaymentMethod.MtnMobileMoney ? "MTN" : "OM";
            var simulatedRef = $"{prefix}-SIM-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
            return new MobileMoneyChargeResult(
                Success: true,
                OperatorReference: simulatedRef,
                IsSimulated: true,
                StatusMessage: $"Paiement simulé confirmé sur +{normalizedPhone}");
        }

        var baseUrl = (string.IsNullOrWhiteSpace(cfg.BaseUrl) ? "https://demo.campay.net/api" : cfg.BaseUrl).Trim().TrimEnd('/');

        try
        {
            progress?.Report("Connexion à la passerelle Campay...");
            var token = await ResolveTokenAsync(baseUrl, cfg, cancellationToken);

            var amountXaf = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture);

            var collectPayload = new
            {
                amount = amountXaf,
                currency = "XAF",
                from = normalizedPhone,
                description = string.IsNullOrWhiteSpace(request.Description)
                    ? $"Paiement commande {request.OrderNumber}"
                    : request.Description,
                external_reference = $"{request.OrderNumber}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
            };

            using var collectReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/collect/");
            collectReq.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
            collectReq.Content = JsonContent.Create(collectPayload);

            using var collectResp = await _httpClient.SendAsync(collectReq, cancellationToken);
            var collectJson = await collectResp.Content.ReadAsStringAsync(cancellationToken);

            if (!collectResp.IsSuccessStatusCode)
            {
                return new MobileMoneyChargeResult(
                    Success: false,
                    OperatorReference: null,
                    IsSimulated: false,
                    StatusMessage: $"Campay a refusé la demande ({(int)collectResp.StatusCode}) : {collectJson}");
            }

            using var collectDoc = JsonDocument.Parse(collectJson);
            var reference = collectDoc.RootElement.TryGetProperty("reference", out var refProp)
                ? refProp.GetString()
                : null;
            var ussdCode = collectDoc.RootElement.TryGetProperty("ussd_code", out var ussdProp)
                ? ussdProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(reference))
            {
                return new MobileMoneyChargeResult(
                    Success: false,
                    OperatorReference: null,
                    IsSimulated: false,
                    StatusMessage: "Réponse Campay invalide (référence absente).");
            }

            var promptHint = string.IsNullOrWhiteSpace(ussdCode)
                ? $"Demande USSD envoyée sur +{normalizedPhone}. Validez sur le téléphone..."
                : $"Demande USSD ({ussdCode}) envoyée sur +{normalizedPhone}. Validez sur le téléphone...";
            progress?.Report(promptHint);

            var timeoutSeconds = Math.Clamp(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 90, 15, 300);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/transaction/{reference}/");
                statusReq.Headers.Authorization = new AuthenticationHeaderValue("Token", token);

                using var statusResp = await _httpClient.SendAsync(statusReq, cancellationToken);
                var statusJson = await statusResp.Content.ReadAsStringAsync(cancellationToken);
                if (!statusResp.IsSuccessStatusCode)
                {
                    continue;
                }

                using var statusDoc = JsonDocument.Parse(statusJson);
                var status = statusDoc.RootElement.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()?.ToUpperInvariant()
                    : null;

                if (status == "SUCCESSFUL")
                {
                    var opRef = statusDoc.RootElement.TryGetProperty("operator_reference", out var opProp)
                        ? opProp.GetString()
                        : null;

                    return new MobileMoneyChargeResult(
                        Success: true,
                        OperatorReference: string.IsNullOrWhiteSpace(opRef) ? reference : opRef,
                        IsSimulated: false,
                        StatusMessage: $"Paiement confirmé sur +{normalizedPhone}");
                }

                if (status is "FAILED" or "CANCELLED")
                {
                    var reason = statusDoc.RootElement.TryGetProperty("reason", out var reasonProp)
                        ? reasonProp.GetString()
                        : null;

                    return new MobileMoneyChargeResult(
                        Success: false,
                        OperatorReference: null,
                        IsSimulated: false,
                        StatusMessage: string.IsNullOrWhiteSpace(reason)
                            ? "Le client a refusé ou annulé le paiement sur son téléphone."
                            : reason);
                }
            }

            return new MobileMoneyChargeResult(
                Success: false,
                OperatorReference: null,
                IsSimulated: false,
                StatusMessage: "Délai d'attente dépassé : aucune confirmation reçue depuis le téléphone.");
        }
        catch (OperationCanceledException)
        {
            return new MobileMoneyChargeResult(
                Success: false,
                OperatorReference: null,
                IsSimulated: false,
                StatusMessage: "Demande de paiement annulée.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return new MobileMoneyChargeResult(
                Success: false,
                OperatorReference: null,
                IsSimulated: false,
                StatusMessage: $"Erreur réseau passerelle Mobile Money : {ex.Message}");
        }
    }

    private async Task<string> ResolveTokenAsync(
        string baseUrl,
        MobileMoneySettings cfg,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ApiToken))
        {
            return cfg.ApiToken.Trim();
        }

        using var resp = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/token/",
            new { username = cfg.Username.Trim(), password = cfg.Password ?? string.Empty },
            JsonOptions,
            cancellationToken);

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.TryGetProperty("token", out var tokenProp)
            ? tokenProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Jeton Campay introuvable dans la réponse /token/.");
        }

        return token;
    }
}
