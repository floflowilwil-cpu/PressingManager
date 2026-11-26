using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>Passerelle d'encaissement Mobile Money (API Campay MTN/Orange ou mode simulation USSD).</summary>
public interface IMobileMoneyGateway
{
    bool IsLiveMode { get; }

    string ActiveProviderName { get; }

    string NormalizeCameroonPhone(string rawPhone);

    Task<MobileMoneyChargeResult> RequestPaymentAsync(
        MobileMoneyChargeRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
