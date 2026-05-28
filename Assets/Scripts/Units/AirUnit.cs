using UnityEngine;
public class AirUnit : RTSUnit
{
    public float FlyHeight = 8f;
    private Vector3 flyTarget;
    private bool hasTarget = false;
    private float _unitScanTimer  = 0f;
    private float _airBuildingScanTimer = 0f;
    private const float AirScanInterval = 0.4f;

    protected override void Awake()
    {
        base.Awake();
        bFlying = true;
        EnsureAirSelectionCollider();
        if (Agent != null) { Agent.enabled = false; }
    }

    private GameObject _shadowDisc;
    private Material _shadowMaterial;
    private TrailRenderer _trail;

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
        Vector3 p = transform.position;
        p.y = FlyHeight;
        transform.position = p;
        CreateGroundShadow();
        CreateContrail();
    }

    /// <summary>飞机尾迹（白色凝结尾流），从机尾延伸出 1.5s 渐隐。</summary>
    void CreateContrail()
    {
        var go = new GameObject("Contrail");
        go.transform.SetParent(transform, false);
        // 机尾偏后下方
        go.transform.localPosition = new Vector3(0f, -0.05f, -0.55f);
        _trail = go.AddComponent<TrailRenderer>();
        _trail.time = 1.4f;
        _trail.startWidth = 0.45f;
        _trail.endWidth = 0.05f;
        _trail.minVertexDistance = 0.4f;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        // 程序化材质：白色 → 透明
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
        var mat = new Material(sh);
        _trail.material = mat;
        _trail.startColor = new Color(1f, 1f, 1f, 0.75f);
        _trail.endColor   = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>地面投影圆：跟随飞机 X/Z 位置，固定 y=0.05，半透明黑色圆盘。</summary>
    void CreateGroundShadow()
    {
        // FX 工厂 Quad+SoftDisc（边缘软过渡实心圆）替代 Cylinder；不作为子节点（独立世界空间，飞机 y=8、影子 y=0）
        _shadowDisc = FxResources.MakeGroundDisc(null, "AirShadow", 1.2f,
            new Color(0f, 0f, 0f, 0.42f), FxResources.DiscStyle.SoftDisc);
        _shadowMaterial = _shadowDisc.GetComponent<Renderer>().sharedMaterial;
    }

    void LateUpdate()
    {
        if (_shadowDisc != null)
        {
            // 跟随飞机 X/Z，固定 y=0.05
            Vector3 pos = transform.position;
            _shadowDisc.transform.position = new Vector3(pos.x, 0.05f, pos.z);
            // 飞行高度越高，影子越大越淡
            float k = Mathf.InverseLerp(2f, FlyHeight, pos.y);
            float scale = Mathf.Lerp(1.4f, 2.4f, k);
            float selectBoost = IsSelected ? 1.25f : 1f;
            // Quad 朝上后 X/Y 是直径维度，Z=1 不参与缩放
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

    void OnDestroy()
    {
        if (_shadowDisc != null) Object.Destroy(_shadowDisc);
    }

    public override void ApplyMoveCommand(Vector3 dest)
    {
        flyTarget = new Vector3(dest.x, FlyHeight, dest.z);
        hasTarget = true;
        CurrentCommand = CommandType.Move;
        AttackTarget = null;
    }

    protected override void UpdateAI()
    {
        if (Mathf.Abs(transform.position.y - FlyHeight) > 0.1f)
        {
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, FlyHeight, Time.deltaTime * 5f);
            transform.position = p;
        }
        if (CurrentCommand == CommandType.Move && hasTarget)
        {
            transform.position = Vector3.MoveTowards(transform.position, flyTarget, MoveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, flyTarget) < 0.5f)
            { hasTarget = false; CurrentCommand = CommandType.None; }
        }
        else if (CurrentCommand == CommandType.Attack || CurrentCommand == CommandType.AttackMove)
            UpdateAirAttack();
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
                // 无单位目标时攻击最近的敌方建筑
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
                    float bDist  = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                    new Vector3(buildingTarget.transform.position.x, 0, buildingTarget.transform.position.z));
                    Vector3 bDir = (bPos - transform.position).normalized;
                    if (bDir != Vector3.zero)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(bDir), Time.deltaTime * 4f);
                    if (bDist <= AttackRange)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, bPos, MoveSpeed * 0.3f * Time.deltaTime);
                        if (AttackTimer <= 0f)
                        {
                            // 联机：只有 Host 计算建筑伤害
                            var _as = GameNetworkSync.Instance;
                            if (_as == null || !_as.IsNetworkGame || _as.IsHost)
                            {
                                int prevHP = buildingTarget.GetHP();
                                buildingTarget.TakeDamage(AttackDamage);
                                if (prevHP > 0 && buildingTarget.GetHP() <= 0) AddKill();
                            }
                            StartCoroutine(FlashAttackLine(buildingTarget.transform.position));
                            AttackTimer = AttackInterval;
                        }
                    }
                    else
                        transform.position = Vector3.MoveTowards(transform.position, bPos, MoveSpeed * Time.deltaTime);
                    return;
                }
                if (CurrentCommand == CommandType.AttackMove)
                {
                    // 无目标时继续飞向命令目标点
                    flyTarget = new Vector3(CommandTarget.x, FlyHeight, CommandTarget.z);
                    hasTarget = true;
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
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
        if (dist <= AttackRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, MoveSpeed * 0.3f * Time.deltaTime);
            if (AttackTimer <= 0f) { DoAttack(AttackTarget); AttackTimer = AttackInterval; }
        }
        else
            transform.position = Vector3.MoveTowards(transform.position, targetPos, MoveSpeed * Time.deltaTime);
    }
}
