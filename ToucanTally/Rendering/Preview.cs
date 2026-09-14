using System.Collections.Concurrent;
using ToucanTally.Events;

namespace ToucanTally.Rendering;

/// <summary>
/// "Preview sample text" on the General page. Sends a made-up hit, heal or buff to the boxes about twice a second,
/// so you can place and style the boxes without fighting anything.
/// </summary>
internal sealed class Preview
{
    private const float Interval = 0.45f;
    private const uint SampleActionIcon = 260;
    private const uint SampleStatusIcon = 210101;

    private static readonly CombatEvent[] Samples =
    [
        new() { Category = EventCategory.OutgoingDamage, Label = "Heavy Swing", Amount = 12345, IconId = SampleActionIcon },
        new() { Category = EventCategory.IncomingHeal, Label = "Cure II", Actor = "Healer Name", ActorRole = Role.Healer, Amount = 8210, IconId = SampleActionIcon },
        new() { Category = EventCategory.BuffGain, Label = "+ Sprint", IconId = SampleStatusIcon },
        new() { Category = EventCategory.OutgoingDamage, Label = "Overpower", Amount = 36186, IconId = SampleActionIcon, Modifier = Modifier.Crit },
        new() { Category = EventCategory.IncomingDamage, Label = "attack", Actor = "Skydeep Horror", Amount = 12338, IconId = SampleActionIcon },
        new() { Category = EventCategory.OwnCast, Label = "Bloodwhetting", IconId = SampleActionIcon },
        new() { Category = EventCategory.OutgoingHeal, Label = "Clemency", Actor = "Tank Name", ActorRole = Role.Tank, Amount = 5412, Overheal = 1200, IconId = SampleActionIcon },
        new() { Category = EventCategory.OutgoingDamage, Label = "Fell Cleave", Amount = 60210, IconId = SampleActionIcon, Modifier = Modifier.CritDirectHit },
        new() { Category = EventCategory.OutgoingMiss, Label = "Miss Tomahawk", IconId = SampleActionIcon },
        new() { Category = EventCategory.DebuffGain, Label = "+ Vulnerability Up", IconId = SampleStatusIcon },
        new() { Category = EventCategory.OutgoingDamage, Label = "Overpower", Amount = 9325, IconId = SampleActionIcon, Hits = 5 },
        new() { Category = EventCategory.KillingBlow, Label = "Killing blow", Actor = "Skydeep Horror", IconId = SampleActionIcon },
        new() { Category = EventCategory.BuffFade, Label = "- Sprint", IconId = SampleStatusIcon },
        new() { Category = EventCategory.MpGain, Label = "Lucid Dreaming", Amount = 550, IconId = SampleActionIcon },
    ];

    private float clock;
    private int index;

    public void Tick(float deltaSeconds, ConcurrentQueue<CombatEvent> queue)
    {
        clock += deltaSeconds;
        if (clock < Interval)
        {
            return;
        }

        clock = 0;
        queue.Enqueue(Samples[index]);
        index = (index + 1) % Samples.Length;
    }
}
