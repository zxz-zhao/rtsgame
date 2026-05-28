using UnityEngine;
public class MainBase : RTSBuilding
{
    private float healTimer = 0f;
    private const float HealInterval = 1f;
    private const int HealAmount = 8;      // 单位治疗量（HP/s）
    private const int RepairAmount = 10;   // 建筑修复量（HP/s）
    private const float HealRadius = 50f;
    private const float RepairRadius = 60f;

    private GameObject _healAura;
    private Material _healAuraMat;
    private Color _healAuraBaseColor;
    private float _healAuraVisibleTimer = 0f;

    protected override float DesiredVisualHeight => 10f;
    protected override float DesiredVisualFootprint => 12f;

    protected override void Start()
    {
        DisplayName = "主基地";
        MaxHP = 3000; GoldCost = 0;
        PopCapBonus = 10; bIsMainBase = true;
        GoldIncomeAmount = 30; GoldIncomeInterval = 5f;
        base.Start();
        // 仅己方基地显示治疗光环（避免敌方基地下方也有绿光）
        if (bPlayerOwned) CreateHealAura();
    }

    void CreateHealAura()
    {
        // 用 FX 工厂 SoftDisc（边缘软过渡圆盘）替代旧 Cylinder
        _healAuraBaseColor = new Color(0.42f, 0.56f, 0.30f, 0.045f);
        _healAura = FxResources.MakeGroundDisc(transform, "HealAura", HealRadius, _healAuraBaseColor, FxResources.DiscStyle.SoftDisc, 0.035f);
        _healAuraMat = _healAura.GetComponent<Renderer>().sharedMaterial;
        _healAura.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        // 联机：只有己方主基地（持有方）负责治疗，避免 remote 副本双重治疗
        var _hs = GameNetworkSync.Instance;
        if (_hs != null && _hs.IsNetworkGame && !bPlayerOwned) return;
        healTimer += Time.deltaTime;
        bool didHealThisTick = false;
        if (healTimer >= HealInterval)
        {
            healTimer = 0f;
            // 治疗附近友方单位
            var units = GameManager.Instance?.GetAllUnits();
            if (units != null)
                foreach (var u in units)
                    if (u != null && u.IsPlayerOwned() == bPlayerOwned
                        && Vector3.Distance(transform.position, u.transform.position) < HealRadius)
                    {
                        u.TakeDamage(-HealAmount);
                        didHealThisTick = true;
                    }
            // 修复附近友方建筑
            var buildings = GameManager.Instance?.GetAllBuildings();
            if (buildings != null)
                foreach (var b in buildings)
                    if (b != null && b != this && b.bPlayerOwned == bPlayerOwned
                        && b.GetHP() < b.GetMaxHP()
                        && Vector3.Distance(transform.position, b.transform.position) < RepairRadius)
                    {
                        b.TakeDamage(-RepairAmount);
                        didHealThisTick = true;
                    }
            if (didHealThisTick)
            {
                _healPulseImpulse = 1f;
                _healAuraVisibleTimer = 0.8f;
                if (_healAura != null) _healAura.SetActive(true);
            }
        }
        UpdateHealAura();
    }

    private float _healPulseImpulse = 0f;

    void UpdateHealAura()
    {
        if (_healAura == null || _healAuraMat == null) return;
        if (_healAuraVisibleTimer > 0f)
            _healAuraVisibleTimer = Mathf.Max(0f, _healAuraVisibleTimer - Time.deltaTime);

        if (_healPulseImpulse > 0f)
        {
            _healPulseImpulse = Mathf.Max(0f, _healPulseImpulse - Time.deltaTime / 0.6f);
        }
        if (_healAuraVisibleTimer <= 0f && _healPulseImpulse <= 0f)
        {
            _healAura.SetActive(false);
            return;
        }

        float alpha = Mathf.Clamp(0.018f + _healPulseImpulse * 0.055f, 0.018f, 0.075f);
        _healAuraMat.color = new Color(_healAuraBaseColor.r, _healAuraBaseColor.g, _healAuraBaseColor.b, alpha);
    }
}
