using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayerKillCounter.Behaviours {
    public class KillCounter : MonoBehaviour {

        public struct Info {
            public ulong KillerClientId;
            public CauseOfDeath CauseOfDeath;
        }
        
        public Dictionary<ulong, Info> KilledBy { get; private set; }

        public void Start() {
            KilledBy = new Dictionary<ulong, Info>();
        }
    }
}