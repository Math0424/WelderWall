using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

/// <summary>
/// Example usage
/// 
/// Init()
///    EasyNetworker.Init(ChannelID)
///    EasyNetworker.RegisterPacket<PacketHere>(PacketInMethod)
/// 
/// PacketInMethod(PacketHere e)
///    // do stuff here
/// 
/// </summary>
namespace WelderWall.Data.Scripts.Math0424.WelderWall.Util
{
    /// <summary>
    /// Author: Math0424
    /// Version 1.5
    /// Feel free to use in your own projects
    /// </summary>
    public static class EasyNetworker
    {

        /// <summary>
        /// Invoked before sending to players. serverside only.
        /// Use this action to verify packets and make 
        /// sure no funny business is happening. 
        /// </summary>
        public static Action<Type, PacketIn> ProcessPacket;

        public enum TransitType
        {
            ToServer = 1,
            ToAll = 2,
            ExcludeSender = 4,
        }

        private static ushort _commsId;
        private static List<IMyPlayer> _tempPlayers;
        private static Dictionary<Type, Action<PacketIn>> _registry;
        private static Dictionary<string, Type> _table;

        public static bool Initalized => _commsId != 0;

        public static void Init(ushort commsId)
        {
            _commsId = commsId;
            _tempPlayers = new List<IMyPlayer>();
            _registry = new Dictionary<Type, Action<PacketIn>>();
            _table = new Dictionary<string, Type>();

            UnRegister();
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_commsId, RecivedPacket);
            if (MyAPIGateway.Entities != null)
                MyAPIGateway.Entities.OnCloseAll += UnRegister;
        }

