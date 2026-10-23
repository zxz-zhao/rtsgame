using Godot;
using System;

public partial class BuildingAndTechPreview : Control
{
    const string ButtonBackdropRoot = "res://assets/unity_migrated/Assets/Resources/UI/ButtonBackdrops/";
    const string TechIconRoot = "res://assets/unity_migrated/Assets/Resources/BattleHud/TechIcons/";
    const string HudIconRoot = "res://assets/unity_migrated/Assets/Resources/icons_final/";

    public override void _Ready()
    {
        Size = new Vector2(1280, 720);

        var bg = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(1280, 720),
            Color = new Color(0.06f, 0.08f, 0.10f, 1f)
        };
        AddChild(bg);

        // Header Title
        var header = new Label
        {
            Position = new Vector2(24, 12),
            Size = new Vector2(1232, 36),
            Text = "GODOT 4 RTS · 建筑与科技实体图标走查面板 (Solid Filled Visual Verification)",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        header.AddThemeFontSizeOverride("font_size", 20);
        header.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.40f));
        AddChild(header);

        // --- SECTION 1: Action Buttons ---
        var actionGroup = new HBoxContainer
        {
            Position = new Vector2(24, 52),
            Size = new Vector2(400, 48)
        };
        actionGroup.AddThemeConstantOverride("separation", 16);
        AddChild(actionGroup);

        actionGroup.AddChild(CreateActionIconPreview("建造面板 (B)", HudIconRoot + "icon_hud_build.png", new Color(1.0f, 0.82f, 0.30f)));
        actionGroup.AddChild(CreateActionIconPreview("科技研发 (T)", HudIconRoot + "icon_hud_tech.png", new Color(0.40f, 0.90f, 1.0f)));

        // --- SECTION 2: Building Icons & Cards ---
        var bldTitle = new Label
        {
            Position = new Vector2(24, 110),
            Size = new Vector2(1232, 28),
            Text = "【战术建筑部署卡牌与实体图标 (9项)】"
        };
        bldTitle.AddThemeFontSizeOverride("font_size", 16);
        bldTitle.AddThemeColorOverride("font_color", new Color(0.35f, 0.90f, 0.55f));
        AddChild(bldTitle);

        var bldGrid = new GridContainer
        {
            Position = new Vector2(24, 142),
            Columns = 5
        };
        bldGrid.AddThemeConstantOverride("h_separation", 12);
        bldGrid.AddThemeConstantOverride("v_separation", 10);
        AddChild(bldGrid);

        var buildings = new (string Key, string Name, int Cost, string IconFile, string BgFile)[]
        {
            ("barracks", "兵营", 300, "build_barracks_icon.png", "build_barracks.png"),
            ("tank_factory", "特勤工厂", 400, "build_special_factory_icon.png", "build_special_factory.png"),
            ("armor_factory", "坦克工厂", 450, "build_tank_factory_icon.png", "build_tank_factory.png"),
            ("airfield", "停机坪", 260, "build_airfield_icon.png", "build_airfield.png"),
            ("air_factory", "飞机工厂", 400, "build_air_factory_icon.png", "build_air_factory.png"),
            ("naval_yard", "船坞", 360, "build_naval_yard_icon.png", "build_naval_yard.png"),
            ("turret", "防御炮台", 200, "build_turret_icon.png", "build_turret.png"),
            ("gold_mine", "金矿采掘", 500, "build_gold_mine_icon.png", "build_gold_mine.png"),
            ("power_plant", "发电厂", 180, "build_power_plant_icon.png", "build_power_plant.png"),
        };

        foreach (var bld in buildings)
        {
            bldGrid.AddChild(CreateBuildingCardPreview(bld.Name, bld.Cost, ButtonBackdropRoot + bld.IconFile, ButtonBackdropRoot + bld.BgFile));
        }

        // --- SECTION 3: Tech Icons & Cards ---
        var techTitle = new Label
        {
            Position = new Vector2(24, 395),
            Size = new Vector2(1232, 28),
            Text = "【战场主动科技卡牌与实体图标 (10项)】"
        };
        techTitle.AddThemeFontSizeOverride("font_size", 16);
        techTitle.AddThemeColorOverride("font_color", new Color(0.40f, 0.85f, 1.0f));
        AddChild(techTitle);

        var techGrid = new GridContainer
        {
            Position = new Vector2(24, 428),
            Columns = 5
        };
        techGrid.AddThemeConstantOverride("h_separation", 12);
        techGrid.AddThemeConstantOverride("v_separation", 10);
        AddChild(techGrid);

        var techs = new (string Key, string Name, string Desc, Color Tint, string IconFile, string BgFile)[]
        {
            ("speed", "机动强化", "全军移速 +32%", new Color(0.38f, 0.92f, 1f), "tech_icon_speed.png", "tech_bg_speed_wings.png"),
            ("armor", "装甲强化", "伤害减免 28%", new Color(0.55f, 0.78f, 1f), "tech_icon_armor.png", "tech_bg_armor_plate.png"),
            ("firepower", "火力强化", "攻击火力 +30%", new Color(1f, 0.48f, 0.22f), "tech_icon_firepower.png", "tech_bg_firepower_barrage.png"),
            ("repair", "战地维修", "紧急纳秒级维修", new Color(0.40f, 0.95f, 0.55f), "tech_icon_repair.png", "tech_bg_repair_workshop.png"),
            ("radar", "雷达扫描", "侦察战雾开全图", new Color(1.0f, 0.85f, 0.25f), "tech_icon_radar.png", "tech_bg_radar_map.png"),
            ("rapid", "急促射击", "射速提升 40%", new Color(1.0f, 0.65f, 0.20f), "tech_icon_rapid.png", "tech_bg_rapid_ammo.png"),
            ("hold", "阵地坚守", "防守减伤加成", new Color(0.50f, 0.95f, 0.70f), "tech_icon_hold.png", "tech_bg_hold_bunker.png"),
            ("assault", "突击冲锋", "正面装甲冲锋", new Color(1.0f, 0.35f, 0.45f), "tech_icon_assault.png", "tech_bg_assault_arrows.png"),
            ("air_refit", "航空改装", "空中战鹰整备", new Color(0.30f, 0.95f, 0.85f), "tech_icon_air_refit.png", "tech_bg_speed_wings.png"),
            ("overdrive", "过载运转", "超频等离子输出", new Color(0.95f, 0.35f, 0.90f), "tech_icon_overdrive.png", "tech_bg_assault_arrows.png"),
        };

        foreach (var t in techs)
        {
            string bgPath = "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/" + t.BgFile;
            string iconPath = TechIconRoot + t.IconFile;
            techGrid.AddChild(CreateTechCardPreview(t.Name, t.Desc, t.Tint, iconPath, bgPath));
        }
    }

    Control CreateActionIconPreview(string label, string iconPath, Color accent)
    {
        var panel = new Panel
        {
            CustomMinimumSize = new Vector2(170, 48),
            Size = new Vector2(170, 48)
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.16f, 0.20f, 0.95f),
            BorderColor = accent,
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        });

        var icon = new TextureRect
        {
            Position = new Vector2(6, 6),
            Size = new Vector2(36, 36),
            Texture = RuntimeTextureLoader.LoadTexture(iconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        panel.AddChild(icon);

        var text = new Label
        {
            Position = new Vector2(48, 12),
            Size = new Vector2(116, 24),
            Text = label
        };
        text.AddThemeFontSizeOverride("font_size", 13);
        text.AddThemeColorOverride("font_color", Colors.White);
        panel.AddChild(text);

        return panel;
    }

    Control CreateBuildingCardPreview(string name, int cost, string iconPath, string bgPath)
    {
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(234, 110),
            Size = new Vector2(234, 110),
            ClipContents = true
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(0.88f, 0.70f, 0.28f, 0.85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        });

        // Background Illustration
        var bg = new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 110),
            Texture = RuntimeTextureLoader.LoadTexture(bgPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(1f, 1f, 1f, 0.75f)
        };
        card.AddChild(bg);

        var wash = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 110),
            Color = new Color(0.04f, 0.08f, 0.12f, 0.40f)
        };
        card.AddChild(wash);

        // Top accent line
        card.AddChild(new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 3),
            Color = new Color(1.0f, 0.82f, 0.30f)
        });

        // Solid Icon Badge Box
        var iconFrame = new Panel
        {
            Position = new Vector2(8, 8),
            Size = new Vector2(48, 48)
        };
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.10f, 0.14f, 0.90f),
            BorderColor = new Color(0.92f, 0.80f, 0.46f, 0.85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        });
        card.AddChild(iconFrame);

        var icon = new TextureRect
        {
            Position = new Vector2(2, 2),
            Size = new Vector2(44, 44),
            Texture = RuntimeTextureLoader.LoadTexture(iconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        iconFrame.AddChild(icon);

        // Name & Cost Labels
        var nameLabel = new Label
        {
            Position = new Vector2(62, 10),
            Size = new Vector2(165, 24),
            Text = name
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        nameLabel.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(nameLabel);

        var costLabel = new Label
        {
            Position = new Vector2(62, 34),
            Size = new Vector2(165, 20),
            Text = $"建造资金: ${cost}"
        };
        costLabel.AddThemeFontSizeOverride("font_size", 12);
        costLabel.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.42f));
        card.AddChild(costLabel);

        return card;
    }

    Control CreateTechCardPreview(string name, string desc, Color tint, string iconPath, string bgPath)
    {
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(234, 110),
            Size = new Vector2(234, 110),
            ClipContents = true
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(tint.R, tint.G, tint.B, 0.85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        });

        // Background Illustration
        var bg = new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 110),
            Texture = RuntimeTextureLoader.LoadTexture(bgPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(1f, 1f, 1f, 0.70f)
        };
        card.AddChild(bg);

        var wash = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 110),
            Color = new Color(0.04f, 0.08f, 0.12f, 0.42f)
        };
        card.AddChild(wash);

        // Top accent line
        card.AddChild(new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(234, 3),
            Color = tint
        });

        // Solid Tech Icon Badge Box
        var iconFrame = new Panel
        {
            Position = new Vector2(8, 8),
            Size = new Vector2(48, 48)
        };
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.10f, 0.14f, 0.90f),
            BorderColor = new Color(tint.R, tint.G, tint.B, 0.85f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        });
        card.AddChild(iconFrame);

        var icon = new TextureRect
        {
            Position = new Vector2(2, 2),
            Size = new Vector2(44, 44),
            Texture = RuntimeTextureLoader.LoadTexture(iconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = Colors.White
        };
        iconFrame.AddChild(icon);

        // Name & Description
        var nameLabel = new Label
        {
            Position = new Vector2(62, 10),
            Size = new Vector2(165, 24),
            Text = name
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.90f, 0.65f));
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        nameLabel.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(nameLabel);

        var descLabel = new Label
        {
            Position = new Vector2(62, 36),
            Size = new Vector2(165, 36),
            Text = desc,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        descLabel.AddThemeFontSizeOverride("font_size", 12);
        descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.92f, 0.96f));
        card.AddChild(descLabel);

        return card;
    }
}
