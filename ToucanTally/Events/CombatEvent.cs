namespace ToucanTally.Events;

/// <summary>
/// This is the event class for received combat events.
/// This is built on the game thread, consumed on the UI thread, never mutated.
/// </summary>
internal sealed class CombatEvent
{
    public required EventCategory Category { get; init; }

    /// <summary>Action or status name. Empty for lines that are only an amount.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The other actor's name: 
    /// - the healer on incoming heals;
    /// - the enemy on incoming damage;
    /// - the target on outgoings heals.
    /// </summary>
    public string Actor { get; init; } = string.Empty;

    public Role ActorRole { get; init; }
    public uint Amount { get; init; }
    public uint Overheal { get; init; }
    public uint IconId { get; init; }
    public Modifier? Modifier { get; init; }

    /// <summary>
    /// How many hits were merged into this one text, for example 5 for "(5 hits)". 1 when nothing was merged.
    /// </summary>
    public int Hits { get; init; } = 1;
    public int Crits { get; init; }

    public bool IsCrit => Modifier is Events.Modifier.Crit or Events.Modifier.CritDirectHit;
}
