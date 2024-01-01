using System;
using System.Diagnostics;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace PlayerKillCounter.Extensions {
    public static class LandmineExtensions {
        public static void Log(this Landmine landmine, LogLevel level, string msg) {
            Vector3 pos = Vector3.negativeInfinity;
            if(landmine != null) {
                pos = landmine.transform.position;
            }
            Mod.Logger.Log(level, $"[Landmine @ {pos}] " + msg);
        }
        public static void LogFatal(this Landmine landmine, string msg) => landmine.Log(LogLevel.Fatal, msg);
        public static void LogError(this Landmine landmine, string msg) => landmine.Log(LogLevel.Error, msg);
        public static void LogWarning(this Landmine landmine, string msg) => landmine.Log(LogLevel.Warning, msg);
        public static void LogMessage(this Landmine landmine, string msg) => landmine.Log(LogLevel.Message, msg);
        public static void LogInfo(this Landmine landmine, string msg) => landmine.Log(LogLevel.Info, msg);
        public static void LogDebug(this Landmine landmine, string msg) => landmine.Log(LogLevel.Debug, msg);
    }
}