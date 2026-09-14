using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Interface.GameFonts;
using ToucanTally.Events;

namespace ToucanTally;

public enum AnimationStyle
{
    Straight,
    Parabola,
    Angled,
    Sprinkler,
    Static,
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}

public enum Outline
{
    None,
    Thin,
    Thick,
}

public enum TextAlign
{
    Left,
    Center,
    Right,
}

public enum IconSide
{
    Left,
    Right,
}

public enum NumberFormat
{
    Full,
    Short,
}

[Serializable]
public class MasterStyle
{
    public GameFontFamily Font { get; set; } = GameFontFamily.Jupiter;
    public int FontSize { get; set; } = 26;
    public Outline Outline { get; set; } = Outline.Thin;
}

[Serializable]
public class TextStyle
{
    public bool InheritFont { get; set; } = true;
    public GameFontFamily Font { get; set; } = GameFontFamily.Jupiter;
    public int FontSize { get; set; } = 26;
    public Outline Outline { get; set; } = Outline.Thin;
    public AnimationStyle Animation { get; set; } = AnimationStyle.Straight;
    public Direction Direction { get; set; } = Direction.Up;
    public bool Alternate { get; set; } = true;
    public float TravelSeconds { get; set; } = 3.0f;

    /// <summary>
    /// Only for the Static animation, which sticky text uses by default. The text starts this many times bigger and
    /// zooms down to normal size. 1 means no zoom.
    /// </summary>
    public float IntroScale { get; set; } = 1.0f;
}

[Serializable]
public class ScrollAreaConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Area";

    /// <summary>
    /// Top-left corner of the box as a fraction of the screen, where 0.5 is the middle. Stored this way so the boxes
    /// stay in place when the screen resolution changes.
    /// </summary>
    public float X { get; set; } = 0.5f;
    public float Y { get; set; } = 0.5f;

    public float Width { get; set; } = 320;
    public float Height { get; set; } = 400;
    public TextAlign Align { get; set; } = TextAlign.Left;
    public bool ShowIcons { get; set; } = true;
    public IconSide IconSide { get; set; } = IconSide.Left;
    public float IconAlpha { get; set; } = 1.0f;

    public TextStyle Normal { get; set; } = new();
    public TextStyle Sticky { get; set; } = new() { FontSize = 40, Outline = Outline.Thick, Animation = AnimationStyle.Static, TravelSeconds = 2.5f, IntroScale = 3.0f };
}

[Serializable]
public class EventSetting
{
    public bool Enabled { get; set; } = true;
    public Guid AreaId { get; set; }
    public Vector4 Color { get; set; } = new(1, 1, 1, 1);
    public int? FontSize { get; set; }
    public bool Sticky { get; set; }
    public float ThrottleSeconds { get; set; }
    public int MinAmount { get; set; }
}

[Serializable]
public class ModifierSetting
{
    public bool Enabled { get; set; } = true;
    public string Text { get; set; } = string.Empty;
    public Vector4 Color { get; set; } = new(1, 0.85f, 0.3f, 1);
    public bool Sticky { get; set; }
}

[Serializable]
public class SpellFilter
{
    public string Name { get; set; } = string.Empty;
    public bool Hide { get; set; }
    public int MinAmount { get; set; }
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool HideNative { get; set; } = true;
    public MasterStyle Master { get; set; } = new();
    public NumberFormat Numbers { get; set; } = NumberFormat.Full;
    public bool ColorNamesByRole { get; set; } = true;
    public Vector4 TankColor { get; set; } = new(0.35f, 0.55f, 1, 1); 
    public Vector4 HealerColor { get; set; } = new(0.4f, 0.9f, 0.45f, 1); 
    public Vector4 DpsColor { get; set; } = new(1, 0.4f, 0.4f, 1);
    public bool ShowNamesOnHeals { get; set; }
    public bool HideFullOverheals { get; set; } = true;
    public bool ShowOverhealAmount { get; set; }
    public bool ShortThrottleText { get; set; }
    public bool MergeAoe { get; set; } = true;
    public float AoeMergeSeconds { get; set; } = 0.3f;

    public List<ScrollAreaConfig> Areas { get; set; } = [];
    public Dictionary<EventCategory, EventSetting> Events { get; set; } = [];
    public Dictionary<Modifier, ModifierSetting> Modifiers { get; set; } = [];
    public List<SpellFilter> SpellFilters { get; set; } = [];

