using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;

namespace ToucanTally.Hooks;

internal enum EffectType : byte
{
    Nothing = 0,
    Miss = 1,
    FullResist = 2,
    Damage = 3,
    Heal = 4,
    BlockedDamage = 5,
    ParriedDamage = 6,
    Invulnerable = 7,
    NoEffect = 8,
    MpLoss = 10,
    MpGain = 11,
    TpLoss = 12,
    TpGain = 13,
    ApplyStatusTarget = 14,
    ApplyStatusSource = 15,
    RecoveredFromStatus = 16,
    LoseStatusTarget = 17,
    LoseStatusSource = 18,
    StatusNoEffect = 20,
    ThreatPosition = 24,
    EnmityUp = 25,
    EnmityDown = 26,
    StartActionCombo = 27,
    ComboSucceed = 28,
    Knockback = 32,
    Mount = 40,
    Vfx = 59,
    JobGauge = 60,
    SetModelState = 61,
    SetHp = 62,
    PartialInvulnerable = 63,
    Interrupt = 64,
}

internal enum DamageKind : byte
{
    Unknown = 0,
    Slashing = 1,
    Piercing = 2,
    Blunt = 3,
    Shot = 4,
    Magic = 5,
    Unique = 6,
    Physical = 7,
    LimitBreak = 8,
}

/// <summary>
/// Decodes one action effect entry. Bit meanings observed on the live client: 
/// - damage crit is Param0 0x20
/// - heal crit is Param1 0x20 
/// - Param1 low nibble is the damage kind and its high nibble the element
/// - Param4 0x40 means Param3 carries the value's high byte.
/// </summary>
internal static class Effects
{
    private const byte CritFlag = 0x20;
    private const byte DirectHitFlag = 0x40;
    private const byte ExtendedValueFlag = 0x40;

    public static bool IsCrit(Effect effect) => (effect.Param0 & CritFlag) != 0;

    public static bool IsHealCrit(Effect effect) => (effect.Param1 & CritFlag) != 0;

    public static bool IsDirectHit(Effect effect) => (effect.Param0 & DirectHitFlag) != 0;

    public static DamageKind KindOf(Effect effect) => (DamageKind)(effect.Param1 & 0x0F);

    public static uint Amount(Effect effect)
    {
        uint amount = effect.Value;
        if ((effect.Param4 & ExtendedValueFlag) != 0)
        {
            amount += (uint)effect.Param3 << 16;
        }

        return amount;
    }

    public static string Describe(Effect effect)
    {
        var type = (EffectType)effect.Type;
        return type switch
        {
            EffectType.Damage or EffectType.BlockedDamage or EffectType.ParriedDamage =>
                $"{type} {Amount(effect)} {KindOf(effect)} elem={effect.Param1 >> 4}{(IsCrit(effect) ? " crit" : string.Empty)}{(IsDirectHit(effect) ? " dh" : string.Empty)}",
            EffectType.Heal => $"Heal {Amount(effect)}{(IsHealCrit(effect) ? " crit" : string.Empty)}",
            EffectType.ApplyStatusTarget or EffectType.ApplyStatusSource or EffectType.LoseStatusTarget or EffectType.LoseStatusSource =>
                $"{type} status={effect.Value} param={effect.Param2}",
            _ => type.ToString(),
        };
    }
}
