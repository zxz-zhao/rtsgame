using UnityEngine;

// 玩家状态数据（金币、人口等）
public class RTSPlayerState : MonoBehaviour
{
    public int Gold = 800;
    public int PopUsed = 0;
    public int PopCap = 20;
    public int PowerUsed = 0;
    public int PowerCap  = 0;
    public int EnemyKillCount = 0;

    public static RTSPlayerState Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool CanAfford(int cost) => Gold >= cost;
    public bool HasPopRoom(int pop) => PopUsed + pop <= PopCap;

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        GameManager.Instance?.RecordGoldSpent(true, amount);
        return true;
    }
}
