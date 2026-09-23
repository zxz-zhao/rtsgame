using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong
{
    public enum TileSuit
    {
        Wan = 0,   // 万
        Tiao = 1,  // 条
        Tong = 2,  // 筒
        Wind = 3,  // 风 (东、南、西、北)
        Dragon = 4, // 箭/字 (中、发、白)
        Unknown = 99 // 隐藏/遮罩未知牌
    }

    public class MahjongTile : IEquatable<MahjongTile>
    {
        public int Id { get; set; }
        public TileSuit Suit { get; set; }
        public int Value { get; set; } // 1-9 for Wan/Tiao/Tong; 1-4 for Wind (东1南2西3北4); 1-3 for Dragon (中1发2白3)
        public string Name { get; set; }
        public string ShortName { get; set; }

        public bool IsHonor => Suit == TileSuit.Wind || Suit == TileSuit.Dragon;

        public MahjongTile(int id, TileSuit suit = TileSuit.Unknown, int value = 0, string name = "未知牌", string shortName = "?")
        {
            Id = id;
            Suit = suit;
            Value = value;
            Name = name;
            ShortName = shortName;
        }

        public int GetSortOrder()
        {
            return ((int)Suit * 100) + Value;
        }

        public bool Equals(MahjongTile? other)
        {
            if (other is null) return false;
            return Suit == other.Suit && Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MahjongTile);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Suit, Value);
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// 生成一副标准 136 张麻将牌
        /// </summary>
        public static List<MahjongTile> CreateStandardDeck()
        {
            List<MahjongTile> deck = new List<MahjongTile>();
            int tileId = 0;

            // 1. 万 (1-9万 x 4)
            string[] wanNames = { "一万", "二万", "三万", "四万", "五万", "六万", "七万", "八万", "九万" };
            string[] wanShorts = { "1万", "2万", "3万", "4万", "5万", "6万", "7万", "8万", "9万" };
            for (int val = 1; val <= 9; val++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    deck.Add(new MahjongTile(tileId++, TileSuit.Wan, val, wanNames[val - 1], wanShorts[val - 1]));
                }
            }

            // 2. 条 (1-9条 x 4)
            string[] tiaoNames = { "一条", "二条", "三条", "四条", "五条", "六条", "七条", "八条", "九条" };
            string[] tiaoShorts = { "1条", "2条", "3条", "4条", "5条", "6条", "7条", "8条", "9条" };
            for (int val = 1; val <= 9; val++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    deck.Add(new MahjongTile(tileId++, TileSuit.Tiao, val, tiaoNames[val - 1], tiaoShorts[val - 1]));
                }
            }

            // 3. 筒 (1-9筒 x 4)
            string[] tongNames = { "一筒", "二筒", "三筒", "四筒", "五筒", "六筒", "七筒", "八筒", "九筒" };
            string[] tongShorts = { "1筒", "2筒", "3筒", "4筒", "5筒", "6筒", "7筒", "8筒", "9筒" };
            for (int val = 1; val <= 9; val++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    deck.Add(new MahjongTile(tileId++, TileSuit.Tong, val, tongNames[val - 1], tongShorts[val - 1]));
                }
            }

            // 4. 风牌 (东、南、西、北 x 4)
            string[] windNames = { "东风", "南风", "西风", "北风" };
            string[] windShorts = { "东", "南", "西", "北" };
            for (int val = 1; val <= 4; val++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    deck.Add(new MahjongTile(tileId++, TileSuit.Wind, val, windNames[val - 1], windShorts[val - 1]));
                }
            }

            // 5. 箭牌 (红中、发财、白板 x 4)
            string[] dragonNames = { "红中", "发财", "白板" };
            string[] dragonShorts = { "中", "发", "白" };
            for (int val = 1; val <= 3; val++)
            {
                for (int copy = 0; copy < 4; copy++)
                {
                    deck.Add(new MahjongTile(tileId++, TileSuit.Dragon, val, dragonNames[val - 1], dragonShorts[val - 1]));
                }
            }

            return deck;
        }
    }
}
