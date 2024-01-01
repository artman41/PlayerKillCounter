using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using PlayerKillCounter.Behaviours;
using UnityEngine;
using static PlayerKillCounter.Extensions.LandmineExtensions;

namespace PlayerKillCounter {
    public static class Patches {
        public static class StartOfRoundPatches {

            [HarmonyPatch(typeof(StartOfRound), "Start")]
            [HarmonyPrefix]
            public static bool Start_Pre(StartOfRound __instance) {
                if (__instance.GetComponent<KillCounter>() == null)
                    __instance.gameObject.AddComponent<KillCounter>();
                if (__instance.GetComponent<RPCHandler>() == null)
                    __instance.gameObject.AddComponent<RPCHandler>();
                return true;
            }

            [HarmonyPatch(typeof(StartOfRound), "Start")]
            [HarmonyPostfix]
            public static void Start_Post(StartOfRound __instance) {
                // Populating notes with dummy data
                foreach (var t in __instance.gameStats.allPlayerStats) {
                    t.playerNotes.Add("THIS IS DUMMY DATA");
                }
            }

            [HarmonyPatch(typeof(StartOfRound), "WritePlayerNotes")]
            [HarmonyPrefix]
            public static bool WritePlayerNotes(StartOfRound __instance) {
                var killCounter = StartOfRound.Instance.GetComponent<KillCounter>();
                Mod.Logger.LogInfo($"killedByDict: [{killCounter}]");
                for (int i = 0; i < __instance.gameStats.allPlayerStats.Length; ++i) {
                    var playerId = __instance.allPlayerScripts[i].playerClientId;
                    if (!killCounter.ContainsPlayerClientId(playerId)) 
                        continue;
                    var killedBy = killCounter[playerId];
                    var killer = __instance.allPlayerScripts.First(player => player.playerClientId == killedBy.KillerClientId);
                    __instance.gameStats.allPlayerStats[i].playerNotes.Add($"Killed by {(killer != null ? killer.playerUsername : "[UNKNOWN]")} via '{killedBy.CauseOfDeath.ToString()}'");
                }
                killCounter.Clear();

                return true;
            }
        }

        public static class LandminePatches {

            [HarmonyPatch(typeof(Landmine), "Start")]
            [HarmonyPostfix]
            public static void Start(Landmine __instance) {
                __instance.LogInfo("Landmine found!");
            }

            [HarmonyPatch(typeof(Landmine), "TriggerOtherMineDelayed")]
            [HarmonyPrefix]
            public static bool TriggerOtherMineDelayed(Landmine __instance, Landmine mine) {
                try {
                    Mod.Logger.LogInfo($"instance: {__instance}, mine: {mine}");
                    if (mine.hasExploded)
                        return true;
                    var instanceBehaviour = __instance.GetComponent<LandmineTriggeredBy>();
                    if (instanceBehaviour == null) {
                        __instance.LogWarning("Landmine was triggered without the LandmineTriggeredBy behaviour being added!");
                    }

                    var mineBehaviour = mine.gameObject.GetComponent<LandmineTriggeredBy>();
                    if (mineBehaviour == null) {
                        mine.LogWarning("No LandmineTriggeredBy behaviour on landmine!");
                        mineBehaviour = mine.gameObject.AddComponent<LandmineTriggeredBy>();
                        mine.LogWarning("Added LandmineTriggeredBy behaviour to landmine!");
                    }

                    __instance.LogInfo($"Setting clientIdTrigger on Landmine {mine}");
                    mineBehaviour.TriggeredByClientId = instanceBehaviour.TriggeredByClientId;
                } catch (Exception ex) {
                    __instance.LogError($"Failed to run Landmine 'SpawnExplosion' Prefixes! {ex} {new StackTrace(ex)}");
                }

                return true;
            }

