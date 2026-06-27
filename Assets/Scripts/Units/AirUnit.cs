using UnityEngine;

/// <summary>
/// Shared aircraft behavior covering fuel, parking, return-to-base logic, and simplified airborne combat.
/// </summary>
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
    private bool _factoryTransferActive = false;
    private bool _factoryTransferClimbComplete = false;
    private Vector3 _factoryTransferClimbPoint;
    private bool _pendingMoveAfterFactoryTransfer = false;
    private Vector3 _pendingMoveAfterFactoryTransferDestination;

    public Airfield HomeAirfield { get; private set; }
    public float CurrentFuel { get; private set; }
    public float FuelRatio => MaxFuelSeconds <= 0f ? 1f : Mathf.Clamp01(CurrentFuel / MaxFuelSeconds);
    public bool IsParked => _parked;
    public bool IsReturningToRefuel => _returningToRefuel;
    public virtual bool RequiresAirfieldSlot => true;
    public bool CanParkAtAirfield => RequiresAirfieldSlot && !_parked && !_returningToRefuel && CurrentHP > 0 && EnsureUsableHomeAirfield();
    public bool CanReturnToAirfield => CanParkAtAirfield;

    /// <summary>
    /// Marks the unit as airborne, upgrades its selection collider, and disables NavMesh steering.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        bFlying = true;
        EnsureAirSelectionCollider();
        if (Agent != null) { Agent.enabled = false; }
    }

    /// <summary>
    /// Enlarges or creates a collider suitable for selecting aircraft from a top-down camera.
    /// </summary>
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

    /// <summary>
    /// Normalizes fuel state, creates aircraft helper visuals, and binds or parks at a home airfield if needed.
    /// </summary>
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

        if (_factoryTransferActive)
            return;

        if (HomeAirfield != null)
            ParkAtHomeAirfield(true);
        else
            SetAirborneHeight(true);
    }

    /// <summary>
    /// Fixes imported aircraft visuals that spawn rotated 180 degrees around Y.
    /// </summary>
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

    /// <summary>
    /// Adds simple faction-colored stripes to aircraft models that do not have readable markings.
    /// </summary>
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

    /// <summary>
    /// Creates one lightweight colored cube used as a procedural aircraft marking.
    /// </summary>
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

    /// <summary>
    /// Recursively searches for a named child transform within the supplied hierarchy.
    /// </summary>
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

    /// <summary>
    /// Creates the trail renderer used as the aircraft contrail effect while airborne.
    /// </summary>
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

    /// <summary>
    /// Creates the soft-disc shadow projected beneath the aircraft.
    /// </summary>
    void CreateGroundShadow()
    {
        _shadowDisc = FxResources.MakeGroundDisc(null, "AirShadow", 1.2f,
            new Color(0f, 0f, 0f, 0.42f), FxResources.DiscStyle.SoftDisc);
        _shadowMaterial = _shadowDisc.GetComponent<Renderer>().sharedMaterial;
    }

    /// <summary>
    /// Refreshes airborne-only helper visuals such as contrails and the ground shadow after movement is resolved.
    /// </summary>
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

    /// <summary>
    /// Releases the home-airfield slot and destroys runtime helper visuals when the aircraft is removed.
    /// </summary>
    void OnDestroy()
    {
        if (HomeAirfield != null)
            HomeAirfield.ReleaseAircraft(this);
        if (_shadowDisc != null)
            Object.Destroy(_shadowDisc);
    }

    /// <summary>
    /// Reassigns the aircraft to a new home airfield, releasing any previous slot reservation.
    /// </summary>
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

    /// <summary>
    /// Clears the home-airfield binding when that airfield is destroyed and lifts parked aircraft back into the air.
    /// </summary>
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

    /// <summary>
    /// Converts a move order into an airborne target point and breaks the unit out of parking state if needed.
    /// </summary>
    public override void ApplyMoveCommand(Vector3 dest)
    {
        if (_factoryTransferActive)
        {
            _pendingMoveAfterFactoryTransfer = true;
            _pendingMoveAfterFactoryTransferDestination = dest;
            return;
        }

        BeginFlightFromParking();
        flyTarget = new Vector3(dest.x, FlyHeight, dest.z);
        hasTarget = true;
        CurrentCommand = CommandType.Move;
        AttackTarget = null;
        buildingTarget = null;
    }

    /// <summary>
    /// Executes aircraft-specific movement and combat logic after fuel handling is evaluated.
    /// </summary>
    protected override void UpdateAI()
    {
        if (_factoryTransferActive)
        {
            UpdateFactoryTransfer();
            return;
        }

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

    /// <summary>
    /// Consumes fuel, triggers return-to-base behavior, and handles refueling or crashes when necessary.
    /// </summary>
    protected bool HandleFuelAndReturn()
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

    /// <summary>
    /// Starts the post-production ferry flight from the factory launch point to the assigned airfield.
    /// </summary>
    public bool BeginFactoryTransferToAirfield(Airfield airfield, Vector3 launchPoint, Vector3 climbPoint)
    {
        if (!RequiresAirfieldSlot || airfield == null)
            return false;
        if (!airfield.TryAcceptAircraft(this))
            return false;

        _factoryTransferActive = true;
        _factoryTransferClimbComplete = false;
        _factoryTransferClimbPoint = new Vector3(climbPoint.x, Mathf.Max(FlyHeight, climbPoint.y), climbPoint.z);
        _returningToRefuel = false;
        _parked = false;
        hasTarget = false;
        CurrentCommand = CommandType.None;
        AttackTarget = null;
        buildingTarget = null;
        CurrentFuel = MaxFuelSeconds;

        launchPoint.y = Mathf.Max(launchPoint.y, 0.35f);
        transform.position = launchPoint;

        Vector3 lookDir = _factoryTransferClimbPoint - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        if (_trail != null)
            _trail.Clear();

        return true;
    }

    /// <summary>
    /// Predicts whether the current fuel state is low enough that the aircraft should head home now.
    /// </summary>
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

    /// <summary>
    /// Restores fuel over time while the aircraft is parked at its home airfield.
    /// </summary>
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

    /// <summary>
    /// Requests an immediate return to base for parking and refueling.
    /// </summary>
    public bool RequestParkAtAirfield()
    {
        if (!RequiresAirfieldSlot)
            return false;

        if (_parked || _returningToRefuel || CurrentHP <= 0)
            return false;

        return BeginReturnToRefuel(false);
    }

    /// <summary>
    /// Alias used by callers that conceptually want a return-to-airfield command.
    /// </summary>
    public bool RequestReturnToAirfield()
    {
        return RequestParkAtAirfield();
    }

    /// <summary>
    /// Starts the return-to-airfield flow and raises the appropriate player-facing warning or status alert.
    /// </summary>
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

    /// <summary>
    /// Flies a freshly produced aircraft from the factory launch point to its home airfield before normal parking behavior begins.
    /// </summary>
    void UpdateFactoryTransfer()
    {
        if (!EnsureUsableHomeAirfield())
        {
            _factoryTransferActive = false;
            SetAirborneHeight(true);
            ReplayPendingFactoryTransferMove();
            return;
        }

        if (!_factoryTransferClimbComplete)
        {
            MoveTowardAirTarget(_factoryTransferClimbPoint, MoveSpeed);
            if (Vector3.Distance(transform.position, _factoryTransferClimbPoint) <= 0.35f)
                _factoryTransferClimbComplete = true;
            return;
        }

        Vector3 approach = HomeAirfield.GetAirApproachPoint(this, FlyHeight);
        MoveTowardAirTarget(approach, MoveSpeed * 1.05f);

        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(approach.x, 0f, approach.z);
        if (Vector3.Distance(flatSelf, flatTarget) > LandingDistance)
            return;

        _factoryTransferActive = false;
        ParkAtHomeAirfield(false);
        ReplayPendingFactoryTransferMove();
    }

    /// <summary>
    /// Steers the aircraft back toward its assigned approach point and parks once it reaches landing distance.
    /// </summary>
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

    /// <summary>
    /// Validates the current home airfield or tries to bind a new compatible one if the old assignment is unusable.
    /// </summary>
    bool EnsureUsableHomeAirfield()
    {
        if (!RequiresAirfieldSlot)
            return false;

        if (IsUsableAirfield(HomeAirfield)) return true;
        HomeAirfield = null;
        return TryBindNearestAirfield();
    }

    /// <summary>
    /// Searches for the nearest friendly airfield that can accept this aircraft and binds it.
    /// </summary>
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

    /// <summary>
    /// Returns whether an airfield is alive, completed, and owned by the same faction as this aircraft.
    /// </summary>
    bool IsUsableAirfield(Airfield airfield)
    {
        return airfield != null
            && airfield.GetHP() > 0
            && !airfield.bUnderConstruction
            && airfield.bPlayerOwned == bPlayerOwned;
    }

    /// <summary>
    /// Lifts a parked aircraft back into active flight and clears any stale contrail segment.
    /// </summary>
    protected void BeginFlightFromParking()
    {
        if (!_parked) return;
        _parked = false;
        _returningToRefuel = false;
        SetAirborneHeight(true);
        if (_trail != null) _trail.Clear();
    }

    /// <summary>
    /// Moves the aircraft toward its configured cruise height, optionally snapping instantly.
    /// </summary>
    protected void SetAirborneHeight(bool snap)
    {
        Vector3 p = transform.position;
        p.y = snap ? FlyHeight : Mathf.MoveTowards(p.y, FlyHeight, Time.deltaTime * 5f);
        transform.position = p;
    }

    /// <summary>
    /// Finalizes the landing flow by parking the aircraft, clearing combat state, and optionally refilling fuel.
    /// </summary>
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

    /// <summary>
    /// Places the aircraft exactly on its assigned parking spot and aligns it with the airfield forward direction.
    /// </summary>
    void SnapToParkingSpot()
    {
        if (HomeAirfield == null) return;
        Vector3 spot = HomeAirfield.GetParkingSpot(this);
        transform.position = spot;
        Vector3 fwd = new Vector3(HomeAirfield.transform.forward.x, 0f, HomeAirfield.transform.forward.z);
        if (fwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    /// <summary>
    /// Handles the fatal fuel-depletion case, including the optional HUD alert for player-owned aircraft.
    /// </summary>
    void CrashFromFuelLoss()
    {
        if (CurrentHP <= 0) return;
        if (bPlayerOwned)
            RTSHUD.Instance?.ShowAlert($"{DisplayName}燃油耗尽，坠毁");
        CurrentFuel = 0f;
        CurrentHP = 0;
        OnDeath();
    }

    /// <summary>
    /// Releases any airfield reservation before falling back to the shared unit death handling.
    /// </summary>
    protected override void OnDeath()
    {
        ClearHomeAirfield();
        base.OnDeath();
    }

    /// <summary>
    /// Releases the current home-airfield reservation without attempting to bind a replacement.
    /// </summary>
    void ClearHomeAirfield()
    {
        if (HomeAirfield != null)
        {
            HomeAirfield.ReleaseAircraft(this);
            HomeAirfield = null;
        }
    }

    void ReplayPendingFactoryTransferMove()
    {
        if (!_pendingMoveAfterFactoryTransfer)
            return;

        Vector3 dest = _pendingMoveAfterFactoryTransferDestination;
        _pendingMoveAfterFactoryTransfer = false;
        ApplyMoveCommand(dest);
    }

    /// <summary>
    /// Rotates toward and advances toward an airborne target position at the supplied speed.
    /// </summary>
    protected void MoveTowardAirTarget(Vector3 target, float speed)
    {
        Vector3 delta = target - transform.position;
        FaceAirDirection(delta);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    /// <summary>
    /// Smoothly yaws the aircraft toward a planar travel or attack direction.
    /// </summary>
    void FaceAirDirection(Vector3 worldDelta)
    {
        worldDelta.y = 0f;
        if (worldDelta.sqrMagnitude <= 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(worldDelta.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * AirTurnSpeed);
    }

    /// <summary>
    /// Runs the simplified aircraft attack loop, including unit scanning, building fallback, and attack-move behavior.
    /// </summary>
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
                            DoAttackBuilding(buildingTarget);
                            AttackTimer = ConsumeAttackCooldown();
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
                AttackTimer = ConsumeAttackCooldown();
            }
        }
        else
            MoveTowardAirTarget(targetPos, MoveSpeed);
    }
}
