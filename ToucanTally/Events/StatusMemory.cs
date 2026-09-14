using System.Collections.Generic;

namespace ToucanTally.Events;

/// <summary>
/// Remembers the last status you put on each enemy, and the last status put on you. Damage-over-time and
/// heal-over-time ticks arrive without a skill name, so this is what lets a tick show as "1,571 Caustic Bite"
/// instead of a bare number.
/// </summary>
internal sealed class StatusMemory
{
    private readonly Dictionary<uint, uint> appliedByPlayer = new();
    private uint lastOnPlayer;

    public void RememberApplied(uint targetId, uint statusId)
    {
        appliedByPlayer[targetId] = statusId;
    }

    public void RememberOnPlayer(uint statusId)
    {
        lastOnPlayer = statusId;
    }

    public uint AppliedOn(uint targetId)
    {
        return appliedByPlayer.TryGetValue(targetId, out var statusId) ? statusId : 0;
    }

    public uint OnPlayer()
    {
        return lastOnPlayer;
    }
}
