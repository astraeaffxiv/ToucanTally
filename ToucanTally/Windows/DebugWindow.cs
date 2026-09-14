using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace ToucanTally.Windows;

internal sealed class DebugWindow : Window
{
    private readonly Plugin plugin;
    private string filter = string.Empty;
    private bool autoScroll = true;
    private int testValue = 1234;

    public DebugWindow(Plugin plugin)
        : base("Toucan Tally debug")
    {
        this.plugin = plugin;
        Size = new Vector2(900, 700);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawHooks();
        DrawLogging();
        DrawSettings();
        DrawTests();
        DrawLog();
    }

    private void DrawHooks()
    {
        if (!ImGui.CollapsingHeader("Hooks", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        var screenLog = plugin.ScreenLogHook;
        var effects = plugin.ActionEffectHook;

        var kindEnabled = screenLog.KindEnabled;
        if (ImGui.Checkbox($"AddToScreenLogWithScreenLogKind ({screenLog.KindHookState})", ref kindEnabled))
        {
            screenLog.SetKindEnabled(kindEnabled);
        }

        ImGui.TextUnformatted($"    calls={screenLog.KindCalls} swallowed={screenLog.KindSwallowed} events={screenLog.EventsBuilt}");

        var messageEnabled = screenLog.MessageEnabled;
        if (ImGui.Checkbox($"AddToScreenLogWithLogMessageId ({screenLog.MessageHookState})", ref messageEnabled))
        {
            screenLog.SetMessageEnabled(messageEnabled);
        }

        ImGui.TextUnformatted($"    calls={screenLog.MessageCalls}");

        var entryEnabled = screenLog.EntryEnabled;
        if (ImGui.Checkbox($"AddScreenLogEntry ({screenLog.EntryHookState})", ref entryEnabled))
        {
            screenLog.SetEntryEnabled(entryEnabled);
        }

        ImGui.TextUnformatted($"    calls={screenLog.EntryCalls} orphans={screenLog.EntryOrphans}");

        var effectsEnabled = effects.Enabled;
        if (ImGui.Checkbox($"ActionEffectHandler.Receive ({effects.HookState})", ref effectsEnabled))
        {
            effects.SetEnabled(effectsEnabled);
        }

        ImGui.TextUnformatted($"    calls={effects.Calls}");
        ImGui.TextUnformatted($"FlyTextCreated: calls={plugin.FlyTextHook.Calls}");
    }

    private void DrawLogging()
    {
        if (!ImGui.CollapsingHeader("Logging"))
        {
            return;
        }

        var configuration = plugin.Configuration;
        var changed = false;

        var enabled = plugin.EventLog.Enabled;
        if (ImGui.Checkbox("Enable event log (off at every load)", ref enabled))
        {
            plugin.EventLog.Enabled = enabled;
        }

        using var disabled = ImRaii.Disabled(!enabled);

        var toDalamud = configuration.LogToDalamud;
        changed |= ImGui.Checkbox("Mirror to Dalamud log (/xllog)", ref toDalamud);
        configuration.LogToDalamud = toDalamud;

        var kind = configuration.LogScreenLogKind;
        changed |= ImGui.Checkbox("Screen log entry points", ref kind);
        configuration.LogScreenLogKind = kind;

        var entry = configuration.LogScreenLogEntry;
        changed |= ImGui.Checkbox("Screen log queue entries", ref entry);
        configuration.LogScreenLogEntry = entry;

        var effects = configuration.LogActionEffects;
        changed |= ImGui.Checkbox("Action effect packets", ref effects);
        configuration.LogActionEffects = effects;

        var created = configuration.LogFlyTextCreated;
        changed |= ImGui.Checkbox("FlyTextCreated events", ref created);
        configuration.LogFlyTextCreated = created;

        if (changed)
        {
            plugin.SaveConfiguration();
        }
    }

    private void DrawSettings()
    {
        if (!ImGui.CollapsingHeader("Game settings that touch fly text"))
        {
            return;
        }

        if (ImGui.BeginTable("settings", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            foreach (var (name, value) in GameSettings.Current())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(name);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(value);
            }

            ImGui.EndTable();
        }
    }

    private void DrawTests()
    {
        if (!ImGui.CollapsingHeader("Tests"))
        {
            return;
        }

        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Value", ref testValue);

        if (ImGui.Button("Damage on self"))
        {
            plugin.ScreenLogHook.FireTestEvent(FlyTextKind.Damage, testValue);
        }

        ImGui.SameLine();
        if (ImGui.Button("Heal on self"))
        {
            plugin.ScreenLogHook.FireTestEvent(FlyTextKind.Healing, testValue);
        }

        ImGui.SameLine();
        if (ImGui.Button("Crit on self"))
        {
            plugin.ScreenLogHook.FireTestEvent(FlyTextKind.DamageCrit, testValue);
        }
    }

    private void DrawLog()
    {
        if (!ImGui.CollapsingHeader("Event log", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (ImGui.Button("Clear"))
        {
            plugin.EventLog.Clear();
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy all"))
        {
            ImGui.SetClipboardText(plugin.EventLog.Dump());
        }

        ImGui.SameLine();
        ImGui.Checkbox("Auto scroll", ref autoScroll);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Filter", ref filter, 128);

        if (ImGui.BeginChild("lines", new Vector2(0, 0), true))
        {
            foreach (var line in plugin.EventLog.Snapshot())
            {
                if (filter.Length > 0 && !line.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ImGui.TextUnformatted(line);
            }

            if (autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 5)
            {
                ImGui.SetScrollHereY(1.0f);
            }
        }

        ImGui.EndChild();
    }
}