    public bool LogToDalamud { get; set; }
    public bool LogScreenLogKind { get; set; } = true;
    public bool LogScreenLogEntry { get; set; }
    public bool LogActionEffects { get; set; }
    public bool LogFlyTextCreated { get; set; }
    public int LogCapacity { get; set; } = 500;

    /// <summary>
    /// This seeds defaults to the configuration for the plogon, so that an empty launch...shows something.
    /// </summary>
    public void EnsureDefaults()
    {
        if (Areas.Count == 0)
        {
            Areas.Add(new ScrollAreaConfig { Name = "Outgoing", X = 0.60f, Y = 0.30f, Width = 360, Height = 420 });
            Areas.Add(new ScrollAreaConfig { Name = "Incoming", X = 0.20f, Y = 0.30f, Width = 360, Height = 420 });
            Areas.Add(new ScrollAreaConfig
            {
                Name = "Notifications",
                X = 0.38f,
                Y = 0.10f,
                Width = 420,
                Height = 160,
                Align = TextAlign.Center,
                Normal = new TextStyle { FontSize = 22, Direction = Direction.Down },
            });
        }

        var outgoing = Areas[0].Id;
        var incoming = Areas.Count > 1 ? Areas[1].Id : outgoing;
        var notifications = Areas.Count > 2 ? Areas[2].Id : outgoing;

        Ensure(EventCategory.OutgoingDamage, outgoing, new Vector4(1, 1, 1, 1));
        Ensure(EventCategory.OutgoingHeal, outgoing, new Vector4(0.45f, 1, 0.55f, 1));
        Ensure(EventCategory.OutgoingMiss, outgoing, new Vector4(0.7f, 0.7f, 0.7f, 1));
        Ensure(EventCategory.IncomingDamage, incoming, new Vector4(1, 0.35f, 0.35f, 1));
        Ensure(EventCategory.IncomingHeal, incoming, new Vector4(0.45f, 1, 0.55f, 1));
        Ensure(EventCategory.IncomingMiss, incoming, new Vector4(0.7f, 0.7f, 0.7f, 1));
        Ensure(EventCategory.MpGain, incoming, new Vector4(0.6f, 0.6f, 1, 1));
        Ensure(EventCategory.MpLoss, incoming, new Vector4(0.6f, 0.6f, 1, 1));
        Ensure(EventCategory.BuffGain, notifications, new Vector4(0.5f, 0.85f, 1, 1));
        Ensure(EventCategory.BuffFade, notifications, new Vector4(0.6f, 0.6f, 0.6f, 1));
        Ensure(EventCategory.DebuffGain, notifications, new Vector4(1, 0.5f, 0.9f, 1));
        Ensure(EventCategory.DebuffFade, notifications, new Vector4(0.6f, 0.6f, 0.6f, 1));
        Ensure(EventCategory.OwnCast, notifications, new Vector4(1, 0.95f, 0.7f, 1));
        Ensure(EventCategory.KillingBlow, notifications, new Vector4(1, 0.3f, 0.3f, 1), sticky: true);

        EnsureModifier(Modifier.Crit, "Crit", new Vector4(1, 0.85f, 0.3f, 1), sticky: true);
        EnsureModifier(Modifier.DirectHit, "DH", new Vector4(1, 1, 1, 1));
        EnsureModifier(Modifier.CritDirectHit, "Crit DH", new Vector4(1, 0.6f, 0.2f, 1), sticky: true);
        EnsureModifier(Modifier.Blocked, "Blocked", new Vector4(0.7f, 0.8f, 1, 1));
        EnsureModifier(Modifier.Parried, "Parried", new Vector4(0.7f, 0.8f, 1, 1));
        EnsureModifier(Modifier.Resisted, "Resisted", new Vector4(0.7f, 0.7f, 0.7f, 1));
        EnsureModifier(Modifier.Overheal, "Overheal", new Vector4(0.6f, 0.75f, 0.6f, 1));
    }

    private void Ensure(EventCategory category, Guid areaId, Vector4 color, bool sticky = false)
    {
        if (!Events.ContainsKey(category))
        {
            Events[category] = new EventSetting { AreaId = areaId, Color = color, Sticky = sticky };
        }
    }

    private void EnsureModifier(Modifier modifier, string text, Vector4 color, bool sticky = false)
    {
        if (!Modifiers.ContainsKey(modifier))
        {
            Modifiers[modifier] = new ModifierSetting { Text = text, Color = color, Sticky = sticky };
        }
    }
}