        private static void UnRegister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_commsId, RecivedPacket);
        }

        public static void Close()
        {
            _registry.Clear();
            _table.Clear();
            _tempPlayers.Clear();
            UnRegister();
        }

        public static void UnRegisterPacket<T>()
        {
            var type = typeof(T);
            if (_table.ContainsKey(type.FullName))
            {
                _registry.Remove(type);
                _table.Remove(type.FullName);
            }
        }

        public static void RegisterPacket<T>(Action<T> callee)
        {
            var type = typeof(T);
            if (_table.ContainsKey(type.FullName))
                MyLog.Default.WriteLineAndConsole($"Replacing registered delegate for '{type.FullName}'");
            _registry[type] = (e) => callee.Invoke(e.UnWrap<T>());
            _table[type.FullName] = type;
        }

        /// <summary>
        /// Sends a packet to the players from server
        /// </summary>
        /// <param name="obj"></param>
        public static void SendToPlayers(ulong[] playerIds, object obj, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, TransitType.ToServer);
            packet.Wrap(obj);
            foreach (var x in playerIds)
                MyAPIGateway.Multiplayer.SendMessageTo(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), x, reliable);
        }

        /// <summary>
        /// Send a packet to the a player from server
        /// </summary>
        /// <param name="obj"></param>
        public static void SendToPlayer(ulong playerId, object obj, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, TransitType.ToServer);
            packet.Wrap(obj);
            MyAPIGateway.Multiplayer.SendMessageTo(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), playerId, reliable);
        }

        /// <summary>
        /// Send a packet to the server
        /// </summary>
        /// <param name="obj"></param>
        public static void SendToServer(object obj, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, TransitType.ToServer);
            packet.Wrap(obj);
            MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), reliable);
        }

        /// <summary>
        /// Send to all players within your current sync range
        /// DO NOT USE ON THE SERVER
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="flag"></param>
        public static void SendToSyncRange(object obj, TransitType flag, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, flag);
            packet.Wrap(obj);
            packet.Range = MyAPIGateway.Session.SessionSettings.SyncDistance;
            packet.TransmitLocation = MyAPIGateway.Session.Player.GetPosition();
            MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), reliable);
        }

        /// <summary>
        /// Send to all players within your current sync range
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="flag"></param>
        public static void SendToSyncRange(object obj, IMyEntity center, TransitType flag, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, flag);
            packet.Wrap(obj);
            packet.Range = MyAPIGateway.Session.SessionSettings.SyncDistance;
            packet.TransmitLocation = center.GetPosition();
            MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), reliable);
        }

        /// <summary>
        /// Transmit to all players, optionally including the sending player
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="excludeSender"></param>
        public static void SendToAllPlayers(object obj, bool excludeSender, bool reliable = true)
        {
            //Validate(obj);
            ServerPacket packet = new ServerPacket(obj.GetType().FullName, TransitType.ToAll | (excludeSender ? TransitType.ExcludeSender : 0));
            packet.Wrap(obj);
            MyAPIGateway.Multiplayer.SendMessageToServer(_commsId, MyAPIGateway.Utilities.SerializeToBinary(packet), reliable);
        }

        private static void RecivedPacket(ushort handler, byte[] raw, ulong id, bool isFromServer)
        {
            try
            {
                ServerPacket packet = MyAPIGateway.Utilities.SerializeFromBinary<ServerPacket>(raw);
                if (!_table.ContainsKey(packet.ID))
                    return;

                PacketIn packetIn = new PacketIn(packet.Data, id, isFromServer);
                ProcessPacket?.Invoke(_table[packet.ID], packetIn);
                if (packetIn.IsCancelled)
                    return;

                if (isFromServer)
                {
                    if (MyAPIGateway.Multiplayer.IsServer)
                    {
                        if (!packet.Flag.HasFlag(TransitType.ExcludeSender))
                            _registry[_table[packet.ID]]?.Invoke(packetIn);
                    }
                    else
                    {
                        _registry[_table[packet.ID]]?.Invoke(packetIn);
                    }
                }

                if (MyAPIGateway.Multiplayer.IsServer && packet.Flag.HasFlag(TransitType.ToAll))
                {
                    TransmitPacket(id, packet);
                }

            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"Malformed packet from {id}!");
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}\n\n{e.InnerException}\n\n{e.Source}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[Mod critical error! | Send SpaceEngineers.Log]", 10000, MyFontEnum.Red);
            }
        }

        public static IMyPlayer GetPlayer(ulong playerID)
        {
            if (MyAPIGateway.Session?.Player.SteamUserId == playerID)
                return MyAPIGateway.Session.Player;

            UpdatePlayers();
            foreach (var x in _tempPlayers)
                if (x.SteamUserId == playerID)
                    return x;
            return null;
        }

        public static IMyPlayer[] GetPlayers()
        {
            if (MyAPIGateway.Session?.MultiplayerAlive ?? false)
                return new IMyPlayer[] { MyAPIGateway.Session.Player };

            UpdatePlayers();
            return _tempPlayers.ToArray();
        }

        private static void UpdatePlayers()
        {
            lock (_tempPlayers)
            {
                _tempPlayers.Clear();
                MyAPIGateway.Players.GetPlayers(_tempPlayers);
            }
        }

        /// <summary>
        /// [Server method]
        /// Send to all players
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="packet"></param>
        private static void TransmitPacket(ulong sender, ServerPacket packet)
        {
            UpdatePlayers();

            foreach (var p in _tempPlayers.ToArray())
            {
                if (p.IsBot || packet.Flag.HasFlag(TransitType.ExcludeSender) && p.SteamUserId == sender ||
                    MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Session?.Player?.SteamUserId == sender)
                    continue;

                ServerPacket send = new ServerPacket(packet.ID, TransitType.ToAll, true);
                send.Data = packet.Data;

                if (packet.Range != -1)
                {
                    if (packet.Range >= Vector3D.Distance(p.GetPosition(), packet.TransmitLocation))
                    {
                        MyAPIGateway.Multiplayer.SendMessageTo(_commsId, MyAPIGateway.Utilities.SerializeToBinary(send), p.SteamUserId);
                    }
                }
                else
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(_commsId, MyAPIGateway.Utilities.SerializeToBinary(send), p.SteamUserId);
                }
            }
        }

        [ProtoContract]
        private class ServerPacket
        {

            [ProtoMember(1)] public string ID;
            [ProtoMember(2)] public int Range = -1;
            [ProtoMember(3)] public Vector3D TransmitLocation = Vector3D.Zero;
            [ProtoMember(4)] public TransitType Flag;
            [ProtoMember(5)] public byte[] Data;
            [ProtoMember(6)] public bool Final;

            public ServerPacket() { }

            public ServerPacket(string Id, TransitType Flag, bool final = false)
            {
                ID = Id;
                this.Flag = Flag;
                Final = final;
            }

            public void Wrap(object data)
            {
                Data = MyAPIGateway.Utilities.SerializeToBinary(data);
            }
        }
    }

    [ProtoContract]
    public class PacketIn
    {
        [ProtoMember(1)] public bool IsCancelled { protected set; get; }
        [ProtoMember(2)] public ulong SenderId { protected set; get; }
        [ProtoMember(3)] public bool IsFromServer { protected set; get; }

        [ProtoMember(4)] private readonly byte[] Data;

        public PacketIn(byte[] data, ulong senderId, bool isFromServer)
        {
            SenderId = senderId;
            IsFromServer = isFromServer;
            Data = data;
        }

        public T UnWrap<T>()
        {
            return MyAPIGateway.Utilities.SerializeFromBinary<T>(Data);
        }

        public void SetCancelled(bool value)
        {
            IsCancelled = value;
        }
    }
}
