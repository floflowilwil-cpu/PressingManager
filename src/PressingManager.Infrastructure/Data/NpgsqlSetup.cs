namespace PressingManager.Infrastructure.Data;

/// <summary>
/// Reglages Npgsql communs a l'application et aux outils EF (dotnet ef).
/// </summary>
public static class NpgsqlSetup
{
    /// <summary>
    /// Les dates du projet sont stockees en UTC mais lues/ecrites avec un Kind variable (Utc, Local, Unspecified),
    /// comme avec SQLite. Ce commutateur Npgsql garde ce comportement : DateTime est stocke en
    /// "timestamp without time zone" et n'est plus refuse selon son Kind. A appeler avant la premiere connexion.
    /// </summary>
    public static void Configure() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
}
