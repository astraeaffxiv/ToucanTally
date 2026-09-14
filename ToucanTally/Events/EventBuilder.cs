using Dalamud.Game.Gui.FlyText;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ToucanTally.Hooks;

namespace ToucanTally.Events;

/// <summary>
/// Turns one flying-text call from the game into this plugin's combat event: who, which skill, how much, crit or not.
/// Returns null for things no box shows, such as another player's damage on an enemy.
/// </summary>
internal sealed unsafe class EventBuilder
{
    private readonly Names names;
    private readonly StatusMemory statuses;
    private readonly HealMemory heals;

    public EventBuilder(Names names, StatusMemory statuses, HealMemory heals)
    {
        this.names = names;
        this.statuses = statuses;
        this.heals = heals;
    }

    public CombatEvent? FromScreenLog(
        FlyTextKind kind,
        byte option,
        ActionType actionKind,
        uint actionId,
        int value1,
        int value2,
        int value3,
        BattleChara* source,
        BattleChara* target)
    {
        var sourceRelation = Actors.RelationOf(source);
        var targetRelation = Actors.RelationOf(target);
        var fromPlayer = sourceRelation is Relation.You or Relation.YourPet;
        var onPlayer = targetRelation == Relation.You;
        var dotTick = kind == FlyTextKind.AutoAttackOrDot && option == 0 && actionKind == ActionType.None && source == target && target != null;

        switch (kind)
        {
            case FlyTextKind.AutoAttackOrDot:
            case FlyTextKind.AutoAttackOrDotDh:
            case FlyTextKind.AutoAttackOrDotCrit:
            case FlyTextKind.AutoAttackOrDotCritDh:
            case FlyTextKind.Damage:
            case FlyTextKind.DamageDh:
            case FlyTextKind.DamageCrit:
            case FlyTextKind.DamageCritDh:
                return Damage(kind, (ScreenLogOption)option, actionKind, actionId, value1, source, target, fromPlayer, onPlayer, dotTick);

            case FlyTextKind.Healing:
            case FlyTextKind.HealingCrit:
                return Heal(kind, actionKind, actionId, value1, source, target, fromPlayer, onPlayer);

            case FlyTextKind.MpRegen:
            case FlyTextKind.NamedMp3:
                return onPlayer ? Resource(EventCategory.MpGain, actionKind, actionId, value1) : null;

            case FlyTextKind.MpDrain:
                return onPlayer ? Resource(EventCategory.MpLoss, actionKind, actionId, value1) : null;

            case FlyTextKind.Miss:
            case FlyTextKind.NamedMiss:
            case FlyTextKind.Dodge:
            case FlyTextKind.NamedDodge:
            case FlyTextKind.Invulnerable:
            case FlyTextKind.Resist:
            case FlyTextKind.FullyResisted:
                return Miss(kind, actionKind, actionId, source, target, fromPlayer, onPlayer);

            case FlyTextKind.Buff:
            case FlyTextKind.BuffFading:
            case FlyTextKind.Debuff:
            case FlyTextKind.DebuffFading:
                return Status(kind, (uint)value1, onPlayer);

            default:
                return null;
        }
    }

    public CombatEvent OwnCast(uint actionId)
    {
        return new CombatEvent
        {
            Category = EventCategory.OwnCast,
            Label = names.ActionName(ActionType.Action, actionId),
            IconId = names.ActionIcon(ActionType.Action, actionId),
        };
    }

    public CombatEvent KillingBlow(string targetName, uint actionId)
    {
        return new CombatEvent
        {
            Category = EventCategory.KillingBlow,
            Label = "Killing blow",
            Actor = targetName,
            IconId = names.ActionIcon(ActionType.Action, actionId),
        };
    }

    private CombatEvent? Damage(FlyTextKind kind, ScreenLogOption option, ActionType actionKind, uint actionId, int amount, BattleChara* source, BattleChara* target, bool fromPlayer, bool onPlayer, bool dotTick)
    {
        var modifier = DamageModifier(kind, option);

        if (dotTick)
        {
            var statusId = statuses.AppliedOn(EntityId(target));
            if (statusId == 0)
            {
                return null;
            }

            return new CombatEvent
            {
                Category = EventCategory.OutgoingDamage,
                Label = names.StatusName(statusId),
                Amount = (uint)amount,
                IconId = names.StatusIcon(statusId),
            };
        }

        if (fromPlayer && !onPlayer)
        {
            return new CombatEvent
            {
                Category = EventCategory.OutgoingDamage,
                Label = names.ActionName(actionKind, actionId),
                Amount = (uint)amount,
                IconId = names.ActionIcon(actionKind, actionId),
                Modifier = modifier,
            };
        }

        if (onPlayer)
        {
            return new CombatEvent
            {
                Category = EventCategory.IncomingDamage,
                Label = names.ActionName(actionKind, actionId),
                Actor = Name(source),
                ActorRole = Actors.RoleOf(source),
                Amount = (uint)amount,
                IconId = names.ActionIcon(actionKind, actionId),
                Modifier = modifier,
            };
        }

        return null;
    }

