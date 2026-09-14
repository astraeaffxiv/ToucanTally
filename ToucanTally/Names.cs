using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace ToucanTally;

/// <summary>Looks up the name and icon of a skill, status or item by its number in the game's data.</summary>
internal sealed class Names
{
    private const uint AbilityCategory = 4;

    private readonly ExcelSheet<Action>? actions = Plugin.DataManager.GetExcelSheet<Action>();
    private readonly ExcelSheet<Status>? statuses = Plugin.DataManager.GetExcelSheet<Status>();
    private readonly ExcelSheet<Item>? items = Plugin.DataManager.GetExcelSheet<Item>();

    public string ActionName(ActionType kind, uint id)
    {
        return kind switch
        {
            ActionType.Action or ActionType.PvPAction => actions?.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty,
            ActionType.Item => items?.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty,
            _ => string.Empty,
        };
    }

    public uint ActionIcon(ActionType kind, uint id)
    {
        return kind switch
        {
            ActionType.Action or ActionType.PvPAction => actions?.GetRowOrDefault(id)?.Icon ?? 0,
            ActionType.Item => items?.GetRowOrDefault(id)?.Icon ?? 0,
            _ => 0,
        };
    }

    public bool IsAbility(uint actionId)
    {
        return actions?.GetRowOrDefault(actionId)?.ActionCategory.RowId == AbilityCategory;
    }

    public string StatusName(uint id)
    {
        return statuses?.GetRowOrDefault(id)?.Name.ExtractText() ?? string.Empty;
    }

    public uint StatusIcon(uint id)
    {
        return statuses?.GetRowOrDefault(id)?.Icon ?? 0;
    }

    public string Action(ActionType kind, uint id)
    {
        return $"{ActionName(kind, id)}#{id} icon={ActionIcon(kind, id)}";
    }

    public string Status(uint id)
    {
        return $"{StatusName(id)}#{id} icon={StatusIcon(id)}";
    }
}
