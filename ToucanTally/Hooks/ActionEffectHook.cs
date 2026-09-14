using System;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;

namespace ToucanTally.Hooks;

/// <summary>
/// Listens to the server's action results: the message that says what a skill did to each target. From it the plugin
/// learns which damage-over-time and heal-over-time effects you put on whom, how much of a heal was overheal, when you
/// use an ability, and when your hit kills something.
/// 
/// Some extra explanation in the code blocks for you to learn how it works under the hood!
/// </summary>
internal sealed unsafe class ActionEffectHook : IDisposable
{
    private delegate void ReceiveDelegate(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPosition,
        Header* header,
        TargetEffects* effects,
        GameObjectId* targetEntityIds);

    private const int MaxTargets = 32;

    private readonly Plugin plugin;
    private readonly Hook<ReceiveDelegate>? hook;

    public string HookState { get; private set; } = "not created";
    public long Calls;

    public ActionEffectHook(Plugin plugin)
    {
        this.plugin = plugin;

        var address = Addresses.Receive.Value;
        if (address == 0)
        {
            HookState = "address unresolved";
            Plugin.Log.Error("ActionEffectHandler.Receive address is zero.");
            return;
        }

        hook = Plugin.GameInteropProvider.HookFromAddress<ReceiveDelegate>(address, ReceiveDetour);
        hook.Enable();
        HookState = $"created at {address:X}";
    }

    public bool Enabled => hook?.IsEnabled ?? false;

    public void SetEnabled(bool enabled)
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

    public void Dispose()
    {
        hook?.Dispose();
    }

    /// <summary>
    /// This is where the magic starts happening, where we receive the actioneffect from the framework.
    /// Note: we always call the original Hook!.Original(...) to make sure that we are just sitting inbetween.
    /// </summary>
    /// <param name="casterEntityId"></param>
    /// <param name="caster"></param>
    /// <param name="targetPosition"></param>
    /// <param name="header"></param>
    /// <param name="effects"></param>
    /// <param name="targetEntityIds"></param>
    private void ReceiveDetour(
        uint casterEntityId,
        Character* caster,
        Vector3* targetPosition,
        Header* header,
        TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        Calls++;
        try
        {
            Inspect(casterEntityId, caster, header, effects, targetEntityIds);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "ActionEffectHandler.Receive detour failed.");
        }

        hook!.Original(casterEntityId, caster, targetPosition, header, effects, targetEntityIds);
    }

    /// <summary>
    /// Reads one action result from the server. It arrives just before the numbers (would otherwise) appear on screen, 
    /// and it carries details the later fly text call leaves out, so we collect this info here.
    /// </summary>
    private void Inspect(uint casterEntityId, Character* caster, Header* header, TargetEffects* effects, GameObjectId* targetEntityIds)
    {
        // Who used the action: you, your pet or summon, or someone else.
        var localPlayerId = Plugin.ObjectTable.LocalPlayer?.EntityId ?? 0;
        var casterIsYou = casterEntityId == localPlayerId;
        var casterIsYours = casterIsYou || (caster != null && Actors.RelationOf((BattleChara*)caster) == Relation.YourPet);

        // Your own abilities (oGCDs) are announced here, the moment the server confirms them.
        var isAction = (ActionType)header->ActionType == ActionType.Action;
        if (casterIsYou && isAction && plugin.Names.IsAbility(header->ActionId))
        {
            plugin.Overlay.Enqueue(plugin.EventBuilder.OwnCast(header->ActionId));
        }

        LogHeader(casterEntityId, caster, header);

        // One action can hit several targets, for example an AoE. Each target gets its own short list of effects:
        // damage, a heal, a status applied, and so on.
        var targetCount = Math.Min((int)header->NumTargets, MaxTargets);
        for (var i = 0; i < targetCount; i++)
        {
            var targetId = targetEntityIds[i].ObjectId;
            var target = Plugin.ObjectTable.SearchByEntityId(targetId) as IBattleChara;

            foreach (var effect in effects[i].Effects)
            {
                // Unused slots in the list have type 0.
                if (effect.Type == 0)
                {
                    continue;
                }

                Track(effect, header->ActionId, targetId, target, casterEntityId, localPlayerId, casterIsYours);
            }

            LogTarget(i, targetId, effects[i]);
        }
    }

    private void LogHeader(uint casterEntityId, Character* caster, Header* header)
    {
        if (!plugin.EventLog.Enabled || !plugin.Configuration.LogActionEffects)
        {
            return;
        }

        var casterName = caster == null ? "null" : Actors.Describe((BattleChara*)caster);
        var action = plugin.Names.Action((ActionType)header->ActionType, header->ActionId);
        plugin.EventLog.Add("Effect",
            $"caster={casterName}({casterEntityId:X}) action={action} targets={header->NumTargets} seq={header->GlobalSequence}");
    }

    private void LogTarget(int index, uint targetId, TargetEffects targetEffects)
    {
        if (!plugin.EventLog.Enabled || !plugin.Configuration.LogActionEffects)
        {
            return;
        }

        var builder = new StringBuilder($"  target[{index}]={targetId:X}");
        foreach (var effect in targetEffects.Effects)
        {
            if (effect.Type == 0)
            {
                continue;
            }

            builder.Append($" | type={effect.Type} p0={effect.Param0:X2} p1={effect.Param1} p2={effect.Param2} p3={effect.Param3:X2} p4={effect.Param4} value={effect.Value} => {Effects.Describe(effect)}");
        }

        plugin.EventLog.Add("Effect", builder.ToString());
    }

    /// <summary>
    /// Takes notes from one effect: which status was put on whom (to name DoT and HoT ticks later), how much of a
    /// heal was overheal (the target's HP is still pre-heal at this point), and whether a hit was a killing blow.
    /// </summary>
    private void Track(Effect effect, uint actionId, uint targetId, IBattleChara? target, uint casterId, uint localPlayerId, bool fromPlayer)
    {
        switch ((EffectType)effect.Type)
        {
            case EffectType.ApplyStatusTarget:
                if (casterId == localPlayerId && targetId != localPlayerId)
                {
                    plugin.StatusMemory.RememberApplied(targetId, effect.Value);
                }

                if (targetId == localPlayerId)
                {
                    plugin.StatusMemory.RememberOnPlayer(effect.Value);
                }

                break;

            case EffectType.ApplyStatusSource:
                if (casterId == localPlayerId)
                {
                    plugin.StatusMemory.RememberOnPlayer(effect.Value);
                }

                break;

            case EffectType.Heal:
                if (target != null && (fromPlayer || targetId == localPlayerId))
                {
                    var amount = Effects.Amount(effect);
                    var missing = target.MaxHp > target.CurrentHp ? target.MaxHp - target.CurrentHp : 0;
                    var overheal = amount > missing ? amount - missing : 0;
                    plugin.HealMemory.Remember(targetId, actionId, amount, overheal);
                }

                break;

            case EffectType.Damage:
                if (fromPlayer && target != null && target.CurrentHp > 0 && Effects.Amount(effect) >= target.CurrentHp)
                {
                    plugin.Overlay.Enqueue(plugin.EventBuilder.KillingBlow(target.Name.TextValue, actionId));
                }

                break;
        }
    }
}
