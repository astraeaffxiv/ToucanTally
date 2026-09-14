using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ToucanTally.Events;

namespace ToucanTally.Hooks;

internal enum Relation
{
    None,
    You,
    YourPet,
    Party,
    Enemy,
    Other,
}

/// <summary>
/// Helper class to read the character pointer and gather info from it.
/// </summary>
internal static unsafe class Actors
{
    private const byte TankRole = 1;
    private const byte HealerRole = 4;

    public static Relation RelationOf(BattleChara* character)
    {
        if (character == null)
        {
            return Relation.None;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            return Relation.Other;
        }

        if ((nint)character == localPlayer.Address)
        {
            return Relation.You;
        }

        var subKind = (BattleNpcSubKind)((GameObject*)character)->SubKind;
        if (subKind is BattleNpcSubKind.Pet or BattleNpcSubKind.Buddy && character->OwnerId == localPlayer.EntityId)
        {
            return Relation.YourPet;
        }

        if (character->IsPartyMember || subKind == BattleNpcSubKind.NpcPartyMember)
        {
            return Relation.Party;
        }

        var isBattleNpc = ((GameObject*)character)->ObjectKind == ObjectKind.BattleNpc;
        if (isBattleNpc && subKind is BattleNpcSubKind.Combatant or BattleNpcSubKind.BNpcPart)
        {
            return Relation.Enemy;
        }

        return Relation.Other;
    }

    public static Role RoleOf(BattleChara* character)
    {
        if (character == null)
        {
            return Role.None;
        }

        var entityId = ((GameObject*)character)->EntityId;
        if (Plugin.ObjectTable.SearchByEntityId(entityId) is not IBattleChara battleChara)
        {
            return Role.None;
        }

        var job = battleChara.ClassJob.ValueNullable;
        if (job == null || job.Value.RowId == 0)
        {
            return Role.None;
        }

        return job.Value.Role switch
        {
            TankRole => Role.Tank,
            HealerRole => Role.Healer,
            _ => Role.Dps,
        };
    }

    public static string Describe(BattleChara* character)
    {
        if (character == null)
        {
            return "null";
        }

        var gameObject = (GameObject*)character;
        var subKind = (BattleNpcSubKind)gameObject->SubKind;
        return $"{gameObject->NameString}({RelationOf(character)} {gameObject->ObjectKind}/{subKind} id={gameObject->EntityId:X} owner={character->OwnerId:X})";
    }
}
