using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Jotunn.Managers;

namespace CoarstalsRPClasses
{
    public static class RPHarmonyPatches
    {
        // ====================================================================
        // 1. BASE HP, BASE STAMINA & INNATE BASE EITR
        //    Hooks Player.GetTotalFoodValue & Player.GetBaseFoodHP so Valheim's
        //    own food/HUD/regen pipeline natively includes class base pools!
        // ====================================================================
        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        public static class Patch_Player_GetTotalFoodValue
        {
            public static void Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr)
            {
                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls == RPClassType.None) return;

                // Beggar "Iron Stomach": +15% more Health, Stamina, and Eitr gained from eaten foods!
                if (cls == RPClassType.Beggar)
                {
                    float foodBonus = RPClassConfig.BeggarFoodStatsBonus.Value;
                    if (foodBonus > 0f)
                    {
                        List<Player.Food> foods = __instance.GetFoods();
                        if (foods != null)
                        {
                            for (int i = 0; i < foods.Count; i++)
                            {
                                Player.Food f = foods[i];
                                if (f != null)
                                {
                                    hp += f.m_health * foodBonus;
                                    stamina += f.m_stamina * foodBonus;
                                    eitr += f.m_eitr * foodBonus;
                                }
                            }
                        }
                    }
                }

                float addHp, addStamina, addEitr;
                RPPlayerClassManager.GetClassBaseStatBonuses(cls, out addHp, out addStamina, out addEitr);
                hp += addHp;
                stamina += addStamina;
                eitr += addEitr;
            }
        }

        [HarmonyPatch(typeof(Player), "GetBaseFoodHP")]
        public static class Patch_Player_GetBaseFoodHP
        {
            public static void Postfix(Player __instance, ref float __result)
            {
                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls == RPClassType.None) return;

                float addHp, addStamina, addEitr;
                RPPlayerClassManager.GetClassBaseStatBonuses(cls, out addHp, out addStamina, out addEitr);
                __result += addHp;
            }
        }

        // Beggar "Iron Stomach": +30% longer duration on any eaten food!
        [HarmonyPatch(typeof(Player), "EatFood")]
        public static class Patch_Player_EatFood
        {
            public static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
            {
                if (!__result || __instance == null || item == null || item.m_shared == null) return;
                if (RPPlayerClassManager.GetPlayerClass(__instance) != RPClassType.Beggar) return;

                float durBonus = RPClassConfig.BeggarFoodDurationBonus.Value;
                if (durBonus <= 0f) return;

                List<Player.Food> foods = __instance.GetFoods();
                if (foods == null) return;

                for (int i = 0; i < foods.Count; i++)
                {
                    Player.Food f = foods[i];
                    if (f != null && f.m_item != null && f.m_item.m_shared != null &&
                        f.m_item.m_shared.m_name == item.m_shared.m_name)
                    {
                        f.m_time = item.m_shared.m_foodBurnTime * (1f + durBonus);
                        break;
                    }
                }
            }
        }

        // ====================================================================
        // 2. FIRST SPAWN UI, RESPAWN STATUS RE-APPLY & ZDO SYNC HEARTBEAT
        // ====================================================================
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Patch_Player_Update
        {
            private static float s_syncTimer;
            private static float s_spawnCheckDelay;

            public static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;

                RPClassSelectionUI.UpdateTick();

                if (__instance.IsDead() || __instance.IsTeleporting() || __instance.InIntro() || __instance.InCutscene())
                {
                    s_spawnCheckDelay = 0.65f;
                    return;
                }

                if (s_spawnCheckDelay > 0f)
                {
                    s_spawnCheckDelay -= Time.deltaTime;
                    return;
                }

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls == RPClassType.None)
                {
                    // Open mandatory initial class selection UI once Valkyrie flight / spawn intro finishes!
                    if (!RPClassSelectionUI.IsOpen && !Menu.IsVisible())
                    {
                        RPClassSelectionUI.Open(isRespecFromScroll: false, forceAllowCancel: false);
                    }
                }
                else
                {
                    s_syncTimer += Time.deltaTime;
                    if (s_syncTimer >= 1.0f)
                    {
                        s_syncTimer = 0f;
                        RPPlayerClassManager.SyncClassToZDO(__instance, cls);
                        RPPlayerClassManager.EnsureStatusEffects(__instance, cls);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        public static class Patch_Player_OnSpawned
        {
            public static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls != RPClassType.None)
                {
                    RPPlayerClassManager.SyncClassToZDO(__instance, cls);
                    RPPlayerClassManager.EnsureStatusEffects(__instance, cls);
                    RPPlayerClassManager.RefreshMaxVitals(__instance, refill: true);
                }
            }
        }

        // Block player movement/attacks while the Class Selection UI is open
        [HarmonyPatch(typeof(Player), "TakeInput")]
        public static class Patch_Player_TakeInput
        {
            public static bool Prefix(ref bool __result)
            {
                if (RPClassSelectionUI.IsOpen)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // ====================================================================
        // 3. POSITIONAL BACKSTAB IN COMBAT (THIEF +35%),
        //    KNIGHT PARRY RIPOSTE (+30%), WARRIOR FULL-HP STRIKE (+25%)
        //    & INITIAL CLASS SELECTION INVULNERABILITY
        // ====================================================================
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        public static class Patch_Character_RPC_Damage
        {
            public static bool Prefix(Character __instance, HitData hit)
            {
                if (__instance == null || hit == null) return true;

                // Protect newly spawned classless player while reading and choosing their initial class!
                if (RPClassConfig.InvulnerableDuringInitialChoice.Value && __instance.IsPlayer())
                {
                    Player targetPlayer = __instance as Player;
                    if (targetPlayer != null && RPPlayerClassManager.GetPlayerClass(targetPlayer) == RPClassType.None)
                    {
                        return false;
                    }
                }

                Character attackerChar = hit.GetAttacker();
                Player attackerPlayer = attackerChar as Player;
                if (attackerPlayer != null && __instance != attackerPlayer)
                {
                    RPClassType atkClass = RPPlayerClassManager.GetPlayerClass(attackerPlayer);

                    // Knight: +30% damage against parried / staggered enemies!
                    if (atkClass == RPClassType.Knight && !__instance.IsPlayer() && __instance.IsStaggering())
                    {
                        float parryBonus = RPClassConfig.KnightParriedEnemyDamageBonus.Value;
                        if (parryBonus > 0f)
                        {
                            SE_RPClass.ScaleCombatDamageOnly(ref hit.m_damage, 1f + parryBonus);
                        }
                    }

                    // Warrior: +25% damage against enemies at full health!
                    if (atkClass == RPClassType.Warrior && __instance.GetHealthPercentage() >= 0.999f)
                    {
                        float fullHpBonus = RPClassConfig.WarriorFullHPEnemyDamageBonus.Value;
                        if (fullHpBonus > 0f)
                        {
                            SE_RPClass.ScaleCombatDamageOnly(ref hit.m_damage, 1f + fullHpBonus);
                        }
                    }

                    // Thief: True Positional Backstab (+35% when attacking from behind in combat)
                    if (atkClass == RPClassType.Thief)
                    {
                        float backBonus = RPClassConfig.ThiefPositionalBackstabBonus.Value;
                        if (backBonus > 0f)
                        {
                            Vector3 victimForward = __instance.transform.forward;
                            victimForward.y = 0f;

                            Vector3 toAttacker = attackerPlayer.transform.position - __instance.transform.position;
                            toAttacker.y = 0f;

                            bool isBehind = false;
                            if (victimForward.sqrMagnitude > 0.001f && toAttacker.sqrMagnitude > 0.001f)
                            {
                                victimForward.Normalize();
                                toAttacker.Normalize();
                                if (Vector3.Dot(victimForward, toAttacker) < -0.25f)
                                {
                                    isBehind = true;
                                }
                            }
                            else if (hit.m_dir.sqrMagnitude > 0.001f && victimForward.sqrMagnitude > 0.001f)
                            {
                                Vector3 hitHoriz = new Vector3(hit.m_dir.x, 0f, hit.m_dir.z).normalized;
                                if (Vector3.Dot(victimForward.normalized, hitHoriz) > 0.25f)
                                {
                                    isBehind = true;
                                }
                            }

                            if (isBehind)
                            {
                                SE_RPClass.ScaleCombatDamageOnly(ref hit.m_damage, 1f + backBonus);
                            }
                        }
                    }
                }

                return true;
            }
        }

        // Warrior "Bloody Onslaught": register melee hits, grant +2.5% lifesteal per stack, & boost melee attack animation speed per stack!
        [HarmonyPatch(typeof(Character), "Damage")]
        public static class Patch_Character_Damage_WarriorOnslaught
        {
            public static void Postfix(Character __instance, HitData hit)
            {
                if (__instance == null || hit == null || __instance.IsDead()) return;
                Player local = Player.m_localPlayer;
                if (local == null || __instance == local || local.IsDead()) return;

                if (hit.GetAttacker() == local && !hit.m_ranged && SE_RPClass.IsCombatMeleeSkill(hit.m_skill))
                {
                    if (RPPlayerClassManager.GetPlayerClass(local) == RPClassType.Warrior)
                    {
                        SE_RPBraveHeart.RegisterMeleeHit();
                        int stacks = SE_RPBraveHeart.GetActiveStacks();
                        float lsPerStack = Mathf.Max(0f, RPClassConfig.WarriorBloodlustLifestealPerStack.Value);
                        if (stacks > 0 && lsPerStack > 0f && !__instance.IsPlayer())
                        {
                            // Estimate post-resistance melee damage dealt to the enemy so lifesteal matches real damage
                            HitData calc = hit.Clone();
                            HitData.DamageModifiers mods = __instance.GetDamageModifiers(null);
                            HitData.DamageModifier sigMod;
                            calc.ApplyResistance(mods, out sigMod);
                            float dealt = Mathf.Max(0f, calc.GetTotalDamage());
                            float maxReasonable = Mathf.Max(1f, __instance.GetMaxHealth());
                            if (dealt > maxReasonable) dealt = maxReasonable;

                            float heal = dealt * (stacks * lsPerStack);
                            if (heal >= 0.05f)
                            {
                                local.Heal(heal, true);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
        public static class Patch_CharacterAnimEvent_CustomFixedUpdate
        {
            private static readonly FieldInfo s_charField = typeof(CharacterAnimEvent).GetField("m_character", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_animField = typeof(CharacterAnimEvent).GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_pauseTimerField = typeof(CharacterAnimEvent).GetField("m_pauseTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Postfix(CharacterAnimEvent __instance)
            {
                if (__instance == null || s_charField == null || s_animField == null) return;
                Player p = s_charField.GetValue(__instance) as Player;
                if (p == null || p != Player.m_localPlayer) return;

                if (RPPlayerClassManager.GetPlayerClass(p) != RPClassType.Warrior) return;

                int stacks = SE_RPBraveHeart.GetActiveStacks();
                if (stacks <= 0) return;

                // Only accelerate melee attack animations (when InAttack and not paused by hitstop freeze-frame!)
                if (!p.InAttack() || SE_RPClass.IsProjectileAttack(p)) return;

                if (s_pauseTimerField != null)
                {
                    float pauseTimer = (float)s_pauseTimerField.GetValue(__instance);
                    if (pauseTimer > 0f) return;
                }

                Animator anim = s_animField.GetValue(__instance) as Animator;
                if (anim != null && anim.speed > 0.05f)
                {
                    float targetSpeed = 1f + stacks * RPClassConfig.WarriorBloodlustAttackSpeedPerStack.Value;
                    if (Mathf.Abs(anim.speed - targetSpeed) > 0.005f)
                    {
                        anim.speed = targetSpeed;
                    }
                }
            }
        }

        // ====================================================================
        // 3b. SORCERER "ARCHMAGE": FORSAKEN POWER MASTERY (SOLO + DEDICATED SERVER CO-OP)
        //     • +50% Forsaken Power duration (7.5 min instead of 5 min) for the Sorcerer AND all allies in range!
        //     • -25% Forsaken Power cooldown (15 min instead of 20 min) on Sorcerer!
        //     NOTE: In Valheim 1.0, `Player.ActivateGuardianPower()` ALWAYS returns `false` (IL_00ac: ldc.i4.0; ret)
        //     even on successful activation, so we track `m_guardianPowerCooldown` across Prefix -> Postfix and
        //     broadcast an explicit ZNetView RPC onto every ally receiving the Forsaken Power!
        // ====================================================================
        internal const string RpcSorcererExtendGuardianPower = "CRP_RPC_ExtendGP";

        internal static bool IsGuardianPowerEffect(StatusEffect se)
        {
            if (se == null) return false;
            string seName = se.name;
            if (!string.IsNullOrEmpty(seName) && seName.StartsWith("GP_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return se.m_cooldown >= 600f;
        }

        internal static void ApplyExtendedGuardianDurationLocal(Player targetPlayer, int gpNameHash, float targetTtlSeconds)
        {
            if (targetPlayer == null || gpNameHash == 0 || targetTtlSeconds <= 0f) return;
            SEMan sem = targetPlayer.GetSEMan();
            if (sem == null) return;

            StatusEffect active = sem.GetStatusEffect(gpNameHash);
            if (active == null && ObjectDB.instance != null)
            {
                StatusEffect prefabSe = ObjectDB.instance.GetStatusEffect(gpNameHash);
                if (prefabSe != null)
                {
                    active = sem.AddStatusEffect(prefabSe, true, 0, 0f);
                }
            }
            if (active != null)
            {
                active.m_ttl = targetTtlSeconds;
                active.ResetTime();
            }
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        public static class Patch_Character_Awake_SorcererGuardianRpc
        {
            public static void Postfix(Character __instance)
            {
                Player p = __instance as Player;
                if (p == null) return;
                ZNetView nv = RPPlayerClassManager.GetNetView(p);
                if (nv == null || !nv.IsValid()) return;

                nv.Register<int, float>(RpcSorcererExtendGuardianPower, delegate(long sender, int gpHash, float ttlSeconds)
                {
                    ApplyExtendedGuardianDurationLocal(p, gpHash, ttlSeconds);
                });
            }
        }

        [HarmonyPatch(typeof(Player), "ActivateGuardianPower")]
        public static class Patch_Player_ActivateGuardianPower_Sorcerer
        {
            private static readonly FieldInfo s_guardianSeField = typeof(Player).GetField("m_guardianSE", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Prefix(Player __instance, out float __state)
            {
                __state = (__instance != null) ? __instance.m_guardianPowerCooldown : -1f;
            }

            public static void Postfix(Player __instance, float __state)
            {
                // Detect actual activation: cooldown transitioned from <= 0f to > 0f!
                if (__instance == null || __state > 0f || __instance.m_guardianPowerCooldown <= 0f) return;
                if (RPPlayerClassManager.GetPlayerClass(__instance) != RPClassType.Sorcerer) return;

                // 1) Reduce Sorcerer's Forsaken Power cooldown by 25% (1200s -> 900s = 15 min)
                float cdReduction = Mathf.Clamp(RPClassConfig.SorcererGuardianCooldownReduction.Value, 0f, 0.90f);
                if (cdReduction > 0f)
                {
                    __instance.m_guardianPowerCooldown *= (1f - cdReduction);
                }

                // 2) Extend Forsaken Power duration by +50% (300s -> 450s = 7.5 min) on BOTH the Sorcerer AND all allies within 10m!
                float durBonus = Mathf.Max(0f, RPClassConfig.SorcererGuardianDurationBonus.Value);
                if (durBonus <= 0f || s_guardianSeField == null) return;

                StatusEffect gpPrefab = s_guardianSeField.GetValue(__instance) as StatusEffect;
                if (gpPrefab == null || gpPrefab.m_ttl <= 0f) return;

                int gpHash = gpPrefab.NameHash();
                float boostedTtl = gpPrefab.m_ttl * (1f + durBonus);

                List<Player> playersInRange = new List<Player>();
                Player.GetPlayersInRange(__instance.transform.position, 10.5f, playersInRange);
                for (int i = 0; i < playersInRange.Count; i++)
                {
                    Player ally = playersInRange[i];
                    if (ally == null || ally.IsDead()) continue;

                    ZNetView nv = RPPlayerClassManager.GetNetView(ally);
                    if (nv != null && nv.IsValid() && !nv.IsOwner())
                    {
                        nv.InvokeRPC(RpcSorcererExtendGuardianPower, gpHash, boostedTtl);
                    }
                    else
                    {
                        ApplyExtendedGuardianDurationLocal(ally, gpHash, boostedTtl);
                    }
                }
            }
        }

        // Also cover re-casting / refreshing an already-active Forsaken Power on a Sorcerer or via Internal_AddStatusEffect!
        [HarmonyPatch(typeof(SEMan), "AddStatusEffect", new Type[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
        public static class Patch_SEMan_AddStatusEffect_SorcererGuardianDuration
        {
            public static void Postfix(SEMan __instance, Character ___m_character, StatusEffect statusEffect, bool resetTime)
            {
                if (__instance == null || statusEffect == null || ___m_character == null) return;
                Player p = ___m_character as Player;
                if (p == null || RPPlayerClassManager.GetPlayerClass(p) != RPClassType.Sorcerer) return;
                if (!IsGuardianPowerEffect(statusEffect) || statusEffect.m_ttl <= 0f) return;

                float durBonus = Mathf.Max(0f, RPClassConfig.SorcererGuardianDurationBonus.Value);
                if (durBonus <= 0f) return;

                StatusEffect active = __instance.GetStatusEffect(statusEffect.NameHash());
                if (active != null)
                {
                    active.m_ttl = statusEffect.m_ttl * (1f + durBonus);
                    if (resetTime)
                    {
                        active.ResetTime();
                    }
                }
            }
        }

        // ====================================================================
        // 4. HUNTER (+15%) BOW DRAW & CROSSBOW RELOAD SPEED,
        //    (-15%) BOW DRAW STAMINA COST,
        //    & 3-HIT CONSECUTIVE SHOT COMBO -> NEXT SHOT DEALS x2 DAMAGE!
        // ====================================================================
        public static class HunterConsecutiveShotTracker
        {
            private static int s_consecutiveHits;
            private static bool s_nextShotEmpowered;
            private static readonly HashSet<int> s_trackedProjectiles = new HashSet<int>();
            private static readonly HashSet<int> s_empoweredProjectiles = new HashSet<int>();
            private static readonly HashSet<int> s_landedCharacterProjectiles = new HashSet<int>();

            public static int ConsecutiveHits
            {
                get { return s_consecutiveHits; }
            }

            public static bool IsNextShotEmpowered
            {
                get { return s_nextShotEmpowered; }
            }

            public static bool IsRangedHunterProjectile(Skills.SkillType skill)
            {
                return skill == Skills.SkillType.Bows
                    || skill == Skills.SkillType.Crossbows
                    || skill == Skills.SkillType.Spears;
            }

            public static void OnProjectileFired(Projectile proj, Character owner)
            {
                if (proj == null) return;
                Player p = owner as Player;
                if (p == null || p != Player.m_localPlayer) return;
                if (RPPlayerClassManager.GetPlayerClass(p) != RPClassType.Hunter) return;
                if (!IsRangedHunterProjectile(proj.m_skill)) return;

                int id = proj.GetInstanceID();
                s_trackedProjectiles.Add(id);

                if (s_nextShotEmpowered)
                {
                    s_nextShotEmpowered = false;
                    s_consecutiveHits = 0;
                    s_empoweredProjectiles.Add(id);

                    // Multiply projectile damage BEFORE flight so even remote multiplayer target owners receive exact x2 damage!
                    float mult = Mathf.Max(1f, RPClassConfig.HunterComboDamageMultiplier.Value);
                    SE_RPClass.ScaleCombatDamageOnly(ref proj.m_damage, mult);
                }
            }

            public static void OnProjectileHitCollider(Projectile proj, Collider collider, bool water)
            {
                if (proj == null) return;
                int id = proj.GetInstanceID();
                if (!s_trackedProjectiles.Contains(id)) return;

                if (!water && collider != null)
                {
                    GameObject hitObj = Projectile.FindHitObject(collider);
                    Character hitChar = hitObj != null ? hitObj.GetComponent<Character>() : null;
                    if (hitChar != null && hitChar != Player.m_localPlayer)
                    {
                        if (s_landedCharacterProjectiles.Add(id))
                        {
                            // Empowered 4th shot consumes the streak; normal shots build 1 -> 2 -> 3!
                            if (!s_empoweredProjectiles.Contains(id))
                            {
                                s_consecutiveHits++;
                                int req = Mathf.Max(1, RPClassConfig.HunterComboHitsRequired.Value);
                                if (s_consecutiveHits >= req)
                                {
                                    s_consecutiveHits = 0;
                                    s_nextShotEmpowered = true;
                                }
                            }
                        }
                        return;
                    }
                }

                // Hit ground, tree, rock, building, or water without hitting a Character -> streak resets!
                if (!s_landedCharacterProjectiles.Contains(id))
                {
                    ResetStreak(id);
                }
            }

            public static void OnProjectileDestroyedById(int id)
            {
                if (!s_trackedProjectiles.Contains(id)) return;

                // If projectile expired in mid-air without hitting any Character -> streak resets!
                if (!s_landedCharacterProjectiles.Contains(id))
                {
                    ResetStreak(id);
                }

                s_trackedProjectiles.Remove(id);
                s_empoweredProjectiles.Remove(id);
                s_landedCharacterProjectiles.Remove(id);
            }

            private static void ResetStreak(int id)
            {
                s_consecutiveHits = 0;
                if (!s_empoweredProjectiles.Contains(id))
                {
                    s_nextShotEmpowered = false;
                }
            }
        }

        public class TrackedHunterProjectileHook : MonoBehaviour
        {
            public int ProjectileId;

            private void OnDestroy()
            {
                if (ProjectileId != 0)
                {
                    HunterConsecutiveShotTracker.OnProjectileDestroyedById(ProjectileId);
                }
            }
        }

        [HarmonyPatch(typeof(Projectile), "Setup")]
        public static class Patch_Projectile_Setup
        {
            public static void Postfix(Projectile __instance, Character owner)
            {
                if (__instance == null) return;
                Player p = owner as Player;
                if (p != null && p == Player.m_localPlayer && RPPlayerClassManager.GetPlayerClass(p) == RPClassType.Hunter)
                {
                    TrackedHunterProjectileHook hook = __instance.gameObject.GetComponent<TrackedHunterProjectileHook>();
                    if (hook == null) hook = __instance.gameObject.AddComponent<TrackedHunterProjectileHook>();
                    hook.ProjectileId = __instance.GetInstanceID();
                }
                HunterConsecutiveShotTracker.OnProjectileFired(__instance, owner);
            }
        }

        [HarmonyPatch(typeof(Projectile), "OnHit")]
        public static class Patch_Projectile_OnHit
        {
            public static void Prefix(Projectile __instance, Collider collider, bool water)
            {
                HunterConsecutiveShotTracker.OnProjectileHitCollider(__instance, collider, water);
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetDrawStaminaDrain")]
        public static class Patch_ItemData_GetDrawStaminaDrain
        {
            public static void Postfix(ref float __result)
            {
                if (__result <= 0f) return;
                Player local = Player.m_localPlayer;
                if (local == null) return;

                if (RPPlayerClassManager.GetPlayerClass(local) == RPClassType.Hunter)
                {
                    __result *= Mathf.Max(0.05f, 1f - RPClassConfig.HunterBowDrawStaminaReduction.Value);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
        public static class Patch_Player_UpdateAttackBowDraw
        {
            private static readonly FieldInfo s_drawTimeField = typeof(Humanoid).GetField("m_attackDrawTime", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Prefix(Player __instance, out float __state)
            {
                __state = -999f;
                if (__instance != null && s_drawTimeField != null)
                {
                    __state = (float)s_drawTimeField.GetValue(__instance);
                }
            }

            public static void Postfix(Player __instance, float __state)
            {
                if (__instance == null || s_drawTimeField == null || __state < 0f) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls != RPClassType.Hunter) return;

                float speedBonus = RPClassConfig.HunterBowCrossbowSpeedBonus.Value;
                if (speedBonus <= 0f) return;

                float after = (float)s_drawTimeField.GetValue(__instance);
                float delta = after - __state;
                if (delta > 0f && delta < 0.2f)
                {
                    s_drawTimeField.SetValue(__instance, after + delta * speedBonus);
                }
            }
        }

        [HarmonyPatch(typeof(Player), "QueueReloadAction")]
        public static class Patch_Player_QueueReloadAction
        {
            private static readonly FieldInfo s_actionQueueField = typeof(Player).GetField("m_actionQueue", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static FieldInfo s_durationField;
            private static FieldInfo s_typeField;

            public static void Postfix(Player __instance)
            {
                if (__instance == null || s_actionQueueField == null) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(__instance);
                if (cls != RPClassType.Hunter) return;

                float speedBonus = RPClassConfig.HunterBowCrossbowSpeedBonus.Value;
                if (speedBonus <= 0f) return;

                IList queue = s_actionQueueField.GetValue(__instance) as IList;
                if (queue == null || queue.Count == 0) return;

                object lastEntry = queue[queue.Count - 1];
                if (lastEntry == null) return;

                if (s_durationField == null)
                {
                    Type entryType = lastEntry.GetType();
                    s_durationField = entryType.GetField("m_duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_typeField = entryType.GetField("m_type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (s_durationField != null && s_typeField != null)
                {
                    int actionType = Convert.ToInt32(s_typeField.GetValue(lastEntry));
                    if (actionType == 2)
                    {
                        float curDur = (float)s_durationField.GetValue(lastEntry);
                        if (curDur > 0.05f)
                        {
                            s_durationField.SetValue(lastEntry, curDur / (1f + speedBonus));
                        }
                    }
                }
            }
        }

        // ====================================================================
        // 5. SHIELD / WEAPON BLOCK POWER MODIFIERS
        //    (KNIGHT +25% with Shield, THIEF -20%, HUNTER -15%)
        // ====================================================================
        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        public static class Patch_Humanoid_BlockAttack
        {
            public static float ActiveBlockMultiplier = 1f;

            public static void Prefix(Humanoid __instance)
            {
                ActiveBlockMultiplier = 1f;
                Player p = __instance as Player;
                if (p == null) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(p);
                if (cls == RPClassType.Knight)
                {
                    if (SE_RPClass.HasShieldEquipped(p))
                    {
                        ActiveBlockMultiplier = 1f + RPClassConfig.KnightShieldBlockBonus.Value;
                    }
                }
                else if (cls == RPClassType.Warrior)
                {
                    ActiveBlockMultiplier = Mathf.Max(0.1f, 1f - RPClassConfig.WarriorBlockPowerPenalty.Value);
                }
                else if (cls == RPClassType.Thief)
                {
                    ActiveBlockMultiplier = Mathf.Max(0.1f, 1f - RPClassConfig.ThiefBlockPowerPenalty.Value);
                }
                else if (cls == RPClassType.Hunter)
                {
                    ActiveBlockMultiplier = Mathf.Max(0.1f, 1f - RPClassConfig.HunterBlockPowerPenalty.Value);
                }
            }

            public static void Finalizer()
            {
                ActiveBlockMultiplier = 1f;
            }
        }

        [HarmonyPatch(typeof(ItemDrop.ItemData), "GetBlockPower", new Type[] { typeof(int), typeof(float) })]
        public static class Patch_ItemData_GetBlockPower
        {
            public static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {
                if (Patch_Humanoid_BlockAttack.ActiveBlockMultiplier != 1f)
                {
                    __result *= Patch_Humanoid_BlockAttack.ActiveBlockMultiplier;
                    return;
                }

                // Also show accurate modified shield block power in inventory tooltip for local player!
                Player local = Player.m_localPlayer;
                if (local == null || __instance == null || __instance.m_shared == null) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(local);
                bool isShield = (__instance.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield);
                if (cls == RPClassType.Knight && isShield)
                {
                    __result *= (1f + RPClassConfig.KnightShieldBlockBonus.Value);
                }
                else if (cls == RPClassType.Warrior)
                {
                    __result *= Mathf.Max(0.1f, 1f - RPClassConfig.WarriorBlockPowerPenalty.Value);
                }
                else if (cls == RPClassType.Thief)
                {
                    __result *= Mathf.Max(0.1f, 1f - RPClassConfig.ThiefBlockPowerPenalty.Value);
                }
                else if (cls == RPClassType.Hunter)
                {
                    __result *= Mathf.Max(0.1f, 1f - RPClassConfig.HunterBlockPowerPenalty.Value);
                }
            }
        }

        // Sorcerer Heavy Armor: clamp total Eitr regen multiplier >= 0 (and 0 when wearing Heavy Armor!)
        [HarmonyPatch(typeof(Player), "GetEquipmentEitrRegenModifier")]
        public static class Patch_Player_GetEquipmentEitrRegenModifier
        {
            public static void Postfix(Player __instance, ref float __result)
            {
                if (__instance == null) return;
                if (RPPlayerClassManager.GetPlayerClass(__instance) == RPClassType.Sorcerer && SE_RPClass.IsWearingHeavyArmor(__instance))
                {
                    float pen = Mathf.Clamp01(RPClassConfig.SorcererHeavyArmorEitrPenalty.Value);
                    if (pen >= 0.999f)
                    {
                        __result = 0f;
                    }
                    else
                    {
                        __result *= (1f - pen);
                    }
                }
            }
        }

        // Sorcerer "Arcane Resonance": every 5th Eitr-consuming spell cast is FREE (0 Eitr)!
        public static class SorcererFreeCastTracker
        {
            private static int s_castsTowardFree;
            private static Attack s_currentFreeAttack;

            public static int CastsTowardFree
            {
                get { return s_castsTowardFree; }
            }

            public static bool IsNextCastFree
            {
                get
                {
                    int req = Mathf.Max(2, RPClassConfig.SorcererFreeCastEveryN.Value);
                    return s_castsTowardFree >= req - 1;
                }
            }

            public static bool IsAttackCurrentlyFree(Attack atk)
            {
                return atk != null && atk == s_currentFreeAttack;
            }

            public static void OnAttackStarted(Attack atk, Humanoid character, bool started, float originalEitrCost)
            {
                if (!started || atk == null || originalEitrCost <= 0.01f) return;
                Player p = character as Player;
                if (p == null || p != Player.m_localPlayer) return;
                if (RPPlayerClassManager.GetPlayerClass(p) != RPClassType.Sorcerer) return;

                if (IsNextCastFree)
                {
                    // This is the 5th cast -> mark this Attack instance as free & reset counter!
                    s_currentFreeAttack = atk;
                    s_castsTowardFree = 0;
                }
                else
                {
                    s_currentFreeAttack = null;
                    s_castsTowardFree++;
                }
            }
        }

        [HarmonyPatch(typeof(Attack), "GetAttackEitr", new Type[0])]
        public static class Patch_Attack_GetAttackEitr
        {
            private static readonly FieldInfo s_atkCharField = typeof(Attack).GetField("m_character", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            public static void Postfix(Attack __instance, ref float __result)
            {
                if (__result <= 0f || __instance == null || s_atkCharField == null) return;

                Player p = s_atkCharField.GetValue(__instance) as Player;
                if (p == null || p != Player.m_localPlayer) return;
                if (RPPlayerClassManager.GetPlayerClass(p) != RPClassType.Sorcerer) return;

                if (SorcererFreeCastTracker.IsAttackCurrentlyFree(__instance) ||
                    (!p.InAttack() && SorcererFreeCastTracker.IsNextCastFree))
                {
                    __result = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Attack), "Start")]
        public static class Patch_Attack_Start
        {
            public static void Prefix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon, out float __state)
            {
                __state = 0f;
                if (__instance == null || weapon == null || weapon.m_shared == null || weapon.m_shared.m_attack == null) return;
                __state = weapon.m_shared.m_attack.m_attackEitr;
                if (__state <= 0.01f && weapon.m_shared.m_attack.m_drawEitrDrain > 0.01f)
                {
                    __state = weapon.m_shared.m_attack.m_drawEitrDrain;
                }
            }

            public static void Postfix(Attack __instance, Humanoid character, bool __result, float __state)
            {
                SorcererFreeCastTracker.OnAttackStarted(__instance, character, __result, __state);
            }
        }

        // ====================================================================
        // 6. HILDIR STORE INTEGRATION (1000 GOLD REBIRTH SCROLL)
        //    & RIGHT-CLICK CONSUMPTION IN INVENTORY
        // ====================================================================
        [HarmonyPatch(typeof(Trader), "GetAvailableItems")]
        public static class Patch_Trader_GetAvailableItems
        {
            public static void Postfix(Trader __instance, ref List<Trader.TradeItem> __result)
            {
                if (__instance == null || __result == null) return;

                // Match Hildir specifically by prefab name ("Hildir") or localization token ("$npc_hildir")
                bool isHildir =
                    (__instance.m_name != null && __instance.m_name.IndexOf("hildir", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (__instance.gameObject != null && __instance.gameObject.name.IndexOf("hildir", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isHildir) return;

                GameObject scrollPrefabObj = PrefabManager.Instance.GetPrefab(RPPlayerClassManager.RebirthScrollPrefabName);
                if (scrollPrefabObj == null) return;

                ItemDrop scrollDrop = scrollPrefabObj.GetComponent<ItemDrop>();
                if (scrollDrop == null) return;

                // Avoid duplicate entry if already injected
                for (int i = 0; i < __result.Count; i++)
                {
                    if (__result[i] != null && __result[i].m_prefab == scrollDrop)
                    {
                        __result[i].m_price = Mathf.Max(1, RPClassConfig.HildirScrollPrice.Value);
                        return;
                    }
                }

                Trader.TradeItem tradeItem = new Trader.TradeItem
                {
                    m_prefab = scrollDrop,
                    m_stack = 1,
                    m_price = Mathf.Max(1, RPClassConfig.HildirScrollPrice.Value),
                    m_requiredGlobalKey = "",
                    m_name = "$item_rp_rebirth_scroll",
                    m_tooltip = "",
                    m_buyKey = "",
                    m_incrementKey = "",
                    m_incrementAmount = 1,
                    m_buyPlayerEffects = new EffectList()
                };
                __result.Add(tradeItem);
            }
        }

        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class Patch_Player_ConsumeItem
        {
            public static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
            {
                if (__instance == null || item == null || item.m_shared == null) return true;

                if (item.m_shared.m_name == "$item_rp_rebirth_scroll")
                {
                    if (__instance == Player.m_localPlayer)
                    {
                        // Open UI without consuming scroll yet — scroll is consumed only upon clicking Confirm!
                        RPClassSelectionUI.Open(isRespecFromScroll: true, forceAllowCancel: true);
                    }
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // ====================================================================
        // 7. OVERHEAD CLASS LABEL ABOVE PLAYER NICKNAME IN MULTIPLAYER
        // ====================================================================
        [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
        public static class Patch_EnemyHud_UpdateHuds
        {
            private static readonly FieldInfo s_hudsField = typeof(EnemyHud).GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static FieldInfo s_charField;
            private static FieldInfo s_nameField;

            public static void Postfix(EnemyHud __instance)
            {
                if (!RPClassConfig.ShowClassOverheadTag.Value || __instance == null || s_hudsField == null) return;

                IDictionary dict = s_hudsField.GetValue(__instance) as IDictionary;
                if (dict == null || dict.Count == 0) return;

                foreach (DictionaryEntry entry in dict)
                {
                    Player p = entry.Key as Player;
                    if (p == null || entry.Value == null) continue;

                    RPClassType cls = RPPlayerClassManager.GetPlayerClass(p);
                    if (cls == RPClassType.None) continue;

                    if (s_nameField == null)
                    {
                        Type hudDataType = entry.Value.GetType();
                        s_charField = hudDataType.GetField("m_character", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        s_nameField = hudDataType.GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    if (s_nameField == null) continue;

                    TextMeshProUGUI nameTmp = s_nameField.GetValue(entry.Value) as TextMeshProUGUI;
                    if (nameTmp == null) continue;

                    string classLabel = Localization.instance != null
                        ? Localization.instance.Localize(RPClassConfig.GetClassToken(cls))
                        : cls.ToString();

                    string hex = RPClassConfig.GetClassHexColor(cls);
                    string basePlayerName = p.GetHoverName();

                    // Class displayed right above the player's nickname!
                    string formatted = string.Format("<size=82%><color={0}>[{1}]</color></size>\n{2}", hex, classLabel, basePlayerName);
                    if (nameTmp.text != formatted)
                    {
                        nameTmp.richText = true;
                        nameTmp.overflowMode = TextOverflowModes.Overflow;
                        nameTmp.text = formatted;
                    }
                }
            }
        }

        // ====================================================================
        // 8. KEEP PASSIVE SKILL RIGHT NEXT TO BUFFS AS THE RIGHTMOST ICON
        //    (In Valheim's HUD, index 0 is placed at x = -4, the rightmost slot,
        //    and subsequent buffs extend leftward: -4 - col * spacing!)
        // ====================================================================
        [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
        public static class Patch_Hud_UpdateStatusEffects
        {
            public static void Prefix(List<StatusEffect> statusEffects)
            {
                if (statusEffects == null || statusEffects.Count <= 1) return;

                // Move IRPPassiveSkillEffect to index 0 so it is ALWAYS the rightmost icon in the buff row!
                List<StatusEffect> passives = null;
                for (int i = 0; i < statusEffects.Count; i++)
                {
                    if (statusEffects[i] is IRPPassiveSkillEffect)
                    {
                        if (passives == null) passives = new List<StatusEffect>();
                        passives.Add(statusEffects[i]);
                    }
                }

                if (passives != null && passives.Count > 0 && passives.Count < statusEffects.Count)
                {
                    // Check if already at the very front to avoid unnecessary work
                    bool alreadyFirst = true;
                    for (int i = 0; i < passives.Count; i++)
                    {
                        if (!(statusEffects[i] is IRPPassiveSkillEffect))
                        {
                            alreadyFirst = false;
                            break;
                        }
                    }

                    if (!alreadyFirst)
                    {
                        statusEffects.RemoveAll(se => se is IRPPassiveSkillEffect);
                        statusEffects.InsertRange(0, passives);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TextsDialog), "AddActiveEffects")]
        public static class Patch_TextsDialog_AddActiveEffects
        {
            private static readonly FieldInfo s_textsField = typeof(TextsDialog).GetField("m_texts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static FieldInfo s_textField;

            public static void Postfix(TextsDialog __instance)
            {
                Player p = Player.m_localPlayer;
                if (__instance == null || p == null || s_textsField == null) return;

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(p);
                if (cls == RPClassType.None) return;

                IList texts = s_textsField.GetValue(__instance) as IList;
                if (texts == null || texts.Count == 0) return;

                object firstEntry = texts[0];
                if (firstEntry == null) return;

                if (s_textField == null)
                {
                    s_textField = firstEntry.GetType().GetField("m_text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (s_textField == null) return;

                string curBody = s_textField.GetValue(firstEntry) as string;
                if (curBody == null) return;

                // Append full RP Class & Passive summary cleanly at the bottom of the Active Effects Raven tab
                string locTitle = Localization.instance != null
                    ? Localization.instance.Localize("$rp_hud_passive_section")
                    : "Пассивные навыки класса";
                string clsName = Localization.instance != null
                    ? Localization.instance.Localize(RPClassConfig.GetClassToken(cls))
                    : cls.ToString();
                string hex = RPClassConfig.GetClassHexColor(cls);

                string classSection = string.Format(
                    "\n\n<color=yellow>{0}</color>\n<color={1}><b>{2}</b></color>\n{3}",
                    locTitle,
                    hex,
                    clsName,
                    RPClassConfig.BuildClassTooltip(cls, includeLore: false));

                s_textField.SetValue(firstEntry, curBody + classSection);
            }
        }

        // ====================================================================
        // 9. HUD ARMOR SHIELD WIDGET RIGHT NEXT TO THE HEALTH BAR
        //    Shows live Armor Class on a crisp heraldic steel shield!
        // ====================================================================
        [HarmonyPatch(typeof(Hud), "UpdateHealth")]
        public static class Patch_Hud_UpdateHealth
        {
            private static readonly FieldInfo s_healthPanelField = typeof(Hud).GetField("m_healthPanel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_healthBarRootField = typeof(Hud).GetField("m_healthBarRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_staminaBarRootField = typeof(Hud).GetField("m_staminaBar2Root", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_eitrBarRootField = typeof(Hud).GetField("m_eitrBarRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_adrenalineBarRootField = typeof(Hud).GetField("m_adrenalineBarRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_foodIconField = typeof(Hud).GetField("m_foodIcon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_healthTextField = typeof(Hud).GetField("m_healthText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            private static readonly FieldInfo s_gpRootField = typeof(Hud).GetField("m_gpRoot", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            private static RectTransform s_trackedGpRoot;
            private static Vector2 s_gpOrigAnchoredPos;
            private const float GuardianPowerLiftPixels = 32f;

            private static GameObject s_armorWidgetObj;
            private static RectTransform s_armorWidgetRt;
            private static Image s_armorShieldImg;
            private static TMP_Text s_armorValueTmp;

            private static GameObject s_stackWidgetObj;
            private static RectTransform s_stackWidgetRt;
            private static Image s_stackWeaponImg;
            private static TMP_Text s_stackCountTmp;

            private static float s_baseColSpacing = 48f;
            private static float s_baseY = 20f;
            private static int s_lastArmorShown = -9999;
            private static bool s_lastBoosted;
            private static RPClassType s_lastStackClass = RPClassType.None;
            private static int s_lastStackVal = -999;
            private static bool s_lastHunterEmpowered;

            public static void Postfix(Hud __instance, Player player)
            {
                if (__instance == null || player == null) return;

                if (!RPClassConfig.ShowHudArmorShield.Value)
                {
                    if (s_armorWidgetObj != null && s_armorWidgetObj.activeSelf) s_armorWidgetObj.SetActive(false);
                    if (s_stackWidgetObj != null && s_stackWidgetObj.activeSelf) s_stackWidgetObj.SetActive(false);
                    UpdateGuardianPowerVerticalOffset(__instance, lift: false);
                    return;
                }

                UpdateGuardianPowerVerticalOffset(__instance, lift: true);
                EnsureArmorWidget(__instance);
                if (s_armorWidgetObj == null || s_armorValueTmp == null || s_armorWidgetRt == null) return;

                if (!s_armorWidgetObj.activeSelf)
                {
                    s_armorWidgetObj.SetActive(true);
                }

                // Keep shield & stack weapon icon right next to the HP/Stamina/Eitr bars!
                UpdateShieldPositionBesideBars(__instance);

                RPClassType cls = RPPlayerClassManager.GetPlayerClass(player);
                int currentArmor = Mathf.RoundToInt(player.GetBodyArmor());
                bool shieldBoostActive = (cls == RPClassType.Knight) && SE_RPClass.IsRaisingShield(player);

                if (currentArmor != s_lastArmorShown || shieldBoostActive != s_lastBoosted)
                {
                    s_lastArmorShown = currentArmor;
                    s_lastBoosted = shieldBoostActive;
                    s_armorValueTmp.text = currentArmor.ToString();

                    if (shieldBoostActive)
                    {
                        s_armorValueTmp.color = new Color(0.65f, 0.92f, 1.0f, 1f);
                        if (s_armorShieldImg != null) s_armorShieldImg.color = new Color(0.85f, 0.96f, 1.0f, 1f);
                    }
                    else
                    {
                        s_armorValueTmp.color = Color.white;
                        if (s_armorShieldImg != null) s_armorShieldImg.color = Color.white;
                    }
                }

                UpdateClassStackWidget(cls);
            }

            private static void UpdateClassStackWidget(RPClassType cls)
            {
                if (s_stackWidgetObj == null || s_stackWeaponImg == null || s_stackCountTmp == null) return;

                if (cls == RPClassType.Warrior)
                {
                    int stacks = SE_RPBraveHeart.GetActiveStacks();
                    if (!s_stackWidgetObj.activeSelf) s_stackWidgetObj.SetActive(true);

                    if (s_lastStackClass != cls || s_lastStackVal != stacks)
                    {
                        s_lastStackClass = cls;
                        s_lastStackVal = stacks;
                        s_stackWeaponImg.sprite = RPIcons.GetHudWarriorSwordIcon();
                        s_stackWeaponImg.color = stacks > 0 ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.72f);
                        s_stackCountTmp.text = stacks.ToString();
                        s_stackCountTmp.color = (stacks >= RPClassConfig.WarriorBloodlustMaxStacks.Value)
                            ? new Color(1.0f, 0.84f, 0.35f, 1f)
                            : Color.white;
                    }
                }
                else if (cls == RPClassType.Hunter)
                {
                    int hits = HunterConsecutiveShotTracker.ConsecutiveHits;
                    bool empowered = HunterConsecutiveShotTracker.IsNextShotEmpowered;
                    if (!s_stackWidgetObj.activeSelf) s_stackWidgetObj.SetActive(true);

                    if (s_lastStackClass != cls || s_lastStackVal != hits || s_lastHunterEmpowered != empowered)
                    {
                        s_lastStackClass = cls;
                        s_lastStackVal = hits;
                        s_lastHunterEmpowered = empowered;
                        s_stackWeaponImg.sprite = RPIcons.GetHudHunterArrowIcon(empowered);
                        s_stackWeaponImg.color = (empowered || hits > 0) ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.72f);

                        if (empowered)
                        {
                            s_stackCountTmp.text = "x" + RPClassConfig.HunterComboDamageMultiplier.Value.ToString("0.#");
                            s_stackCountTmp.color = new Color(1.0f, 0.86f, 0.32f, 1f);
                        }
                        else
                        {
                            s_stackCountTmp.text = hits.ToString();
                            s_stackCountTmp.color = Color.white;
                        }
                    }

                    // Gentle golden pulse while 4th empowered arrow is ready to fire!
                    if (empowered)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.08f;
                        s_stackWidgetRt.localScale = new Vector3(pulse, pulse, 1f);
                    }
                    else
                    {
                        s_stackWidgetRt.localScale = Vector3.one;
                    }
                }
                else if (cls == RPClassType.Sorcerer)
                {
                    int casts = SorcererFreeCastTracker.CastsTowardFree;
                    bool freeReady = SorcererFreeCastTracker.IsNextCastFree;
                    if (!s_stackWidgetObj.activeSelf) s_stackWidgetObj.SetActive(true);

                    if (s_lastStackClass != cls || s_lastStackVal != casts || s_lastHunterEmpowered != freeReady)
                    {
                        s_lastStackClass = cls;
                        s_lastStackVal = casts;
                        s_lastHunterEmpowered = freeReady;
                        s_stackWeaponImg.sprite = RPIcons.GetHudSorcererCastIcon(freeReady);
                        s_stackWeaponImg.color = (freeReady || casts > 0) ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.72f);

                        if (freeReady)
                        {
                            s_stackCountTmp.text = "0";
                            s_stackCountTmp.color = new Color(0.45f, 0.94f, 1.0f, 1f);
                        }
                        else
                        {
                            s_stackCountTmp.text = casts.ToString();
                            s_stackCountTmp.color = Color.white;
                        }
                    }

                    if (freeReady)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.08f;
                        s_stackWidgetRt.localScale = new Vector3(pulse, pulse, 1f);
                    }
                    else
                    {
                        s_stackWidgetRt.localScale = Vector3.one;
                    }
                }
                else if (cls == RPClassType.Thief)
                {
                    bool isNight = EnvMan.IsNight();
                    int stateVal = isNight ? 1 : 0;
                    if (!s_stackWidgetObj.activeSelf) s_stackWidgetObj.SetActive(true);

                    if (s_lastStackClass != cls || s_lastStackVal != stateVal)
                    {
                        s_lastStackClass = cls;
                        s_lastStackVal = stateVal;
                        s_stackWeaponImg.sprite = RPIcons.GetHudThiefDayNightIcon(isNight);
                        s_stackWeaponImg.color = isNight ? Color.white : new Color(0.92f, 0.88f, 0.78f, 0.78f);
                        s_stackCountTmp.text = "";
                    }

                    if (isNight)
                    {
                        float pulse = 1f + Mathf.Sin(Time.time * 3.5f) * 0.04f;
                        s_stackWidgetRt.localScale = new Vector3(pulse, pulse, 1f);
                    }
                    else
                    {
                        s_stackWidgetRt.localScale = Vector3.one;
                    }
                }
                else
                {
                    if (s_stackWidgetObj.activeSelf)
                    {
                        s_stackWidgetObj.SetActive(false);
                        s_lastStackClass = RPClassType.None;
                    }
                }
            }

            private static bool IsBarCurrentlyVisible(RectTransform barRoot)
            {
                if (barRoot == null || !barRoot.gameObject.activeInHierarchy) return false;
                CanvasGroup cg = barRoot.GetComponentInChildren<CanvasGroup>();
                if (cg != null && cg.alpha <= 0.05f) return false;
                Animator anim = barRoot.GetComponentInChildren<Animator>();
                if (anim != null && anim.isInitialized)
                {
                    // Valheim stamina/eitr bar animators use bool "Visible"
                    try
                    {
                        if (!anim.GetBool("Visible")) return false;
                    }
                    catch
                    {
                    }
                }
                return true;
            }

            private static void UpdateShieldPositionBesideBars(Hud hud)
            {
                RectTransform hpRoot = s_healthBarRootField != null ? s_healthBarRootField.GetValue(hud) as RectTransform : null;
                if (hpRoot == null) return;

                float rightmostX = hpRoot.localPosition.x;

                RectTransform stamRoot = s_staminaBarRootField != null ? s_staminaBarRootField.GetValue(hud) as RectTransform : null;
                if (IsBarCurrentlyVisible(stamRoot) && stamRoot.localPosition.x > rightmostX)
                {
                    rightmostX = stamRoot.localPosition.x;
                }

                RectTransform eitrRoot = s_eitrBarRootField != null ? s_eitrBarRootField.GetValue(hud) as RectTransform : null;
                if (IsBarCurrentlyVisible(eitrRoot) && eitrRoot.localPosition.x > rightmostX)
                {
                    rightmostX = eitrRoot.localPosition.x;
                }

                RectTransform adrenRoot = s_adrenalineBarRootField != null ? s_adrenalineBarRootField.GetValue(hud) as RectTransform : null;
                if (IsBarCurrentlyVisible(adrenRoot) && adrenRoot.localPosition.x > rightmostX)
                {
                    rightmostX = adrenRoot.localPosition.x;
                }

                float targetX = rightmostX + s_baseColSpacing;
                Vector3 cur = s_armorWidgetRt.localPosition;
                cur.x = Mathf.Lerp(cur.x, targetX, Time.deltaTime * 14f);
                cur.y = s_baseY;
                s_armorWidgetRt.localPosition = cur;

                if (s_stackWidgetRt != null)
                {
                    s_stackWidgetRt.localPosition = new Vector3(cur.x + 42f, s_baseY, 0f);
                }
            }

            private static void UpdateGuardianPowerVerticalOffset(Hud hud, bool lift)
            {
                if (hud == null || s_gpRootField == null) return;
                RectTransform gpRt = s_gpRootField.GetValue(hud) as RectTransform;
                if (gpRt == null) return;

                if (s_trackedGpRoot != gpRt)
                {
                    s_trackedGpRoot = gpRt;
                    s_gpOrigAnchoredPos = gpRt.anchoredPosition;
                }

                Vector2 desired = lift
                    ? new Vector2(s_gpOrigAnchoredPos.x, s_gpOrigAnchoredPos.y + GuardianPowerLiftPixels)
                    : s_gpOrigAnchoredPos;

                if ((gpRt.anchoredPosition - desired).sqrMagnitude > 0.01f)
                {
                    gpRt.anchoredPosition = desired;
                }
            }

            private static void EnsureArmorWidget(Hud hud)
            {
                if (s_armorWidgetObj != null) return;

                RectTransform healthPanel = s_healthPanelField != null ? s_healthPanelField.GetValue(hud) as RectTransform : null;
                RectTransform healthBarRoot = s_healthBarRootField != null ? s_healthBarRootField.GetValue(hud) as RectTransform : null;
                Image foodFork = s_foodIconField != null ? s_foodIconField.GetValue(hud) as Image : null;
                TMP_Text healthText = s_healthTextField != null ? s_healthTextField.GetValue(hud) as TMP_Text : null;

                if (healthPanel == null || healthText == null) return;

                Transform parent = healthBarRoot != null ? healthBarRoot.parent : healthPanel;
                s_armorWidgetObj = new GameObject("Coarstal_HudArmorShield", typeof(RectTransform), typeof(Image));
                s_armorWidgetObj.transform.SetParent(parent, false);

                s_armorWidgetRt = s_armorWidgetObj.GetComponent<RectTransform>();

                // Dedicated heraldic steel shield sprite!
                s_armorShieldImg = s_armorWidgetObj.GetComponent<Image>();
                s_armorShieldImg.sprite = RPIcons.GetHudArmorShieldIcon();
                s_armorShieldImg.preserveAspect = true;
                s_armorShieldImg.raycastTarget = false;

                if (healthBarRoot != null && foodFork != null)
                {
                    s_baseColSpacing = Mathf.Abs(healthBarRoot.localPosition.x - foodFork.transform.localPosition.x);
                    if (s_baseColSpacing < 38f) s_baseColSpacing = 48f;
                    // Align vertically right on the exact bottom icon row level (Fork & Red Health Triquetra)!
                    s_baseY = parent.InverseTransformPoint(foodFork.transform.position).y;
                    s_armorWidgetRt.localPosition = new Vector3(healthBarRoot.localPosition.x + s_baseColSpacing, s_baseY, 0f);
                }
                else if (healthBarRoot != null)
                {
                    s_baseColSpacing = 48f;
                    s_baseY = healthBarRoot.localPosition.y - 22f;
                    s_armorWidgetRt.localPosition = new Vector3(healthBarRoot.localPosition.x + s_baseColSpacing, s_baseY, 0f);
                }
                s_armorWidgetRt.sizeDelta = new Vector2(38f, 40f);

                // Clone exact Valheim health TMP_Text so font, material & black outline match "75" 1:1!
                GameObject txtClone = UnityEngine.Object.Instantiate(healthText.gameObject, s_armorWidgetObj.transform);
                txtClone.name = "ArmorValueText";
                RectTransform txtRt = txtClone.GetComponent<RectTransform>();
                txtRt.localRotation = Quaternion.identity;
                txtRt.localScale = Vector3.one;
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.pivot = new Vector2(0.5f, 0.5f);
                txtRt.offsetMin = new Vector2(-12f, -1f);
                txtRt.offsetMax = new Vector2(12f, 3f);

                s_armorValueTmp = txtClone.GetComponent<TMP_Text>();
                if (s_armorValueTmp != null)
                {
                    s_armorValueTmp.alignment = TextAlignmentOptions.Center;
                    s_armorValueTmp.enableAutoSizing = false;
                    s_armorValueTmp.fontSize = Mathf.Max(13f, healthText.fontSize * 0.84f);
                    s_armorValueTmp.raycastTarget = false;
                    s_armorValueTmp.text = "0";
                }

                // Create companion Weapon Stack Widget right beside the Armor Shield (Sword for Warrior, Arrow / Empowered Arrow for Hunter)
                s_stackWidgetObj = new GameObject("Coarstal_HudClassStackWeapon", typeof(RectTransform), typeof(Image));
                s_stackWidgetObj.transform.SetParent(parent, false);
                s_stackWidgetRt = s_stackWidgetObj.GetComponent<RectTransform>();
                s_stackWidgetRt.localPosition = s_armorWidgetRt.localPosition + new Vector3(42f, 0f, 0f);
                s_stackWidgetRt.sizeDelta = new Vector2(38f, 40f);

                s_stackWeaponImg = s_stackWidgetObj.GetComponent<Image>();
                s_stackWeaponImg.sprite = RPIcons.GetHudWarriorSwordIcon();
                s_stackWeaponImg.preserveAspect = true;
                s_stackWeaponImg.raycastTarget = false;

                // Stack counter in the bottom-right corner of the sword/arrow icon!
                GameObject stackTxtObj = UnityEngine.Object.Instantiate(healthText.gameObject, s_stackWidgetObj.transform);
                stackTxtObj.name = "StackCountCornerText";
                RectTransform stackTxtRt = stackTxtObj.GetComponent<RectTransform>();
                stackTxtRt.localRotation = Quaternion.identity;
                stackTxtRt.localScale = Vector3.one;
                stackTxtRt.anchorMin = new Vector2(1f, 0f);
                stackTxtRt.anchorMax = new Vector2(1f, 0f);
                stackTxtRt.pivot = new Vector2(1f, 0f);
                stackTxtRt.anchoredPosition = new Vector2(6f, -6f);
                stackTxtRt.sizeDelta = new Vector2(40f, 22f);

                s_stackCountTmp = stackTxtObj.GetComponent<TMP_Text>();
                if (s_stackCountTmp != null)
                {
                    s_stackCountTmp.alignment = TextAlignmentOptions.BottomRight;
                    s_stackCountTmp.enableAutoSizing = false;
                    s_stackCountTmp.fontSize = Mathf.Max(12f, healthText.fontSize * 0.76f);
                    s_stackCountTmp.raycastTarget = false;
                    s_stackCountTmp.text = "0";
                }

                s_stackWidgetObj.SetActive(false);
                s_lastArmorShown = -9999;
                s_lastStackClass = RPClassType.None;
            }
        }
    }
}
