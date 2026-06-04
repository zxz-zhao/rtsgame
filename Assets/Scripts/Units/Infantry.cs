using UnityEngine;
public class Infantry : RTSUnit
{
    protected override float DesiredVisualHeight => 3.6f;
    protected override float DesiredVisualFootprint => 2.0f;

    protected override void Awake()
    {
        DisplayName = "步兵";
        MaxHP = 200; AttackDamage = 25; AttackRange = 8f;
        AttackInterval = 1f; SightRange = 15f;
        GoldCost = 100; PopCost = 1; MoveSpeed = 8f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 82f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.34f;
        ProjectileTint = new Color(1f, 0.86f, 0.28f, 1f);
        TracerDuration = 0.04f;
        base.Awake();
    }

    protected override void ApplyMilitaryTint()
    {
        Color uniform = bPlayerOwned ? new Color(0.23f, 0.34f, 0.18f) : new Color(0.46f, 0.39f, 0.24f);
        Color faction = bPlayerOwned ? new Color(0.16f, 0.62f, 1f) : new Color(1f, 0.24f, 0.18f);

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (ShouldSkipInfantryTint(renderer.transform)) continue;

            if (HasAncestorNamed(renderer.transform, "FactionPlate"))
            {
                RendererColorUtil.TrySetColor(renderer, faction);
                continue;
            }

            if (HasAncestorNamed(renderer.transform, "KenneyWeapon")
                || HasAncestorNamed(renderer.transform, "SkinFace")
                || HasAncestorNamed(renderer.transform, "SkinLeftHand")
                || HasAncestorNamed(renderer.transform, "SkinRightHand")
                || renderer.name.IndexOf("Rifle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RendererColorUtil.ClearPropertyBlock(renderer);
                continue;
            }

            RendererColorUtil.TrySetColor(renderer, uniform);
        }
    }

    static bool ShouldSkipInfantryTint(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionRing" || n == "SelectionCircle"
                || n == "HPLabel" || n == "UnitLabel" || n == "AttackLine"
                || n.EndsWith("Aura") || n.EndsWith("Disc") || n.EndsWith("Ring"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static bool HasAncestorNamed(Transform t, string name)
    {
        while (t != null)
        {
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }
}
