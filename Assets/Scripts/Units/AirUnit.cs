using UnityEngine;

public class AirUnit : RTSUnit
{
    public float FlyHeight = 8f;
    public const float DefaultBattleFuelSeconds = 40f;
    public const float DefaultLowFuelReturnRatio = 0.10f;
    public float MaxFuelSeconds = DefaultBattleFuelSeconds;
    public float LowFuelReturnRatio = DefaultLowFuelReturnRatio;
    public float RefuelSeconds = 14f;

    private Vector3 flyTarget;
    private bool hasTarget = false;
    private float _unitScanTimer = 0f;
    private float _airBuildingScanTimer = 0f;
    private const float AirScanInterval = 0.4f;
    private const float AirTurnSpeed = 6.5f;
    private const float LandingDistance = 1.1f;
    private const float ReturnFuelReserveSeconds = 2f;
    protected virtual float AttackRunSpeedMultiplier => 0.3f;

    private GameObject _shadowDisc;
    private Material _shadowMaterial;
    private TrailRenderer _trail;
    private bool _parked = false;
    private bool _returningToRefuel = false;
    private bool _warnedLowFuel = false;
    private bool _warnedNoAirfield = false;

    public Airfield HomeAirfield { get; private set; }
    public float CurrentFuel { get; private set; }
    public float FuelRatio => MaxFuelSeconds <= 0f ? 1f : Mathf.Clamp01(CurrentFuel / MaxFuelSeconds);
    public bool IsParked => _parked;
    public bool IsReturningToRefuel => _returningToRefuel;
    public virtual bool RequiresAirfieldSlot => true;
    public bool CanParkAtAirfield => RequiresAirfieldSlot && !_parked && !_returningToRefuel && CurrentHP > 0 && EnsureUsableHomeAirfield();
    public bool CanReturnToAirfield => CanParkAtAirfield;

    protected override void Awake()
    {
        base.Awake();
        bFlying = true;
        EnsureAirSelectionCollider();
        if (Agent != null) { Agent.enabled = false; }
    }

    void EnsureAirSelectionCollider()
    {
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = 1;
            capsule.center = Vector3.zero;
            capsule.radius = Mathf.Max(capsule.radius, 2.2f);
            capsule.height = Mathf.Max(capsule.height, 1.2f);
            return;
        }

