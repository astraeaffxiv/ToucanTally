using System;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;

namespace ToucanTally.Hooks;

/// <summary>
/// Listens to Dalamud's FlyTextCreated event, which fires right before the game draws one of its own flying texts.
/// Only used to write the debug log. It changes nothing.
/// </summary>
internal sealed class FlyTextHook : IDisposable
{
    private readonly Plugin plugin;

    public long Calls;

    public FlyTextHook(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.FlyTextGui.FlyTextCreated += OnFlyTextCreated;
    }

    public void Dispose()
    {
        Plugin.FlyTextGui.FlyTextCreated -= OnFlyTextCreated;
    }

    private void OnFlyTextCreated(
        ref FlyTextKind kind,
        ref int val1,
        ref int val2,
        ref SeString text1,
        ref SeString text2,
        ref uint color,
        ref uint icon,
        ref uint damageTypeIcon,
        ref float yOffset,
        ref bool handled)
    {
        Calls++;
        try
        {
            if (plugin.EventLog.Enabled && plugin.Configuration.LogFlyTextCreated)
            {
                plugin.EventLog.Add("Created",
                    $"{kind} v1={val1} v2={val2} text1='{text1.TextValue}' text2='{text2.TextValue}' color={color:X8} icon={icon} dmgIcon={damageTypeIcon} y={yOffset}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "FlyTextCreated handler failed.");
        }
    }
}
