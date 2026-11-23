namespace PressingManager.Core.Common;

/// <summary>Une regle metier est violee ; le message est destine a etre affiche a l'utilisateur.</summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

/// <summary>L'utilisateur courant n'a pas le droit d'effectuer l'action demandee.</summary>
public sealed class PermissionDeniedException : Exception
{
    public PermissionDeniedException(string message) : base(message)
    {
    }
}
