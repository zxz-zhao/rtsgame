using UnityEngine;

/// <summary>
/// Mobile anti-air platform that trades ground pressure for strong aircraft denial.
/// </summary>
public class AntiAirGun : RTSUnit
{
    public float HullRotSpeed = 70f;
    public float TurretRotSpeed = 95f;

    private Vector3 _lastTrackPos;
    private bool _trackInit = false;
    private const float TrackStep = 2.0f;
    private const float TrackHalfWidth = 0.72f;
    private const float TrackLifetime = 6f;

    protected override float DesiredVisualHeight => 3.0f;
    protected override float DesiredVisualFootprint => 3.4f;
    protected override float HealthBarHeight => 3.5f;
    protected override float UnitLabelHeight => 3.2f;
    protected override float SelectionRingRadius => 1.85f;
    protected override bool CanTraverseForestZones => false;

    protected override void Awake()
    {
        DisplayName = "\u9632\u7a7a\u70ae";
        MaxHP = 320;
        AttackDamage = 52;
        AttackRange = 16f;
        AttackInterval = 0.45f;
        SightRange = 20f;
        GoldCost = 260;
        PopCost = 2;
        MoveSpeed = 4.9f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 110f;
        ProjectileArcHeight = 0f;
        ProjectileImpactRadius = 0.52f;
        ProjectileTint = new Color(0.72f, 0.94f, 1f, 1f);
        TracerDuration = 0.05f;
        base.Awake();
    }

    protected override bool CanAttackAirUnit(AirUnit target)
    {
        return target != null;
    }

    public override bool CanAttackUnit(RTSUnit target)
    {
        if (target == null || target == this || target.IsDead())
            return false;
        if (target.bPlayerOwned == bPlayerOwned)
            return false;
        if (!target.bFlying)
            return false;

        var airTarget = target as AirUnit;
        return airTarget != null && CanAttackAirUnit(airTarget);
    }

    protected override void Update()
    {
        base.Update();
        UpdateTrackMarks();
    }

    void UpdateTrackMarks()
    {
        if (IsDead() || !bPlayerOwned)
        {
            _trackInit = false;
            return;
        }

        if (!_trackInit)
        {
            _lastTrackPos = transform.position;
            _trackInit = true;
            return;
        }

        Vector3 cur = transform.position;
        if ((cur - _lastTrackPos).sqrMagnitude < TrackStep * TrackStep)
            return;

        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude < 0.001f)
            fwd = (cur - _lastTrackPos).normalized;

        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        EffectsManager.SpawnTrackMark(cur + right * TrackHalfWidth, fwd, TrackLifetime);
        EffectsManager.SpawnTrackMark(cur - right * TrackHalfWidth, fwd, TrackLifetime);
        _lastTrackPos = cur;
    }
}
