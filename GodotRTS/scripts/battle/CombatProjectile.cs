using Godot;

public partial class CombatProjectile : Node3D
{
    Node3D? target;
    Node3D? attacker;
    Vector3 start;
    Vector3 lastTarget;
    Color tint;
    float damage;
    float elapsed;
    float duration = 0.25f;
    float arcHeight;
    float impactRadius = 0.65f;
    float splashRadius;
    float splashFalloff = 0.5f;
    bool groundAttack;
    bool projectilePlayerOwned;
    bool applied;

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range)
        => Spawn(owner, attacker, targetNode, damageAmount, playerOwned, range, 0f, 0.5f);

    public static void Spawn(Node owner, Node3D attacker, Node3D targetNode, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var source = attacker.GlobalPosition + Vector3.Up * 1.1f;
        var destination = targetNode.GlobalPosition + Vector3.Up * 0.9f;
        var color = playerOwned
            ? new Color(1f, 0.82f, 0.26f, 1f)
            : new Color(1f, 0.32f, 0.18f, 1f);
        projectile.Configure(source, attacker, targetNode, destination, damageAmount, color, range, areaRadius, falloff, false, playerOwned);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    public static void SpawnGround(Node owner, Node3D attacker, Vector3 groundPoint, float damageAmount, bool playerOwned, float range, float areaRadius, float falloff)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var projectile = new CombatProjectile();
        var source = attacker.GlobalPosition + Vector3.Up * 1.1f;
        var destination = new Vector3(groundPoint.X, 0.08f, groundPoint.Z);
        var color = playerOwned
            ? new Color(1f, 0.68f, 0.22f, 1f)
            : new Color(1f, 0.26f, 0.16f, 1f);
        projectile.Configure(source, attacker, null, destination, damageAmount, color, range, areaRadius, falloff, true, playerOwned);
        root.AddChild(projectile);
        projectile.GlobalPosition = source;
    }

    public static void SpawnImpactEffect(Node owner, Vector3 position, Color color, float radius = 0.8f)
    {
        var root = owner.GetTree().CurrentScene ?? owner;
        var node = new Node3D
        {
            Name = "ImpactFlash",
            Scale = Vector3.One * 0.2f
        };

        var disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.06f,
                RadialSegments = 24
            },
            MaterialOverride = MakeMaterial(new Color(color.R, color.G, color.B, 0.42f), true)
        };
        node.AddChild(disc);
        root.AddChild(node);
        node.GlobalPosition = position + Vector3.Up * 0.08f;

        var tween = node.CreateTween();
        tween.TweenProperty(node, "scale", Vector3.One, 0.12);
        tween.TweenProperty(node, "scale", Vector3.One * 1.35f, 0.12);
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    void Configure(Vector3 source, Node3D sourceAttacker, Node3D? targetNode, Vector3 destination, float damageAmount, Color projectileTint, float range, float areaRadius, float falloff, bool isGroundAttack, bool sourcePlayerOwned)
    {
        start = source;
        target = targetNode;
        attacker = sourceAttacker;
        lastTarget = destination;
        damage = damageAmount;
        tint = projectileTint;
        splashRadius = Mathf.Max(0f, areaRadius);
        splashFalloff = Mathf.Clamp(falloff, 0f, 1f);
        groundAttack = isGroundAttack;
        projectilePlayerOwned = sourcePlayerOwned;
        arcHeight = range > 10f ? 5.5f : 0.8f;
        var areaFlashRadius = splashRadius > 0.05f ? Mathf.Clamp(splashRadius * 0.32f, 0.45f, 2.35f) : 0f;
        impactRadius = Mathf.Max(range > 10f ? 1.25f : 0.72f, areaFlashRadius);
        duration = Mathf.Clamp(source.DistanceTo(destination) / (range > 10f ? 34f : 48f), 0.10f, 0.9f);
    }

    public override void _Ready()
    {
        Name = "CombatProjectile";
        BattleFeedback.WeaponFire(this, start, splashRadius > 0.05f || arcHeight > 2.5f);
        var longRange = arcHeight > 2.5f;
        AddChild(new MeshInstance3D
        {
            Name = "Tracer",
            Mesh = longRange
                ? new CapsuleMesh { Radius = 0.11f, Height = 0.76f, RadialSegments = 8, Rings = 4 }
                : new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = MakeMaterial(tint, true)
        });

        AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = tint,
            LightEnergy = 0.35f,
            OmniRange = 4f
        });
    }

    public override void _Process(double delta)
    {
        if (applied)
            return;

        elapsed += (float)delta;
        if (GodotObject.IsInstanceValid(target))
            lastTarget = target!.GlobalPosition + Vector3.Up * 0.9f;

        var t = Mathf.Clamp(elapsed / duration, 0f, 1f);
        var next = start.Lerp(lastTarget, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * arcHeight);
        var travel = next - GlobalPosition;
        GlobalPosition = next;
        if (travel.LengthSquared() > 0.0001f)
            LookAt(GlobalPosition + travel.Normalized(), Vector3.Up, true);

        if (t >= 1f)
            Impact();
    }

    void Impact()
    {
        applied = true;
        Node3D? directTarget = null;
        var damageAmount = Mathf.RoundToInt(damage);
        if (!groundAttack && GodotObject.IsInstanceValid(target) && target!.HasMethod("ApplyDamage"))
        {
            if (target is RtsUnit targetUnit)
                BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetUnit.PlayerOwned, damageAmount);
            else if (target is RtsBuilding targetBuilding)
                BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, targetBuilding.PlayerOwned, damageAmount);
            target.Call("ApplyDamage", damage);
            directTarget = target;
        }

        if (splashRadius > 0.05f)
            ApplyAreaDamage(lastTarget, directTarget);

        SpawnImpactEffect(this, lastTarget, tint, impactRadius);
        BattleFeedback.Impact(this, lastTarget, splashRadius > 0.05f || arcHeight > 2.5f);
        QueueFree();
    }

    void ApplyAreaDamage(Vector3 center, Node3D? directTarget)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        foreach (var unit in manager.GetUnits(!projectilePlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead || unit == attacker || unit == directTarget)
                continue;
            if (BattleUnitCatalog.IsAirUnit(unit.UnitKey))
                continue;

            var distance = GroundDistance(center, unit.GlobalPosition);
            if (distance > splashRadius)
                continue;

            var dealt = DamageAtDistance(distance);
            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, unit.PlayerOwned, Mathf.RoundToInt(dealt));
            unit.ApplyDamage(dealt);
        }

        foreach (var building in manager.GetBuildings(!projectilePlayerOwned))
        {
            if (!GodotObject.IsInstanceValid(building) || building.Health <= 0f || building == directTarget)
                continue;

            var distance = DistanceToBuildingGround(center, building);
            if (distance > splashRadius)
                continue;

            var dealt = DamageAtDistance(distance);
            BattleGameManager.Instance?.RecordDamage(projectilePlayerOwned, building.PlayerOwned, Mathf.RoundToInt(dealt));
            building.ApplyDamage(dealt);
        }
    }

    float DamageAtDistance(float distance)
    {
        var t = splashRadius <= 0.05f ? 0f : Mathf.Clamp(distance / splashRadius, 0f, 1f);
        return damage * Mathf.Lerp(1f, splashFalloff, t);
    }

    static float GroundDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static float DistanceToBuildingGround(Vector3 point, RtsBuilding building)
    {
        if (building.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape is BoxShape3D box)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.X - building.GlobalPosition.X) - box.Size.X * 0.5f);
            var dz = Mathf.Max(0f, Mathf.Abs(point.Z - building.GlobalPosition.Z) - box.Size.Z * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        return GroundDistance(point, building.GlobalPosition);
    }

    static StandardMaterial3D MakeMaterial(Color color, bool emissive)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.58f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = emissive,
            Emission = color,
            EmissionEnergyMultiplier = emissive ? 1.8f : 0f
        };
    }
}
