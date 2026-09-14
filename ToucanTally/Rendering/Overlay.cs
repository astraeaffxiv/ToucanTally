using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ToucanTally.Events;
using ToucanTally.Formatting;

namespace ToucanTally.Rendering;

/// <summary>
/// Draws all combat text on top of the game. It is an invisible window the size of the whole screen that mouse
/// clicks pass straight through. Every frame it takes the new combat events, drops the ones the settings hide,
/// merges repeated hits, and sends each event to the box it belongs to (Outgoing, Incoming, Notifications, or a
/// custuom box the player added). In move mode it also shows a window for every box that you can drag and resize.
/// </summary>
internal sealed class Overlay : IDisposable
{
    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

    private const ImGuiWindowFlags MoveBoxFlags =
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoNav;

    private readonly Plugin plugin;
    private readonly FontService fonts = new();
    private readonly ConcurrentQueue<CombatEvent> queue = new();
    private readonly Dictionary<Guid, ScrollAreaRuntime> runtimes = new();
    private readonly Aggregator aggregator = new();
    private readonly List<CombatEvent> ready = new();
    private readonly Preview preview = new();

    public bool MoveMode;
    public bool PreviewMode;

    public Overlay(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.PluginInterface.UiBuilder.Draw += Draw;
    }

    /// <summary>
    /// Called by the game hooks, on the game's own thread. The event waits in a queue and is picked up the next
    /// time a frame is drawn.
    /// </summary>
    public void Enqueue(CombatEvent combatEvent)
    {
        queue.Enqueue(combatEvent);
    }

    /// <summary>Removes all text on screen and everything still waiting. Used by the factory reset.</summary>
    public void Clear()
    {
        runtimes.Clear();
        queue.Clear();
    }

    public void Dispose()
    {
        Plugin.PluginInterface.UiBuilder.Draw -= Draw;
        fonts.Dispose();
    }

    /// <summary>
    /// Runs once per frame - this draws the "area".
    /// </summary>
    private void Draw()
    {
        var configuration = plugin.Configuration;
        var viewport = ImGuiHelpers.MainViewport;
        var deltaSeconds = ImGui.GetIO().DeltaTime;

        if (PreviewMode)
        {
            preview.Tick(deltaSeconds, queue);
        }

        Route(configuration, deltaSeconds);

        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(viewport.Size);
        if (ImGui.Begin("ToucanTally##overlay", OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            foreach (var area in configuration.Areas)
            {
                if (runtimes.TryGetValue(area.Id, out var runtime))
                {
                    runtime.Draw(area, configuration, fonts, drawList, viewport.Pos, viewport.Size, deltaSeconds);
                }
            }
        }

        ImGui.End();

        if (MoveMode)
        {
            DrawMoveBoxes(configuration, viewport.Pos, viewport.Size);
        }
    }

    /// <summary>
    /// Takes every event that came in since the last frame. Drops the ones that are switched off or filtered out,
    /// merges repeated hits of the same skill, and hands the rest to the box they belong to.
    /// </summary>
    private void Route(Configuration configuration, float deltaSeconds)
    {
        ready.Clear();
        while (queue.TryDequeue(out var combatEvent))
        {
            if (!configuration.Events.TryGetValue(combatEvent.Category, out var setting) || !setting.Enabled)
            {
                continue;
            }

            if (Filtered(combatEvent, setting, configuration))
            {
                continue;
            }

            // How long to wait for more hits of the same skill before showing them as one text, for example
            // "18,210 Overpower (5 hits)" for an AoE. Zero shows every hit on its own.
            var window = setting.ThrottleSeconds;
            if (configuration.MergeAoe && combatEvent.Category is EventCategory.OutgoingDamage or EventCategory.OutgoingHeal)
            {
                window = MathF.Max(window, configuration.AoeMergeSeconds);
            }

            aggregator.Add(combatEvent, window, ready);
        }

        aggregator.Tick(deltaSeconds, ready);

        foreach (var combatEvent in ready)
        {
            var setting = configuration.Events[combatEvent.Category];
            if (!runtimes.TryGetValue(setting.AreaId, out var runtime))
            {
                if (!configuration.Areas.Exists(a => a.Id == setting.AreaId))
                {
                    continue;
                }

                runtime = new ScrollAreaRuntime();
                runtimes[setting.AreaId] = runtime;
            }

            runtime.Add(LineComposer.Compose(combatEvent, setting, configuration));
        }
    }

    /// <summary>
    /// True when the settings say this event should not show: its number is below the minimum set on the Events
    /// page, it is a heal that was all overheal, or it is hidden on the Filters page.
    /// </summary>
    private static bool Filtered(CombatEvent combatEvent, EventSetting setting, Configuration configuration)
    {
        if (combatEvent.Amount > 0 && combatEvent.Amount < setting.MinAmount)
        {
            return true;
        }

        if (configuration.HideFullOverheals && combatEvent.Overheal > 0 && combatEvent.Overheal >= combatEvent.Amount)
        {
            return true;
        }

        var name = PlainName(combatEvent.Label);
        foreach (var filter in configuration.SpellFilters)
        {
            if (!string.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (filter.Hide || (combatEvent.Amount > 0 && combatEvent.Amount < filter.MinAmount))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] LabelPrefixes = ["+ ", "- ", "Miss ", "Dodge ", "Invulnerable ", "Resist "];

    /// <summary>
    /// Removes the "+ " or "- " in front of a buff name and the "Miss " in front of a missed skill, so the Filters
    /// page can match on the plain name.
    /// </summary>
    private static string PlainName(string label)
    {
        foreach (var prefix in LabelPrefixes)
        {
            if (label.StartsWith(prefix, StringComparison.Ordinal))
            {
                return label[prefix.Length..];
            }
        }

        return label;
    }

    /// <summary>
    /// Move mode: shows one normal window per box. Dragging or resizing that window moves or resizes the box, and
    /// the new position is saved. Positions are stored as a fraction of the screen size, so the layout stays the
    /// same after a resolution change.
    /// </summary>
    private void DrawMoveBoxes(Configuration configuration, Vector2 viewportPos, Vector2 viewportSize)
    {
        var changed = false;
        foreach (var area in configuration.Areas)
        {
            var titleHeight = ImGui.GetFrameHeight();
            ImGui.SetNextWindowPos(viewportPos + new Vector2(area.X * viewportSize.X, area.Y * viewportSize.Y - titleHeight), ImGuiCond.Appearing);
            ImGui.SetNextWindowSize(new Vector2(area.Width, area.Height + titleHeight), ImGuiCond.Appearing);
            ImGui.SetNextWindowBgAlpha(0.35f);
            if (ImGui.Begin($"{area.Name}##move{area.Id}", MoveBoxFlags))
            {
                ImGui.TextDisabled("Drag to move. Drag the corner to resize.");
                var pos = ImGui.GetWindowPos() + new Vector2(0, titleHeight);
                var size = ImGui.GetWindowSize() - new Vector2(0, titleHeight);
                var x = (pos.X - viewportPos.X) / viewportSize.X;
                var y = (pos.Y - viewportPos.Y) / viewportSize.Y;
                if (MathF.Abs(x - area.X) > 0.0005f || MathF.Abs(y - area.Y) > 0.0005f || MathF.Abs(size.X - area.Width) > 0.5f || MathF.Abs(size.Y - area.Height) > 0.5f)
                {
                    area.X = x;
                    area.Y = y;
                    area.Width = size.X;
                    area.Height = size.Y;
                    changed = true;
                }
            }

            ImGui.End();
        }

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }
}
