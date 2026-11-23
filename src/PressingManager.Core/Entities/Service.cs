namespace PressingManager.Core.Entities;

/// <summary>
/// Prestation proposee par le pressing : lavage, repassage, nettoyage a sec, etc.
/// </summary>
public class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Un service desactive n'est plus propose pour les nouvelles commandes.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ServicePrice> Prices { get; set; } = new List<ServicePrice>();
}
