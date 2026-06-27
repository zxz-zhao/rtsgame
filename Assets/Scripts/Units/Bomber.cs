using UnityEngine;

/// <summary>
/// Heavy aircraft that trades speed and uptime for strong splash damage against clustered ground targets.
/// </summary>
public class Bomber : AirUnit
{
    public const float MinBombingRunLength = 10f;
    public const float MaxBombingRunLength = 24f;
    public const float BombingRunLaneSpacing = 4f;

    const float BombingRunApproachLead = 9f;
    const float BombingRunExitLead = 7f;
    const float BombingRunArrivalThreshold = 1.25f;
    const float BombingRunFinishThreshold = 1.5f;
    const float BombingRunReleaseProgress = 0.58f;
    const float BombingRunReleaseSpacingForward = 2.2f;
    const float BombingRunReleaseSpacingSide = 2.0f;
    const float BombingRunDamageMultiplier = 0.58f;
    const float BombingRunImpactJitter = 0.55f;
    public const float BattleFuelSeconds = 120f;
    public const float RefuelDurationSeconds = 8f;

    bool _bombingRunActive;
    bool _bombingRunOnAttackLeg;
    Vector3 _bombingRunStart;
    Vector3 _bombingRunEnd;
    Vector3 _bombingRunApproach;
    Vector3 _bombingRunExit;
    Vector3 _bombingRunDirection;
    Vector3 _bombingRunPerpendicular;
    float _bombingRunLength;
    float _bombingRunReleaseDistance;
    bool _bombingRunVolleyReleased;

    protected override float DesiredVisualHeight => 2.0f;
    protected override float DesiredVisualFootprint => 6.0f;

    protected override void Awake()
    {
        DisplayName = "\u8f70\u70b8\u673a";
        MaxHP = 300;
        AttackDamage = 130;
        AttackRange = 8f;
        AttackInterval = 3f;
        SightRange = 20f;
        GoldCost = 500;
        PopCost = 3;
        MoveSpeed = 11f;
        SplashRadius = 7f;
        SplashFalloff = 0.3f;
        FlyHeight = 11f;
        MaxFuelSeconds = BattleFuelSeconds;
        RefuelSeconds = RefuelDurationSeconds;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bomb";
        ProjectileSpeed = 30f;
        ProjectileArcHeight = 4f;
        ProjectileImpactRadius = 2.1f;
        ProjectileTint = new Color(1f, 0.36f, 0.08f, 1f);
        TracerDuration = 0.08f;
        base.Awake();
    }

    public void ApplyBombingRunCommand(Vector3 start, Vector3 end)
    {
        Vector3 flatStart = new Vector3(start.x, 0f, start.z);
        Vector3 flatEnd = new Vector3(end.x, 0f, end.z);
        Vector3 flatDelta = flatEnd - flatStart;
        flatDelta.y = 0f;

        if (flatDelta.sqrMagnitude < 0.01f)
        {
            Vector3 fallback = transform.forward;
            fallback.y = 0f;
            if (fallback.sqrMagnitude < 0.01f)
                fallback = Vector3.forward;
            flatDelta = fallback.normalized * MinBombingRunLength;
        }

        _bombingRunDirection = flatDelta.normalized;
        _bombingRunLength = Mathf.Clamp(flatDelta.magnitude, MinBombingRunLength, MaxBombingRunLength);
        _bombingRunPerpendicular = Vector3.Cross(Vector3.up, _bombingRunDirection).normalized;

        _bombingRunStart = new Vector3(flatStart.x, FlyHeight, flatStart.z);
        _bombingRunEnd = _bombingRunStart + _bombingRunDirection * _bombingRunLength;
        _bombingRunApproach = _bombingRunStart - _bombingRunDirection * BombingRunApproachLead;
        _bombingRunExit = _bombingRunEnd + _bombingRunDirection * BombingRunExitLead;
        _bombingRunReleaseDistance = Mathf.Clamp(
            _bombingRunLength * BombingRunReleaseProgress,
            BombingRunReleaseSpacingForward,
            Mathf.Max(BombingRunReleaseSpacingForward, _bombingRunLength - BombingRunReleaseSpacingForward));
        _bombingRunVolleyReleased = false;
        _bombingRunOnAttackLeg = false;
        _bombingRunActive = true;

        BeginFlightFromParking();
        CurrentCommand = CommandType.None;
        AttackTarget = null;
        buildingTarget = null;
        AttackTimer = 0f;
    }

    protected override void UpdateAI()
    {
        if (HandleFuelAndReturn())
        {
            if (_bombingRunActive && (IsReturningToRefuel || IsParked || CurrentFuel <= 0f))
                CancelBombingRun();
            return;
        }

        if (_bombingRunActive)
        {
            bool replacedByNormalCommand = CurrentCommand != CommandType.None
                || AttackTarget != null
                || buildingTarget != null;
            if (replacedByNormalCommand)
                CancelBombingRun();
            else
            {
                UpdateBombingRun();
                return;
            }
        }

        base.UpdateAI();
    }

