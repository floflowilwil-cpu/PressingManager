namespace PressingManager.Core.Common;

public static class NumberGenerator
{
    // Numéro de commande, format : CMD-20261123-0001
    // "sequence" = numéro de la commande du jour (1, 2, 3...)
    public static string OrderNumber(DateTime date, int sequence)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "La séquence doit être supérieure ou égale à 1.");

        return $"CMD-{date:yyyyMMdd}-{sequence:D4}";
    }

    // Numéro client, format : CLI-0001
    public static string CustomerNumber(int sequence)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence), "La séquence doit être supérieure ou égale à 1.");

        return $"CLI-{sequence:D4}";
    }
}