using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace PlayerKillCounter {
    [BepInPlugin(Statics.GUID, Statics.NAME, Statics.VERSION)]
    public class Mod : BaseUnityPlugin {
        private static ManualLogSource _logger;
        public new static ManualLogSource Logger => _logger;

        public void Awake() { 
            _logger = base.Logger;
            Logger.LogInfo("Loading harmony...");
            Harmony val = new Harmony(GetType().FullName);
            try {
                foreach (var t in typeof(Patches).GetNestedTypes()) {
                    if(t.IsClass && t.IsAbstract && t.IsSealed) {
                        Logger.LogInfo($"Patching {t.Name}...");
                        val.PatchAll(t);
                    }
                }
            } catch (Exception ex) {
                Logger.LogError("Failed to patch: " + ex);
            }
        }
    }
}