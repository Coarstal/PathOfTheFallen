using System;
using System.Collections.Generic;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsEnemyOverhaul
{
    /// <summary>
    /// Marker interface for all Coarstal Combat Injury debuffs.
    /// Allows external mods (such as a future Purification Staff in Coarstal's Weapon Pack)
    /// to identify and cleanse all combat injuries cleanly!
    /// </summary>
    public interface ICombatInjuryEffect
    {
        InjuryType Type { get; }
    }

    public abstract class SE_CombatInjuryBase : StatusEffect, ICombatInjuryEffect
    {
        public abstract InjuryType Type { get; }

        public const string InjuryCategoryTag = "CoarstalCombatInjury";
    }

    // ====================================================================
    // 1. Рваная рана (Кровотечение) — Laceration / Bleeding
    //    Periodic pure physical blood loss; x2 tick damage when sprinting/rolling,
    //    and heals/clots faster (+60% timer decay) when standing or walking!
    // ====================================================================
    public class SE_InjuryBleeding : SE_CombatInjuryBase
    {
        private float m_tickTimer;

        public override InjuryType Type
        {
            get { return InjuryType.Bleeding; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.BleedingDuration.Value);
            base.Setup(character);
            m_tickTimer = 0.75f;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (m_character == null || m_character.IsDead()) return;

            bool exerting = m_character.IsRunning() || m_character.InDodge();

            // Clot faster when calm (not running or dodge-rolling)
            if (!exerting)
            {
                m_time += dt * 0.55f;
            }

            m_tickTimer -= dt;
            if (m_tickTimer <= 0f)
            {
                m_tickTimer = 1.0f;

                // Only authoritative owner applies bleed HP loss to prevent double damage over network
                Player p = m_character as Player;
                if (p != null && p != Player.m_localPlayer) return;

                float baseDps = Mathf.Max(0.5f, EnemyOverhaulConfig.BleedingBaseDPS.Value);
                // Mild scaling (+1.2% of max HP per sec) so bleed stays meaningful from Meadows to Ashlands without one-shotting early game
                float scaledDps = baseDps + m_character.GetMaxHealth() * 0.012f;
                if (exerting)
                {
                    scaledDps *= Mathf.Max(1f, EnemyOverhaulConfig.BleedingSprintMultiplier.Value);
                }

                ApplyNonStaggeringBloodLoss(m_character, scaledDps, new Color(0.95f, 0.18f, 0.22f, 1f));
            }
        }

        public override string GetTooltipString()
        {
            return EnemyLocalization.Loc("$ceo_se_bleeding_tip");
        }

        internal static void ApplyNonStaggeringBloodLoss(Character target, float amount, Color popupColor)
        {
            if (target == null || target.IsDead() || amount <= 0.05f) return;

            float curHp = target.GetHealth();
            // Leave at least 1 HP from passive bleed/manaburn OR allow lethal? Real bleed/manaburn can kill if below 1.2 HP,
            // using HitData with m_staggerMultiplier = 0 so it triggers proper death/tombstone cleanly without interrupting animations!
            if (curHp <= amount + 0.5f)
            {
                HitData fatalHit = new HitData();
                fatalHit.m_damage.m_damage = amount + 1f;
                fatalHit.m_point = target.GetCenterPoint();
                fatalHit.m_staggerMultiplier = 0f;
                fatalHit.m_pushForce = 0f;
                fatalHit.m_hitType = HitData.HitType.EnemyHit;
                target.Damage(fatalHit);
            }
            else
            {
                target.SetHealth(curHp - amount);
                if (DamageText.instance != null)
                {
                    DamageText.instance.ShowText(DamageText.TextType.Normal, target.GetTopPoint(), amount, true);
                }
            }
        }
    }

    // ====================================================================
    // 2. Простреленное сухожилие (Хромота) — Pierced Tendon / Hamstrung
    //    -25% movement speed, +30% jump & dodge stamina cost (8s)
    // ====================================================================
    public class SE_InjuryHamstrung : SE_CombatInjuryBase
    {
        public override InjuryType Type
        {
            get { return InjuryType.Hamstrung; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.HamstrungDuration.Value);
            base.Setup(character);
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            float pen = Mathf.Clamp(EnemyOverhaulConfig.HamstrungSpeedPenalty.Value, 0f, 0.85f);
            speed = Mathf.Max(0.5f, speed - baseSpeed * pen);
        }

        public override void ModifyJumpStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            staminaUse += baseStaminaUse * Mathf.Max(0f, EnemyOverhaulConfig.HamstrungJumpDodgeStaminaPenalty.Value);
        }

        public override void ModifyDodgeStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            staminaUse += baseStaminaUse * Mathf.Max(0f, EnemyOverhaulConfig.HamstrungJumpDodgeStaminaPenalty.Value);
        }

        public override string GetTooltipString()
        {
            int spd = Mathf.RoundToInt(EnemyOverhaulConfig.HamstrungSpeedPenalty.Value * 100f);
            int stam = Mathf.RoundToInt(EnemyOverhaulConfig.HamstrungJumpDodgeStaminaPenalty.Value * 100f);
            return string.Format(EnemyLocalization.Loc("$ceo_se_hamstrung_tip"), spd, stam);
        }
    }

    // ====================================================================
    // 3. Раскол брони — Sundered Armor
    //    -30% Total Armor (15s)
    // ====================================================================
    public class SE_InjuryArmorSunder : SE_CombatInjuryBase
    {
        public override InjuryType Type
        {
            get { return InjuryType.ArmorSunder; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.ArmorSunderDuration.Value);
            base.Setup(character);
        }

        public override void ModifyArmorMods(ref float armor)
        {
            float pen = Mathf.Clamp(EnemyOverhaulConfig.ArmorSunderPenalty.Value, 0f, 0.90f);
            armor *= (1f - pen);
        }

        public override string GetTooltipString()
        {
            int pen = Mathf.RoundToInt(EnemyOverhaulConfig.ArmorSunderPenalty.Value * 100f);
            return string.Format(EnemyLocalization.Loc("$ceo_se_armorsunder_tip"), pen);
        }
    }

    // ====================================================================
    // 4. Выбитое дыхание (Контузия) — Winded
    //    -50% Stamina regeneration (12s)
    // ====================================================================
    public class SE_InjuryWinded : SE_CombatInjuryBase
    {
        public override InjuryType Type
        {
            get { return InjuryType.Winded; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.WindedDuration.Value);
            base.Setup(character);
        }

        public override void ModifyStaminaRegen(ref float staminaRegen)
        {
            float pen = Mathf.Clamp(EnemyOverhaulConfig.WindedStaminaRegenPenalty.Value, 0f, 0.95f);
            staminaRegen *= (1f - pen);
        }

        public override string GetTooltipString()
        {
            int pen = Mathf.RoundToInt(EnemyOverhaulConfig.WindedStaminaRegenPenalty.Value * 100f);
            return string.Format(EnemyLocalization.Loc("$ceo_se_winded_tip"), pen);
        }
    }

    // ====================================================================
    // 5. Ожог Эйтра (Манабёрн) — Eitr Burn
    //    -40% Eitr regeneration & spending Eitr burns 50% of spent Eitr from Health (10s)
    // ====================================================================
    public class SE_InjuryEitrBurn : SE_CombatInjuryBase
    {
        public override InjuryType Type
        {
            get { return InjuryType.EitrBurn; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.EitrBurnDuration.Value);
            base.Setup(character);
        }

        public override void ModifyEitrRegen(ref float eitrRegen)
        {
            float pen = Mathf.Clamp(EnemyOverhaulConfig.EitrBurnRegenPenalty.Value, 0f, 0.95f);
            eitrRegen *= (1f - pen);
        }

        public override string GetTooltipString()
        {
            int regenPen = Mathf.RoundToInt(EnemyOverhaulConfig.EitrBurnRegenPenalty.Value * 100f);
            int hpRatio = Mathf.RoundToInt(EnemyOverhaulConfig.EitrBurnHealthDamagePerEitr.Value * 100f);
            return string.Format(EnemyLocalization.Loc("$ceo_se_eitrburn_tip"), regenPen, hpRatio);
        }
    }

    // ====================================================================
    // 6. Заражение (Гниение) — Putrid Rot
    //    -50% incoming healing & passive health regeneration (15s)
    // ====================================================================
    public class SE_InjuryRot : SE_CombatInjuryBase
    {
        public override InjuryType Type
        {
            get { return InjuryType.Rot; }
        }

        public override void Setup(Character character)
        {
            m_ttl = Mathf.Max(2f, EnemyOverhaulConfig.RotDuration.Value);
            base.Setup(character);
        }

        public override void ModifyHealthRegen(ref float regenMultiplier)
        {
            float pen = Mathf.Clamp(EnemyOverhaulConfig.RotHealingPenalty.Value, 0f, 0.95f);
            regenMultiplier *= (1f - pen);
        }

        public override string GetTooltipString()
        {
            int pen = Mathf.RoundToInt(EnemyOverhaulConfig.RotHealingPenalty.Value * 100f);
            return string.Format(EnemyLocalization.Loc("$ceo_se_rot_tip"), pen);
        }
    }

    // ====================================================================
    // Manager, Procedural Crisp HUD Icons & Public Purification API
    // ====================================================================
    public static class EnemyInjuryManager
    {
        public const string EffectNameBleeding = "SE_CEO_Injury_Bleeding";
        public const string EffectNameHamstrung = "SE_CEO_Injury_Hamstrung";
        public const string EffectNameArmorSunder = "SE_CEO_Injury_ArmorSunder";
        public const string EffectNameWinded = "SE_CEO_Injury_Winded";
        public const string EffectNameEitrBurn = "SE_CEO_Injury_EitrBurn";
        public const string EffectNameRot = "SE_CEO_Injury_Rot";

        public const string RPC_CleansePlayerInjuries = "CEO_RPC_CleanseInjuries";

        private static readonly Dictionary<InjuryType, StatusEffect> s_effects = new Dictionary<InjuryType, StatusEffect>();
        private static readonly Dictionary<InjuryType, int> s_effectHashes = new Dictionary<InjuryType, int>();
        private static readonly Dictionary<InjuryType, Sprite> s_icons = new Dictionary<InjuryType, Sprite>();
        private static bool s_registered;

        public static void RegisterAll()
        {
            if (s_registered) return;

            RegisterOne<SE_InjuryBleeding>(
                InjuryType.Bleeding,
                EffectNameBleeding,
                "$ceo_se_bleeding",
                "$ceo_se_bleeding_start",
                EnemyOverhaulConfig.BleedingDuration.Value);

            RegisterOne<SE_InjuryHamstrung>(
                InjuryType.Hamstrung,
                EffectNameHamstrung,
                "$ceo_se_hamstrung",
                "$ceo_se_hamstrung_start",
                EnemyOverhaulConfig.HamstrungDuration.Value);

            RegisterOne<SE_InjuryArmorSunder>(
                InjuryType.ArmorSunder,
                EffectNameArmorSunder,
                "$ceo_se_armorsunder",
                "$ceo_se_armorsunder_start",
                EnemyOverhaulConfig.ArmorSunderDuration.Value);

            RegisterOne<SE_InjuryWinded>(
                InjuryType.Winded,
                EffectNameWinded,
                "$ceo_se_winded",
                "$ceo_se_winded_start",
                EnemyOverhaulConfig.WindedDuration.Value);

            RegisterOne<SE_InjuryEitrBurn>(
                InjuryType.EitrBurn,
                EffectNameEitrBurn,
                "$ceo_se_eitrburn",
                "$ceo_se_eitrburn_start",
                EnemyOverhaulConfig.EitrBurnDuration.Value);

            RegisterOne<SE_InjuryRot>(
                InjuryType.Rot,
                EffectNameRot,
                "$ceo_se_rot",
                "$ceo_se_rot_start",
                EnemyOverhaulConfig.RotDuration.Value);

            s_registered = true;
        }

        private static void RegisterOne<T>(InjuryType type, string effectName, string nameToken, string startMsgToken, float defaultTtl)
            where T : SE_CombatInjuryBase
        {
            Sprite icon = GetOrCreateIcon(type);
            T se = ScriptableObject.CreateInstance<T>();
            se.name = effectName;
            se.m_name = nameToken;
            se.m_icon = icon;
            se.m_flashIcon = true;
            se.m_cooldownIcon = true;
            se.m_ttl = defaultTtl;
            se.m_category = SE_CombatInjuryBase.InjuryCategoryTag;
            se.m_startMessageType = MessageHud.MessageType.TopLeft;
            se.m_startMessage = startMsgToken;

            s_effects[type] = se;
            s_effectHashes[type] = effectName.GetStableHashCode();

            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
        }

        public static bool HasInjury(Character ch, InjuryType type)
        {
            if (ch == null) return false;
            SEMan seman = ch.GetSEMan();
            if (seman == null) return false;
            int hash;
            return s_effectHashes.TryGetValue(type, out hash) && seman.HaveStatusEffect(hash);
        }

        public static void ApplyInjury(Player target, InjuryType type)
        {
            if (target == null || target.IsDead() || type == InjuryType.None) return;
            SEMan seman = target.GetSEMan();
            if (seman == null) return;

            StatusEffect se;
            if (s_effects.TryGetValue(type, out se) && se != null)
            {
                // resetTime: true refreshes duration if hit again by the same injury type
                seman.AddStatusEffect(se, true, 0, 0f);
            }
        }

        /// <summary>
        /// Public API for future Purification / Cleanse Staff: removes all Combat Injuries (and optionally vanilla debuffs) on a single character.
        /// Works seamlessly in singleplayer and co-op!
        /// </summary>
        public static int ClearAllInjuries(Character target)
        {
            if (target == null) return 0;

            ZNetView nview = target.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid() && !nview.IsOwner())
            {
                nview.InvokeRPC(RPC_CleansePlayerInjuries);
                return 1;
            }

            SEMan seman = target.GetSEMan();
            if (seman == null) return 0;

            int removed = 0;
            foreach (KeyValuePair<InjuryType, int> kv in s_effectHashes)
            {
                if (seman.HaveStatusEffect(kv.Value))
                {
                    seman.RemoveStatusEffect(kv.Value, false);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Public AoE Cleanse API ready for the upcoming Purification Staff rework in Coarstal's Weapon Pack:
        /// call `EnemyInjuryManager.ClearAllInjuriesInRadius(center, radius)` (directly or via reflection).
        /// </summary>
        public static int ClearAllInjuriesInRadius(Vector3 center, float radius)
        {
            int affectedPlayers = 0;
            float rSq = radius * radius;
            List<Player> players = Player.GetAllPlayers();
            if (players == null) return 0;

            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || p.IsDead()) continue;
                if ((p.transform.position - center).sqrMagnitude <= rSq)
                {
                    ClearAllInjuries(p);
                    affectedPlayers++;
                }
            }
            return affectedPlayers;
        }

        /// <summary>
        /// Evaluates unblocked enemy hit data & attacker species/stars to roll and apply a matching tactical injury.
        /// </summary>
        public static void TryInflictFromEnemyHit(Player victim, Character attacker, HitData hit)
        {
            if (!EnemyOverhaulConfig.EnableInjuries.Value) return;
            if (victim == null || victim != Player.m_localPlayer || victim.IsDead()) return;
            if (attacker == null || attacker.IsPlayer() || hit == null) return;

            float totalDmg = hit.GetTotalDamage();
            if (totalDmg <= 0.5f) return;

            // Compute proc chance by star level / cursed dungeon status / champion / boss
            bool isChampionOrBoss = attacker.IsBoss() || ChampionSystem.IsChampion(attacker);
            bool isCursedMob = CursedDungeonSystem.IsCursedDungeonCreature(attacker);
            int stars = Mathf.Max(0, attacker.GetLevel() - 1);

            float chance;
            if (isChampionOrBoss || stars >= 2)
            {
                chance = EnemyOverhaulConfig.InjuryChanceTwoStarOrChampion.Value;
            }
            else if (stars == 1 || isCursedMob)
            {
                chance = EnemyOverhaulConfig.InjuryChanceOneStarOrCursed.Value;
            }
            else
            {
                chance = EnemyOverhaulConfig.InjuryChanceZeroStar.Value;
            }

            if (chance <= 0f || UnityEngine.Random.value > chance) return;

            InjuryType chosen = PickInjuryTypeForAttack(attacker, hit);
            if (chosen != InjuryType.None)
            {
                ApplyInjury(victim, chosen);
            }
        }

        private static InjuryType PickInjuryTypeForAttack(Character attacker, HitData hit)
        {
            List<InjuryType> candidates = new List<InjuryType>(4);

            string prefabLower = GetCleanPrefabLower(attacker);
            HitData.DamageTypes d = hit.m_damage;

            // 1) Arcane / Shaman / Ghost / Cultist / Seeker / Wraith / Dvergr Mage -> Eitr Burn
            bool isArcaneAttacker =
                d.m_lightning > 0.5f ||
                d.m_frost > 0.5f ||
                d.m_spirit > 0.5f ||
                prefabLower.Contains("shaman") ||
                prefabLower.Contains("ghost") ||
                prefabLower.Contains("wraith") ||
                prefabLower.Contains("cultist") ||
                prefabLower.Contains("dverger") ||
                prefabLower.Contains("seeker") ||
                prefabLower.Contains("dragon") ||
                prefabLower.Contains("valkyrie");

            if (isArcaneAttacker)
            {
                candidates.Add(InjuryType.EitrBurn);
            }

            // 2) Swamp / Putrid / Poison / Undead / Ashlands -> Putrid Rot
            bool isRotAttacker =
                d.m_poison > 0.5f ||
                prefabLower.Contains("blob") ||
                prefabLower.Contains("oozer") ||
                prefabLower.Contains("draugr") ||
                prefabLower.Contains("leech") ||
                prefabLower.Contains("abomination") ||
                prefabLower.Contains("growth") ||
                prefabLower.Contains("charred") ||
                prefabLower.Contains("twitcher") ||
                prefabLower.Contains("bonemaw") ||
                prefabLower.Contains("bonemass");

            if (isRotAttacker)
            {
                candidates.Add(InjuryType.Rot);
            }

            // 3) Slash / Claws / Fangs -> Laceration (Bleeding)
            if (d.m_slash > 0.5f || (d.m_damage > 0.5f && d.m_blunt <= 0.1f && d.m_pierce <= 0.1f))
            {
                candidates.Add(InjuryType.Bleeding);
            }

            // 4) Pierce / Arrows / Spears / Stingers -> Hamstrung (and secondary chance of Bleeding)
            if (d.m_pierce > 0.5f || hit.m_ranged)
            {
                candidates.Add(InjuryType.Hamstrung);
                if (d.m_pierce > 5f && !hit.m_ranged)
                {
                    candidates.Add(InjuryType.Bleeding);
                }
            }

            // 5) Blunt / Crushing / Clubs / Golems / Trolls -> Sundered Armor or Winded
            if (d.m_blunt > 0.5f)
            {
                candidates.Add(InjuryType.ArmorSunder);
                candidates.Add(InjuryType.Winded);
            }

            // Fallback if pure fire/elemental or untyped damage
            if (candidates.Count == 0)
            {
                if (d.m_fire > 0.5f)
                {
                    candidates.Add(InjuryType.Winded);
                    candidates.Add(InjuryType.EitrBurn);
                }
                else
                {
                    candidates.Add(InjuryType.Bleeding);
                }
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static string GetCleanPrefabLower(Character ch)
        {
            if (ch == null || ch.gameObject == null) return string.Empty;
            string n = Utils.GetPrefabName(ch.gameObject);
            return n != null ? n.ToLowerInvariant() : string.Empty;
        }

        // ====================================================================
        // Crisp 64x64 Procedural Emblem Icons for HUD Buff/Debuff Bar
        // ====================================================================
        public static Sprite GetOrCreateIcon(InjuryType type)
        {
            Sprite sp;
            if (s_icons.TryGetValue(type, out sp) && sp != null)
            {
                return sp;
            }

            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.name = "CEO_InjuryIcon_" + type;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color accent = GetInjuryColor(type);
            DrawDarkBadgeBase(tex, accent);
            DrawInjuryGlyph(tex, type, accent);
            tex.Apply();

            sp = Sprite.Create(tex, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
            s_icons[type] = sp;
            return sp;
        }

        private static Color GetInjuryColor(InjuryType type)
        {
            switch (type)
            {
                case InjuryType.Bleeding: return new Color(0.95f, 0.18f, 0.22f, 1f);    // Crimson Blood
                case InjuryType.Hamstrung: return new Color(0.96f, 0.56f, 0.20f, 1f);   // Amber Tendon
                case InjuryType.ArmorSunder: return new Color(0.76f, 0.84f, 0.94f, 1f); // Cracked Steel
                case InjuryType.Winded: return new Color(0.45f, 0.82f, 0.98f, 1f);      // Breath Cyan
                case InjuryType.EitrBurn: return new Color(0.78f, 0.30f, 1.00f, 1f);    // Arcane Violet
                case InjuryType.Rot: return new Color(0.48f, 0.88f, 0.26f, 1f);         // Necrotic Green
                default: return Color.white;
            }
        }

        private static void DrawDarkBadgeBase(Texture2D tex, Color rimColor)
        {
            Vector2 c = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    if (dist > 30.2f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist > 27.2f)
                    {
                        float edgeAlpha = Mathf.Clamp01((30.2f - dist) / 1.4f);
                        Color border = Color.Lerp(new Color(0.12f, 0.08f, 0.10f, 1f), rimColor, 0.82f);
                        border.a = edgeAlpha;
                        tex.SetPixel(x, y, border);
                    }
                    else
                    {
                        float t = dist / 27.2f;
                        Color bg = Color.Lerp(new Color(0.18f, 0.11f, 0.14f, 0.94f), new Color(0.06f, 0.04f, 0.06f, 0.96f), t);
                        bg = Color.Lerp(bg, rimColor, 0.12f * (1f - t));
                        tex.SetPixel(x, y, bg);
                    }
                }
            }
        }

        private static void DrawInjuryGlyph(Texture2D tex, InjuryType type, Color col)
        {
            Color bright = Color.Lerp(col, Color.white, 0.45f);
            switch (type)
            {
                case InjuryType.Bleeding:
                    // Three diagonal claw slashes + blood droplet
                    DrawLine(tex, new Vector2(18, 44), new Vector2(36, 18), 2.5f, col);
                    DrawLine(tex, new Vector2(26, 48), new Vector2(45, 20), 3.0f, bright);
                    DrawLine(tex, new Vector2(36, 46), new Vector2(50, 24), 2.3f, col);
                    DrawCircle(tex, new Vector2(22, 20), 4.5f, bright);
                    DrawLine(tex, new Vector2(22, 20), new Vector2(22, 29), 2.4f, bright);
                    break;

                case InjuryType.Hamstrung:
                    // Boot/leg silhouette pierced diagonally by an arrow shaft
                    DrawLine(tex, new Vector2(28, 48), new Vector2(25, 23), 4.2f, new Color(0.72f, 0.62f, 0.52f, 1f));
                    DrawLine(tex, new Vector2(25, 23), new Vector2(38, 19), 4.0f, new Color(0.72f, 0.62f, 0.52f, 1f));
                    DrawLine(tex, new Vector2(14, 42), new Vector2(49, 26), 2.4f, bright);
                    DrawLine(tex, new Vector2(49, 26), new Vector2(43, 31), 2.2f, bright);
                    DrawLine(tex, new Vector2(49, 26), new Vector2(44, 23), 2.2f, bright);
                    break;

                case InjuryType.ArmorSunder:
                    // Shield split down the middle by a jagged crack
                    DrawLine(tex, new Vector2(20, 46), new Vector2(44, 46), 3.0f, col);
                    DrawLine(tex, new Vector2(20, 46), new Vector2(22, 27), 3.0f, col);
                    DrawLine(tex, new Vector2(44, 46), new Vector2(42, 27), 3.0f, col);
                    DrawLine(tex, new Vector2(22, 27), new Vector2(32, 15), 3.0f, col);
                    DrawLine(tex, new Vector2(42, 27), new Vector2(32, 15), 3.0f, col);
                    // Crimson/bright fracture crack
                    Color crack = new Color(1.0f, 0.35f, 0.28f, 1f);
                    DrawLine(tex, new Vector2(33, 49), new Vector2(28, 36), 2.6f, crack);
                    DrawLine(tex, new Vector2(28, 36), new Vector2(36, 28), 2.6f, crack);
                    DrawLine(tex, new Vector2(36, 28), new Vector2(30, 14), 2.6f, crack);
                    break;

                case InjuryType.Winded:
                    // Pair of lungs / swirling knocked-out wind gusts
                    DrawCircle(tex, new Vector2(24, 31), 7.5f, col);
                    DrawCircle(tex, new Vector2(40, 31), 7.5f, col);
                    DrawLine(tex, new Vector2(32, 47), new Vector2(32, 33), 2.8f, bright);
                    DrawLine(tex, new Vector2(18, 22), new Vector2(46, 42), 2.3f, new Color(1f, 0.42f, 0.42f, 1f));
                    break;

                case InjuryType.EitrBurn:
                    // Arcane crystal/flame crackling with violet-crimson sparks
                    DrawLine(tex, new Vector2(32, 50), new Vector2(21, 30), 2.8f, bright);
                    DrawLine(tex, new Vector2(32, 50), new Vector2(43, 30), 2.8f, bright);
                    DrawLine(tex, new Vector2(21, 30), new Vector2(32, 14), 2.8f, col);
                    DrawLine(tex, new Vector2(43, 30), new Vector2(32, 14), 2.8f, col);
                    DrawCircle(tex, new Vector2(32, 31), 5.0f, new Color(1f, 0.35f, 0.78f, 1f));
                    break;

                case InjuryType.Rot:
                    // Bio-skull / three festering necrotic spores
                    DrawCircle(tex, new Vector2(32, 39), 5.5f, bright);
                    DrawCircle(tex, new Vector2(23, 24), 5.5f, col);
                    DrawCircle(tex, new Vector2(41, 24), 5.5f, col);
                    DrawCircle(tex, new Vector2(32, 30), 3.2f, new Color(0.15f, 0.25f, 0.10f, 1f));
                    break;
            }
        }

        private static void DrawCircle(Texture2D tex, Vector2 center, float radius, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 2f));
            int maxX = Mathf.Min(63, Mathf.CeilToInt(center.x + radius + 2f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 2f));
            int maxY = Mathf.Min(63, Mathf.CeilToInt(center.y + radius + 2f));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    if (d <= radius + 1.0f)
                    {
                        float a = Mathf.Clamp01(radius + 0.8f - d);
                        BlendPixel(tex, x, y, new Color(color.r, color.g, color.b, color.a * a));
                    }
                }
            }
        }

        private static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float thickness, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - thickness - 2f));
            int maxX = Mathf.Min(63, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + thickness + 2f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - thickness - 2f));
            int maxY = Mathf.Min(63, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + thickness + 2f));

            Vector2 ab = b - a;
            float abLenSq = Mathf.Max(0.0001f, ab.sqrMagnitude);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
                    Vector2 proj = a + ab * t;
                    float d = Vector2.Distance(p, proj);
                    if (d <= thickness + 0.9f)
                    {
                        float alpha = Mathf.Clamp01(thickness + 0.6f - d);
                        BlendPixel(tex, x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                    }
                }
            }
        }

        private static void BlendPixel(Texture2D tex, int x, int y, Color src)
        {
            Color dst = tex.GetPixel(x, y);
            float outA = src.a + dst.a * (1f - src.a);
            if (outA <= 0.001f) return;
            Color outC = (src * src.a + dst * dst.a * (1f - src.a)) / outA;
            outC.a = outA;
            tex.SetPixel(x, y, outC);
        }
    }
}
