using System;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ToucanTally.Hooks;

/// <summary>
/// Catches the game's calls that put flying text (the numbers over characters) on screen.
///
/// When something is hit or healed, the game calls AddToScreenLogWithScreenLogKind with who did it, who it happened
/// to, which skill it was, and the number. The Toucan sees that call and builds its own combat event from it.
/// If "Hide the game's flying text" is on, the plugin then stops the call, so the game's own text never appears.
///
/// Most attacks first go through a second function, AddToScreenLogWithLogMessageId, which then calls the one
/// above. The plugin only logs that one and always lets it through, so every hit still ends up in the first function.
///
/// AddScreenLogEntry is the very last step before text is queued for the screen. Crafting numbers only pass
/// through this one. The plugin logs it and always lets it through.
/// </summary>
internal sealed unsafe class ScreenLogHook : IDisposable
{
    private delegate void AddToScreenLogWithScreenLogKindDelegate(
        BattleChara* target,
        BattleChara* source,
        int screenLogKind,
        byte option,
        byte actionKind,
        uint actionId,
        int value1,
        int value2,
        int value3);

    private delegate void AddToScreenLogWithLogMessageIdDelegate(
        BattleChara* target,
        BattleChara* source,
        int logMessageId,
        byte actionKind,
        uint actionId,
        int value1,
        int value2,
        int value3);

    private delegate void* AddScreenLogEntryDelegate(void* queue, ScreenLogEntry* entry);

    private readonly Plugin plugin;
    private readonly Hook<AddToScreenLogWithScreenLogKindDelegate>? kindHook;
    private readonly Hook<AddToScreenLogWithLogMessageIdDelegate>? messageHook;
    private readonly Hook<AddScreenLogEntryDelegate>? entryHook;

    /// <summary>
    /// True while one of the two functions above is running. When AddScreenLogEntry is called while this is false,
    /// the text came from somewhere else, such as crafting. The debug window counts those as orphans.
    /// </summary>
    [ThreadStatic]
    private static bool insideEntryPoint;

    public string KindHookState { get; private set; } = "not created";
    public string MessageHookState { get; private set; } = "not created";
    public string EntryHookState { get; private set; } = "not created";

    // Counters shown in the debug window.
    public long KindCalls;
    public long KindSwallowed;
    public long MessageCalls;
    public long EntryCalls;
    public long EntryOrphans;
    public long EventsBuilt;

    /// <summary>
    /// The function addresses come from the FFXIVClientStructs library, so we luckily don't need custom signatures. 
    /// An address of zero means that FFXIVClientStructs does not know the function on this game version (yet). The hook is then
    /// skipped and the debug window shows it as unresolved, helpful when Dalamud updates and clientstructs are still WIP.
    /// </summary>
    public ScreenLogHook(Plugin plugin)
    {
        this.plugin = plugin;

        var kindAddress = BattleLog.Addresses.AddToScreenLogWithScreenLogKind.Value;
        if (kindAddress != 0)
        {
            kindHook = Plugin.GameInteropProvider.HookFromAddress<AddToScreenLogWithScreenLogKindDelegate>(kindAddress, AddToScreenLogWithScreenLogKindDetour);
            KindHookState = $"created at {kindAddress:X}";
        }
        else
        {
            KindHookState = "address unresolved";
            Plugin.Log.Error("BattleLog.AddToScreenLogWithScreenLogKind address is zero.");
        }

        var messageAddress = BattleLog.Addresses.AddToScreenLogWithLogMessageId.Value;
        if (messageAddress != 0)
        {
            messageHook = Plugin.GameInteropProvider.HookFromAddress<AddToScreenLogWithLogMessageIdDelegate>(messageAddress, AddToScreenLogWithLogMessageIdDetour);
            MessageHookState = $"created at {messageAddress:X}";
        }
        else
        {
            MessageHookState = "address unresolved";
            Plugin.Log.Error("BattleLog.AddToScreenLogWithLogMessageId address is zero.");
        }

        var entryAddress = ScreenLog.Addresses.AddScreenLogEntry.Value;
        if (entryAddress != 0)
        {
            entryHook = Plugin.GameInteropProvider.HookFromAddress<AddScreenLogEntryDelegate>(entryAddress, AddScreenLogEntryDetour);
            EntryHookState = $"created at {entryAddress:X}";
        }
        else
        {
            EntryHookState = "address unresolved";
            Plugin.Log.Error("ScreenLog.AddScreenLogEntry address is zero.");
        }

        kindHook?.Enable();
        messageHook?.Enable();
        entryHook?.Enable();
    }

    public bool KindEnabled => kindHook?.IsEnabled ?? false;
    public bool MessageEnabled => messageHook?.IsEnabled ?? false;
    public bool EntryEnabled => entryHook?.IsEnabled ?? false;

    public void SetKindEnabled(bool enabled) => Toggle(kindHook, enabled);
    public void SetMessageEnabled(bool enabled) => Toggle(messageHook, enabled);
    public void SetEntryEnabled(bool enabled) => Toggle(entryHook, enabled);

