using PressingManager.Core.DTOs;

namespace PressingManager.Core.Interfaces;

/// <summary>Persistance locale des parametres SMTP et Mobile Money (integrations.json).</summary>
public interface IIntegrationSettingsStore
{
    IntegrationSettings Load();

    void Save(IntegrationSettings settings);
}
