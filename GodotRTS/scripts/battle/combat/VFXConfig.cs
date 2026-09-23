using Godot;

namespace GodotRTS.Battle.Combat
{
    /// <summary>
    /// 单条特效配置资源（在 Inspector 中配置后赋给 EffectSpawner.Configs[]）。
    /// </summary>
    [GlobalClass]
    public partial class VFXConfig : Resource
    {
        [Export] public VFXType     Type        { get; set; } = VFXType.None;
        [Export] public PackedScene ScenePrefab { get; set; }
        [Export] public float       LifeTime    { get; set; } = 1.5f;
        [Export] public int         PoolSize    { get; set; } = 8;
        [Export] public AudioStream HitSound    { get; set; }
        [Export] public float       SoundVolume { get; set; } = 0f;  // dB
        [Export] public float       SoundMaxDist{ get; set; } = 40f; // 3D 最远可听距离
    }
}