        var sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();
        sphere.center = Vector3.zero;
        sphere.radius = Mathf.Max(sphere.radius, 2.2f);
    }

    protected override void Start()
    {
        base.Start();
        MaxFuelSeconds = Mathf.Max(8f, MaxFuelSeconds);
        LowFuelReturnRatio = Mathf.Clamp(LowFuelReturnRatio, 0.01f, 1f);
        RefuelSeconds = Mathf.Max(2f, RefuelSeconds);
        if (CurrentFuel <= 0f || CurrentFuel > MaxFuelSeconds)
            CurrentFuel = MaxFuelSeconds;

        NormalizeAircraftVisualForward();
        CreateAircraftMarkings();
        CreateGroundShadow();
        CreateContrail();

        if (!RequiresAirfieldSlot)
        {
            ClearHomeAirfield();
            SetAirborneHeight(true);
            return;
        }

        if (HomeAirfield == null)
            TryBindNearestAirfield();

        if (HomeAirfield != null)
            ParkAtHomeAirfield(true);
        else
            SetAirborneHeight(true);
    }

    void NormalizeAircraftVisualForward()
    {
        Transform visualRoot = transform.Find("Model");
        if (visualRoot == null) return;

        Transform aircraft = FindChildRecursive(visualRoot, "KenneyAircraft");
        if (aircraft == null) return;

        Vector3 euler = aircraft.localEulerAngles;
        if (Mathf.Abs(Mathf.DeltaAngle(euler.y, 180f)) <= 2f)
            aircraft.localRotation = Quaternion.Euler(euler.x, 0f, euler.z);
    }

    void CreateAircraftMarkings()
    {
        Transform visualRoot = transform.Find("Model");
        if (visualRoot == null || visualRoot.Find("AircraftForwardStripe") != null) return;

        Color faction = bPlayerOwned ? new Color(0.20f, 0.72f, 1f, 1f) : new Color(1f, 0.24f, 0.16f, 1f);
        Color nose = bPlayerOwned ? new Color(0.82f, 0.96f, 1f, 1f) : new Color(1f, 0.64f, 0.48f, 1f);
        CreateMarkingCube(visualRoot, "AircraftForwardStripe", new Vector3(0f, 0.44f, 0.52f), new Vector3(0.18f, 0.035f, 0.30f), nose);
        CreateMarkingCube(visualRoot, "FactionStripeL", new Vector3(-0.56f, 0.38f, 0.04f), new Vector3(0.30f, 0.035f, 0.13f), faction);
        CreateMarkingCube(visualRoot, "FactionStripeR", new Vector3(0.56f, 0.38f, 0.04f), new Vector3(0.30f, 0.035f, 0.13f), faction);
    }

    void CreateMarkingCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    void CreateContrail()
    {
        var go = new GameObject("Contrail");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.05f, -0.55f);
        _trail = go.AddComponent<TrailRenderer>();
        _trail.time = 1.4f;
        _trail.startWidth = 0.45f;
        _trail.endWidth = 0.05f;
        _trail.minVertexDistance = 0.4f;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        _trail.material = new Material(sh);
        _trail.startColor = new Color(1f, 1f, 1f, 0.75f);
        _trail.endColor = new Color(1f, 1f, 1f, 0f);
        _trail.enabled = !_parked;
    }

    void CreateGroundShadow()
    {
        _shadowDisc = FxResources.MakeGroundDisc(null, "AirShadow", 1.2f,
            new Color(0f, 0f, 0f, 0.42f), FxResources.DiscStyle.SoftDisc);
        _shadowMaterial = _shadowDisc.GetComponent<Renderer>().sharedMaterial;
    }

    void LateUpdate()
    {
        if (_trail != null)
            _trail.enabled = !_parked;

        if (_shadowDisc != null)
        {
            _shadowDisc.SetActive(!_parked);
            if (!_parked)
            {
                Vector3 pos = transform.position;
                _shadowDisc.transform.position = new Vector3(pos.x, 0.05f, pos.z);
                float k = Mathf.InverseLerp(2f, FlyHeight, pos.y);
                float scale = Mathf.Lerp(1.4f, 2.4f, k);
                float selectBoost = IsSelected ? 1.25f : 1f;
                _shadowDisc.transform.localScale = new Vector3(scale * selectBoost, scale * selectBoost, 1f);
                if (_shadowMaterial != null)
                {
                    float alpha = Mathf.Lerp(IsSelected ? 0.72f : 0.55f, IsSelected ? 0.48f : 0.32f, k);
                    _shadowMaterial.color = IsSelected
                        ? new Color(1f, 0.78f, 0.18f, alpha)
                        : new Color(0f, 0f, 0f, alpha);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (HomeAirfield != null)
            HomeAirfield.ReleaseAircraft(this);
        if (_shadowDisc != null)
            Object.Destroy(_shadowDisc);
    }

    public void SetHomeAirfieldFromAirport(Airfield airfield)
    {
        if (!RequiresAirfieldSlot)
        {
            ClearHomeAirfield();
            airfield?.ReleaseAircraft(this);
            return;
        }

        if (HomeAirfield == airfield) return;
        if (HomeAirfield != null)
            HomeAirfield.ReleaseAircraft(this);
        HomeAirfield = airfield;
        if (CurrentFuel <= 0f)
            CurrentFuel = MaxFuelSeconds;
    }

    public void NotifyHomeAirfieldDestroyed(Airfield airfield)
    {
        if (HomeAirfield != airfield) return;
        HomeAirfield = null;
        _returningToRefuel = false;
        if (_parked)
        {
            _parked = false;
            SetAirborneHeight(true);
        }
    }

    public override void ApplyMoveCommand(Vector3 dest)
    {
        BeginFlightFromParking();
        flyTarget = new Vector3(dest.x, FlyHeight, dest.z);
        hasTarget = true;
        CurrentCommand = CommandType.Move;
        AttackTarget = null;
        buildingTarget = null;
    }

    protected override void UpdateAI()
    {
        if (HandleFuelAndReturn()) return;

        SetAirborneHeight(false);
        if (CurrentCommand == CommandType.Move && hasTarget)
        {
            MoveTowardAirTarget(flyTarget, MoveSpeed);
            if (Vector3.Distance(transform.position, flyTarget) < 0.5f)
            {
                hasTarget = false;
                CurrentCommand = CommandType.None;
            }
        }
        else if (CurrentCommand == CommandType.Attack || CurrentCommand == CommandType.AttackMove)
        {
            BeginFlightFromParking();
            UpdateAirAttack();
        }
    }

    bool HandleFuelAndReturn()
    {
        if (!RequiresAirfieldSlot)
        {
            _returningToRefuel = false;
            if (_parked)
                BeginFlightFromParking();
            CurrentFuel = MaxFuelSeconds;
            return false;
        }

        if (CurrentFuel <= 0f)
        {
            CrashFromFuelLoss();
            return true;
        }

        if (_parked)
        {
            RefuelWhileParked();
            SnapToParkingSpot();
            if (CurrentCommand == CommandType.Move || CurrentCommand == CommandType.Attack || CurrentCommand == CommandType.AttackMove)
            {
                BeginFlightFromParking();
                return false;
            }
            return true;
        }

        CurrentFuel = Mathf.Max(0f, CurrentFuel - Time.deltaTime);
        if (CurrentFuel <= 0f)
        {
            CrashFromFuelLoss();
            return true;
        }

        if (!_returningToRefuel && ShouldReturnForFuel())
            BeginReturnToRefuel(true);

        if (_returningToRefuel)
        {
            UpdateReturnToRefuel();
            return true;
        }

        return false;
    }

    bool ShouldReturnForFuel()
    {
        if (FuelRatio <= LowFuelReturnRatio)
            return true;

        if (!IsUsableAirfield(HomeAirfield))
            return false;

        Vector3 approach = HomeAirfield.GetAirApproachPoint(this, FlyHeight);
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(approach.x, 0f, approach.z);
        float returnSpeed = Mathf.Max(0.1f, MoveSpeed * 1.08f);
        float fuelNeededToReturn = Vector3.Distance(flatSelf, flatTarget) / returnSpeed + ReturnFuelReserveSeconds;
        return CurrentFuel <= fuelNeededToReturn;
    }

    void RefuelWhileParked()
    {
        float rate = MaxFuelSeconds / Mathf.Max(2f, RefuelSeconds);
        CurrentFuel = Mathf.Min(MaxFuelSeconds, CurrentFuel + rate * Time.deltaTime);
        if (CurrentFuel >= MaxFuelSeconds * 0.98f)
        {
            _warnedLowFuel = false;
            _warnedNoAirfield = false;
        }
    }

    public bool RequestParkAtAirfield()
    {
        if (!RequiresAirfieldSlot)
            return false;

        if (_parked || _returningToRefuel || CurrentHP <= 0)
            return false;

        return BeginReturnToRefuel(false);
    }

    public bool RequestReturnToAirfield()
    {
        return RequestParkAtAirfield();
    }

    bool BeginReturnToRefuel(bool lowFuel)
    {
        if (!EnsureUsableHomeAirfield())
        {
            if (bPlayerOwned && !_warnedNoAirfield)
            {
                RTSHUD.Instance?.ShowAlert(lowFuel ? "飞机油量不足，但没有可用停机场" : "没有可用停机场，无法停机");
                _warnedNoAirfield = true;
            }
            return false;
        }

        _returningToRefuel = true;
        CurrentCommand = CommandType.Move;
        AttackTarget = null;
        buildingTarget = null;
        hasTarget = false;
        if (bPlayerOwned)
        {
            if (lowFuel && !_warnedLowFuel)
            {
                RTSHUD.Instance?.ShowAlert($"{DisplayName}油量不足，正在返场加油");
                _warnedLowFuel = true;
            }
            else if (!lowFuel)
            {
                RTSHUD.Instance?.ShowAlert($"{DisplayName}正在返回停机场");
            }
        }
        return true;
    }

    void UpdateReturnToRefuel()
    {
        if (!EnsureUsableHomeAirfield())
        {
            _returningToRefuel = false;
            return;
        }

        Vector3 approach = HomeAirfield.GetAirApproachPoint(this, FlyHeight);
        MoveTowardAirTarget(approach, MoveSpeed * 1.08f);
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(approach.x, 0f, approach.z);
        if (Vector3.Distance(flatSelf, flatTarget) <= LandingDistance)
            ParkAtHomeAirfield(false);
    }

    bool EnsureUsableHomeAirfield()
    {
        if (!RequiresAirfieldSlot)
            return false;

        if (IsUsableAirfield(HomeAirfield)) return true;
        HomeAirfield = null;
        return TryBindNearestAirfield();
    }

    bool TryBindNearestAirfield()
    {
        if (!RequiresAirfieldSlot)
            return false;

        var gm = GameManager.Instance;
        var allBuildings = gm != null ? gm.GetAllBuildings() : null;
        if (allBuildings == null) return false;

        Airfield best = null;
        float bestDist = float.MaxValue;
        foreach (var b in allBuildings)
        {
            var airfield = b as Airfield;
            if (!IsUsableAirfield(airfield)) continue;
            if (!airfield.CanAcceptAircraft(this)) continue;
            float d = Vector3.Distance(transform.position, airfield.transform.position);
            if (d < bestDist)
            {
                best = airfield;
                bestDist = d;
            }
        }

        return best != null && best.TryAcceptAircraft(this);
    }

    bool IsUsableAirfield(Airfield airfield)
    {
        return airfield != null
            && airfield.GetHP() > 0
            && !airfield.bUnderConstruction
            && airfield.bPlayerOwned == bPlayerOwned;
    }

    void BeginFlightFromParking()
    {
        if (!_parked) return;
        _parked = false;
        _returningToRefuel = false;
        SetAirborneHeight(true);
        if (_trail != null) _trail.Clear();
    }

    void SetAirborneHeight(bool snap)
    {
        Vector3 p = transform.position;
        p.y = snap ? FlyHeight : Mathf.MoveTowards(p.y, FlyHeight, Time.deltaTime * 5f);
        transform.position = p;
    }

    void ParkAtHomeAirfield(bool initial)
    {
        if (!RequiresAirfieldSlot)
            return;

        if (!EnsureUsableHomeAirfield()) return;
        _parked = true;
        _returningToRefuel = false;
        hasTarget = false;
        CurrentCommand = CommandType.None;
        AttackTarget = null;
        buildingTarget = null;
        SnapToParkingSpot();
        if (initial)
            CurrentFuel = MaxFuelSeconds;
    }

    void SnapToParkingSpot()
    {
        if (HomeAirfield == null) return;
        Vector3 spot = HomeAirfield.GetParkingSpot(this);
        transform.position = spot;
        Vector3 fwd = new Vector3(HomeAirfield.transform.forward.x, 0f, HomeAirfield.transform.forward.z);
        if (fwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    void CrashFromFuelLoss()
    {
        if (CurrentHP <= 0) return;
        if (bPlayerOwned)
            RTSHUD.Instance?.ShowAlert($"{DisplayName}燃油耗尽，坠毁");
        CurrentFuel = 0f;
        CurrentHP = 0;
        OnDeath();
    }

    protected override void OnDeath()
    {
        ClearHomeAirfield();
        base.OnDeath();
    }

    void ClearHomeAirfield()
    {
        if (HomeAirfield != null)
        {
            HomeAirfield.ReleaseAircraft(this);
            HomeAirfield = null;
        }
    }

    void MoveTowardAirTarget(Vector3 target, float speed)
    {
        Vector3 delta = target - transform.position;
        FaceAirDirection(delta);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    void FaceAirDirection(Vector3 worldDelta)
    {
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude <= 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(worldDelta.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * AirTurnSpeed);
    }

    void UpdateAirAttack()
    {
        if (AttackTarget == null || AttackTarget.IsDead())
        {
            AttackTarget = null;
            _unitScanTimer -= Time.deltaTime;
            if (_unitScanTimer <= 0f)
            {
                AttackTarget = FindNearestEnemy();
                _unitScanTimer = AirScanInterval;
            }
            if (AttackTarget == null)
            {
                if (buildingTarget == null || buildingTarget.GetHP() <= 0)
                {
                    _airBuildingScanTimer -= Time.deltaTime;
                    if (_airBuildingScanTimer <= 0f)
                    {
                        buildingTarget = FindNearestEnemyBuilding();
                        _airBuildingScanTimer = AirScanInterval;
                    }
                }
                if (buildingTarget != null)
                {
                    Vector3 bPos = new Vector3(buildingTarget.transform.position.x, FlyHeight, buildingTarget.transform.position.z);
                    float bDist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                   new Vector3(buildingTarget.transform.position.x, 0, buildingTarget.transform.position.z));
                    Vector3 bDir = (bPos - transform.position).normalized;
                    FaceAirDirection(bDir);
                    if (bDist <= AttackRange)
                    {
                        MoveTowardAirTarget(bPos, MoveSpeed * AttackRunSpeedMultiplier);
                        if (AttackTimer <= 0f)
                        {
                            var sync = GameNetworkSync.Instance;
                            if (sync == null || !sync.IsNetworkGame || sync.IsHost)
                            {
                                int prevHP = buildingTarget.GetHP();
                                buildingTarget.TakeDamage(AttackDamage);
                                if (prevHP > 0 && buildingTarget.GetHP() <= 0) AddKill();
                            }
                            PlayAttackVisuals(buildingTarget.transform.position);
                            AttackTimer = AttackInterval;
                        }
                    }
                    else
                        MoveTowardAirTarget(bPos, MoveSpeed);
                    return;
                }
                if (CurrentCommand == CommandType.AttackMove)
                {
                    flyTarget = new Vector3(CommandTarget.x, FlyHeight, CommandTarget.z);
                    hasTarget = true;
                    MoveTowardAirTarget(flyTarget, MoveSpeed);
                    if (Vector3.Distance(transform.position, flyTarget) < 0.5f)
                    {
                        hasTarget = false;
                        CurrentCommand = CommandType.None;
                    }
                }
                else
                    CurrentCommand = CommandType.None;
                return;
            }
        }

        Vector3 targetPos = new Vector3(AttackTarget.transform.position.x, FlyHeight, AttackTarget.transform.position.z);
        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(AttackTarget.transform.position.x, 0, AttackTarget.transform.position.z));
        Vector3 dir = (targetPos - transform.position).normalized;
        FaceAirDirection(dir);
        if (dist <= AttackRange)
        {
            MoveTowardAirTarget(targetPos, MoveSpeed * AttackRunSpeedMultiplier);
            if (AttackTimer <= 0f)
            {
                DoAttack(AttackTarget);
                AttackTimer = AttackInterval;
            }
        }
        else
            MoveTowardAirTarget(targetPos, MoveSpeed);
    }
}
