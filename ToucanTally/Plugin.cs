using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ToucanTally.Events;
using ToucanTally.Hooks;
using ToucanTally.Rendering;
using ToucanTally.Windows;

namespace ToucanTally;

/**
 * 
  _______                       _______    _ _        
 |__   __|                     |__   __|  | | |       
    | | ___  _   _  ___ __ _ _ __ | | __ _| | |_   _  
    | |/ _ \| | | |/ __/ _` | '_ \| |/ _` | | | | | | 
    | | (_) | |_| | (_| (_| | | | | | (_| | | | |_| | 
    |_|\___/ \__,_|\___\__,_|_| |_|_|\__,_|_|_|\__, | 
                                                __/ | 
                                               |___/  
 * 
                          _,---._      __,...-----...___
                      _,-:::,,--.`,--''                 `'--._
                    ,':::::/((##)):                           `-.
                  ,':.::::/  `--' :         _____.....______ (:::\
                 /:::::::/        :__,.-''''..- - - - --  -- .`_-:\
                /:,:::.::|        ::.          ____....-----.....`.
               /,:::::::/          ::::__.--'''
               |:::::::|           _:'
               |:.:::::|         ,'
               |:::::::|         |
              /::::.:::|         |
         __,-'::.::::::|         |
  _,.--''::::_::::::::::\        |
''::_::,:--''  `'--.:::::\       ;
-'''::::::::::::::::\:::::`.     ;
:::::::::::::::::::::|:.::::`-..-
::;::::::::::::::::::|::::::::::/
:/::::::/::::::;:::::|::::.::::/
(::::::/::::::/::::::):.::::::'
:`:__,;::::::;:::::,':::::::'
,-':::`.__,-'::::,'::::::-'
:::::,-'::::::,-':::_:-'
_,-':::::::,-'::_:-'
     ::::,-_:--''
::,::--''
 *
 * Toucan Tally
 *
 * Created by Astraea
 * Requested by Nihal
 * Cheers to the Toucan Team
 *
 */
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IFlyTextGui FlyTextGui { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    private const string CommandName = "/toucan";

    public Configuration Configuration { get; private set; }

    public readonly WindowSystem WindowSystem = new("ToucanTally");

    internal EventLog EventLog { get; }
    internal Names Names { get; }
    internal StatusMemory StatusMemory { get; }
    internal HealMemory HealMemory { get; }
    internal EventBuilder EventBuilder { get; }
    internal Overlay Overlay { get; }
    internal GameSettings GameSettings { get; }
    internal ScreenLogHook ScreenLogHook { get; }
    internal ActionEffectHook ActionEffectHook { get; }
    internal FlyTextHook FlyTextHook { get; }

    private ConfigWindow ConfigWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.EnsureDefaults();

        EventLog = new EventLog(this);
        Names = new Names();
        StatusMemory = new StatusMemory();
        HealMemory = new HealMemory();
        EventBuilder = new EventBuilder(Names, StatusMemory, HealMemory);
        Overlay = new Overlay(this);
        GameSettings = new GameSettings(EventLog);
        ScreenLogHook = new ScreenLogHook(this);
        ActionEffectHook = new ActionEffectHook(this);
        FlyTextHook = new FlyTextHook(this);

        ConfigWindow = new ConfigWindow(this);
        DebugWindow = new DebugWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(DebugWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Toucan Tally settings. '/toucan move' toggles area placement, '/toucan debug' opens the debug window.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        Log.Information("Toucan Tally loaded. Hooks: kind={0} message={1} entry={2} actionEffect={3}",
            ScreenLogHook.KindHookState, ScreenLogHook.MessageHookState, ScreenLogHook.EntryHookState, ActionEffectHook.HookState);
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);

        FlyTextHook.Dispose();
        ActionEffectHook.Dispose();
        ScreenLogHook.Dispose();
        GameSettings.Dispose();
        Overlay.Dispose();
    }

    public void SaveConfiguration()
    {
        PluginInterface.SavePluginConfig(Configuration);
    }

    public void ResetConfiguration()
    {
        Configuration = new Configuration();
        Configuration.EnsureDefaults();
        Overlay.Clear();
        SaveConfiguration();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "debug":
                DebugWindow.Toggle();
                break;
            case "move":
                Overlay.MoveMode = !Overlay.MoveMode;
                break;
            default:
                ConfigWindow.Toggle();
                break;
        }
    }
}
