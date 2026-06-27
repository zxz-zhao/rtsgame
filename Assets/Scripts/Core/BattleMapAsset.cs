using UnityEngine;

[CreateAssetMenu(fileName = "BattleMap", menuName = "RTS/Map/Battle Map Asset")]
public sealed class BattleMapAsset : ScriptableObject
{
    public BattleMapDefinition Map = BattleMapDefinitionUtility.CreateDefault("New Map");

    public BattleMapDefinition ToDefinition()
    {
        BattleMapDefinitionUtility.Sanitize(Map, name);
        return BattleMapDefinitionUtility.Clone(Map);
    }

    public void CopyFrom(BattleMapDefinition source)
    {
        Map = BattleMapDefinitionUtility.Clone(source);
        BattleMapDefinitionUtility.Sanitize(Map, name);
    }

    void OnValidate()
    {
        if (Map == null)
            Map = BattleMapDefinitionUtility.CreateDefault(name);

        BattleMapDefinitionUtility.Sanitize(Map, name);
    }
}
