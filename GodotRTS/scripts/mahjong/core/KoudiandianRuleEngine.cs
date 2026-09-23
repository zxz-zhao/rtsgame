using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong
{
    public class HuResult
    {
        public bool IsHu { get; set; } = false;
        public string HuName { get; set; } = "";
        public int Fan { get; set; } = 1;
        public int Points { get; set; } = 0;
        public bool IsSelfDraw { get; set; } = false;
    }

    public class KoudiandianRuleEngine
    {
        private Random random = new Random();

        /// <summary>
        /// 洗牌并生成随机牌墙
        /// </summary>
        public Queue<MahjongTile> GenerateShuffledDeck()
        {
            List<MahjongTile> deck = MahjongTile.CreateStandardDeck();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int k = random.Next(i + 1);
                var temp = deck[i];
                deck[i] = deck[k];
                deck[k] = temp;
            }
            return new Queue<MahjongTile>(deck);
        }

        /// <summary>
        /// 发牌：每人发 13 张，庄家发 14 张
        /// </summary>
        public void DealTiles(Queue<MahjongTile> deck, List<PlayerHandData> players, int dealerIndex)
        {
            for (int round = 0; round < 13; round++)
            {
                for (int p = 0; p < players.Count; p++)
                {
                    if (deck.Count > 0)
                    {
                        players[p].ConcealedTiles.Add(deck.Dequeue());
                    }
                }
            }

            // 庄家多摸一张牌 (第 14 张)
            if (deck.Count > 0)
            {
                players[dealerIndex].ConcealedTiles.Add(deck.Dequeue());
            }

            foreach (var player in players)
            {
                player.SortHand();
            }
        }

        /// <summary>
        /// 检查是否可以吃牌 (仅对上家打出的牌)
        /// </summary>
        public List<List<MahjongTile>> CheckChi(PlayerHandData hand, MahjongTile targetTile)
        {
            List<List<MahjongTile>> options = new List<List<MahjongTile>>();

            // 只能吃数牌 (万、条、筒)，不能吃字风牌
            if (targetTile.IsHonor) return options;
            if (hand.LackSuit.HasValue && targetTile.Suit == hand.LackSuit.Value) return options;

            var concealed = hand.ConcealedTiles;
            TileSuit s = targetTile.Suit;
            int v = targetTile.Value;

            // 选项 1: v-2, v-1, [v]
            var tMinus2 = concealed.Find(t => t.Suit == s && t.Value == v - 2);
            var tMinus1 = concealed.Find(t => t.Suit == s && t.Value == v - 1);
            if (tMinus2 != null && tMinus1 != null)
            {
                options.Add(new List<MahjongTile> { tMinus2, tMinus1, targetTile });
            }

            // 选项 2: v-1, [v], v+1
            var tPlus1 = concealed.Find(t => t.Suit == s && t.Value == v + 1);
            if (tMinus1 != null && tPlus1 != null)
            {
                options.Add(new List<MahjongTile> { tMinus1, targetTile, tPlus1 });
            }

            // 选项 3: [v], v+1, v+2
            var tPlus2 = concealed.Find(t => t.Suit == s && t.Value == v + 2);
            if (tPlus1 != null && tPlus2 != null)
            {
                options.Add(new List<MahjongTile> { targetTile, tPlus1, tPlus2 });
            }

            return options;
        }

        /// <summary>
        /// 检查是否可以碰牌
        /// </summary>
        public bool CheckPong(PlayerHandData hand, MahjongTile targetTile)
        {
            if (hand.LackSuit.HasValue && targetTile.Suit == hand.LackSuit.Value) return false;
            return hand.CountMatchingTiles(targetTile) >= 2;
        }

        /// <summary>
        /// 检查是否可以杠牌 (包括明杠、暗杠、补杠、拐子风杠)
        /// </summary>
        public List<Meld> CheckGang(PlayerHandData hand, MahjongTile targetTile, bool isSelfTurn, int fromPlayerIndex = -1)
        {
            List<Meld> gangs = new List<Meld>();

            if (isSelfTurn)
            {
                // 1. 暗杠: 手牌中有 4 张相同的牌
                Dictionary<string, List<MahjongTile>> groups = new Dictionary<string, List<MahjongTile>>();
                foreach (var tile in hand.ConcealedTiles)
                {
                    string key = $"{tile.Suit}_{tile.Value}";
                    if (!groups.ContainsKey(key)) groups[key] = new List<MahjongTile>();
                    groups[key].Add(tile);
                }

                foreach (var kvp in groups)
                {
                    if (kvp.Value.Count == 4)
                    {
                        gangs.Add(new Meld(MeldType.AnGang, kvp.Value[0], new List<MahjongTile>(kvp.Value), -1));
                    }
                }

                // 2. 补杠: 手牌摸到的牌与已碰的副牌匹配
                foreach (var meld in hand.Melds)
                {
                    if (meld.Type == MeldType.Pong)
                    {
                        var matchTile = hand.ConcealedTiles.Find(t => t.Suit == meld.TargetTile.Suit && t.Value == meld.TargetTile.Value);
                        if (matchTile != null)
                        {
                            var list = new List<MahjongTile>(meld.Tiles) { matchTile };
                            gangs.Add(new Meld(MeldType.BuGang, matchTile, list, -1));
                        }
                    }
                }

                // 3. 山西特色: 风/箭拐子杠 (集齐东南西北4张不同风牌，或中发白3张不同箭牌)
                var windTiles = hand.ConcealedTiles.FindAll(t => t.Suit == TileSuit.Wind);
                HashSet<int> windValues = new HashSet<int>();
                List<MahjongTile> distinctWinds = new List<MahjongTile>();
                foreach (var w in windTiles)
                {
                    if (!windValues.Contains(w.Value))
                    {
                        windValues.Add(w.Value);
                        distinctWinds.Add(w);
                    }
                }
                if (distinctWinds.Count == 4)
                {
                    gangs.Add(new Meld(MeldType.WindGang, distinctWinds[0], distinctWinds, -1));
                }
            }
            else if (targetTile != null)
            {
                // 明杠: 他人打出的牌，手牌有 3 张匹配
                if (hand.CountMatchingTiles(targetTile) == 3)
                {
                    var matchTiles = hand.ConcealedTiles.FindAll(t => t.Suit == targetTile.Suit && t.Value == targetTile.Value);
                    var meldTiles = new List<MahjongTile>(matchTiles) { targetTile };
                    gangs.Add(new Meld(MeldType.MingGang, targetTile, meldTiles, fromPlayerIndex));
                }
            }

            return gangs;
        }

        /// <summary>
        /// 判定胡牌与计算番数 (平胡、七对、清一色、自摸/点炮)
        /// </summary>
        public HuResult CheckHu(
            PlayerHandData hand,
            MahjongTile? testTile,
            bool isSelfDraw,
            bool isGangKai = false,
            bool isHaiDiLao = false,
            bool isQiangGang = false)
        {
            HuResult res = new HuResult { IsSelfDraw = isSelfDraw };

            List<MahjongTile> testHand = new List<MahjongTile>(hand.ConcealedTiles);
            if (testTile != null && !isSelfDraw)
            {
                testHand.Add(testTile);
            }

            // 规则约束: 缺门限制。如果有缺门且手牌或副牌中还存在缺门花色，则不能胡牌
            if (hand.LackSuit.HasValue)
            {
                if (testHand.Exists(t => t.Suit == hand.LackSuit.Value)) return res;
                if (hand.Melds.Exists(m => m.Tiles.Exists(t => t.Suit == hand.LackSuit.Value))) return res;
            }

            // 0. 检查十三幺 (国士无双 - 13番)
            if (testHand.Count == 14 && CheckThirteenOrphans(testHand))
            {
                res.IsHu = true;
                res.HuName = "十三幺(国士无双)";
                res.Fan = 13;
            }
            // 1. 检查豪华七对 / 普通七对
            else if (testHand.Count == 14 && CheckSevenPairs(testHand))
            {
                res.IsHu = true;
                bool isDragon7 = CheckDragonSevenPairs(testHand);
                res.HuName = isDragon7 ? "豪华七对" : "七对";
                res.Fan = isDragon7 ? 8 : 4;
            }
            // 2. 检查碰碰胡 / 对对胡
            else if (CheckAllTriplets(testHand, hand.Melds))
            {
                res.IsHu = true;
                res.HuName = "碰碰胡";
                res.Fan = 4;
            }
            // 3. 检查标准推倒胡 (4组面子 + 1对将牌)
            else if (CheckStandardHu(testHand))
            {
                res.IsHu = true;
                res.HuName = "推倒胡";
                res.Fan = 1;
            }

            if (!res.IsHu) return res;

            // 检查清一色加番 (×4)
            if (IsPureSuit(testHand, hand.Melds))
            {
                res.HuName = "清一色 " + res.HuName;
                res.Fan *= 4;
            }

            // 检查特殊杠开/海底/抢杠番数
            if (isGangKai)
            {
                res.HuName = "杠上开花 " + res.HuName;
                res.Fan *= 2;
            }
            if (isHaiDiLao)
            {
                res.HuName = "海底捞月 " + res.HuName;
                res.Fan *= 2;
            }
            if (isQiangGang)
            {
                res.HuName = "抢杠胡 " + res.HuName;
                res.Fan *= 2;
            }

            // 自摸加番/底分 (×2)
            if (isSelfDraw)
            {
                res.HuName = "自摸 " + res.HuName;
                res.Fan *= 2;
            }
            else
            {
                res.HuName = "点炮 " + res.HuName;
            }

            // 计算分值: 基础底分 10 × 番数
            res.Points = 10 * res.Fan;
            return res;
        }

        private bool CheckThirteenOrphans(List<MahjongTile> tiles)
        {
            if (tiles.Count != 14) return false;
            HashSet<string> uniqueOrphans = new HashSet<string>();
            bool hasDuplicateOrphan = false;

            foreach (var t in tiles)
            {
                bool isOrphan = t.IsHonor || (!t.IsHonor && (t.Value == 1 || t.Value == 9));
                if (!isOrphan) return false;

                string key = $"{t.Suit}_{t.Value}";
                if (uniqueOrphans.Contains(key))
                {
                    hasDuplicateOrphan = true;
                }
                else
                {
                    uniqueOrphans.Add(key);
                }
            }
            return uniqueOrphans.Count == 13 && hasDuplicateOrphan;
        }

        private bool CheckDragonSevenPairs(List<MahjongTile> tiles)
        {
            var copy = new List<MahjongTile>(tiles);
            copy.Sort((a, b) => a.GetSortOrder().CompareTo(b.GetSortOrder()));
            for (int i = 0; i < copy.Count - 3; i++)
            {
                if (copy[i].Suit == copy[i + 3].Suit && copy[i].Value == copy[i + 3].Value)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckAllTriplets(List<MahjongTile> concealed, List<Meld> melds)
        {
            foreach (var m in melds)
            {
                if (m.Type == MeldType.Chi) return false;
            }

            if (concealed.Count % 3 != 2) return false;

            var list = new List<MahjongTile>(concealed);
            list.Sort((a, b) => a.GetSortOrder().CompareTo(b.GetSortOrder()));

            for (int i = 0; i < list.Count - 1; i++)
            {
                if (list[i].Suit == list[i + 1].Suit && list[i].Value == list[i + 1].Value)
                {
                    List<MahjongTile> remaining = new List<MahjongTile>(list);
                    remaining.RemoveAt(i + 1);
                    remaining.RemoveAt(i);

                    if (CanFormOnlyTriplets(remaining)) return true;
                }
            }
            return false;
        }

        private bool CanFormOnlyTriplets(List<MahjongTile> tiles)
        {
            if (tiles.Count == 0) return true;
            MahjongTile first = tiles[0];
            int matchCount = tiles.FindAll(t => t.Suit == first.Suit && t.Value == first.Value).Count;
            if (matchCount >= 3)
            {
                List<MahjongTile> next = new List<MahjongTile>(tiles);
                for (int k = 0; k < 3; k++)
                {
                    int idx = next.FindIndex(t => t.Suit == first.Suit && t.Value == first.Value);
                    next.RemoveAt(idx);
                }
                return CanFormOnlyTriplets(next);
            }
            return false;
        }

        private bool CheckSevenPairs(List<MahjongTile> tiles)
        {
            if (tiles.Count != 14) return false;
            var copy = new List<MahjongTile>(tiles);
            copy.Sort((a, b) => a.GetSortOrder().CompareTo(b.GetSortOrder()));

            int pairs = 0;
            for (int i = 0; i < copy.Count - 1; i += 2)
            {
                if (copy[i].Suit == copy[i + 1].Suit && copy[i].Value == copy[i + 1].Value)
                {
                    pairs++;
                }
                else
                {
                    return false;
                }
            }
            return pairs == 7;
        }

        private bool CheckStandardHu(List<MahjongTile> tiles)
        {
            if (tiles.Count % 3 != 2) return false;
            var list = new List<MahjongTile>(tiles);
            list.Sort((a, b) => a.GetSortOrder().CompareTo(b.GetSortOrder()));

            // 尝试选择一对作为将牌
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (list[i].Suit == list[i + 1].Suit && list[i].Value == list[i + 1].Value)
                {
                    List<MahjongTile> remaining = new List<MahjongTile>(list);
                    remaining.RemoveAt(i + 1);
                    remaining.RemoveAt(i);

                    if (CanFormMelds(remaining))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CanFormMelds(List<MahjongTile> tiles)
        {
            if (tiles.Count == 0) return true;

            MahjongTile first = tiles[0];

            // 尝试刻子 (3张相同)
            int matchCount = tiles.FindAll(t => t.Suit == first.Suit && t.Value == first.Value).Count;
            if (matchCount >= 3)
            {
                List<MahjongTile> nextTiles = new List<MahjongTile>(tiles);
                for (int k = 0; k < 3; k++)
                {
                    int idx = nextTiles.FindIndex(t => t.Suit == first.Suit && t.Value == first.Value);
                    nextTiles.RemoveAt(idx);
                }
                if (CanFormMelds(nextTiles)) return true;
            }

            // 尝试顺子 (仅针对万条筒数牌)
            if (!first.IsHonor)
            {
                int idx2 = tiles.FindIndex(t => t.Suit == first.Suit && t.Value == first.Value + 1);
                int idx3 = tiles.FindIndex(t => t.Suit == first.Suit && t.Value == first.Value + 2);

                if (idx2 >= 0 && idx3 >= 0)
                {
                    List<MahjongTile> nextTiles = new List<MahjongTile>(tiles);
                    // 倒序移除避免索引偏移
                    List<int> removeIndices = new List<int> { 0, idx2, idx3 };
                    removeIndices.Sort();
                    for (int k = removeIndices.Count - 1; k >= 0; k--)
                    {
                        nextTiles.RemoveAt(removeIndices[k]);
                    }
                    if (CanFormMelds(nextTiles)) return true;
                }
            }

            return false;
        }

        private bool IsPureSuit(List<MahjongTile> concealed, List<Meld> melds)
        {
            TileSuit? targetSuit = null;
            foreach (var t in concealed)
            {
                if (t.IsHonor) return false;
                if (!targetSuit.HasValue) targetSuit = t.Suit;
                else if (t.Suit != targetSuit.Value) return false;
            }
            foreach (var m in melds)
            {
                foreach (var t in m.Tiles)
                {
                    if (t.IsHonor) return false;
                    if (!targetSuit.HasValue) targetSuit = t.Suit;
                    else if (t.Suit != targetSuit.Value) return false;
                }
            }
            return targetSuit.HasValue;
        }
    }
}