    void UpdateBombingRun()
    {
        SetAirborneHeight(false);

        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        if (!_bombingRunOnAttackLeg)
        {
            MoveTowardAirTarget(_bombingRunApproach, MoveSpeed * 1.04f);
            Vector3 flatApproach = new Vector3(_bombingRunApproach.x, 0f, _bombingRunApproach.z);
            if (Vector3.Distance(flatSelf, flatApproach) <= BombingRunArrivalThreshold)
                _bombingRunOnAttackLeg = true;
            return;
        }

        MoveTowardAirTarget(_bombingRunExit, MoveSpeed * 0.96f);

        Vector3 runOffset = flatSelf - new Vector3(_bombingRunStart.x, 0f, _bombingRunStart.z);
        float runProgress = Vector3.Dot(runOffset, _bombingRunDirection);
        if (!_bombingRunVolleyReleased && runProgress + 0.3f >= _bombingRunReleaseDistance)
            ReleaseMissileVolley();

        Vector3 flatExit = new Vector3(_bombingRunExit.x, 0f, _bombingRunExit.z);
        if (Vector3.Distance(flatSelf, flatExit) <= BombingRunFinishThreshold)
            CancelBombingRun();
    }

    void ReleaseMissileVolley()
    {
        _bombingRunVolleyReleased = true;

        Vector3 releaseCenter = _bombingRunStart + _bombingRunDirection * _bombingRunReleaseDistance;
        int missileIndex = 0;
        for (int row = 0; row < 2; row++)
        {
            float forwardOffset = row == 0 ? -BombingRunReleaseSpacingForward * 0.5f : BombingRunReleaseSpacingForward * 0.5f;
            for (int col = 0; col < 2; col++)
            {
                float sideOffset = col == 0 ? -BombingRunReleaseSpacingSide * 0.5f : BombingRunReleaseSpacingSide * 0.5f;
                Vector3 impactPoint = releaseCenter
                    + _bombingRunDirection * forwardOffset
                    + _bombingRunPerpendicular * sideOffset;
                impactPoint += ComputeVolleyJitter(missileIndex);
                impactPoint.y = 0f;

                PlayAttackVisuals(impactPoint);
                ApplyBombingRunDamage(impactPoint);
                missileIndex++;
            }
        }
    }

    Vector3 ComputeVolleyJitter(int missileIndex)
    {
        int seed = NetId != 0 ? NetId : GetInstanceID();
        float noiseA = Mathf.Sin((seed * 0.731f + missileIndex * 1.913f + 0.37f) * 12.9898f);
        float noiseB = Mathf.Sin((seed * 1.117f + missileIndex * 2.357f + 1.19f) * 78.233f);
        return _bombingRunDirection * (noiseA * BombingRunImpactJitter)
             + _bombingRunPerpendicular * (noiseB * BombingRunImpactJitter);
    }

    void ApplyBombingRunDamage(Vector3 impactPoint)
    {
        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && !sync.IsHost)
            return;

        int bombDamage = Mathf.Max(1, Mathf.RoundToInt(AttackDamage * BombingRunDamageMultiplier));
        float splashRadius = Mathf.Max(1.5f, SplashRadius);

        var allUnits = GameManager.Instance?.GetAllUnits();
        if (allUnits != null)
        {
            for (int i = 0; i < allUnits.Count; i++)
            {
                RTSUnit unit = allUnits[i];
                if (unit == null || unit == this || unit.IsDead() || unit.bPlayerOwned == bPlayerOwned || unit.bFlying)
                    continue;

                float distance = Vector3.Distance(impactPoint, unit.transform.position);
                if (distance > splashRadius)
                    continue;

                float damageMultiplier = Mathf.Lerp(1f, SplashFalloff, distance / splashRadius);
                bool wasAlive = !unit.IsDead();
                unit.TakeDamageFrom(this, Mathf.RoundToInt(bombDamage * damageMultiplier));
                if (wasAlive && unit.IsDead())
                    AddKill();
            }
        }

        var allBuildings = GameManager.Instance?.GetAllBuildings();
        if (allBuildings != null)
        {
            for (int i = 0; i < allBuildings.Count; i++)
            {
                RTSBuilding building = allBuildings[i];
                if (building == null || building.GetHP() <= 0 || building.bPlayerOwned == bPlayerOwned)
                    continue;

                float distance = Vector3.Distance(impactPoint, building.transform.position);
                if (distance > splashRadius)
                    continue;

                float damageMultiplier = Mathf.Lerp(1f, SplashFalloff, distance / splashRadius);
                int previousHp = building.GetHP();
                building.TakeDamageFrom(this, Mathf.RoundToInt(bombDamage * damageMultiplier));
                if (previousHp > 0 && building.GetHP() <= 0)
                    AddKill();
            }
        }
    }

    void CancelBombingRun()
    {
        _bombingRunActive = false;
        _bombingRunOnAttackLeg = false;
        _bombingRunVolleyReleased = false;
        _bombingRunReleaseDistance = 0f;
    }
}
