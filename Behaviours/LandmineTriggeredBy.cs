using UnityEngine;

namespace PlayerKillCounter.Behaviours {
    public class LandmineTriggeredBy : MonoBehaviour, ICausedBy {
        
        public ulong TriggeredByClientId { get; set; }
        
        public ulong GetCausingPlayerId() {
            return TriggeredByClientId;
        }

    }
}