using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong
{
    public enum MeldType
    {
        Chi,       // 吃
        Pong,      // 碰
        MingGang,  // 明杠 (他人点杠)
        AnGang,    // 暗杠 (自己摸4张)
        BuGang,    // 补杠 (碰后摸第4张)
        WindGang   // 拐子杠 (风牌杠/箭牌杠)
    }

    public class Meld
    {
        public MeldType Type { get; set; }
        public MahjongTile TargetTile { get; set; }
        public List<MahjongTile> Tiles { get; set; } = new List<MahjongTile>();
        public int FromPlayerIndex { get; set; } // 来源玩家 (-1 表示暗杠/自摸)

        public Meld(MeldType type, MahjongTile targetTile, List<MahjongTile> tiles, int fromPlayerIndex = -1)
        {
            Type = type;
            TargetTile = targetTile;
            Tiles = tiles;
            FromPlayerIndex = fromPlayerIndex;
        }
    }

    public class PlayerHandData
    {
        public int PlayerIndex { get; set; }
        public string PlayerName { get; set; }
        public bool IsBot { get; set; }

        public List<MahjongTile> ConcealedTiles { get; set; } = new List<MahjongTile>(); // 手牌 (立牌)
        public List<Meld> Melds { get; set; } = new List<Meld>();                         // 副牌 (吃碰杠)
        public List<MahjongTile> Discards { get; set; } = new List<MahjongTile>();       // 河牌 (打出的牌)

        public TileSuit? LackSuit { get; set; } = null; // 选缺花色
        public int Score { get; set; } = 100;           // 积分/点数
        public bool IsTing { get; set; } = false;       // 是否听牌

        public PlayerHandData(int index, string name, bool isBot)
        {
            PlayerIndex = index;
            PlayerName = name;
            IsBot = isBot;
        }

        public void SortHand()
        {
            ConcealedTiles.Sort((a, b) =>
            {
                // 如果有选缺，缺门花色排在最右边或最前
                if (LackSuit.HasValue)
                {
                    if (a.Suit == LackSuit.Value && b.Suit != LackSuit.Value) return 1;
                    if (a.Suit != LackSuit.Value && b.Suit == LackSuit.Value) return -1;
                }
                return a.GetSortOrder().CompareTo(b.GetSortOrder());
            });
        }

        public bool RemoveConcealedTile(int tileId)
        {
            int idx = ConcealedTiles.FindIndex(t => t.Id == tileId);
            if (idx >= 0)
            {
                ConcealedTiles.RemoveAt(idx);
                return true;
            }
            return false;
        }

        public bool RemoveConcealedTile(MahjongTile tile)
        {
            int idx = ConcealedTiles.FindIndex(t => t.Suit == tile.Suit && t.Value == tile.Value);
            if (idx >= 0)
            {
                ConcealedTiles.RemoveAt(idx);
                return true;
            }
            return false;
        }

        public int CountMatchingTiles(MahjongTile tile)
        {
            int count = 0;
            foreach (var t in ConcealedTiles)
            {
                if (t.Suit == tile.Suit && t.Value == tile.Value) count++;
            }
            return count;
        }

        // ==================== 反外挂与数据完整性印章防护 (Anti-Cheat Protection) ====================
        private string currentIntegritySeal = "";
        private static readonly string SecretSalt = "AntiCheat-Mahjong-SecKey-2026";

        public string ComputeIntegrityChecksum()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"{PlayerIndex}:{PlayerName}:{Score}:{LackSuit}:");
            foreach (var t in ConcealedTiles)
            {
                sb.Append($"{t.Id}_{(int)t.Suit}_{t.Value};");
            }
            sb.Append($"Melds:{Melds.Count};Discards:{Discards.Count};Salt:{SecretSalt}");

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public void UpdateIntegritySeal()
        {
            currentIntegritySeal = ComputeIntegrityChecksum();
        }

        public bool VerifyIntegritySeal()
        {
            if (string.IsNullOrEmpty(currentIntegritySeal))
            {
                UpdateIntegritySeal();
                return true;
            }
            string calculated = ComputeIntegrityChecksum();
            return calculated == currentIntegritySeal;
        }
    }
}
