using Godot;
using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong
{
    public partial class MahjongNetManager : Node
    {
        public static MahjongNetManager Instance { get; private set; } = null!;

        public const int DefaultPort = 9010;

        public bool IsNetworked { get; set; } = false;
        public bool IsHost => !IsNetworked || Multiplayer.IsServer();
        public int LocalPlayerIndex { get; set; } = 0;

        public string RoomName { get; set; } = "扣点点麻将房间";
        public int MaxPlayers { get; set; } = 4;
        public List<string> ConnectedPlayerNames { get; private set; } = new List<string>();

        public event Action<int, string>? PlayerJoined;
        public event Action<int>? PlayerLeft;

        public override void _Ready()
        {
            Instance = this;
        }

        public Error CreateOnlineRoom(int port = DefaultPort)
        {
            DisconnectRoom();
            ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
            Error err = peer.CreateServer(port, 4);
            if (err == Error.Ok)
            {
                Multiplayer.MultiplayerPeer = peer;
                IsNetworked = true;
                LocalPlayerIndex = 0;
                ConnectedPlayerNames.Clear();
                ConnectedPlayerNames.Add("房主(玩家)");

                Multiplayer.PeerConnected += OnPeerConnected;
                Multiplayer.PeerDisconnected += OnPeerDisconnected;
                GD.Print($"[MahjongNet] 成功创建麻将在线房间 Port: {port}");
            }
            return err;
        }

        public Error JoinOnlineRoom(string ipAddress, int port = DefaultPort)
        {
            DisconnectRoom();
            ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
            Error err = peer.CreateClient(ipAddress, port);
            if (err == Error.Ok)
            {
                Multiplayer.MultiplayerPeer = peer;
                IsNetworked = true;
                ConnectedPlayerNames.Clear();

                Multiplayer.ConnectedToServer += OnConnectedToServer;
                Multiplayer.ConnectionFailed += OnConnectionFailed;
                GD.Print($"[MahjongNet] 正在连接麻将在线房间 {ipAddress}:{port}");
            }
            return err;
        }

        public void DisconnectRoom()
        {
            if (Multiplayer != null)
            {
                Multiplayer.PeerConnected -= OnPeerConnected;
                Multiplayer.PeerDisconnected -= OnPeerDisconnected;
                Multiplayer.ConnectedToServer -= OnConnectedToServer;
                Multiplayer.ConnectionFailed -= OnConnectionFailed;
                if (IsNetworked && Multiplayer.MultiplayerPeer != null)
                {
                    Multiplayer.MultiplayerPeer.Close();
                    Multiplayer.MultiplayerPeer = null;
                }
            }
            IsNetworked = false;
            ConnectedPlayerNames.Clear();
        }

        private void OnPeerConnected(long id)
        {
            int index = ConnectedPlayerNames.Count;
            string name = $"玩家_{id}";
            ConnectedPlayerNames.Add(name);
            PlayerJoined?.Invoke(index, name);
            GD.Print($"[MahjongNet] 玩家加入: {name} (ID: {id})");
        }

        private void OnPeerDisconnected(long id)
        {
            GD.Print($"[MahjongNet] 玩家离开 ID: {id}");
        }

        private void OnConnectedToServer()
        {
            LocalPlayerIndex = Multiplayer.GetUniqueId() % 4;
            GD.Print($"[MahjongNet] 已连接到服务器，分配索引: {LocalPlayerIndex}");
        }

        public Error SmartQuickMatch(int port = DefaultPort)
        {
            Error err = CreateOnlineRoom(port);
            if (err != Error.Ok)
            {
                GD.Print($"[MahjongNet] 端口 {port} 已存在服务端，自动作为客户端加入...");
                err = JoinOnlineRoom("127.0.0.1", port);
            }
            return err;
        }

        private void OnConnectionFailed()
        {
            GD.PrintErr("[MahjongNet] 目标服务器未打开，已自动开启本地房间！");
            CreateOnlineRoom(DefaultPort);
        }
    }
}