    private CombatEvent? Heal(FlyTextKind kind, ActionType actionKind, uint actionId, int amount, BattleChara* source, BattleChara* target, bool fromPlayer, bool onPlayer)
    {
        var crit = kind == FlyTextKind.HealingCrit;
        var tick = actionKind == ActionType.None && actionId == 0;
        var overheal = tick ? 0 : heals.Take(EntityId(target), actionId, (uint)amount);
        var modifier = overheal > 0 ? Modifier.Overheal : crit ? Modifier.Crit : (Modifier?)null;

        if (onPlayer)
        {
            var statusId = tick ? statuses.OnPlayer() : 0;
            var label = tick
                ? (statusId == 0 ? "Regen" : names.StatusName(statusId))
                : names.ActionName(actionKind, actionId);
            return new CombatEvent
            {
                Category = EventCategory.IncomingHeal,
                Label = label,
                Actor = fromPlayer || tick ? string.Empty : Name(source),
                ActorRole = fromPlayer || tick ? Role.None : Actors.RoleOf(source),
                Amount = (uint)amount,
                Overheal = overheal,
                IconId = tick ? names.StatusIcon(statusId) : names.ActionIcon(actionKind, actionId),
                Modifier = modifier,
            };
        }

        if (fromPlayer)
        {
            return new CombatEvent
            {
                Category = EventCategory.OutgoingHeal,
                Label = names.ActionName(actionKind, actionId),
                Actor = Name(target),
                ActorRole = Actors.RoleOf(target),
                Amount = (uint)amount,
                Overheal = overheal,
                IconId = names.ActionIcon(actionKind, actionId),
                Modifier = modifier,
            };
        }

        return null;
    }

    private CombatEvent Resource(EventCategory category, ActionType actionKind, uint actionId, int amount)
    {
        return new CombatEvent
        {
            Category = category,
            Label = actionId == 0 ? "MP" : names.ActionName(actionKind, actionId),
            Amount = (uint)amount,
            IconId = names.ActionIcon(actionKind, actionId),
        };
    }

    private CombatEvent? Miss(FlyTextKind kind, ActionType actionKind, uint actionId, BattleChara* source, BattleChara* target, bool fromPlayer, bool onPlayer)
    {
        var word = kind switch
        {
            FlyTextKind.Dodge or FlyTextKind.NamedDodge => "Dodge",
            FlyTextKind.Invulnerable => "Invulnerable",
            FlyTextKind.Resist or FlyTextKind.FullyResisted => "Resist",
            _ => "Miss",
        };
        var label = $"{word} {names.ActionName(actionKind, actionId)}".Trim();

        if (fromPlayer && !onPlayer)
        {
            return new CombatEvent
            {
                Category = EventCategory.OutgoingMiss,
                Label = label,
                IconId = names.ActionIcon(actionKind, actionId),
            };
        }

        if (onPlayer)
        {
            return new CombatEvent
            {
                Category = EventCategory.IncomingMiss,
                Label = label,
                Actor = Name(source),
                ActorRole = Actors.RoleOf(source),
                IconId = names.ActionIcon(actionKind, actionId),
            };
        }

        return null;
    }

    private CombatEvent? Status(FlyTextKind kind, uint statusId, bool onPlayer)
    {
        if (!onPlayer)
        {
            return null;
        }

        var category = kind switch
        {
            FlyTextKind.Buff => EventCategory.BuffGain,
            FlyTextKind.BuffFading => EventCategory.BuffFade,
            FlyTextKind.Debuff => EventCategory.DebuffGain,
            _ => EventCategory.DebuffFade,
        };
        var sign = kind is FlyTextKind.Buff or FlyTextKind.Debuff ? "+" : "-";
        return new CombatEvent
        {
            Category = category,
            Label = $"{sign} {names.StatusName(statusId)}",
            IconId = names.StatusIcon(statusId),
        };
    }

    private static Modifier? DamageModifier(FlyTextKind kind, ScreenLogOption option)
    {
        var crit = kind is FlyTextKind.AutoAttackOrDotCrit or FlyTextKind.AutoAttackOrDotCritDh or FlyTextKind.DamageCrit or FlyTextKind.DamageCritDh;
        var directHit = kind is FlyTextKind.AutoAttackOrDotDh or FlyTextKind.AutoAttackOrDotCritDh or FlyTextKind.DamageDh or FlyTextKind.DamageCritDh;

        if (crit && directHit)
        {
            return Modifier.CritDirectHit;
        }

        if (crit)
        {
            return Modifier.Crit;
        }

        if (directHit)
        {
            return Modifier.DirectHit;
        }

        return option switch
        {
            ScreenLogOption.Blocked => Modifier.Blocked,
            ScreenLogOption.Parried => Modifier.Parried,
            ScreenLogOption.Resisted => Modifier.Resisted,
            _ => null,
        };
    }

    private static uint EntityId(BattleChara* character)
    {
        return character == null ? 0 : ((GameObject*)character)->EntityId;
    }

    private static string Name(BattleChara* character)
    {
        return character == null ? "?" : ((GameObject*)character)->NameString;
    }
}
