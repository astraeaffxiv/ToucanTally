using System;
using System.Collections.Generic;
using Dalamud.Game.Config;
using ToucanTally.Events;

namespace ToucanTally;

/// <summary>
/// Reads the game's own flying-text settings (Character Configuration, UI Settings), such as whether flying text is
/// shown and how big it is, and writes to the debug log when one of them changes.
/// </summary>
internal sealed class GameSettings : IDisposable
{
    private readonly EventLog eventLog;

    public GameSettings(EventLog eventLog)
    {
        this.eventLog = eventLog;
        Plugin.GameConfig.UiControlChanged += OnUiControlChanged;
        Plugin.GameConfig.UiConfigChanged += OnUiConfigChanged;
    }

    public static readonly UiControlOption[] ControlOptions =
    [
        UiControlOption.FlyTextDisp,
        UiControlOption.PopUpTextDisp,
    ];

    public static readonly UiConfigOption[] ConfigOptions =
    [
        UiConfigOption.FlyTextDispSize,
        UiConfigOption.PopUpTextDispSize,
        UiConfigOption.BattleEffectSelf,
        UiConfigOption.BattleEffectParty,
        UiConfigOption.BattleEffectOther,
        UiConfigOption.BattleEffectPvPEnemyPc,
        UiConfigOption.LogChatFilter,
    ];

    public static IEnumerable<(string Name, string Value)> Current()
    {
        foreach (var option in ControlOptions)
        {
            yield return (option.ToString(), Read(option));
        }

        foreach (var option in ConfigOptions)
        {
            yield return (option.ToString(), Read(option));
        }
    }

    public void LogAll()
    {
        foreach (var (name, value) in Current())
        {
            eventLog.Add("Settings", $"{name} = {value}");
        }
    }

    public void Dispose()
    {
        Plugin.GameConfig.UiControlChanged -= OnUiControlChanged;
        Plugin.GameConfig.UiConfigChanged -= OnUiConfigChanged;
    }

    private static string Read(UiControlOption option)
    {
        if (Plugin.GameConfig.TryGet(option, out uint value))
        {
            return value.ToString();
        }

        if (Plugin.GameConfig.TryGet(option, out bool flag))
        {
            return flag.ToString();
        }

        return "unreadable";
    }

    private static string Read(UiConfigOption option)
    {
        if (Plugin.GameConfig.TryGet(option, out uint value))
        {
            return value.ToString();
        }

        if (Plugin.GameConfig.TryGet(option, out bool flag))
        {
            return flag.ToString();
        }

        return "unreadable";
    }

    private void OnUiControlChanged(object? sender, ConfigChangeEvent e)
    {
        if (e.Option is UiControlOption option && Array.IndexOf(ControlOptions, option) >= 0)
        {
            eventLog.Add("Settings", $"{option} changed to {Read(option)}");
        }
    }

    private void OnUiConfigChanged(object? sender, ConfigChangeEvent e)
    {
        if (e.Option is UiConfigOption option && Array.IndexOf(ConfigOptions, option) >= 0)
        {
            eventLog.Add("Settings", $"{option} changed to {Read(option)}");
        }
    }
}
