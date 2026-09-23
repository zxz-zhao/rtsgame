using Godot;
using System;
using GodotRTS.Mahjong;

namespace GodotRTS.Mahjong
{
    public partial class MahjongTileView : Control
    {
        public MahjongTile? TileData { get; private set; }
        public bool IsSelected { get; set; } = false;
        public bool IsFaceDown { get; private set; } = false;
        public bool IsInteractable { get; set; } = true;
        public bool IsSmallTile { get; private set; } = false;

        public event Action<MahjongTileView>? TileClicked;
        public event Action<MahjongTileView>? TileDoubleClicked;

        private PanelContainer tilePanel = null!;
        private Label titleLabel = null!;
        private Label suitLabel = null!;
        private ColorRect highlightBorder = null!;
        private Label? discardMarker;
        private ulong lastClickTime = 0;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(48, 68);
            MouseFilter = MouseFilterEnum.Stop;
            BuildUI();

            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            GuiInput += OnGuiInput;
        }

        public void SetLatestDiscardMarker(bool show)
        {
            var marker = GetNodeOrNull<Control>("DiscardMarker");
            if (marker != null)
            {
                marker.Visible = show;
            }
        }

        public void Setup(MahjongTile tile, bool faceDown = false, bool isSmall = false)
        {
            TileData = tile;
            IsFaceDown = faceDown;
            IsSmallTile = isSmall;

            if (isSmall)
            {
                CustomMinimumSize = new Vector2(34, 48);
                titleLabel.AddThemeFontSizeOverride("font_size", 14);
                suitLabel.AddThemeFontSizeOverride("font_size", 10);
            }
            else
            {
                CustomMinimumSize = new Vector2(48, 68);
                titleLabel.AddThemeFontSizeOverride("font_size", 20);
                suitLabel.AddThemeFontSizeOverride("font_size", 12);
            }

            UpdateVisuals();
        }

