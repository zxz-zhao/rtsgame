using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong
{
    public class MahjongBotAI
    {
        private Random random = new Random();

        /// <summary>
        /// 电脑 AI 选择打出一张最不具有战略价值的手牌
        /// </summary>
        public MahjongTile SelectDiscard(PlayerHandData hand)
        {
            var concealed = hand.ConcealedTiles;
            if (concealed.Count == 0) return null;

            // 1. 优先打出选缺花色的牌
            if (hand.LackSuit.HasValue)
            {
                var lackTile = concealed.Find(t => t.Suit == hand.LackSuit.Value);
                if (lackTile != null) return lackTile;
            }

            // 2. 打出单张风牌 / 箭牌 (非对子/刻子)
            foreach (var tile in concealed)
            {
                if (tile.IsHonor && hand.CountMatchingTiles(tile) == 1)
                {
                    return tile;
                }
            }

            // 3. 打出边角孤张 (1或9万条筒，且两端无相邻牌)
            foreach (var tile in concealed)
            {
                if (!tile.IsHonor && (tile.Value == 1 || tile.Value == 9))
                {
                    if (hand.CountMatchingTiles(tile) == 1)
                    {
                        bool hasNeighbor = concealed.Exists(t => t.Suit == tile.Suit && Math.Abs(t.Value - tile.Value) <= 2 && t.Id != tile.Id);
                        if (!hasNeighbor) return tile;
                    }
                }
            }

            // 4. 打出任意孤张牌 (无刻子/对子/顺子搭子)
            foreach (var tile in concealed)
            {
                if (hand.CountMatchingTiles(tile) == 1)
                {
                    bool hasNeighbor = concealed.Exists(t => t.Suit == tile.Suit && Math.Abs(t.Value - tile.Value) <= 2 && t.Id != tile.Id);
                    if (!hasNeighbor) return tile;
                }
            }

            // 5. 默认打出最靠右的一张牌 (通常为最新摸入或无搭子牌)
            return concealed[concealed.Count - 1];
        }

        /// <summary>
        /// AI 决策动作 (胡 > 杠 > 碰 > 吃)
        /// </summary>
        public string DecideAction(
            PlayerHandData hand,
            MahjongTile discard,
            HuResult huRes,
            List<Meld> gangs,
            bool canPong,
            List<List<MahjongTile>> chiOptions)
        {
            if (huRes != null && huRes.IsHu)
            {
                return "HU";
            }

            if (gangs != null && gangs.Count > 0)
            {
                return "GANG";
            }

            if (canPong && random.NextDouble() < 0.85)
            {
                return "PONG";
            }

            if (chiOptions != null && chiOptions.Count > 0 && random.NextDouble() < 0.65)
            {
                return "CHI";
            }

            return "PASS";
        }
    }
}