    /// <summary>
    /// Test button in the debug window. Pretends you hit yourself for the given number and sends it through this
    /// plugin's own code, the same way a real hit from the game would arrive.
    /// </summary>
    public void FireTestEvent(FlyTextKind kind, int value)
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null || kindHook == null)
        {
            plugin.EventLog.Add("Test", "No local player or no hook, cannot fire.");
            return;
        }

        var self = (BattleChara*)localPlayer.Address;
        const byte defaultOption = 5;
        const uint testActionId = 2555;
        AddToScreenLogWithScreenLogKindDetour(self, self, (int)kind, defaultOption, (byte)ActionType.Action, testActionId, value, 0, 1);
    }

    public void Dispose()
    {
        kindHook?.Dispose();
        messageHook?.Dispose();
        entryHook?.Dispose();
    }

    private static void Toggle<T>(Hook<T>? hook, bool enabled)
        where T : Delegate
    {
        if (hook == null)
        {
            return;
        }

        if (enabled)
        {
            hook.Enable();
        }
        else
        {
            hook.Disable();
        }
    }

    /// <summary>
    /// Every hit, heal, miss and buff passes through this function. It builds this plugin's combat event, then either
    /// stops the game's call, which hides the game's own text, or passes it on unchanged.
    /// </summary>
    private void AddToScreenLogWithScreenLogKindDetour(
        BattleChara* target,
        BattleChara* source,
        int screenLogKind,
        byte option,
        byte actionKind,
        uint actionId,
        int value1,
        int value2,
        int value3)
    {
        KindCalls++;
        var swallow = false;
        try
        {
            var kind = (FlyTextKind)screenLogKind;
            var kindOfAction = (ActionType)actionKind;

            if (plugin.EventLog.Enabled && plugin.Configuration.LogScreenLogKind)
            {
                var subject = kind is FlyTextKind.Buff or FlyTextKind.Debuff or FlyTextKind.BuffFading or FlyTextKind.DebuffFading
                    ? plugin.Names.Status((uint)value1)
                    : plugin.Names.Action(kindOfAction, actionId);
                plugin.EventLog.Add("Kind",
                    $"{kind}({screenLogKind}) opt={(ScreenLogOption)option} action={subject} v1={value1} v2={value2} v3={value3} src={Actors.Describe(source)} tgt={Actors.Describe(target)}");
            }

            var combatEvent = plugin.EventBuilder.FromScreenLog(kind, option, kindOfAction, actionId, value1, value2, value3, source, target);
            if (combatEvent != null)
            {
                EventsBuilt++;
                plugin.Overlay.Enqueue(combatEvent);
            }

            swallow = plugin.Configuration.HideNative;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "AddToScreenLogWithScreenLogKind detour failed.");
        }

        // Not calling the original means the game never draws its own text for this hit.
        // The chat log is written by a different function, so it keeps working.
        if (swallow)
        {
            KindSwallowed++;
            return;
        }

        insideEntryPoint = true;
        try
        {
            kindHook!.Original(target, source, screenLogKind, option, actionKind, actionId, value1, value2, value3);
        }
        finally
        {
            insideEntryPoint = false;
        }
    }

    /// <summary>
    /// Only logs. It must always pass the call on: the game calls the function above from inside this one, and that
    /// is where the combat event is built. Stopping the call here would lose the hit.
    /// </summary>
    private void AddToScreenLogWithLogMessageIdDetour(
        BattleChara* target,
        BattleChara* source,
        int logMessageId,
        byte actionKind,
        uint actionId,
        int value1,
        int value2,
        int value3)
    {
        MessageCalls++;
        try
        {
            if (plugin.EventLog.Enabled && plugin.Configuration.LogScreenLogKind)
            {
                plugin.EventLog.Add("Msg",
                    $"logMessage={logMessageId} action={plugin.Names.Action((ActionType)actionKind, actionId)} v1={value1} v2={value2} v3={value3} src={Actors.Describe(source)} tgt={Actors.Describe(target)}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "AddToScreenLogWithLogMessageId detour failed.");
        }

        insideEntryPoint = true;
        try
        {
            messageHook!.Original(target, source, logMessageId, actionKind, actionId, value1, value2, value3);
        }
        finally
        {
            insideEntryPoint = false;
        }
    }

    /// <summary>Only logs, and counts the texts that did not come through the two functions above.</summary>
    private void* AddScreenLogEntryDetour(void* queue, ScreenLogEntry* entry)
    {
        EntryCalls++;
        try
        {
            var orphan = !insideEntryPoint;
            if (orphan)
            {
                EntryOrphans++;
            }

            if (plugin.EventLog.Enabled && plugin.Configuration.LogScreenLogEntry)
            {
                var kind = (FlyTextKind)entry->ScreenLogKind;
                plugin.EventLog.Add("Entry",
                    $"{(orphan ? "orphan " : string.Empty)}{kind}({entry->ScreenLogKind}) srcRel={entry->SourceRelation} tgtRel={entry->TargetRelation} opt={(ScreenLogOption)entry->Option} actionKind={(ActionType)entry->ActionKind} actionId={entry->ActionId} v1={entry->Value1} v2={entry->Value2} v3={entry->Value3}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "AddScreenLogEntry detour failed.");
        }

        return entryHook!.Original(queue, entry);
    }
}