        private void BuildUI()
        {
            foreach (Node child in GetChildren())
            {
                child.QueueFree();
            }

            tilePanel = new PanelContainer();
            tilePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            tilePanel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(tilePanel);

            var sb = new StyleBoxFlat();
            sb.CornerRadiusTopLeft = 6;
            sb.CornerRadiusTopRight = 6;
            sb.CornerRadiusBottomLeft = 6;
            sb.CornerRadiusBottomRight = 6;
            sb.BorderWidthLeft = 2;
            sb.BorderWidthTop = 2;
            sb.BorderWidthRight = 2;
            sb.BorderWidthBottom = 4; // 加深底部3D立体压边
            sb.BorderColor = new Color(0.75f, 0.65f, 0.45f);
            sb.BgColor = new Color(0.96f, 0.94f, 0.88f);
            sb.ShadowColor = new Color(0, 0, 0, 0.25f);
            sb.ShadowSize = 3;
            sb.ShadowOffset = new Vector2(0, 3);
            tilePanel.AddThemeStyleboxOverride("panel", sb);

            highlightBorder = new ColorRect();
            highlightBorder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            highlightBorder.Color = new Color(1.0f, 0.85f, 0.2f, 0.35f);
            highlightBorder.Visible = false;
            highlightBorder.MouseFilter = MouseFilterEnum.Ignore;
            discardMarker = new Label();
            discardMarker.Text = "▼";
            discardMarker.HorizontalAlignment = HorizontalAlignment.Center;
            discardMarker.AddThemeFontSizeOverride("font_size", 12);
            discardMarker.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.15f));
            discardMarker.Position = new Vector2(0, -14);
            discardMarker.Size = new Vector2(48, 14);
            discardMarker.Visible = false;
            discardMarker.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(discardMarker);

            VBoxContainer contentBox = new VBoxContainer();
            contentBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            contentBox.Alignment = BoxContainer.AlignmentMode.Center;
            contentBox.MouseFilter = MouseFilterEnum.Ignore;
            tilePanel.AddChild(contentBox);

            titleLabel = new Label();
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.VerticalAlignment = VerticalAlignment.Center;
            titleLabel.AddThemeFontSizeOverride("font_size", 20);
            titleLabel.MouseFilter = MouseFilterEnum.Ignore;
            contentBox.AddChild(titleLabel);

            suitLabel = new Label();
            suitLabel.HorizontalAlignment = HorizontalAlignment.Center;
            suitLabel.VerticalAlignment = VerticalAlignment.Center;
            suitLabel.AddThemeFontSizeOverride("font_size", 12);
            suitLabel.MouseFilter = MouseFilterEnum.Ignore;
            contentBox.AddChild(suitLabel);
        }

        private void UpdateVisuals()
        {
            if (tilePanel == null) return;

            StyleBoxFlat sb = (StyleBoxFlat)tilePanel.GetThemeStylebox("panel");

            if (IsFaceDown)
            {
                sb.BgColor = new Color(0.12f, 0.42f, 0.28f);
                sb.BorderColor = new Color(0.08f, 0.28f, 0.18f);
                titleLabel.Text = "";
                suitLabel.Text = "";
                return;
            }

            sb.BgColor = new Color(0.97f, 0.96f, 0.92f);
            sb.BorderColor = new Color(0.78f, 0.72f, 0.58f);

            if (TileData == null) return;

            titleLabel.Text = GetTileSymbolText(TileData);
            suitLabel.Text = TileData.ShortName;

            Color fontColor = GetSuitColor(TileData);
            titleLabel.AddThemeColorOverride("font_color", fontColor);
            suitLabel.AddThemeColorOverride("font_color", fontColor * 0.85f);
        }

        private string GetTileSymbolText(MahjongTile tile)
        {
            if (tile.Suit == TileSuit.Wan) return GetChineseNumber(tile.Value);
            if (tile.Suit == TileSuit.Tiao) return $"{tile.Value}";
            if (tile.Suit == TileSuit.Tong) return $"{tile.Value}";
            if (tile.Suit == TileSuit.Wind)
            {
                string[] winds = { "东", "南", "西", "北" };
                return winds[Math.Clamp(tile.Value - 1, 0, 3)];
            }
            if (tile.Suit == TileSuit.Dragon)
            {
                string[] dragons = { "中", "发", "白" };
                return dragons[Math.Clamp(tile.Value - 1, 0, 2)];
            }
            return tile.ShortName;
        }

        private string GetChineseNumber(int val)
        {
            string[] nums = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            return (val >= 1 && val <= 9) ? nums[val - 1] : val.ToString();
        }

        private Color GetSuitColor(MahjongTile tile)
        {
            return tile.Suit switch
            {
                TileSuit.Wan => new Color(0.82f, 0.12f, 0.12f),
                TileSuit.Tiao => new Color(0.08f, 0.52f, 0.22f),
                TileSuit.Tong => new Color(0.12f, 0.32f, 0.72f),
                TileSuit.Wind => new Color(0.35f, 0.25f, 0.55f),
                TileSuit.Dragon => tile.Value switch
                {
                    1 => new Color(0.85f, 0.10f, 0.10f),
                    2 => new Color(0.05f, 0.55f, 0.15f),
                    _ => new Color(0.40f, 0.50f, 0.65f)
                },
                _ => Colors.Black
            };
        }

        private Tween? activeTween;

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            float targetY = IsSelected ? -14.0f : 0.0f;
            AnimatePositionY(targetY, 0.12f);
            SetFieldHighlight(IsSelected);
        }

        public void SetFieldHighlight(bool match)
        {
            if (highlightBorder != null)
            {
                highlightBorder.Visible = match || IsSelected;
                highlightBorder.Color = IsSelected 
                    ? new Color(1.0f, 0.85f, 0.20f, 0.45f)
                    : (match ? new Color(0.30f, 0.75f, 1.0f, 0.35f) : new Color(0, 0, 0, 0));
            }
        }

        private void OnMouseEntered()
        {
            if (!IsInteractable || IsFaceDown) return;
            if (!IsSelected) AnimatePositionY(-6.0f, 0.08f);
        }

        private void OnMouseExited()
        {
            if (!IsInteractable || IsFaceDown) return;
            if (!IsSelected) AnimatePositionY(0.0f, 0.08f);
        }

        public void AnimateDiscardSlap()
        {
            PivotOffset = CustomMinimumSize * 0.5f;
            Scale = new Vector2(1.28f, 1.28f);
            Modulate = new Color(1.2f, 1.2f, 1.0f, 0.7f);

            activeTween?.Kill();
            activeTween = CreateTween();
            activeTween.SetParallel(true);
            activeTween.TweenProperty(this, "scale", new Vector2(0.92f, 0.92f), 0.10f)
                       .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            activeTween.TweenProperty(this, "modulate", Colors.White, 0.10f);

            activeTween.Chain();
            activeTween.TweenProperty(this, "scale", Vector2.One, 0.08f)
                       .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

            if (highlightBorder != null)
            {
                highlightBorder.Visible = true;
                highlightBorder.Color = new Color(1.0f, 0.85f, 0.20f, 0.90f);
                Tween flashTween = CreateTween();
                flashTween.TweenProperty(highlightBorder, "color:a", 0.0f, 0.60f);
            }
        }

        private void AnimatePositionY(float targetY, float duration)
        {
            activeTween?.Kill();
            activeTween = CreateTween();
            activeTween.TweenProperty(this, "position:y", targetY, duration)
                       .SetTrans(Tween.TransitionType.Quad)
                       .SetEase(Tween.EaseType.Out);
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (!IsInteractable || IsFaceDown) return;

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    // 触感按压凹陷微反馈 (Press Compress Feedback)
                    Scale = new Vector2(0.92f, 0.92f);
                    PivotOffset = Size * 0.5f;

                    ulong now = Time.GetTicksMsec();
                    if (now - lastClickTime < 350)
                    {
                        TileDoubleClicked?.Invoke(this);
                    }
                    else
                    {
                        TileClicked?.Invoke(this);
                    }
                    lastClickTime = now;
                }
                else
                {
                    // 释放弹性恢复 (Bounce Release)
                    Tween t = CreateTween();
                    t.TweenProperty(this, "scale", Vector2.One, 0.12f)
                     .SetTrans(Tween.TransitionType.Back)
                     .SetEase(Tween.EaseType.Out);
                }
            }
        }
    }
}
