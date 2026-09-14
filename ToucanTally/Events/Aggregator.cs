using System.Collections.Generic;

namespace ToucanTally.Events;

/// <summary>
/// Merges repeated hits. Hits of the same skill that arrive within the merge time are held back and shown as one text
/// with the total, for example "18,210 Overpower (5 hits)". A merge time of zero shows every hit on its own.
/// </summary>
internal sealed class Aggregator
{
    private sealed class Batch
    {
        public required CombatEvent First { get; init; }
        public required float Window { get; init; }
        public uint Amount;
        public uint Overheal;
        public int Hits;
        public int Crits;
        public float Age;
    }

    private readonly Dictionary<(EventCategory, string), Batch> batches = new();
    private readonly List<(EventCategory, string)> expired = new();

    public void Add(CombatEvent combatEvent, float window, List<CombatEvent> output)
    {
        if (window <= 0)
        {
            output.Add(combatEvent);
            return;
        }

        var key = (combatEvent.Category, combatEvent.Label);
        if (!batches.TryGetValue(key, out var batch))
        {
            batch = new Batch { First = combatEvent, Window = window };
            batches[key] = batch;
        }

        batch.Amount += combatEvent.Amount;
        batch.Overheal += combatEvent.Overheal;
        batch.Hits++;
        if (combatEvent.IsCrit)
        {
            batch.Crits++;
        }
    }

    public void Tick(float deltaSeconds, List<CombatEvent> output)
    {
        expired.Clear();
        foreach (var (key, batch) in batches)
        {
            batch.Age += deltaSeconds;
            if (batch.Age >= batch.Window)
            {
                expired.Add(key);
            }
        }

        foreach (var key in expired)
        {
            var batch = batches[key];
            batches.Remove(key);
            output.Add(Merge(batch));
        }
    }

    private static CombatEvent Merge(Batch batch)
    {
        var first = batch.First;
        if (batch.Hits == 1)
        {
            return first;
        }

        var modifier = batch.Crits == batch.Hits ? first.Modifier : batch.Crits > 0 ? Modifier.Crit : null;
        return new CombatEvent
        {
            Category = first.Category,
            Label = first.Label,
            Actor = first.Actor,
            ActorRole = first.ActorRole,
            Amount = batch.Amount,
            Overheal = batch.Overheal,
            IconId = first.IconId,
            Modifier = modifier,
            Hits = batch.Hits,
            Crits = batch.Crits,
        };
    }
}
