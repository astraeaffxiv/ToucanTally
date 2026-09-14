namespace ToucanTally.Events;

public enum EventCategory
{
    OutgoingDamage,
    OutgoingHeal,
    OutgoingMiss,
    IncomingDamage,
    IncomingHeal,
    IncomingMiss,
    MpGain,
    MpLoss,
    BuffGain,
    BuffFade,
    DebuffGain,
    DebuffFade,
    OwnCast,
    KillingBlow,
}

public enum Modifier
{
    Crit,
    DirectHit,
    CritDirectHit,
    Blocked,
    Parried,
    Resisted,
    Overheal,
}

public enum Role
{
    None,
    Tank,
    Healer,
    Dps,
}
