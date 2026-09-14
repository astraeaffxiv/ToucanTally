using System.Collections.Generic;

namespace ToucanTally.Events;

/// <summary>
/// The game's flying text only says how big a heal was, not how much of it was wasted (overheal). The server's action
/// result arrives a moment earlier, while the target's HP is still the old value. The overheal is worked out then,
/// stored here, and picked up again when the flying text for the same heal comes in.
/// </summary>
internal sealed class HealMemory
{
    private const int MaxEntries = 64;

    private readonly Dictionary<(uint Target, uint Action, uint Amount), uint> overheals = new();
    private readonly Queue<(uint Target, uint Action, uint Amount)> order = new();

    public void Remember(uint targetId, uint actionId, uint amount, uint overheal)
    {
        var key = (targetId, actionId, amount);
        if (!overheals.ContainsKey(key))
        {
            order.Enqueue(key);
        }

        overheals[key] = overheal;

        while (order.Count > MaxEntries)
        {
            overheals.Remove(order.Dequeue());
        }
    }

    public uint Take(uint targetId, uint actionId, uint amount)
    {
        var key = (targetId, actionId, amount);
        if (overheals.Remove(key, out var overheal))
        {
            return overheal;
        }

        return 0;
    }
}