            [HarmonyPatch(typeof(Landmine), "ExplodeMineClientRpc")]
            [HarmonyPrefix]
            public static bool ExplodeMineClientRpc(Landmine __instance) {
                try {
                    var players = GetKillRadiusPlayers(__instance.transform.position + Vector3.up);
                    __instance.LogInfo($"Found [{players.Join(o => o.playerUsername)}] players in landmine radius");
                    var behaviour = __instance.GetComponent<LandmineTriggeredBy>();
                    if (behaviour == null) {
                        __instance.LogWarning("No LandmineTriggeredBy behaviour on landmine!");
                        behaviour = __instance.gameObject.AddComponent<LandmineTriggeredBy>();
                        __instance.LogWarning("Added LandmineTriggeredBy behaviour to landmine!");
                    }
                    foreach (var player in players) {
                        __instance.LogInfo($"Player [{behaviour.TriggeredByClientId}] killed [{player.playerClientId}] via Landmine");
                        if(player.playerClientId == behaviour.TriggeredByClientId) {
                            __instance.LogInfo($"Player [{behaviour.TriggeredByClientId}] killed themselves via Landmine, not noteworthy.");
                            continue;
                        }
                        var killCounter = StartOfRound.Instance.GetComponent<KillCounter>();
                        if (killCounter.ContainsPlayerClientId(player.playerClientId))
                            continue;
                        killCounter.Add(player.playerClientId, new KillCounter.Info{KillerClientId = behaviour.TriggeredByClientId, CauseOfDeath = CauseOfDeath.Blast});
                    }
                } catch (Exception ex) {
                    __instance.LogError($"Failed to run Landmine 'ExplodeMineClientRpc' Prefixes! {ex} {new StackTrace(ex)}");
                }

                return true;
            }

            [HarmonyPatch(typeof(Landmine), "OnTriggerExit")]
            [HarmonyPrefix]
            public static bool OnTriggerExit(Landmine __instance, Collider other) {
                try {
                    if (__instance.hasExploded)
                        return true;

                    if (other.CompareTag("Player")) {
                        PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
                        __instance.LogInfo($"Found Player {component.playerUsername} ({component.playerClientId})!");
                        __instance.gameObject.GetComponent<LandmineTriggeredBy>().TriggeredByClientId = component.playerClientId;
                    }
                    else __instance.LogInfo($"Failed to find Player!");
                } catch (Exception ex) {
                    __instance.LogError($"Failed to run Landmine 'OnTriggerExit' Prefixes! {ex} {new StackTrace(ex)}");
                }

                return true;
            }

            [HarmonyPatch(typeof(Landmine), "IHittable.Hit")]
            [HarmonyPrefix]
            public static bool IHittable_Hit(Landmine __instance, int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX) {
                try {
                    __instance.LogInfo($"Player [{playerWhoHit.playerClientId}] triggered Landmine");
                    var behaviour = __instance.GetComponent<LandmineTriggeredBy>();
                    if (behaviour == null) {
                        __instance.LogWarning("No LandmineTriggeredBy behaviour on landmine!");
                        behaviour = __instance.gameObject.AddComponent<LandmineTriggeredBy>();
                        __instance.LogWarning("Added LandmineTriggeredBy behaviour to landmine!");
                    }
                    behaviour.TriggeredByClientId = playerWhoHit.playerClientId;
                } catch (Exception ex) {
                    __instance.LogError($"Failed to run Landmine 'OnTriggerExit' Prefixes! {ex} {new StackTrace(ex)}");
                }

                return true;
            }

            private static bool PositionWithinExplosionRange(Vector3 explosionPos, Vector3 targetPos) {
                return Physics.Linecast(explosionPos, targetPos + Vector3.up * 0.3f, 256, QueryTriggerInteraction.Ignore);
            }

            private static IEnumerable<PlayerControllerB> GetKillRadiusPlayers(Vector3 explosionPosition) {
                // ReSharper disable once Unity.PreferNonAllocApi
                var colliderArray = Physics.OverlapSphere(explosionPosition, 6f, 2621448, QueryTriggerInteraction.Collide);
                foreach (var t in colliderArray) {
                    var num2 = Vector3.Distance(explosionPosition, t.transform.position);
                    if (!(num2 <= 4.0) && !PositionWithinExplosionRange(explosionPosition, t.transform.position))
                        continue;
                    if (t.gameObject.layer != 3)
                        continue;
                    yield return t.gameObject.GetComponent<PlayerControllerB>();
                }
            }
        }

        public static class PlayerControllerBPatches {
            [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayerFromOtherClientClientRpc")]
            [HarmonyPrefix]
            public static void DamagePlayerFromOtherClientClientRpc(PlayerControllerB __instance, int damageAmount, Vector3 hitDirection, int playerWhoHit, int newHealthAmount) {
                if(newHealthAmount <= 0)
                    KillCounter.Instance.Add(__instance.playerClientId, new KillCounter.Info{KillerClientId = (ulong)playerWhoHit, CauseOfDeath = CauseOfDeath.Bludgeoning});
            }
        }
    }
}