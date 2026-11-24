namespace PressingManager.Tests.Integration.Support;

/// <summary>Horloge controlable pour tester les dates (tarifs, historique, mouvements).</summary>
public sealed class ProcessingTestClock : TimeProvider
{
    private DateTimeOffset _now;

    public ProcessingTestClock(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}
