using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PlayerKillCounter.Behaviours {
    public class KillCounter : MonoBehaviour {
        private static KillCounter _instance;
        public static KillCounter Instance => _instance;

        public struct Info {
            public ulong KillerClientId;
            public CauseOfDeath CauseOfDeath;

            public byte[] Encode() {
                FastBufferWriter bufferWriter = new FastBufferWriter(1024, Allocator.Temp, sizeof(ulong) + sizeof(uint));
                BytePacker.WriteValueBitPacked(bufferWriter, KillerClientId);
                BytePacker.WriteValueBitPacked(bufferWriter, (uint)CauseOfDeath);
                return bufferWriter.ToArray();
            }

            public Info Decode(byte[] data) {
                FastBufferReader bufferReader = new FastBufferReader(data, Allocator.Temp);
                ByteUnpacker.ReadValueBitPacked(bufferReader, out KillerClientId);
                uint _causeOfDeathId;
                ByteUnpacker.ReadValueBitPacked(bufferReader, out _causeOfDeathId);
                try {
                    CauseOfDeath = (CauseOfDeath)_causeOfDeathId;
                }
                catch (InvalidCastException _) {
                    Mod.Logger.LogError($"Unknown CauseOfDeath Id {_causeOfDeathId}");
                    CauseOfDeath = CauseOfDeath.Unknown;
                }

                return this;
            }
        }
        
        private Dictionary<ulong, Info> KilledBy { get; set; }

        public void Start() {
            _instance = this;
            KilledBy = new Dictionary<ulong, Info>();
        }

        public void Add(ulong targetPlayerClientId, Info info, bool broadcast = true) {
            if(broadcast)
                RPCHandler.Instance.Send(RPCHandler.Action.UPDATE_KILL_COUNTER, targetPlayerClientId, info.Encode());
            KilledBy.Add(targetPlayerClientId, info);
        }

        public bool ContainsPlayerClientId(ulong targetPlayerClientId) {
            return KilledBy.ContainsKey(targetPlayerClientId);
        }

        public void Clear() {
            KilledBy.Clear();
        }

        public override string ToString() {
            return KilledBy.Keys.Join(o => o.ToString());
        }

        public Info this[ulong targetPlayerClientId] => KilledBy[targetPlayerClientId];
    }
}