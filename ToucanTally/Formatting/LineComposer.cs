using System.Collections.Generic;
using System.Numerics;
using ToucanTally.Events;

namespace ToucanTally.Formatting;

internal readonly record struct Segment(string Text, Vector4 Color);

/// <summary>
/// This is the actual "UI element" that scrolls the screen and appears, icon, textsize and segments of text.
/// </summary>
internal sealed class ComposedLine
{
    public required List<Segment> Segments { get; init; }

    /// <summary>Smaller second line (used for healer name, if enabled)</summary>
    public Segment? SubLine { get; init; }

    public uint IconId { get; init; }
    public bool Sticky { get; init; }
    public int? FontSize { get; init; }
}

/// <summary>
/// Turns a combat event into the actual ComposedLine.
/// </summary>
internal static class LineComposer
{
    public static ComposedLine Compose(CombatEvent combatEvent, EventSetting setting, Configuration configuration)
    {
        var segments = new List<Segment>(5);
        var color = setting.Color;
        var sticky = setting.Sticky;
        ModifierSetting? modifier = null;

        if (combatEvent.Modifier is { } kind && configuration.Modifiers.TryGetValue(kind, out var candidate) && candidate.Enabled)
        {
            modifier = candidate;
            sticky |= candidate.Sticky;
        }

        if (combatEvent.Amount > 0)
        {
            var sign = combatEvent.Category switch
            {
                EventCategory.IncomingDamage or EventCategory.MpLoss => "-",
                EventCategory.IncomingHeal or EventCategory.OutgoingHeal or EventCategory.MpGain => "+",
                _ => string.Empty,
            };
            segments.Add(new Segment(sign + NumberFormatter.Format(combatEvent.Amount, configuration.Numbers), modifier?.Color ?? color));
        }

        if (combatEvent.Label.Length > 0)
        {
            segments.Add(new Segment(Lead(segments) + combatEvent.Label, color));
        }

        var overhealShownAsAmount = combatEvent.Modifier == Modifier.Overheal && configuration.ShowOverhealAmount;
        if (modifier != null && modifier.Text.Length > 0 && !overhealShownAsAmount)
        {
            segments.Add(new Segment(" " + modifier.Text, modifier.Color));
        }

        if (combatEvent.Overheal > 0 && configuration.ShowOverhealAmount)
        {
            segments.Add(new Segment($" ({NumberFormatter.Format(combatEvent.Overheal, configuration.Numbers)} over)", color));
        }

        if (combatEvent.Hits > 1)
        {
            var hits = configuration.ShortThrottleText ? $" {combatEvent.Hits}++" : $" ({combatEvent.Hits} hits)";
            segments.Add(new Segment(hits, color));
        }

        var isHeal = combatEvent.Category is EventCategory.IncomingHeal or EventCategory.OutgoingHeal;
        Segment? subLine = null;
        if (combatEvent.Actor.Length > 0 && (!isHeal || configuration.ShowNamesOnHeals))
        {
            var actorColor = configuration.ColorNamesByRole ? RoleColor(combatEvent.ActorRole, configuration, color) : color;
            subLine = new Segment(combatEvent.Actor, actorColor);
        }

        return new ComposedLine
        {
            Segments = segments,
            SubLine = subLine,
            IconId = combatEvent.IconId,
            Sticky = sticky,
            FontSize = setting.FontSize,
        };
    }

    private static string Lead(List<Segment> segments)
    {
        return segments.Count == 0 ? string.Empty : " ";
    }

    private static Vector4 RoleColor(Role role, Configuration configuration, Vector4 fallback)
    {
        return role switch
        {
            Role.Tank => configuration.TankColor,
            Role.Healer => configuration.HealerColor,
            Role.Dps => configuration.DpsColor,
            _ => fallback,
        };
    }
}
