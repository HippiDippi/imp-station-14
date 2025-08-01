using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Heretic.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Heretic;
using Content.Shared.Physics;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using System.Linq;

namespace Content.Server.Heretic.Abilities;

public sealed partial class HereticAbilitySystem : EntitySystem
{
    private void SubscribeVoid()
    {
        SubscribeLocalEvent<HereticComponent, HereticAristocratWayEvent>(OnAristocratWay);
        SubscribeLocalEvent<HereticComponent, HereticAscensionVoidEvent>(OnAscensionVoid);

        SubscribeLocalEvent<HereticComponent, HereticVoidBlastEvent>(OnVoidBlast);
        SubscribeLocalEvent<HereticComponent, HereticVoidBlinkEvent>(OnVoidBlink);
        SubscribeLocalEvent<HereticComponent, HereticVoidPullEvent>(OnVoidPull);
    }

    private void OnAristocratWay(Entity<HereticComponent> ent, ref HereticAristocratWayEvent args)
    {
        RemComp<TemperatureComponent>(ent);
        RemComp<TemperatureSpeedComponent>(ent);
        RemComp<RespiratorComponent>(ent);
    }
    private void OnAscensionVoid(Entity<HereticComponent> ent, ref HereticAscensionVoidEvent args)
    {
        RemComp<BarotraumaComponent>(ent);
        EnsureComp<AristocratComponent>(ent);

        if(TryComp<FixturesComponent>(ent, out var fixtures) && TryComp<PhysicsComponent>(ent, out var physics))
        {
            var fix = fixtures.Fixtures.First();
            _phys.SetCollisionMask(ent, fix.Key, fix.Value, (int)CollisionGroup.Opaque);
            _phys.SetCollisionLayer(ent, fix.Key, fix.Value, (int)CollisionGroup.BulletImpassable);
        }
    }

    private void OnVoidBlast(Entity<HereticComponent> ent, ref HereticVoidBlastEvent args)
    {
        if (!TryUseAbility(ent, args))
            return;

        var rod = Spawn("ImmovableVoidRod", Transform(ent).Coordinates);

        if (TryComp(rod, out PhysicsComponent? phys))
        {
            _phys.SetLinearDamping(rod, phys, 0f);
            _phys.SetFriction(rod, phys, 0f);
            _phys.SetBodyStatus(rod, phys, BodyStatus.InAir);

            var xform = Transform(rod);
            var direction = _transform.ToMapCoordinates(args.Target).Position - _transform.GetWorldPosition(ent);
            direction.Normalize();

            var vel = direction * 15f;

            _phys.SetLinearVelocity(rod, vel, body: phys);
            xform.LocalRotation = direction.ToAngle();
        }

        args.Handled = true;
    }

    private void OnVoidBlink(Entity<HereticComponent> ent, ref HereticVoidBlinkEvent args)
    {
        if (!TryUseAbility(ent, args) || !_interaction.InRangeUnobstructed(args.Performer, args.Target, range:1000F, collisionMask:CollisionGroup.Opaque, popup: true))
            return;

        _aud.PlayPvs(new SoundPathSpecifier("/Audio/Effects/tesla_consume.ogg"), ent);

        foreach (var pookie in GetNearbyPeople(ent, 2f))
            _stun.TryKnockdown(pookie, TimeSpan.FromSeconds(2.5f), true);

        _transform.SetCoordinates(ent, args.Target);

        // repeating for both sides
        _aud.PlayPvs(new SoundPathSpecifier("/Audio/Effects/tesla_consume.ogg"), ent);

        foreach (var pookie in GetNearbyPeople(ent, 2f))
            _stun.TryKnockdown(pookie, TimeSpan.FromSeconds(2.5f), true);

        args.Handled = true;
    }

    private void OnVoidPull(Entity<HereticComponent> ent, ref HereticVoidPullEvent args)
    {
        if (!TryUseAbility(ent, args))
            return;

        var topPriority = GetNearbyPeople(ent, 1.5f);
        var midPriority = GetNearbyPeople(ent, 2.5f);
        var farPriority = GetNearbyPeople(ent, 5f);

        // damage closest ones
        foreach (var pookie in topPriority)
        {
            if (!TryComp<DamageableComponent>(pookie, out var dmgComp))
                continue;

            // total damage + 20 divided by all damage types.
            var damage = (dmgComp.TotalDamage + 20f) / _prot.EnumeratePrototypes<DamageTypePrototype>().Count();

            // apply gaming.
            _dmg.SetAllDamage(pookie, dmgComp, damage);
        }

        // stun close-mid range
        foreach (var pookie in midPriority)
            _stun.TryKnockdown(pookie, TimeSpan.FromSeconds(2.5f), true);

        // pull in farthest ones
        foreach (var pookie in farPriority)
            _throw.TryThrow(pookie, Transform(ent).Coordinates);

        args.Handled = true;
    }
}
