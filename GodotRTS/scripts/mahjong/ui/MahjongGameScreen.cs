using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotRTS.Mahjong
{
    public partial class MahjongGameScreen : Control
    {
        private KoudiandianRuleEngine ruleEngine = new KoudiandianRuleEngine();
        private MahjongBotAI botAI = new MahjongBotAI();

        private Queue<MahjongTile> deck = null!;
        private List<PlayerHandData> players = null!;
        private int dealerIndex = 0;
        private int currentTurnIndex = 0;
        private MahjongTile? lastDiscardedTile = null;
        private int lastDiscardPlayerIndex = -1;

        // UI 节点引用
        private HBoxContainer player0HandContainer = null!; // 玩家0立牌
        private HBoxContainer player0MeldContainer = null!; // 玩家0副牌 (碰杠)
        private HBoxContainer player0RiverContainer = null!; // 玩家0河牌

        private VBoxContainer player1HandContainer = null!; // 右AI立牌
        private VBoxContainer player1MeldContainer = null!; // 右AI副牌 (碰杠)
        private HBoxContainer player1RiverContainer = null!; // 右AI河牌

        private HBoxContainer player2HandContainer = null!; // 上AI立牌
        private HBoxContainer player2MeldContainer = null!; // 上AI副牌 (碰杠)
        private HBoxContainer player2RiverContainer = null!; // 上AI河牌

        private VBoxContainer player3HandContainer = null!; // 左AI立牌
        private VBoxContainer player3MeldContainer = null!; // 左AI副牌 (碰杠)
        private HBoxContainer player3RiverContainer = null!; // 左AI河牌

        private Label centerDeckLabel = null!;
        private Label turnIndicatorLabel = null!;
        private Label announcementLabel = null!;

        private HBoxContainer actionBar = null!;
        private Button btnChi = null!;
        private Button btnPong = null!;
        private Button btnGang = null!;
        private Button btnHu = null!;
        private Button btnPass = null!;
        private Button btnDiscardAction = null!;

        private HBoxContainer lackSuitPanel = null!;
        private Button btnLackWan = null!;
        private Button btnLackTiao = null!;
        private Button btnLackTong = null!;

        private Control settlementModal = null!;
        private Label settlementTitle = null!;
        private Label settlementDetail = null!;
        private Button btnPlayAgain = null!;
        private Button btnReturnLobby = null!;

        private PanelContainer tingInfoPanel = null!;
        private Label tingInfoLabel = null!;
        private Control actionCalloutOverlay = null!;
        private Label actionCalloutLabel = null!;

        private MahjongTileView? selectedTileView = null;
        private bool isWaitingForUserAction = false;
        private bool isGameEnding = false;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            BuildUI();
            StartNewGame();

            var cmdArgs = CommandLineArgs.Get();
            var captureIndex = System.Array.IndexOf(cmdArgs, "--capture-path");
            if (captureIndex != -1 && captureIndex + 1 < cmdArgs.Length)
            {
                var outputPath = cmdArgs[captureIndex + 1];
                CaptureMahjongScreenDeferred(outputPath);
            }
        }

        private async void CaptureMahjongScreenDeferred(string outputPath)
        {
            for (int i = 0; i < 25; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var texture = GetViewport().GetTexture();
            var image = texture?.GetImage();
            if (image is not null && !image.IsEmpty())
            {
                var globalPath = ProjectSettings.GlobalizePath(outputPath);
                var dir = System.IO.Path.GetDirectoryName(globalPath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                image.SavePng(globalPath);
                GD.Print($"[MAHJONG_CAPTURE] Saved screen to {globalPath}");
            }
            GetTree().Quit();
        }

        private void BuildUI()
        {
            // 背景板：优雅深红与复古绿台面
            TextureRect bg = new TextureRect();
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bg);

            ColorRect baseBg = new ColorRect();
            baseBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            baseBg.Color = new Color(0.10f, 0.28f, 0.20f); // 翡翠麻将台布
            AddChild(baseBg);

            // 1. 中央信息盘
            Control centerDisk = new Control();
            centerDisk.CustomMinimumSize = new Vector2(280, 140);
            centerDisk.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            AddChild(centerDisk);

            PanelContainer diskPanel = new PanelContainer();
            diskPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            centerDisk.AddChild(diskPanel);

            StyleBoxFlat diskSb = new StyleBoxFlat();
            diskSb.BgColor = new Color(0.06f, 0.18f, 0.13f, 0.90f);
            diskSb.CornerRadiusTopLeft = 12;
            diskSb.CornerRadiusTopRight = 12;
            diskSb.CornerRadiusBottomLeft = 12;
            diskSb.CornerRadiusBottomRight = 12;
            diskSb.BorderWidthLeft = 2;
            diskSb.BorderWidthTop = 2;
            diskSb.BorderWidthRight = 2;
            diskSb.BorderWidthBottom = 2;
            diskSb.BorderColor = new Color(0.85f, 0.72f, 0.45f);
            diskPanel.AddThemeStyleboxOverride("panel", diskSb);

            VBoxContainer diskBox = new VBoxContainer();
            diskBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            diskBox.Alignment = BoxContainer.AlignmentMode.Center;
            diskPanel.AddChild(diskBox);

            Label titleLabel = new Label();
            titleLabel.Text = "山西扣点点麻将";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.AddThemeFontSizeOverride("font_size", 18);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
            diskBox.AddChild(titleLabel);

            turnIndicatorLabel = new Label();
            turnIndicatorLabel.Text = "当前回合: 玩家";
            turnIndicatorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            turnIndicatorLabel.AddThemeFontSizeOverride("font_size", 16);
            turnIndicatorLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.95f, 0.75f));
            diskBox.AddChild(turnIndicatorLabel);

            centerDeckLabel = new Label();
            centerDeckLabel.Text = "剩余牌数: 108";
            centerDeckLabel.HorizontalAlignment = HorizontalAlignment.Center;
            centerDeckLabel.AddThemeFontSizeOverride("font_size", 14);
            diskBox.AddChild(centerDeckLabel);

            Label netStatusLabel = new Label();
            netStatusLabel.Text = (MahjongNetManager.Instance != null && MahjongNetManager.Instance.IsNetworked) ? "🌐 在线 4人联网对局" : "💻 离线 人机练习局";
            netStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            netStatusLabel.AddThemeFontSizeOverride("font_size", 12);
            netStatusLabel.AddThemeColorOverride("font_color", new Color(0.70f, 0.85f, 1.0f));
            diskBox.AddChild(netStatusLabel);

            // 公告横幅
            announcementLabel = new Label();
            announcementLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            announcementLabel.Position = new Vector2(announcementLabel.Position.X, announcementLabel.Position.Y - 120);
            announcementLabel.HorizontalAlignment = HorizontalAlignment.Center;
            announcementLabel.AddThemeFontSizeOverride("font_size", 28);
            announcementLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.90f, 0.30f));
            announcementLabel.Visible = false;
            AddChild(announcementLabel);

            // 2. 玩家0 (底部 - 本地玩家)
            VBoxContainer p0Box = new VBoxContainer();
            p0Box.AnchorLeft = 0.10f;
            p0Box.AnchorRight = 0.90f;
            p0Box.AnchorTop = 0.75f;
            p0Box.AnchorBottom = 0.98f;
            AddChild(p0Box);

            player0RiverContainer = new HBoxContainer();
            player0RiverContainer.CustomMinimumSize = new Vector2(400, 48);
            player0RiverContainer.Alignment = BoxContainer.AlignmentMode.Center;
            p0Box.AddChild(player0RiverContainer);

            HBoxContainer p0CardsRow = new HBoxContainer();
            p0CardsRow.Alignment = BoxContainer.AlignmentMode.Center;
            p0Box.AddChild(p0CardsRow);

            player0MeldContainer = new HBoxContainer();
            p0CardsRow.AddChild(player0MeldContainer);

            player0HandContainer = new HBoxContainer();
            p0CardsRow.AddChild(player0HandContainer);

            // 3. 上方 AI (玩家2)
            VBoxContainer p2Box = new VBoxContainer();
            p2Box.AnchorLeft = 0.20f;
            p2Box.AnchorRight = 0.80f;
            p2Box.AnchorTop = 0.02f;
            p2Box.AnchorBottom = 0.20f;
            AddChild(p2Box);

            HBoxContainer p2CardsRow = new HBoxContainer();
            p2CardsRow.Alignment = BoxContainer.AlignmentMode.Center;
            p2Box.AddChild(p2CardsRow);

            player2MeldContainer = new HBoxContainer();
            p2CardsRow.AddChild(player2MeldContainer);

            player2HandContainer = new HBoxContainer();
            player2HandContainer.Alignment = BoxContainer.AlignmentMode.Center;
            p2CardsRow.AddChild(player2HandContainer);

            player2RiverContainer = new HBoxContainer();
            player2RiverContainer.Alignment = BoxContainer.AlignmentMode.Center;
            p2Box.AddChild(player2RiverContainer);

            // 4. 左侧 AI (玩家3) & 右侧 AI (玩家1)
            VBoxContainer p3Box = new VBoxContainer();
            p3Box.AnchorLeft = 0.01f;
            p3Box.AnchorTop = 0.22f;
            p3Box.AnchorBottom = 0.72f;
            AddChild(p3Box);

            player3MeldContainer = new VBoxContainer();
            p3Box.AddChild(player3MeldContainer);

            player3HandContainer = new VBoxContainer();
            p3Box.AddChild(player3HandContainer);

            player3RiverContainer = new HBoxContainer();
            player3RiverContainer.AnchorLeft = 0.12f;
            player3RiverContainer.AnchorTop = 0.35f;
            AddChild(player3RiverContainer);

            VBoxContainer p1Box = new VBoxContainer();
            p1Box.AnchorRight = 0.99f;
            p1Box.AnchorTop = 0.22f;
            p1Box.AnchorBottom = 0.72f;
            p1Box.GrowHorizontal = GrowDirection.Begin;
            AddChild(p1Box);

            player1MeldContainer = new VBoxContainer();
            p1Box.AddChild(player1MeldContainer);

            player1HandContainer = new VBoxContainer();
            p1Box.AddChild(player1HandContainer);

            player1RiverContainer = new HBoxContainer();
            player1RiverContainer.AnchorRight = 0.88f;
            player1RiverContainer.AnchorTop = 0.35f;
            player1RiverContainer.GrowHorizontal = GrowDirection.Begin;
            AddChild(player1RiverContainer);

            // 5. 操作面板 (吃、碰、杠、胡、过、出牌)
            actionBar = new HBoxContainer();
            actionBar.AnchorLeft = 0.50f;
            actionBar.AnchorRight = 0.50f;
            actionBar.AnchorTop = 0.68f;
            actionBar.AnchorBottom = 0.68f;
            actionBar.GrowHorizontal = GrowDirection.Both;
            actionBar.Alignment = BoxContainer.AlignmentMode.Center;
            actionBar.Visible = false;
            AddChild(actionBar);

            btnChi = CreateActionButton("吃牌", new Color(0.20f, 0.60f, 0.30f));
            btnPong = CreateActionButton("碰牌", new Color(0.20f, 0.40f, 0.80f));
            btnGang = CreateActionButton("杠牌", new Color(0.80f, 0.50f, 0.10f));
            btnHu = CreateActionButton("胡牌!", new Color(0.90f, 0.15f, 0.15f));
            btnPass = CreateActionButton("过", new Color(0.40f, 0.40f, 0.40f));
            btnDiscardAction = CreateActionButton("出牌", new Color(0.85f, 0.70f, 0.20f));
            Button btnSort = CreateActionButton("理牌", new Color(0.35f, 0.55f, 0.75f));

            actionBar.AddChild(btnChi);
            actionBar.AddChild(btnPong);
            actionBar.AddChild(btnGang);
            actionBar.AddChild(btnHu);
            actionBar.AddChild(btnPass);
            actionBar.AddChild(btnDiscardAction);
            actionBar.AddChild(btnSort);

            btnChi.Pressed += () => OnUserActionClicked("CHI");
            btnPong.Pressed += () => OnUserActionClicked("PONG");
            btnGang.Pressed += () => OnUserActionClicked("GANG");
            btnHu.Pressed += () => OnUserActionClicked("HU");
            btnPass.Pressed += () => OnUserActionClicked("PASS");
            btnDiscardAction.Pressed += () => OnUserActionClicked("DISCARD");
            btnSort.Pressed += () =>
            {
                players[0].SortHand();
                RefreshAllViews();
                Mahjong.Audio.MahjongAudioManager.Instance?.PlaySfx("deal");
                ShowAnnouncement("玩家 手牌已自动整理对齐");
            };

            // 6. 选缺面板
            lackSuitPanel = new HBoxContainer();
            lackSuitPanel.AnchorLeft = 0.50f;
            lackSuitPanel.AnchorRight = 0.50f;
            lackSuitPanel.AnchorTop = 0.65f;
            lackSuitPanel.GrowHorizontal = GrowDirection.Both;
            lackSuitPanel.Visible = false;
            AddChild(lackSuitPanel);

            btnLackWan = CreateActionButton("选缺万", new Color(0.80f, 0.20f, 0.20f));
            btnLackTiao = CreateActionButton("选缺条", new Color(0.20f, 0.70f, 0.30f));
            btnLackTong = CreateActionButton("选缺筒", new Color(0.20f, 0.40f, 0.80f));

            lackSuitPanel.AddChild(btnLackWan);
            lackSuitPanel.AddChild(btnLackTiao);
            lackSuitPanel.AddChild(btnLackTong);

            btnLackWan.Pressed += () => SelectUserLackSuit(TileSuit.Wan);
            btnLackTiao.Pressed += () => SelectUserLackSuit(TileSuit.Tiao);
            btnLackTong.Pressed += () => SelectUserLackSuit(TileSuit.Tong);

            // 7. 结算弹窗与听牌喷发UI
            BuildSettlementModal();
            BuildTingAndCalloutUI();
        }

        private void BuildTingAndCalloutUI()
        {
            tingInfoPanel = new PanelContainer();
            tingInfoPanel.AnchorLeft = 0.62f;
            tingInfoPanel.AnchorRight = 0.96f;
            tingInfoPanel.AnchorTop = 0.60f;
            tingInfoPanel.AnchorBottom = 0.72f;
            tingInfoPanel.Visible = false;
            AddChild(tingInfoPanel);

            StyleBoxFlat tingSb = new StyleBoxFlat();
            tingSb.BgColor = new Color(0.08f, 0.14f, 0.20f, 0.92f);
            tingSb.CornerRadiusTopLeft = 8;
            tingSb.CornerRadiusTopRight = 8;
            tingSb.CornerRadiusBottomLeft = 8;
            tingSb.CornerRadiusBottomRight = 8;
            tingSb.BorderWidthLeft = 2;
            tingSb.BorderWidthTop = 2;
            tingSb.BorderWidthRight = 2;
            tingSb.BorderWidthBottom = 2;
            tingSb.BorderColor = new Color(0.95f, 0.80f, 0.30f);
            tingInfoPanel.AddThemeStyleboxOverride("panel", tingSb);

            tingInfoLabel = new Label();
            tingInfoLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            tingInfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            tingInfoLabel.VerticalAlignment = VerticalAlignment.Center;
            tingInfoLabel.AddThemeFontSizeOverride("font_size", 14);
            tingInfoLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.92f, 0.60f));
            tingInfoPanel.AddChild(tingInfoLabel);

            actionCalloutOverlay = new Control();
            actionCalloutOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            actionCalloutOverlay.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(actionCalloutOverlay);

            actionCalloutLabel = new Label();
            actionCalloutLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            actionCalloutLabel.HorizontalAlignment = HorizontalAlignment.Center;
            actionCalloutLabel.VerticalAlignment = VerticalAlignment.Center;
            actionCalloutLabel.AddThemeFontSizeOverride("font_size", 54);
            actionCalloutLabel.Visible = false;
            actionCalloutLabel.MouseFilter = MouseFilterEnum.Ignore;
            actionCalloutOverlay.AddChild(actionCalloutLabel);
        }

        private Button CreateActionButton(string text, Color color)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(90, 44);
            btn.AddThemeFontSizeOverride("font_size", 16);

            StyleBoxFlat normalSb = new StyleBoxFlat();
            normalSb.BgColor = color;
            normalSb.CornerRadiusTopLeft = 6;
            normalSb.CornerRadiusTopRight = 6;
            normalSb.CornerRadiusBottomLeft = 6;
            normalSb.CornerRadiusBottomRight = 6;
            normalSb.BorderWidthLeft = 2;
            normalSb.BorderWidthTop = 2;
            normalSb.BorderWidthRight = 2;
            normalSb.BorderWidthBottom = 2;
            normalSb.BorderColor = new Color(0.95f, 0.85f, 0.55f);
            btn.AddThemeStyleboxOverride("normal", normalSb);

            return btn;
        }

        private void BuildSettlementModal()
        {
            settlementModal = new Control();
            settlementModal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            settlementModal.Visible = false;
            AddChild(settlementModal);

            ColorRect mask = new ColorRect();
            mask.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mask.Color = new Color(0, 0, 0, 0.75f);
            settlementModal.AddChild(mask);

            PanelContainer panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(480, 320);
            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            settlementModal.AddChild(panel);

            StyleBoxFlat sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.12f, 0.16f, 0.22f);
            sb.CornerRadiusTopLeft = 12;
            sb.CornerRadiusTopRight = 12;
            sb.CornerRadiusBottomLeft = 12;
            sb.CornerRadiusBottomRight = 12;
            sb.BorderWidthLeft = 3;
            sb.BorderWidthTop = 3;
            sb.BorderWidthRight = 3;
            sb.BorderWidthBottom = 3;
            sb.BorderColor = new Color(0.95f, 0.80f, 0.35f);
            panel.AddThemeStyleboxOverride("panel", sb);

            VBoxContainer box = new VBoxContainer();
            box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            box.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(box);

            settlementTitle = new Label();
            settlementTitle.Text = "牌局结算";
            settlementTitle.HorizontalAlignment = HorizontalAlignment.Center;
            settlementTitle.AddThemeFontSizeOverride("font_size", 28);
            settlementTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.25f));
            box.AddChild(settlementTitle);

            settlementDetail = new Label();
            settlementDetail.HorizontalAlignment = HorizontalAlignment.Center;
            settlementDetail.AddThemeFontSizeOverride("font_size", 16);
            box.AddChild(settlementDetail);

            HBoxContainer btnRow = new HBoxContainer();
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;
            box.AddChild(btnRow);

            btnPlayAgain = CreateActionButton("再来一局", new Color(0.20f, 0.65f, 0.35f));
            btnReturnLobby = CreateActionButton("返回大厅", new Color(0.65f, 0.25f, 0.25f));

            btnRow.AddChild(btnPlayAgain);
            btnRow.AddChild(btnReturnLobby);

            btnPlayAgain.Pressed += () =>
            {
                settlementModal.Visible = false;
                StartNewGame();
            };

            btnReturnLobby.Pressed += () =>
            {
                GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScreen.tscn");
            };
        }

        private void StartNewGame()
        {
            isGameEnding = false;
            deck = ruleEngine.GenerateShuffledDeck();
            players = new List<PlayerHandData>
            {
                new PlayerHandData(0, "玩家", false),
                new PlayerHandData(1, "电脑东", true),
                new PlayerHandData(2, "电脑南", true),
                new PlayerHandData(3, "电脑西", true)
            };

            ruleEngine.DealTiles(deck, players, dealerIndex);

            // AI 随机选缺
            TileSuit[] suits = { TileSuit.Wan, TileSuit.Tiao, TileSuit.Tong };
            Random r = new Random();
            for (int i = 1; i < 4; i++)
            {
                players[i].LackSuit = suits[r.Next(3)];
            }

            // 提示玩家选缺
            lackSuitPanel.Visible = true;
            ShowAnnouncement("请选择选缺花色");
            RefreshAllViews();
        }

        private void SelectUserLackSuit(TileSuit suit)
        {
            players[0].LackSuit = suit;
            lackSuitPanel.Visible = false;
            ShowAnnouncement($"玩家选缺: {suit}");

            currentTurnIndex = dealerIndex;
            ProcessTurn();
        }

        private async void ProcessTurn()
        {
            if (isGameEnding) return;

            RefreshAllViews();
            PlayerHandData current = players[currentTurnIndex];
            turnIndicatorLabel.Text = $"当前回合: {current.PlayerName}";

            // 摸牌逻辑 (如果不是开局第一回合庄家)
            if (current.ConcealedTiles.Count % 3 == 1)
            {
                if (deck.Count == 0)
                {
                    EndGameDraw();
                    return;
                }

                MahjongTile drawn = deck.Dequeue();
                current.ConcealedTiles.Add(drawn);
                centerDeckLabel.Text = $"剩余牌数: {deck.Count}";
                RefreshAllViews();
            }

            // 本地玩家回合
            if (currentTurnIndex == 0)
            {
                // 检查玩家自摸胡或暗杠
                HuResult selfHu = ruleEngine.CheckHu(current, null, true);
                List<Meld> selfGangs = ruleEngine.CheckGang(current, null, true);

                actionBar.Visible = true;
                btnChi.Visible = false;
                btnPong.Visible = false;
                btnGang.Visible = selfGangs.Count > 0;
                btnHu.Visible = selfHu.IsHu;
                btnPass.Visible = selfHu.IsHu || selfGangs.Count > 0;
                btnDiscardAction.Visible = true;

                isWaitingForUserAction = true;
            }
            else
            {
                // AI 思考时间
                await Task.Delay(700);

                // AI 检查自摸胡或杠
                HuResult selfHu = ruleEngine.CheckHu(current, null, true);
                if (selfHu.IsHu)
                {
                    EndGameWin(currentTurnIndex, selfHu, null);
                    return;
                }

                MahjongTile discard = botAI.SelectDiscard(current);
                ExecuteDiscard(currentTurnIndex, discard);
            }
        }

        private void ExecuteDiscard(int playerIdx, MahjongTile tile)
        {
            PlayerHandData player = players[playerIdx];
            player.RemoveConcealedTile(tile);
            player.Discards.Add(tile);
            player.SortHand();

            lastDiscardedTile = tile;
            lastDiscardPlayerIndex = playerIdx;

            Mahjong.Audio.MahjongAudioManager.Instance?.PlaySfx("discard");
            ShowAnnouncement($"{player.PlayerName} 打出 [{tile.Name}]");
            RefreshAllViews();

            // 轮询其他玩家响应吃/碰/杠/胡
            CheckInterruptsAfterDiscard(tile, playerIdx);
        }

        private async void CheckInterruptsAfterDiscard(MahjongTile discard, int fromPlayerIdx)
        {
            // 检查玩家0 (人类玩家) 能否响应
            if (fromPlayerIdx != 0)
            {
                PlayerHandData p0 = players[0];
                HuResult hu = ruleEngine.CheckHu(p0, discard, false);
                bool pong = ruleEngine.CheckPong(p0, discard);
                List<Meld> gangs = ruleEngine.CheckGang(p0, discard, false, fromPlayerIdx);
                List<List<MahjongTile>> chi = (fromPlayerIdx == 3) ? ruleEngine.CheckChi(p0, discard) : new List<List<MahjongTile>>();

                if (hu.IsHu || pong || gangs.Count > 0 || chi.Count > 0)
                {
                    actionBar.Visible = true;
                    btnHu.Visible = hu.IsHu;
                    btnPong.Visible = pong;
                    btnGang.Visible = gangs.Count > 0;
                    btnChi.Visible = chi.Count > 0;
                    btnPass.Visible = true;
                    btnDiscardAction.Visible = false;

                    isWaitingForUserAction = true;
                    return;
                }
            }

            // AI 响应检查
            for (int i = 0; i < 4; i++)
            {
                if (i == fromPlayerIdx || i == 0) continue;
                PlayerHandData bot = players[i];
                HuResult hu = ruleEngine.CheckHu(bot, discard, false);
                if (hu.IsHu)
                {
                    await Task.Delay(500);
                    EndGameWin(i, hu, discard);
                    return;
                }
            }

            // 无人响应，进入下一玩家回合
            await Task.Delay(500);
            currentTurnIndex = (fromPlayerIdx + 1) % 4;
            ProcessTurn();
        }

        private void OnUserActionClicked(string action)
        {
            if (!isWaitingForUserAction) return;

            actionBar.Visible = false;
            isWaitingForUserAction = false;

            PlayerHandData p0 = players[0];

            if (action == "DISCARD")
            {
                if (selectedTileView != null && selectedTileView.TileData != null)
                {
                    MahjongTile t = selectedTileView.TileData;
                    selectedTileView = null;
                    ExecuteDiscard(0, t);
                }
                else
                {
                    // 默认打出最后一张牌
                    MahjongTile t = p0.ConcealedTiles[p0.ConcealedTiles.Count - 1];
                    ExecuteDiscard(0, t);
                }
            }
            else if (action == "HU")
            {
                ShowAnnouncement("🀄 玩家 喊牌 胡牌！");
                HuResult hu = ruleEngine.CheckHu(p0, lastDiscardedTile, currentTurnIndex == 0);
                EndGameWin(0, hu, lastDiscardedTile);
            }
            else if (action == "PONG")
            {
                ShowAnnouncement("🀄 玩家 喊牌 碰牌！");
                if (lastDiscardedTile != null)
                {
                    p0.RemoveConcealedTile(lastDiscardedTile);
                    p0.RemoveConcealedTile(lastDiscardedTile);
                    p0.Melds.Add(new Meld(MeldType.Pong, lastDiscardedTile, new List<MahjongTile> { lastDiscardedTile, lastDiscardedTile, lastDiscardedTile }, lastDiscardPlayerIndex));
                    
                    if (lastDiscardPlayerIndex >= 0 && lastDiscardPlayerIndex < players.Count)
                    {
                        var discards = players[lastDiscardPlayerIndex].Discards;
                        if (discards.Count > 0) discards.RemoveAt(discards.Count - 1);
                    }
                }
                currentTurnIndex = 0;
                RefreshAllViews();

                actionBar.Visible = true;
                btnChi.Visible = false;
                btnPong.Visible = false;
                btnGang.Visible = false;
                btnHu.Visible = false;
                btnPass.Visible = false;
                btnDiscardAction.Visible = true;
                isWaitingForUserAction = true;
            }
            else if (action == "CHI")
            {
                ShowAnnouncement("🀄 玩家 喊牌 吃牌！");
                if (lastDiscardedTile != null)
                {
                    var chiOptions = ruleEngine.CheckChi(p0, lastDiscardedTile);
                    if (chiOptions.Count > 0)
                    {
                        var opt = chiOptions[0];
                        foreach (var tile in opt)
                        {
                            if (tile.Suit != lastDiscardedTile.Suit || tile.Value != lastDiscardedTile.Value)
                            {
                                p0.RemoveConcealedTile(tile);
                            }
                        }
                        p0.Melds.Add(new Meld(MeldType.Chi, lastDiscardedTile, opt, lastDiscardPlayerIndex));
                        
                        if (lastDiscardPlayerIndex >= 0 && lastDiscardPlayerIndex < players.Count)
                        {
                            var discards = players[lastDiscardPlayerIndex].Discards;
                            if (discards.Count > 0) discards.RemoveAt(discards.Count - 1);
                        }
                    }
                }
                currentTurnIndex = 0;
                RefreshAllViews();

                actionBar.Visible = true;
                btnChi.Visible = false;
                btnPong.Visible = false;
                btnGang.Visible = false;
                btnHu.Visible = false;
                btnPass.Visible = false;
                btnDiscardAction.Visible = true;
                isWaitingForUserAction = true;
            }
            else if (action == "GANG")
            {
                var gangs = ruleEngine.CheckGang(p0, lastDiscardedTile, currentTurnIndex == 0, lastDiscardPlayerIndex);
                if (gangs.Count > 0)
                {
                    var g = gangs[0];
                    if (g.Type == MeldType.MingGang && lastDiscardedTile != null)
                    {
                        for (int i = 0; i < 3; i++) p0.RemoveConcealedTile(lastDiscardedTile);
                        p0.Melds.Add(g);
                        if (lastDiscardPlayerIndex >= 0 && lastDiscardPlayerIndex < players.Count)
                        {
                            var discards = players[lastDiscardPlayerIndex].Discards;
                            if (discards.Count > 0) discards.RemoveAt(discards.Count - 1);
                            players[lastDiscardPlayerIndex].Score -= 300;
                            p0.Score += 300;
                        }
                        ShowAnnouncement($"⛈ 刮风下雨！玩家 明杠 获得点杠者 +300 金币");
                    }
                    else if (g.Type == MeldType.AnGang)
                    {
                        for (int i = 0; i < 4; i++) p0.RemoveConcealedTile(g.TargetTile);
                        p0.Melds.Add(g);
                        p0.Score += 600;
                        for (int i = 1; i < 4; i++) players[i].Score -= 200;
                        ShowAnnouncement("🌪 刮风下雨！玩家 暗杠 获得三家 +600 金币");
                    }
                    else if (g.Type == MeldType.BuGang)
                    {
                        p0.RemoveConcealedTile(g.TargetTile);
                        var oldMeldIdx = p0.Melds.FindIndex(m => m.Type == MeldType.Pong && m.TargetTile.Suit == g.TargetTile.Suit && m.TargetTile.Value == g.TargetTile.Value);
                        if (oldMeldIdx >= 0) p0.Melds[oldMeldIdx] = g;
                        else p0.Melds.Add(g);
                        p0.Score += 300;
                        for (int i = 1; i < 4; i++) players[i].Score -= 100;
                        ShowAnnouncement("⚡ 刮风下雨！玩家 补杠 获得三家 +300 金币");
                    }

                    // 杠后摸补牌 (摸一张牌)
                    if (deck.Count == 0)
                    {
                        EndGameDraw();
                        return;
                    }

                    MahjongTile drawn = deck.Dequeue();
                    p0.ConcealedTiles.Add(drawn);
                    centerDeckLabel.Text = $"剩余牌数: {deck.Count}";
                }
                
                currentTurnIndex = 0;
                RefreshAllViews();

                HuResult selfHu = ruleEngine.CheckHu(p0, null, true);
                List<Meld> selfGangs = ruleEngine.CheckGang(p0, null, true);

                actionBar.Visible = true;
                btnChi.Visible = false;
                btnPong.Visible = false;
                btnGang.Visible = selfGangs.Count > 0;
                btnHu.Visible = selfHu.IsHu;
                btnPass.Visible = selfHu.IsHu || selfGangs.Count > 0;
                btnDiscardAction.Visible = true;
                isWaitingForUserAction = true;
            }
            else if (action == "PASS")
            {
                if (currentTurnIndex == 0)
                {
                    // 本地玩家回合弃胡/杠，要求出牌
                    actionBar.Visible = true;
                    btnChi.Visible = false;
                    btnPong.Visible = false;
                    btnGang.Visible = false;
                    btnHu.Visible = false;
                    btnPass.Visible = false;
                    btnDiscardAction.Visible = true;
                    isWaitingForUserAction = true;
                }
                else
                {
                    // 跳过响应，转下一玩家
                    currentTurnIndex = (lastDiscardPlayerIndex + 1) % 4;
                    ProcessTurn();
                }
            }
        }

        private void EndGameWin(int winnerIdx, HuResult huRes, MahjongTile? winTile)
        {
            isGameEnding = true;
            PlayerHandData winner = players[winnerIdx];

            int basePoints = huRes.Points;
            int[] goldChanges = new int[4];

            if (huRes.IsSelfDraw)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == winnerIdx) continue;
                    goldChanges[i] = -basePoints;
                    goldChanges[winnerIdx] += basePoints;
                }
            }
            else
            {
                int paoIdx = (lastDiscardPlayerIndex >= 0) ? lastDiscardPlayerIndex : (winnerIdx + 3) % 4;
                for (int i = 0; i < 4; i++)
                {
                    if (i == winnerIdx) continue;
                    int pay = (i == paoIdx) ? basePoints * 2 : basePoints;
                    goldChanges[i] = -pay;
                    goldChanges[winnerIdx] += pay;
                }
            }

            int p0Change = goldChanges[0];
            GameState.Instance?.AddGold(p0Change);
            int currentGold = GameState.Instance?.Gold ?? 1000;

            ShowAnnouncement($"恭喜 [{winner.PlayerName}] 胡牌！ ({huRes.HuName})");

            settlementTitle.Text = winnerIdx == 0 ? "🎉 大吉大利，扣点胜！" : "💔 遗憾落败";

            string billingStr = $"💰 金币战绩账单明细:\n" +
                                $"----------------------------------------\n" +
                                $"【玩家】: {(goldChanges[0] >= 0 ? "+" : "")}{goldChanges[0]} 金币 (总余额: {currentGold})\n" +
                                $"【电脑东】: {(goldChanges[1] >= 0 ? "+" : "")}{goldChanges[1]} 金币\n" +
                                $"【电脑南】: {(goldChanges[2] >= 0 ? "+" : "")}{goldChanges[2]} 金币\n" +
                                $"【电脑西】: {(goldChanges[3] >= 0 ? "+" : "")}{goldChanges[3]} 金币";

            settlementDetail.Text = $"获胜玩家: {winner.PlayerName}\n" +
                                   $"胡牌牌型: {huRes.HuName}\n" +
                                   $"结算番数: {huRes.Fan} 番 | 扣点底分: {basePoints} 分\n\n" +
                                   billingStr;

            settlementModal.Visible = true;
        }

        private void EndGameDraw()
        {
            isGameEnding = true;
            ShowAnnouncement("牌墙已空，本局流局！");

            settlementTitle.Text = "⚖ 牌局流局 (平局)";
            settlementDetail.Text = "牌墙 136 张麻将牌已全部摸完，无人胡牌。\n所有玩家金币无变动。";
            settlementModal.Visible = true;
        }

        private void ShowAnnouncement(string msg)
        {
            announcementLabel.Text = msg;
            announcementLabel.Visible = true;
        }

        private void RefreshAllViews()
        {
            RefreshPlayerHand(player0HandContainer, players[0], false);
            RefreshPlayerMeld(player0MeldContainer, players[0]);
            RefreshPlayerRiver(player0RiverContainer, players[0]);

            RefreshPlayerHand(player1HandContainer, players[1], true);
            RefreshPlayerMeld(player1MeldContainer, players[1]);
            RefreshPlayerRiver(player1RiverContainer, players[1]);

            RefreshPlayerHand(player2HandContainer, players[2], true);
            RefreshPlayerMeld(player2MeldContainer, players[2]);
            RefreshPlayerRiver(player2RiverContainer, players[2]);

            RefreshPlayerHand(player3HandContainer, players[3], true);
            RefreshPlayerMeld(player3MeldContainer, players[3]);
            RefreshPlayerRiver(player3RiverContainer, players[3]);
        }

        private void RefreshPlayerMeld(Control container, PlayerHandData hand)
        {
            foreach (Node c in container.GetChildren()) c.QueueFree();

            foreach (var meld in hand.Melds)
            {
                HBoxContainer meldGroup = new HBoxContainer();
                meldGroup.AddThemeConstantOverride("separation", 2);
                container.AddChild(meldGroup);

                foreach (var tile in meld.Tiles)
                {
                    MahjongTileView tv = new MahjongTileView();
                    meldGroup.AddChild(tv);
                    tv.Setup(tile, false, true);
                }
            }
        }

        private void RefreshPlayerHand(Control container, PlayerHandData hand, bool isFaceDown)
        {
            foreach (Node c in container.GetChildren()) c.QueueFree();

            bool hasDrawnTileGap = (hand.ConcealedTiles.Count % 3 == 2);

            for (int i = 0; i < hand.ConcealedTiles.Count; i++)
            {
                var tile = hand.ConcealedTiles[i];

                if (hasDrawnTileGap && i == hand.ConcealedTiles.Count - 1)
                {
                    Control gap = new Control();
                    if (container is VBoxContainer)
                        gap.CustomMinimumSize = new Vector2(0, 14);
                    else
                        gap.CustomMinimumSize = new Vector2(14, 0);
                    container.AddChild(gap);
                }

                MahjongTileView tv = new MahjongTileView();
                container.AddChild(tv);
                tv.Setup(tile, isFaceDown);

                if (!isFaceDown)
                {
                    tv.TileClicked += (view) =>
                    {
                        if (selectedTileView != null && selectedTileView != view)
                        {
                            selectedTileView.SetSelected(false);
                        }
                        selectedTileView = view;
                        selectedTileView.SetSelected(true);
                        HighlightMatchingTilesOnField(selectedTileView?.TileData);
                    };

                    tv.TileDoubleClicked += (view) =>
                    {
                        if (currentTurnIndex == 0 && isWaitingForUserAction && btnDiscardAction.Visible)
                        {
                            selectedTileView = view;
                            OnUserActionClicked("DISCARD");
                        }
                    };
                }
            }
        }

        private void HighlightMatchingTilesOnField(MahjongTile? target)
        {
            Control[] riverContainers = { player0RiverContainer, player1RiverContainer, player2RiverContainer, player3RiverContainer };
            Control[] meldContainers = { player0MeldContainer, player1MeldContainer, player2MeldContainer, player3MeldContainer };

            foreach (var container in riverContainers)
            {
                if (container == null) continue;
                foreach (Node node in container.GetChildren())
                {
                    if (node is MahjongTileView tv && tv.TileData != null)
                    {
                        bool match = (target != null && tv.TileData.Suit == target.Suit && tv.TileData.Value == target.Value);
                        tv.SetFieldHighlight(match);
                    }
                }
            }

            foreach (var container in meldContainers)
            {
                if (container == null) continue;
                foreach (Node groupNode in container.GetChildren())
                {
                    foreach (Node node in groupNode.GetChildren())
                    {
                        if (node is MahjongTileView tv && tv.TileData != null)
                        {
                            bool match = (target != null && tv.TileData.Suit == target.Suit && tv.TileData.Value == target.Value);
                            tv.SetFieldHighlight(match);
                        }
                    }
                }
            }
        }

        private void RefreshPlayerRiver(Control container, PlayerHandData hand)
        {
            foreach (Node c in container.GetChildren()) c.QueueFree();

            for (int i = 0; i < hand.Discards.Count; i++)
            {
                var tile = hand.Discards[i];
                MahjongTileView tv = new MahjongTileView();
                container.AddChild(tv);
                tv.Setup(tile, false, true);

                if (i == hand.Discards.Count - 1 && tile == lastDiscardedTile)
                {
                    tv.SetLatestDiscardMarker(true);
                    tv.AnimateDiscardSlap();
                }
            }
        }
    }
}
