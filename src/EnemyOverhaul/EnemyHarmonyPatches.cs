using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsEnemyOverhaul
{
    public static class EnemyHarmonyPatches
    {
        // ====================================================================
        // 1. ZRoutedRpc Registration & Character Awake (Champion Component + Cleanse RPC)
        // ====================================================================
        [HarmonyPatch(typeof(Game), "Start")]
        public static class Patch_Game_Start
        {
            public static void Postfix()
            {
                ChampionSystem.ResetSessionFlag();
                CursedDungeonSystem.ResetSessionFlag();
                ServerPingTracker.ResetSession();
                if (ZRoutedRpc.instance != null)
                {
                    ChampionSystem.RegisterRPCs(ZRoutedRpc.instance);
                    CursedDungeonSystem.RegisterRPCs(ZRoutedRpc.instance);
                }
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        public static class Patch_Character_Awake
        {
            public static void Postfix(Character __instance)
            {
                if (__instance == null) return;
                ZNetView nv = __instance.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) return;

                if (__instance.IsPlayer())
                {
                    nv.Register(EnemyInjuryManager.RPC_CleansePlayerInjuries, delegate (long sender)
                    {
                        EnemyInjuryManager.ClearAllInjuries(__instance);
                    });
                    return;
                }

                ZDO zdo = nv.GetZDO();
                if (zdo != null && zdo.GetBool(ChampionSystem.ZdoIsChampion, false))
                {
                    __instance.m_boss = false;
                    if (__instance.gameObject.GetComponent<ChampionBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<ChampionBehaviour>();
                    }
                }
            }
        }

        // Also ensure Character.IsBoss() returns true ONLY while inside BloodDominion projectile/cloud checks if needed,
        // while keeping m_boss = false everywhere else so no top-screen Boss HUD ever shows!
        // (Handled directly by our BloodDominion_RegisterMinion_Prefix hook!)

        // ====================================================================
        // 2. Player Update Tick (Cursed Dungeon Detection & Mystical Fog)
        // ====================================================================
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Patch_Player_Update
        {
            public static void Postfix(Player __instance)
            {
                if (__instance == null || __instance != Player.m_localPlayer) return;
                CursedDungeonSystem.UpdateLocalPlayerDungeonTick(__instance, Time.deltaTime);
            }
        }

        // ====================================================================
        // 3. Combat Damage Pipeline:
        //    - Cancel vanilla 3-star 2.5x Attack.GetLevelDamageFactor on Champions so both Attack & Aoe share true 1.0x base
        //    - Champion & Cursed Dungeon Mob outgoing damage multipliers
        //    - Champion stagger resistance (anti-stunlock)
        //    - Block detection so ONLY unblocked/unparried hits inflict Injuries!
        //    - Sundered Armor (-30% Total Armor) applied to Player.GetBodyArmor()
        // ====================================================================
        [HarmonyPatch(typeof(Attack), "GetLevelDamageFactor")]
        public static class Patch_Attack_GetLevelDamageFactor
        {
            private static readonly FieldInfo s_atkCharField = typeof(Attack).GetField("m_character", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static bool Prefix(Attack __instance, ref float __result)
            {
                if (__instance == null || s_atkCharField == null) return true;
                Humanoid h = s_atkCharField.GetValue(__instance) as Humanoid;
                if (h != null && !h.IsPlayer() && ChampionSystem.IsChampion(h))
                {
                    __result = 1.0f;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "GetBodyArmor")]
        public static class Patch_Player_GetBodyArmor
        {
            public static void Postfix(Player __instance, ref float __result)
            {
                if (__instance == null || __result <= 0f) return;
                if (EnemyInjuryManager.HasInjury(__instance, InjuryType.ArmorSunder))
                {
                    float pen = Mathf.Clamp(EnemyOverhaulConfig.ArmorSunderPenalty.Value, 0f, 0.90f);
                    __result *= (1f - pen);
                }
            }
        }

        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        public static class Patch_Humanoid_BlockAttack_Tracker
        {
            public static bool CurrentHitWasSuccessfullyBlocked;

            public static void Postfix(Humanoid __instance, bool __result)
            {
                if (__instance != null && __instance == Player.m_localPlayer && __result)
                {
                    CurrentHitWasSuccessfullyBlocked = true;
                }
            }
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        public static class Patch_Character_RPC_Damage
        {
            public static void Prefix(Character __instance, HitData hit, out float __state)
            {
                __state = 0f;
                Patch_Humanoid_BlockAttack_Tracker.CurrentHitWasSuccessfullyBlocked = false;
                if (__instance == null || hit == null) return;

                Character attacker = hit.GetAttacker();

                // A) Outgoing damage scaling for Named Champions & Cursed Dungeon Mobs
                if (attacker != null && !attacker.IsPlayer())
                {
                    if (ChampionSystem.IsChampion(attacker))
                    {
                        ChampionSystem.ApplyChampionElementalPerkDamage(attacker, ref hit);
                    }
                    else if (CursedDungeonSystem.IsCursedDungeonCreature(attacker))
                    {
                        float bonus = Mathf.Max(0f, EnemyOverhaulConfig.CursedDungeonMobDamageBonus.Value);
                        if (bonus > 0f)
                        {
                            ChampionSystem.ScaleHitDamage(ref hit.m_damage, 1f + bonus);
                        }
                    }
                }

                // B) Champion Stagger Resistance (prevents infinite stunlock!)
                if (!__instance.IsPlayer() && ChampionSystem.IsChampion(__instance))
                {
                    float stagRed = Mathf.Clamp(EnemyOverhaulConfig.ChampionStaggerResistance.Value, 0f, 0.95f);
                    hit.m_staggerMultiplier *= (1f - stagRed);
                }

                // Record local player HP before damage so we know whether unblocked HP damage actually landed
                if (__instance == Player.m_localPlayer && !__instance.IsDead())
                {
                    __state = __instance.GetHealth();
                }
            }

            public static void Postfix(Character __instance, HitData hit, float __state)
            {
                try
                {
                    if (__instance == null || __instance != Player.m_localPlayer || __state <= 0f || hit == null) return;
                    if (__instance.IsDead() || __instance.InDodge() || __instance.InGhostMode()) return;

                    // If shield/weapon successfully blocked or parried the attack -> NO injury!
                    if (Patch_Humanoid_BlockAttack_Tracker.CurrentHitWasSuccessfullyBlocked) return;

                    float hpLost = __state - __instance.GetHealth();
                    float minHpThreshold = Mathf.Max(0.1f, EnemyOverhaulConfig.InjuryMinHealthLost.Value);
                    if (hpLost < minHpThreshold) return;

                    Character attacker = hit.GetAttacker();
                    if (attacker != null && !attacker.IsPlayer())
                    {
                        EnemyInjuryManager.TryInflictFromEnemyHit((Player)__instance, attacker, hit);
                    }
                }
                finally
                {
                    Patch_Humanoid_BlockAttack_Tracker.CurrentHitWasSuccessfullyBlocked = false;
                }
            }
        }

        // ====================================================================
        // 4. Champion Berserk Fury (<40% HP: +25% move & attack animation speed)
        // ====================================================================
        [HarmonyPatch(typeof(Character), "GetJogSpeedFactor")]
        public static class Patch_Character_GetJogSpeedFactor_ChampionFury
        {
            public static void Postfix(Character __instance, ref float __result)
            {
                if (__instance == null || __instance.IsPlayer()) return;
                ChampionBehaviour champ = __instance.GetComponent<ChampionBehaviour>();
                if (champ != null && champ.IsEnraged)
                {
                    __result *= 1.25f;
                }
            }
        }

        [HarmonyPatch(typeof(Character), "GetRunSpeedFactor")]
        public static class Patch_Character_GetRunSpeedFactor_ChampionFury
        {
            public static void Postfix(Character __instance, ref float __result)
            {
                if (__instance == null || __instance.IsPlayer()) return;
                ChampionBehaviour champ = __instance.GetComponent<ChampionBehaviour>();
                if (champ != null && champ.IsEnraged)
                {
                    __result *= 1.25f;
                }
            }
        }

        [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
        public static class Patch_CharacterAnimEvent_ChampionFury
        {
            private static readonly FieldInfo s_charField = typeof(CharacterAnimEvent).GetField("m_character", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_animField = typeof(CharacterAnimEvent).GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Postfix(CharacterAnimEvent __instance)
            {
                if (__instance == null || s_charField == null || s_animField == null) return;
                Character ch = s_charField.GetValue(__instance) as Character;
                if (ch == null || ch.IsPlayer() || ch.IsDead()) return;

                ChampionBehaviour champ = ch.GetComponent<ChampionBehaviour>();
                if (champ == null || !champ.IsEnraged) return;

                Animator anim = s_animField.GetValue(__instance) as Animator;
                if (anim != null && anim.speed > 0.05f && anim.speed < 1.24f)
                {
                    anim.speed = 1.25f;
                }
            }
        }

        // ====================================================================
        // 5. Eitr Burn (Manaburn) & Putrid Rot (-50% Healing) Hooks
        // ====================================================================
        [HarmonyPatch(typeof(Player), "UseEitr")]
        public static class Patch_Player_UseEitr
        {
            public static void Postfix(Player __instance, float v)
            {
                if (__instance == null || __instance != Player.m_localPlayer || v <= 0.05f || __instance.IsDead()) return;
                if (!EnemyInjuryManager.HasInjury(__instance, InjuryType.EitrBurn)) return;

                float ratio = Mathf.Max(0f, EnemyOverhaulConfig.EitrBurnHealthDamagePerEitr.Value);
                float burnHp = v * ratio;
                if (burnHp > 0.1f)
                {
                    SE_InjuryBleeding.ApplyNonStaggeringBloodLoss(__instance, burnHp, new Color(0.85f, 0.30f, 1.0f, 1f));
                }
            }
        }

        [HarmonyPatch(typeof(Character), "Heal")]
        public static class Patch_Character_Heal
        {
            public static void Prefix(Character __instance, ref float hp)
            {
                if (__instance == null || hp <= 0f || !__instance.IsPlayer()) return;
                if (EnemyInjuryManager.HasInjury(__instance, InjuryType.Rot))
                {
                    float pen = Mathf.Clamp(EnemyOverhaulConfig.RotHealingPenalty.Value, 0f, 0.95f);
                    hp *= (1f - pen);
                }
            }
        }

        // ====================================================================
        // 6. Creature Death Hook:
        //    - Track last player attacker per creature
        //    - Increment server-wide species kill counter -> spawn Hunt Champion
        //    - Drop Champion bonus trophies, x3 loot & Coins
        //    - Unseal Cursed Dungeon when its Warden Champion dies!
        // ====================================================================
        [HarmonyPatch(typeof(Character), "Damage")]
        public static class Patch_Character_Damage_LastPlayerTracker
        {
            private static readonly Dictionary<int, Player> s_lastPlayerAttacker = new Dictionary<int, Player>();

            public static void Prefix(Character __instance, HitData hit)
            {
                if (__instance == null || __instance.IsPlayer() || hit == null) return;
                Player atk = hit.GetAttacker() as Player;
                if (atk != null)
                {
                    s_lastPlayerAttacker[__instance.GetInstanceID()] = atk;
                }
            }

            public static Player ConsumeLastAttacker(Character victim)
            {
                if (victim == null) return null;
                int id = victim.GetInstanceID();
                Player p;
                if (s_lastPlayerAttacker.TryGetValue(id, out p))
                {
                    s_lastPlayerAttacker.Remove(id);
                    return p;
                }
                return null;
            }
        }

        [HarmonyPatch(typeof(Character), "OnDeath")]
        public static class Patch_Character_OnDeath
        {
            public static void Prefix(Character __instance)
            {
                if (__instance == null || __instance.IsPlayer()) return;

                ZNetView nv = __instance.GetComponent<ZNetView>();
                bool isOwner = (nv == null || !nv.IsValid() || nv.IsOwner());

                // 1) If a Champion died: drop bonus Trophies + x3 Loot + Coins, and cleanse Cursed Dungeon if it was the Warden!
                if (ChampionSystem.IsChampion(__instance))
                {
                    if (isOwner)
                    {
                        ChampionSystem.SpawnChampionBonusRewards(__instance);
                        CursedDungeonSystem.NotifyDungeonWardenSlain(__instance);
                    }
                    return;
                }

                // 2) Regular creature killed -> attribute kill to the decisive player (or closest player in combat)
                if (isOwner)
                {
                    Player killer = Patch_Character_Damage_LastPlayerTracker.ConsumeLastAttacker(__instance);
                    if (killer == null)
                    {
                        killer = Player.GetClosestPlayer(__instance.transform.position, 40f);
                    }
                    if (killer != null)
                    {
                        ChampionSystem.NotifyCreatureKilledByPlayer(__instance, killer);
                    }
                }
            }
        }

        // ====================================================================
        // 7. Cursed Dungeon Sealed Exit Portal Hooks (`Teleport`)
        //    - Blocks ONLY interior -> exterior exit while cursed!
        //    - Exterior -> interior entry ALWAYS stays open for co-op & corpse runs!
        // ====================================================================
        [HarmonyPatch(typeof(Teleport), "Interact")]
        public static class Patch_Teleport_Interact
        {
            public static bool Prefix(Teleport __instance, Humanoid character, bool hold, ref bool __result)
            {
                if (hold || __instance == null) return true;
                if (CursedDungeonSystem.IsExitTeleportSealed(__instance, allowSpawnFallback: true))
                {
                    if (character != null)
                    {
                        character.Message(MessageHud.MessageType.Center, EnemyLocalization.Loc("$ceo_cursed_exit_locked"), 0, null);
                    }
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Teleport), "OnTriggerEnter")]
        public static class Patch_Teleport_OnTriggerEnter
        {
            private static float s_lastMsgTime;

            public static bool Prefix(Teleport __instance, Collider collider)
            {
                if (__instance == null || collider == null) return true;
                Player p = collider.GetComponent<Player>();
                if (p == null || p != Player.m_localPlayer) return true;

                if (CursedDungeonSystem.IsExitTeleportSealed(__instance, allowSpawnFallback: true))
                {
                    if (Time.time - s_lastMsgTime > 2.0f)
                    {
                        s_lastMsgTime = Time.time;
                        p.Message(MessageHud.MessageType.Center, EnemyLocalization.Loc("$ceo_cursed_exit_locked"), 0, null);
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Teleport), "GetHoverText")]
        public static class Patch_Teleport_GetHoverText
        {
            public static void Postfix(Teleport __instance, ref string __result)
            {
                if (__instance == null) return;
                // Read-only hover check — NEVER trigger an early spawn on frame 1 while rooms are still loading!
                if (CursedDungeonSystem.IsExitTeleportSealed(__instance, allowSpawnFallback: false))
                {
                    __result = EnemyLocalization.Loc("$ceo_cursed_exit_hover");
                }
            }
        }

        // ====================================================================
        // 8. Overhead Enemy HUD Formatting for Champions & Cursed Mobs (Zero-Alloc Cached!)
        // ====================================================================
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        public static class Patch_EnemyHud_UpdateHuds
        {
            private static readonly FieldInfo s_hudsField = typeof(EnemyHud).GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static FieldInfo s_nameField;

            private struct CachedHudLabel
            {
                public byte Kind; // 0 = normal, 1 = champ, 2 = cursedMob
                public string Formatted;
                public float ExpireTime;
            }

            private static readonly Dictionary<int, CachedHudLabel> s_labelCache = new Dictionary<int, CachedHudLabel>();
            private static float s_nextPruneTime;

            public static void Postfix(EnemyHud __instance)
            {
                if (__instance == null || s_hudsField == null) return;
                IDictionary dict = s_hudsField.GetValue(__instance) as IDictionary;
                if (dict == null || dict.Count == 0) return;

                float now = Time.time;
                if (now >= s_nextPruneTime)
                {
                    s_nextPruneTime = now + 10f;
                    if (s_labelCache.Count > 128) s_labelCache.Clear();
                }

                foreach (DictionaryEntry entry in dict)
                {
                    Character ch = entry.Key as Character;
                    if (ch == null || ch.IsPlayer() || entry.Value == null) continue;

                    int id = ch.GetInstanceID();
                    CachedHudLabel cached;
                    if (!s_labelCache.TryGetValue(id, out cached) || now >= cached.ExpireTime)
                    {
                        bool isChamp = ChampionSystem.IsChampion(ch);
                        bool isCursedMob = !isChamp && CursedDungeonSystem.IsCursedDungeonCreature(ch);
                        cached.ExpireTime = now + 1.5f;

                        if (isChamp)
                        {
                            cached.Kind = 1;
                            cached.Formatted = ChampionSystem.FormatChampionOverheadName(ch);
                        }
                        else if (isCursedMob)
                        {
                            cached.Kind = 2;
                            cached.Formatted = string.Format("<color=#D46BFF>[{0}]</color> {1}", EnemyLocalization.Loc("$ceo_cursed_mob_tag"), ch.GetHoverName());
                        }
                        else
                        {
                            cached.Kind = 0;
                            cached.Formatted = null;
                        }
                        s_labelCache[id] = cached;
                    }

                    if (cached.Kind == 0 || string.IsNullOrEmpty(cached.Formatted)) continue;

                    if (s_nameField == null)
                    {
                        s_nameField = entry.Value.GetType().GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    if (s_nameField == null) continue;

                    TextMeshProUGUI nameTmp = s_nameField.GetValue(entry.Value) as TextMeshProUGUI;
                    if (nameTmp == null) continue;

                    if (nameTmp.text != cached.Formatted)
                    {
                        nameTmp.richText = true;
                        nameTmp.overflowMode = TextOverflowModes.Overflow;
                        nameTmp.text = cached.Formatted;
                    }
                }
            }
        }

        // ====================================================================
        // 8b. Minimap HUD Overlays:
        //     • Bottom-Right: Hunt Champion Remaining Counter
        //     • Top-Left: True Round-Trip Server Ping (ping: X ms)
        //       Hooks ZRpc.UpdatePing / ZRpc.ReceivePing on the server peer RPC
        //       so it works with ANY server version without custom RPC handlers!
        // ====================================================================
        internal static class ServerPingTracker
        {
            private static float s_pingSentRealtime = -1f;
            private static float s_smoothedRttMs = -1f;
            private static int s_displayPingMs;

            public static void ResetSession()
            {
                s_pingSentRealtime = -1f;
                s_smoothedRttMs = -1f;
                s_displayPingMs = 0;
            }

            public static void OnSendingPing(ZRpc rpc)
            {
                if (rpc == null || ZNet.instance == null || ZNet.instance.IsServer()) return;
                if (rpc != ZNet.instance.GetServerRPC()) return;

                s_pingSentRealtime = Time.realtimeSinceStartup;
            }

            public static void OnReceivedPingReply(ZRpc rpc)
            {
                if (rpc == null || s_pingSentRealtime <= 0f) return;
                if (ZNet.instance == null || ZNet.instance.IsServer()) return;
                if (rpc != ZNet.instance.GetServerRPC()) return;

                float rttMs = (Time.realtimeSinceStartup - s_pingSentRealtime) * 1000f;
                s_pingSentRealtime = -1f;
                if (rttMs <= 0f || rttMs > 9999f) return;

                if (s_smoothedRttMs < 0f)
                {
                    s_smoothedRttMs = rttMs;
                }
                else
                {
                    s_smoothedRttMs = Mathf.Lerp(s_smoothedRttMs, rttMs, 0.35f);
                }
                s_displayPingMs = Mathf.Max(1, Mathf.RoundToInt(s_smoothedRttMs));
            }

            public static int GetPingMs()
            {
                ZNet znet = ZNet.instance;
                if (znet == null || znet.IsServer()) return 0;

                if (s_smoothedRttMs >= 0f)
                {
                    return s_displayPingMs;
                }

                float localQ, remoteQ, outB, inB;
                int netPing;
                znet.GetNetStats(out localQ, out remoteQ, out netPing, out outB, out inB);
                return netPing > 0 ? netPing : 0;
            }
        }

        [HarmonyPatch(typeof(ZRpc), "UpdatePing")]
        public static class Patch_ZRpc_UpdatePing_MeasureStart
        {
            private static readonly FieldInfo s_pingTimerField = typeof(ZRpc).GetField("m_pingTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_pingIntervalField = typeof(ZRpc).GetField("m_pingInterval", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Prefix(ZRpc __instance, float dt)
            {
                if (__instance == null || s_pingTimerField == null || s_pingIntervalField == null) return;
                if (ZNet.instance == null || ZNet.instance.IsServer() || __instance != ZNet.instance.GetServerRPC()) return;

                float nextTimer = (float)s_pingTimerField.GetValue(__instance) + dt;
                float interval = (float)s_pingIntervalField.GetValue(null);
                if (nextTimer > interval)
                {
                    ServerPingTracker.OnSendingPing(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(ZRpc), "ReceivePing")]
        public static class Patch_ZRpc_ReceivePing_MeasureEnd
        {
            private static readonly FieldInfo s_timeSinceLastPingField = typeof(ZRpc).GetField("m_timeSinceLastPing", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Postfix(ZRpc __instance)
            {
                if (__instance == null || s_timeSinceLastPingField == null) return;
                if (ZNet.instance == null || ZNet.instance.IsServer() || __instance != ZNet.instance.GetServerRPC()) return;

                // In ZRpc.ReceivePing: when package.ReadBool() == false (server's Pong reply), m_timeSinceLastPing is reset to 0f!
                float sinceLast = (float)s_timeSinceLastPingField.GetValue(__instance);
                if (sinceLast == 0f)
                {
                    ServerPingTracker.OnReceivedPingReply(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(Game), "Logout")]
        public static class Patch_Game_Logout_ResetPing
        {
            public static void Postfix()
            {
                ServerPingTracker.ResetSession();
            }
        }

        [HarmonyPatch(typeof(Minimap), "Update")]
        public static class Patch_Minimap_Update_ChampionCounter
        {
            private static GameObject s_counterObj;
            private static TMP_Text s_counterTmp;
            private static int s_lastShownRemaining = -9999;
            private static bool s_lastWasRussian;

            private static GameObject s_pingObj;
            private static TMP_Text s_pingTmp;
            private static int s_lastShownPing = -9999;
            private static float s_nextPingPollTime;

            public static void Postfix(Minimap __instance)
            {
                if (__instance == null || Player.m_localPlayer == null) return;

                bool smallMapVisible = __instance.m_smallRoot != null && __instance.m_smallRoot.activeSelf;

                UpdateChampionCounter(__instance, smallMapVisible);
                UpdateServerPing(__instance, smallMapVisible);
            }

            private static void UpdateChampionCounter(Minimap mm, bool smallMapVisible)
            {
                bool shouldShow = smallMapVisible &&
                                  EnemyOverhaulConfig.EnableChampions.Value &&
                                  EnemyOverhaulConfig.ShowMinimapChampionCounter.Value;

                if (!shouldShow)
                {
                    if (s_counterObj != null && s_counterObj.activeSelf)
                    {
                        s_counterObj.SetActive(false);
                    }
                    return;
                }

                EnsureMinimapCounterWidget(mm);
                if (s_counterObj == null || s_counterTmp == null) return;

                if (!s_counterObj.activeSelf)
                {
                    s_counterObj.SetActive(true);
                }

                int remaining = ChampionSystem.GetRemainingKillsForMinimap();
                bool isRus = EnemyLocalization.IsRussian();
                if (remaining != s_lastShownRemaining || isRus != s_lastWasRussian)
                {
                    s_lastShownRemaining = remaining;
                    s_lastWasRussian = isRus;

                    string numStr = (remaining <= 3)
                        ? string.Format("<color=#FF785A><b>{0}</b></color>", remaining)
                        : string.Format("<color=#FFF8E7><b>{0}</b></color>", remaining);

                    s_counterTmp.text = string.Format(EnemyLocalization.Loc("$ceo_minimap_champ_left"), numStr);
                }
            }

            private static void UpdateServerPing(Minimap mm, bool smallMapVisible)
            {
                bool shouldShow = smallMapVisible && EnemyOverhaulConfig.ShowMinimapPing.Value;
                if (!shouldShow)
                {
                    if (s_pingObj != null && s_pingObj.activeSelf)
                    {
                        s_pingObj.SetActive(false);
                    }
                    return;
                }

                EnsureMinimapPingWidget(mm);
                if (s_pingObj == null || s_pingTmp == null) return;

                if (!s_pingObj.activeSelf)
                {
                    s_pingObj.SetActive(true);
                }

                float now = Time.unscaledTime;
                if (now < s_nextPingPollTime && s_lastShownPing >= 0) return;
                s_nextPingPollTime = now + 0.5f;

                int pingMs = ReadServerPingMs();
                if (pingMs != s_lastShownPing)
                {
                    s_lastShownPing = pingMs;

                    string valColor;
                    if (pingMs <= 80)
                    {
                        valColor = "#8CE884";
                    }
                    else if (pingMs <= 150)
                    {
                        valColor = "#FFD86B";
                    }
                    else
                    {
                        valColor = "#FF785A";
                    }

                    s_pingTmp.text = string.Format("ping: <color={0}><b>{1}</b></color>", valColor, pingMs);
                }
            }

            private static int ReadServerPingMs()
            {
                return ServerPingTracker.GetPingMs();
            }

            private static void EnsureMinimapCounterWidget(Minimap mm)
            {
                if (s_counterObj != null) return;
                if (mm.m_biomeNameSmall == null) return;

                Transform mapParent = (mm.m_mapImageSmall != null)
                    ? mm.m_mapImageSmall.transform
                    : mm.m_smallRoot.transform;

                s_counterObj = UnityEngine.Object.Instantiate(mm.m_biomeNameSmall.gameObject, mapParent);
                s_counterObj.name = "CEO_MinimapChampionCounter";
                s_counterObj.SetActive(true);

                RectTransform rt = s_counterObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-6f, 4f);
                    rt.sizeDelta = new Vector2(175f, 22f);
                }

                s_counterTmp = s_counterObj.GetComponent<TMP_Text>();
                if (s_counterTmp != null)
                {
                    float baseSize = mm.m_biomeNameSmall.fontSize > 4f ? mm.m_biomeNameSmall.fontSize : 16f;
                    s_counterTmp.enableAutoSizing = false;
                    s_counterTmp.fontSize = Mathf.Clamp(Mathf.Round(baseSize * 0.78f), 11f, 13.5f);
                    s_counterTmp.alignment = TextAlignmentOptions.BottomRight;
                    s_counterTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    s_counterTmp.overflowMode = TextOverflowModes.Overflow;
                    s_counterTmp.richText = true;
                    s_counterTmp.raycastTarget = false;
                    s_counterTmp.color = new Color(0.92f, 0.90f, 0.85f, 0.95f);
                }

                s_lastShownRemaining = -9999;
            }

            private static void EnsureMinimapPingWidget(Minimap mm)
            {
                if (s_pingObj != null) return;
                if (mm.m_biomeNameSmall == null) return;

                Transform mapParent = (mm.m_mapImageSmall != null)
                    ? mm.m_mapImageSmall.transform
                    : mm.m_smallRoot.transform;

                s_pingObj = UnityEngine.Object.Instantiate(mm.m_biomeNameSmall.gameObject, mapParent);
                s_pingObj.name = "CEO_MinimapServerPing";
                s_pingObj.SetActive(true);

                RectTransform rt = s_pingObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(6f, -4f);
                    rt.sizeDelta = new Vector2(140f, 22f);
                }

                s_pingTmp = s_pingObj.GetComponent<TMP_Text>();
                if (s_pingTmp != null)
                {
                    float baseSize = mm.m_biomeNameSmall.fontSize > 4f ? mm.m_biomeNameSmall.fontSize : 16f;
                    s_pingTmp.enableAutoSizing = false;
                    s_pingTmp.fontSize = Mathf.Clamp(Mathf.Round(baseSize * 0.78f), 11f, 13.5f);
                    s_pingTmp.alignment = TextAlignmentOptions.TopLeft;
                    s_pingTmp.textWrappingMode = TextWrappingModes.NoWrap;
                    s_pingTmp.overflowMode = TextOverflowModes.Overflow;
                    s_pingTmp.richText = true;
                    s_pingTmp.raycastTarget = false;
                    s_pingTmp.color = new Color(0.92f, 0.90f, 0.85f, 0.95f);
                }

                s_lastShownPing = -9999;
            }
        }

        // ====================================================================
        // 9. Admin Console Command (`enemyoverhaul` / `ceo`) for Instant Testing
        // ====================================================================
        public static void RegisterConsoleCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new EnemyOverhaulConsoleCommand());
        }
    }

    public class EnemyOverhaulConsoleCommand : ConsoleCommand
    {
        public override string Name
        {
            get { return "enemyoverhaul"; }
        }

        public override string Help
        {
            get
            {
                return "[champion [prefab] | injury <bleed|hamstring|sunder|winded|eitrburn|rot|all|clear> | cursedungeon | kills [prefab]] - Admin testing commands.";
            }
        }

        public override bool IsCheat
        {
            get { return true; }
        }

        public override void Run(string[] args)
        {
            Player p = Player.m_localPlayer;
            if (p == null)
            {
                Console.instance.Print("Enter a world first.");
                return;
            }

            bool isAdmin = SynchronizationManager.Instance.PlayerIsAdmin || (ZNet.instance != null && ZNet.instance.IsServer());
            if (!isAdmin)
            {
                Console.instance.Print("Only server administrators or singleplayer hosts can run enemyoverhaul.");
                return;
            }

            if (args == null || args.Length == 0)
            {
                Console.instance.Print("Usage: enemyoverhaul [map [clear] | champion [prefab] | injury <bleed|hamstring|sunder|winded|eitrburn|rot|all|clear> | cursedungeon | uncurse | kills [prefab]]");
                return;
            }

            string sub = args[0].Trim().ToLowerInvariant();
            if (sub == "map" || sub == "showcursed" || sub == "cursedmap" || sub == "pins")
            {
                bool clearPins = args.Length >= 2 && (args[1].Equals("clear", StringComparison.OrdinalIgnoreCase) || args[1].Equals("hide", StringComparison.OrdinalIgnoreCase) || args[1].Equals("off", StringComparison.OrdinalIgnoreCase));
                string mapMsg;
                CursedDungeonSystem.ToggleOrShowAllCursedDungeonsOnMap(clearPins, out mapMsg);
                Console.instance.Print("[EnemyOverhaul] " + mapMsg);
                return;
            }
            if (sub == "champion" || sub == "champ")
            {
                string prefab = (args.Length >= 2 && !string.IsNullOrEmpty(args[1])) ? args[1] : "Greydwarf_Elite";
                GameObject spawned = ChampionSystem.SpawnAmbushChampionNear(prefab, p.transform.position);
                if (spawned != null)
                {
                    Console.instance.Print("[EnemyOverhaul] Ambush Champion (" + prefab + ") spawned nearby and is stalking you!");
                }
                else
                {
                    Console.instance.Print("[EnemyOverhaul] Could not spawn '" + prefab + "' (check prefab name or move away from PlayerBase).");
                }
                return;
            }

            if (sub == "injury" && args.Length >= 2)
            {
                string kind = args[1].Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "clear":
                    case "cleanse":
                        int n = EnemyInjuryManager.ClearAllInjuriesInRadius(p.transform.position, 15f);
                        Console.instance.Print("[EnemyOverhaul] Cleansed all Combat Injuries in 15m radius (players cleansed: " + n + ").");
                        break;
                    case "all":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Bleeding);
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Hamstrung);
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.ArmorSunder);
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Winded);
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.EitrBurn);
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Rot);
                        Console.instance.Print("[EnemyOverhaul] Applied all 6 Combat Injuries!");
                        break;
                    case "bleed":
                    case "bleeding":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Bleeding);
                        Console.instance.Print("[EnemyOverhaul] Applied Bleeding (Рваная рана).");
                        break;
                    case "hamstring":
                    case "hamstrung":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Hamstrung);
                        Console.instance.Print("[EnemyOverhaul] Applied Hamstrung (Простреленное сухожилие).");
                        break;
                    case "sunder":
                    case "armorsunder":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.ArmorSunder);
                        Console.instance.Print("[EnemyOverhaul] Applied Armor Sunder (Раскол брони).");
                        break;
                    case "winded":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Winded);
                        Console.instance.Print("[EnemyOverhaul] Applied Winded (Выбитое дыхание).");
                        break;
                    case "eitrburn":
                    case "manaburn":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.EitrBurn);
                        Console.instance.Print("[EnemyOverhaul] Applied Eitr Burn (Ожог Эйтра).");
                        break;
                    case "rot":
                        EnemyInjuryManager.ApplyInjury(p, InjuryType.Rot);
                        Console.instance.Print("[EnemyOverhaul] Applied Putrid Rot (Заражение).");
                        break;
                    default:
                        Console.instance.Print("Unknown injury. Valid: bleed, hamstring, sunder, winded, eitrburn, rot, all, clear");
                        break;
                }
                return;
            }

            if (sub == "cursedungeon" || sub == "curse")
            {
                string msg;
                CursedDungeonSystem.ForceCurseNearestDungeonForTesting(p.transform.position, out msg);
                Console.instance.Print("[EnemyOverhaul] " + msg);
                return;
            }

            if (sub == "uncurse" || sub == "cleanse" || sub == "unseal")
            {
                string msg2;
                CursedDungeonSystem.ForceCleanseNearestDungeonForAdmin(p.transform.position, out msg2);
                Console.instance.Print("[EnemyOverhaul] " + msg2);
                return;
            }

            if (sub == "debugdungeon" || sub == "status")
            {
                Console.instance.Print("[EnemyOverhaul] " + CursedDungeonSystem.GetCurrentDungeonDebugInfo(p.transform.position));
                return;
            }

            if (sub == "kills")
            {
                if (args.Length >= 2)
                {
                    int newVal;
                    if (int.TryParse(args[1], out newVal))
                    {
                        ChampionSystem.SetServerGlobalKillCount(newVal, forcePersistToZoneSystem: true);
                        ChampionSystem.BroadcastRemainingKillsFromServer();
                        Console.instance.Print("[EnemyOverhaul] Set global Hunt Champion kill counter to: " + newVal + " / " + ChampionSystem.GetCurrentTargetThreshold());
                        return;
                    }
                }
                int c = ChampionSystem.GetServerGlobalKillCount();
                int target = ChampionSystem.GetCurrentTargetThreshold();
                Console.instance.Print(string.Format("[EnemyOverhaul] Global Hunt Champion kill counter: {0} / {1} (window {2}..{3})",
                    c, target, EnemyOverhaulConfig.HuntChampionMinKills.Value, EnemyOverhaulConfig.HuntChampionMaxKills.Value));
                return;
            }

            Console.instance.Print("Usage: enemyoverhaul [champion [prefab] | injury <bleed|hamstring|sunder|winded|eitrburn|rot|all|clear> | cursedungeon | kills [prefab]]");
        }
    }
}
