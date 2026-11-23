namespace PressingManager.Core.Common;

/// <summary>Conversion d'une période choisie à l'écran (dates locales) en intervalle UTC pour la base.</summary>
public static class ReportPeriod
{
    /// <summary>
    /// <paramref name="fromLocalDate"/> et <paramref name="toLocalDate"/> sont des jours calendaires locaux, bornes incluses.
    /// Retourne un intervalle UTC semi-ouvert [FromUtc, ToUtc[ : le dernier jour est donc compris en entier.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) ToUtcRange(
        DateTime fromLocalDate, DateTime toLocalDate, TimeZoneInfo timeZone)
    {
        if (toLocalDate.Date < fromLocalDate.Date)
        {
            throw new BusinessRuleException("La date de fin doit être postérieure ou égale à la date de début.");
        }

        var startLocal = DateTime.SpecifyKind(fromLocalDate.Date, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(toLocalDate.Date.AddDays(1), DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone));
    }
}
