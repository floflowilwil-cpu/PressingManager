using System.Text.Json;
using PressingManager.Core.DTOs;
using PressingManager.Core.Interfaces;

namespace PressingManager.Infrastructure.Services;

/// <summary>
/// Charge et sauvegarde les parametres d'integration (SMTP & Mobile Money) dans %LocalAppData%\PressingManager\integrations.json.
/// </summary>
public sealed class JsonIntegrationSettingsStore : IIntegrationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PressingManager",
        "integrations.json");

    private readonly string _filePath;
    private readonly object _gate = new();

    public JsonIntegrationSettingsStore() : this(DefaultFilePath)
    {
    }

    public JsonIntegrationSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public IntegrationSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
            {
                return new IntegrationSettings();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var parsed = JsonSerializer.Deserialize<IntegrationSettings>(json, JsonOptions);
                if (parsed is null)
                {
                    return new IntegrationSettings();
                }

                return new IntegrationSettings
                {
                    Email = parsed.Email ?? new EmailNotificationSettings(),
                    MobileMoney = parsed.MobileMoney ?? new MobileMoneySettings()
                };
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return new IntegrationSettings();
            }
        }
    }

    public void Save(IntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
