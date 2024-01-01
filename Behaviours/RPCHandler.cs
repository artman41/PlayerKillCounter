using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace PlayerKillCounter.Behaviours {
    public class RPCHandler : NetworkBehaviour {

        private static RPCHandler _instance;
        public static RPCHandler Instance => _instance;

        public enum Direction {
            UNKNOWN,
            TO_SERVER,
            TO_CLIENT
        }

        public enum Action {
            UNKNOWN,
            UPDATE_KILL_COUNTER
        }

        private bool IsClient =>  __rpc_exec_stage != __RpcExecStage.Client && (NetworkManager.IsServer || NetworkManager.IsHost);
        private bool IsServer =>  __rpc_exec_stage != __RpcExecStage.Server && (NetworkManager.IsClient || NetworkManager.IsHost);

        public void Send(Action action, params object[] args) {
            if (action == Action.UNKNOWN)
                throw new ArgumentException("UNKNOWN passed as the action!", "action");
            NetworkManager networkManager = NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;
            if (IsClient)
                SendToServer(action, args);
            else if(IsServer)
                SendToClient(action, args);
        }

        private void SendToServer(Action action, params object[] args) {
            ServerRpcParams serverRpcParams = new ServerRpcParams();
            FastBufferWriter bufferWriter = __beginSendServerRpc(Statics.NETWORK_ID, serverRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)Direction.TO_SERVER);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)action);
            switch (action) {
                case Action.UPDATE_KILL_COUNTER when args.Length == 2:
                    var targetPlayerClientId = (ulong)args[0];
                    var encodedInfo = (byte[])args[1];
                    BytePacker.WriteValueBitPacked(bufferWriter, targetPlayerClientId);
                    BytePacker.WriteValueBitPacked(bufferWriter, encodedInfo.Length);
                    bufferWriter.WriteBytes(encodedInfo);
                    break;
                default:
                    Mod.Logger.LogError($"Unknown combination of {action} and {args}");
                    return;
            }
            __endSendServerRpc(ref bufferWriter, Statics.NETWORK_ID, serverRpcParams, RpcDelivery.Reliable);
        }

        private void SendToClient(Action action, params object[] args) {
            ClientRpcParams clientRpcParams = new ClientRpcParams();
            FastBufferWriter bufferWriter = this.__beginSendClientRpc(Statics.NETWORK_ID, clientRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)Direction.TO_CLIENT);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)action);
            switch (action) {
                case Action.UPDATE_KILL_COUNTER when args.Length == 2:
                    var targetPlayerClientId = (ulong)args[0];
                    var encodedInfo = (byte[])args[1];
                    BytePacker.WriteValueBitPacked(bufferWriter, targetPlayerClientId);
                    BytePacker.WriteValueBitPacked(bufferWriter, encodedInfo.Length);
                    bufferWriter.WriteBytes(encodedInfo);
                    break;
                default:
                    Mod.Logger.LogError($"Unknown combination of {action} and {args}");
                    return;
            }
            __endSendClientRpc(ref bufferWriter, Statics.NETWORK_ID, clientRpcParams, RpcDelivery.Reliable);
        }

        public void Awake() {
            _instance = this;
            foreach (var (key, value) in NetworkManager.__rpc_func_table) {
                Mod.Logger.LogInfo($"id: {key}, handler: {value}");
            }

            Mod.Logger.LogInfo($"Largest id is {NetworkManager.__rpc_func_table.Keys.Max(o => o)}");
            
            NetworkManager.__rpc_func_table.Add(Statics.NETWORK_ID, RPCReceiveHandler);
        }

        private void RPCReceiveHandler(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams) {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;

            if (!ReadHeader(ref reader, out Direction direction, out Action action))
                return;

            HandleAction(target, reader, rpcParams, direction, action);
        }

        private bool ReadHeader(ref FastBufferReader reader, out Direction direction, out Action action) {
            
            uint directionId;
            ByteUnpacker.ReadValueBitPacked(reader, out directionId);
            try {
                direction = (Direction)directionId;
            } catch (InvalidCastException _) {
                Mod.Logger.LogError($"Unknown message direction {directionId}");
                direction = Direction.UNKNOWN;
                action = Action.UNKNOWN;
                return false;
            }
            
            uint actionId;
            ByteUnpacker.ReadValueBitPacked(reader, out actionId);
            try {
                action = (Action)actionId;
            } catch (InvalidCastException _) {
                Mod.Logger.LogError($"Unknown action id {actionId}");
                action = Action.UNKNOWN;
                return false;
            }

            return true;
        }

        private void HandleAction(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams, Direction direction, Action action) {
            if (IsClient && direction == Direction.TO_CLIENT)
                HandleClientAction(target, reader, rpcParams, action);
            else if (IsServer && direction == Direction.TO_SERVER)
                HandleServerAction(target, reader, rpcParams, action);
        }

        private void HandleClientAction(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams, Action action) {
            switch (action) {
                case Action.UPDATE_KILL_COUNTER:
                    ByteUnpacker.ReadValueBitPacked(reader, out ulong targetPlayerClientId);
                    if (KillCounter.Instance.ContainsPlayerClientId(targetPlayerClientId))
                        return;
                    ByteUnpacker.ReadValueBitPacked(reader, out int encodedInfoLength);
                    byte[] encodedInfo = new byte[encodedInfoLength];
                    reader.ReadBytes(ref encodedInfo, encodedInfoLength);
                    var info = new KillCounter.Info().Decode(encodedInfo);
                    KillCounter.Instance.Add(targetPlayerClientId, info, false);
                    break;
            }
        }

        private void HandleServerAction(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams, Action action) {
            ClientRpcParams clientRpcParams = new ClientRpcParams();
            FastBufferWriter bufferWriter = __beginSendClientRpc(Statics.NETWORK_ID, clientRpcParams, RpcDelivery.Reliable);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)Direction.TO_CLIENT);
            BytePacker.WriteValueBitPacked(bufferWriter, (uint)action);
            bufferWriter.WriteBytes(reader.ToArray());
            __endSendClientRpc(ref bufferWriter, Statics.NETWORK_ID, clientRpcParams, RpcDelivery.Reliable);
        }
    }
}