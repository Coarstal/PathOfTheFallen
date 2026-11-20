using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsRPClasses
{
    public interface IRPPassiveSkillEffect
    {
    }

    /// <summary>
    /// Hidden background StatusEffect for each RP Class (Warrior, Knight, Thief, Hunter, Sorcerer, Beggar).
    /// Handles base class modifiers silently (m_hidden = true) so ONLY the class Passive Skill is displayed!
    /// </summary>
    public class SE_RPClass : StatusEffect
    {
        public RPClassType ClassType = RPClassType.None;

        private static readonly FieldInfo s_helmetField = typeof(Humanoid).GetField("m_helmetItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_chestField = typeof(Humanoid).GetField("m_chestItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_legField = typeof(Humanoid).GetField("m_legItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_leftItemField = typeof(Humanoid).GetField("m_leftItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_curAttackField = typeof(Humanoid).GetField("m_currentAttack", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static bool HasFullThreePieceArmor(Character ch)
        {
            Humanoid hum = ch as Humanoid;
            if (hum == null) return false;
            object helm = s_helmetField != null ? s_helmetField.GetValue(hum) : null;
            object chest = s_chestField != null ? s_chestField.GetValue(hum) : null;
            object legs = s_legField != null ? s_legField.GetValue(hum) : null;
            return helm != null && chest != null && legs != null;
        }

        /// <summary>
        /// Checks if the character is wearing any Heavy Armor piece.
        /// In Valheim, Heavy Chest/Legs apply m_movementModifier <= -0.05f (-5% speed),
        /// and Heavy Helmets have non-positive Eitr regen with metal/heavy prefab naming (Bronze/Iron/Drake/Wolf/Padded/Carapace/Flametal).
        /// </summary>
        public static bool IsWearingHeavyArmor(Character ch)
        {
            Humanoid hum = ch as Humanoid;
            if (hum == null) return false;

            ItemDrop.ItemData chest = s_chestField != null ? s_chestField.GetValue(hum) as ItemDrop.ItemData : null;
            ItemDrop.ItemData legs = s_legField != null ? s_legField.GetValue(hum) as ItemDrop.ItemData : null;
            ItemDrop.ItemData helm = s_helmetField != null ? s_helmetField.GetValue(hum) as ItemDrop.ItemData : null;

            if (IsItemHeavyArmor(chest) || IsItemHeavyArmor(legs) || IsItemHeavyHelmet(helm))
            {
                return true;
            }
            return false;
        }

        private static bool IsItemHeavyArmor(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_shared.m_eitrRegenModifier > 0f) return false; // Mage robes never count as heavy
            // All vanilla & modded heavy chest/leg armors carry a -5% (or stronger) movement speed penalty
            if (item.m_shared.m_movementModifier <= -0.04f) return true;
            return false;
        }

        private static bool IsItemHeavyHelmet(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_shared.m_eitrRegenModifier > 0f) return false;
            if (item.m_shared.m_movementModifier <= -0.04f) return true;

            string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
            if (!string.IsNullOrEmpty(prefabName))
            {
                string low = prefabName.ToLowerInvariant();
                if (low.Contains("bronze") || low.Contains("iron") || low.Contains("drake") ||
                    low.Contains("padded") || low.Contains("carapace") || low.Contains("flametal"))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasShieldEquipped(Character ch)
        {
            Humanoid hum = ch as Humanoid;
            if (hum == null || s_leftItemField == null) return false;
            ItemDrop.ItemData leftItem = s_leftItemField.GetValue(hum) as ItemDrop.ItemData;
            return leftItem != null && leftItem.m_shared != null && leftItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield;
        }

        public static bool IsRaisingShield(Character ch)
        {
            return ch != null && ch.IsBlocking() && HasShieldEquipped(ch);
        }

        public static bool IsProjectileAttack(Character ch)
        {
            Humanoid hum = ch as Humanoid;
            if (hum == null || s_curAttackField == null) return false;
            Attack atk = s_curAttackField.GetValue(hum) as Attack;
            if (atk == null) return false;
            return atk.m_attackType == Attack.AttackType.Projectile;
        }

        public static bool IsCombatMeleeSkill(Skills.SkillType skill)
        {
            return skill == Skills.SkillType.Swords
                || skill == Skills.SkillType.Knives
                || skill == Skills.SkillType.Clubs
                || skill == Skills.SkillType.Polearms
                || skill == Skills.SkillType.Spears
                || skill == Skills.SkillType.Axes
                || skill == Skills.SkillType.Unarmed;
        }

        public static bool IsRangedSkill(Skills.SkillType skill)
        {
            return skill == Skills.SkillType.Bows
                || skill == Skills.SkillType.Crossbows;
        }

        public static bool IsMagicSkill(Skills.SkillType skill)
        {
            return skill == Skills.SkillType.ElementalMagic
                || skill == Skills.SkillType.BloodMagic;
        }

        public static void ScaleCombatDamageOnly(ref HitData.DamageTypes dmg, float mult)
        {
            // Never penalize or inflate tree-chopping (m_chop) or mining (m_pickaxe) terrain damage!
            dmg.m_damage *= mult;
            dmg.m_blunt *= mult;
            dmg.m_slash *= mult;
            dmg.m_pierce *= mult;
            dmg.m_fire *= mult;
            dmg.m_frost *= mult;
            dmg.m_lightning *= mult;
            dmg.m_poison *= mult;
            dmg.m_spirit *= mult;
        }

        public override bool IsDone()
        {
            return false;
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (skill == Skills.SkillType.WoodCutting || skill == Skills.SkillType.Pickaxes)
            {
                return;
            }

            switch (ClassType)
            {
                case RPClassType.Warrior:
                {
                    if (IsCombatMeleeSkill(skill) && !IsProjectileAttack(m_character))
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, 1f + RPClassConfig.WarriorMeleeDamageBonus.Value);
                    }
                    else if (IsRangedSkill(skill))
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, Mathf.Max(0.05f, 1f - RPClassConfig.WarriorRangedDamagePenalty.Value));
                    }
                    break;
                }

                case RPClassType.Thief:
                {
                    if (skill == Skills.SkillType.Knives)
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, 1f + RPClassConfig.ThiefKnifeDamageBonus.Value);
                    }
                    else if (IsRangedSkill(skill))
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, Mathf.Max(0.05f, 1f - RPClassConfig.ThiefRangedDamagePenalty.Value));
                    }
                    break;
                }

                case RPClassType.Hunter:
                {
                    bool isThrownSpear = (skill == Skills.SkillType.Spears && IsProjectileAttack(m_character));
                    if (IsRangedSkill(skill) || isThrownSpear)
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, 1f + RPClassConfig.HunterRangedDamageBonus.Value);
                    }
                    else if (IsCombatMeleeSkill(skill) && skill != Skills.SkillType.Knives && skill != Skills.SkillType.Spears)
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, Mathf.Max(0.05f, 1f - RPClassConfig.HunterHeavyMeleePenalty.Value));
                    }
                    break;
                }

                case RPClassType.Sorcerer:
                {
                    if (IsCombatMeleeSkill(skill) || IsRangedSkill(skill))
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, Mathf.Max(0.05f, 1f - RPClassConfig.SorcererPhysicalWeaponPenalty.Value));
                    }
                    break;
                }

                case RPClassType.Beggar:
                {
                    float bonus = RPClassConfig.BeggarAllDamageBonus.Value;
                    if (bonus > 0f)
                    {
                        ScaleCombatDamageOnly(ref hitData.m_damage, 1f + bonus);
                    }
                    break;
                }
            }
        }

        public override void ModifyArmorMods(ref float armor)
        {
            switch (ClassType)
            {
                case RPClassType.Thief:
                    armor *= Mathf.Max(0f, 1f - RPClassConfig.ThiefArmorPenalty.Value);
                    break;

                case RPClassType.Sorcerer:
                    armor *= Mathf.Max(0f, 1f - RPClassConfig.SorcererArmorPenalty.Value);
                    break;
            }
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (ClassType == RPClassType.Knight)
            {
                float pen = RPClassConfig.KnightMoveSpeedPenalty.Value;
                if (character != null && character.IsSwimming())
                {
                    speed -= baseSpeed * pen * 0.5f;
                }
                else
                {
                    speed -= baseSpeed * pen;
                }
                if (speed < 0f) speed = 0f;
            }
            else if (ClassType == RPClassType.Beggar)
            {
                float bonus = RPClassConfig.BeggarMoveSpeedBonus.Value;
                if (character != null && character.IsSwimming())
                {
                    speed += baseSpeed * bonus * 0.5f;
                }
                else
                {
                    speed += baseSpeed * bonus;
                }
            }
        }

        public override void ModifyDodgeStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (ClassType == RPClassType.Knight)
            {
                staminaUse += baseStaminaUse * RPClassConfig.KnightDodgeStaminaPenalty.Value;
            }
            else if (ClassType == RPClassType.Warrior)
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.WarriorDodgeStaminaReduction.Value);
            }
            else if (ClassType == RPClassType.Thief)
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefDodgeStaminaReduction.Value);
            }
        }

        public override void ModifySneakStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (ClassType == RPClassType.Thief)
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefSneakStaminaReduction.Value);
            }
        }

        public override void ModifyRaiseSkill(Skills.SkillType skill, ref float value)
        {
            if (ClassType == RPClassType.Sorcerer)
            {
                value += RPClassConfig.SorcererSkillXPBonus.Value;
            }
        }

        public override void ModifySkillLevel(Skills.SkillType skill, ref float level)
        {
            if (skill == Skills.SkillType.Sneak)
            {
                if (ClassType == RPClassType.Knight)
                {
                    level = Mathf.Max(0f, level - RPClassConfig.KnightSneakPenalty.Value);
                }
                else if (ClassType == RPClassType.Hunter)
                {
                    level += RPClassConfig.HunterSneakBonus.Value;
                }
                else if (ClassType == RPClassType.Beggar)
                {
                    level += RPClassConfig.BeggarSneakBonus.Value;
                }
            }
        }

        public override void ModifyStealth(float baseStealth, ref float stealth)
        {
            if (ClassType == RPClassType.Knight)
            {
                stealth += 0.25f;
            }
            else if (ClassType == RPClassType.Hunter)
            {
                stealth -= 0.08f;
            }
            else if (ClassType == RPClassType.Beggar)
            {
                stealth -= 0.04f;
            }
        }

        public override void ModifyNoise(float baseNoise, ref float noise)
        {
            if (ClassType == RPClassType.Knight)
            {
                noise += baseNoise * 0.25f;
            }
        }

        public override void ModifyEitrRegen(ref float eitrRegen)
        {
            if (ClassType == RPClassType.Sorcerer)
            {
                if (IsWearingHeavyArmor(m_character))
                {
                    eitrRegen *= Mathf.Max(0f, 1f - RPClassConfig.SorcererHeavyArmorEitrPenalty.Value);
                }
                else
                {
                    eitrRegen += RPClassConfig.SorcererEitrRegenBonus.Value;
                }
            }
            else if (ClassType == RPClassType.Warrior)
            {
                eitrRegen *= Mathf.Max(0f, 1f - RPClassConfig.WarriorEitrRegenPenalty.Value);
            }
            else if (ClassType == RPClassType.Thief)
            {
                eitrRegen *= Mathf.Max(0f, 1f - RPClassConfig.ThiefEitrRegenPenalty.Value);
            }
            else if (ClassType == RPClassType.Hunter)
            {
                eitrRegen *= Mathf.Max(0f, 1f - RPClassConfig.HunterEitrRegenPenalty.Value);
            }
            else if (ClassType == RPClassType.Knight)
            {
                eitrRegen *= Mathf.Max(0f, 1f - RPClassConfig.KnightEitrRegenPenalty.Value);
            }
        }

        public override void ModifyMaxCarryWeight(float baseLimit, ref float limit)
        {
            if (ClassType == RPClassType.Beggar)
            {
                limit += RPClassConfig.BeggarCarryWeightBonus.Value;
            }
            else if (ClassType == RPClassType.Sorcerer)
            {
                limit = Mathf.Max(50f, limit - RPClassConfig.SorcererCarryWeightPenalty.Value);
            }
        }
    }

    // ========================================================================
    // 1. WARRIOR PASSIVE: "Храброе сердце"
    //    (+20% damage below 50% HP, +5% move speed, Bloody Onslaught stacking melee bonus)
    // ========================================================================
    public class SE_RPBraveHeart : StatusEffect, IRPPassiveSkillEffect
    {
        private static int s_onslaughtStacks;
        private static float s_onslaughtExpireTime;
        private static float s_lastStackGrantTime;

        public static void RegisterMeleeHit()
        {
            float now = Time.time;
            // Prevent a single multi-target cleave swing from granting multiple stacks in the same frame!
            if (now - s_lastStackGrantTime < 0.08f) return;
            s_lastStackGrantTime = now;

            if (now > s_onslaughtExpireTime)
            {
                s_onslaughtStacks = 0;
            }

            int maxStacks = Mathf.Max(1, RPClassConfig.WarriorBloodlustMaxStacks.Value);
            s_onslaughtStacks = Mathf.Min(maxStacks, s_onslaughtStacks + 1);
            s_onslaughtExpireTime = now + Mathf.Max(0.5f, RPClassConfig.WarriorBloodlustDuration.Value);
        }

        public static int GetActiveStacks()
        {
            if (Time.time > s_onslaughtExpireTime)
            {
                s_onslaughtStacks = 0;
            }
            return s_onslaughtStacks;
        }

        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildBraveHeartTooltip();
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (skill == Skills.SkillType.WoodCutting || skill == Skills.SkillType.Pickaxes) return;

            if (m_character != null && m_character.GetHealthPercentage() < RPClassConfig.WarriorBraveHeartHPThreshold.Value)
            {
                SE_RPClass.ScaleCombatDamageOnly(ref hitData.m_damage, 1f + RPClassConfig.WarriorBraveHeartDamageBonus.Value);
            }
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            float bonus = RPClassConfig.WarriorBraveHeartMoveSpeedBonus.Value;
            if (bonus <= 0f) return;
            if (character != null && character.IsSwimming())
            {
                speed += baseSpeed * bonus * 0.5f;
            }
            else
            {
                speed += baseSpeed * bonus;
            }
        }
    }

    // ========================================================================
    // 2. KNIGHT PASSIVE: "Непоколебимость" (+15 base armor, +10% shield speed, +25% armor while raising shield)
    // ========================================================================
    public class SE_RPUnwavering : StatusEffect, IRPPassiveSkillEffect
    {
        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildUnwaveringTooltip();
        }

        public override void ModifyArmorMods(ref float armor)
        {
            armor += RPClassConfig.KnightFlatBaseArmor.Value;
            if (SE_RPClass.IsRaisingShield(m_character))
            {
                armor *= (1f + RPClassConfig.KnightFullArmorBonus.Value);
            }
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (character != null && SE_RPClass.HasShieldEquipped(character))
            {
                float bonus = RPClassConfig.KnightShieldSpeedBonus.Value;
                if (character.IsSwimming())
                {
                    speed += baseSpeed * bonus * 0.5f;
                }
                else
                {
                    speed += baseSpeed * bonus;
                }
            }
        }
    }

    // ========================================================================
    // 3. THIEF PASSIVE: "Восхождение в тени" (+20 Sneak, +50% Stealth Backstab)
    // ========================================================================
    public class SE_RPShadowAscension : StatusEffect, IRPPassiveSkillEffect
    {
        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildShadowAscensionTooltip();
        }

        public override void ModifySkillLevel(Skills.SkillType skill, ref float level)
        {
            if (skill == Skills.SkillType.Sneak)
            {
                level += RPClassConfig.ThiefSneakBonus.Value;
            }
        }

        public override void ModifyStealth(float baseStealth, ref float stealth)
        {
            stealth -= 0.15f;
        }

        public override void ModifyNoise(float baseNoise, ref float noise)
        {
            noise = Mathf.Max(0f, noise - baseNoise * 0.20f);
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            if (!EnvMan.IsNight()) return;
            float bonus = RPClassConfig.ThiefNightMoveSpeedBonus.Value;
            if (bonus <= 0f) return;

            if (character != null && character.IsSwimming())
            {
                speed += baseSpeed * bonus * 0.5f;
            }
            else
            {
                speed += baseSpeed * bonus;
            }
        }

        public override void ModifyRunStaminaDrain(float baseDrain, ref float drain, Vector3 dir)
        {
            if (EnvMan.IsNight())
            {
                drain = Mathf.Max(0f, drain - baseDrain * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifyJumpStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifyAttackStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifyDodgeStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifySneakStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifySwimStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifyBlockStaminaUsage(float baseStaminaUse, ref float staminaUse)
        {
            if (EnvMan.IsNight())
            {
                staminaUse = Mathf.Max(0f, staminaUse - baseStaminaUse * RPClassConfig.ThiefNightStaminaReduction.Value);
            }
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (skill == Skills.SkillType.WoodCutting || skill == Skills.SkillType.Pickaxes) return;

            if (EnvMan.IsNight())
            {
                float nightBonus = RPClassConfig.ThiefNightDamageBonus.Value;
                if (nightBonus > 0f)
                {
                    SE_RPClass.ScaleCombatDamageOnly(ref hitData.m_damage, 1f + nightBonus);
                }
            }

            if (hitData.m_backstabBonus > 1f)
            {
                hitData.m_backstabBonus *= (1f + RPClassConfig.ThiefStealthBackstabBonus.Value);
            }
        }
    }

    // ========================================================================
    // 4. HUNTER PASSIVE: "Соколиная сноровка"
    //    (+15% Bow draw & Crossbow reload, +5% Move Speed, +50% Sneak Attack Multiplier)
    // ========================================================================
    public class SE_RPFalconEye : StatusEffect, IRPPassiveSkillEffect
    {
        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildFalconEyeTooltip();
        }

        public override void ModifySpeed(float baseSpeed, ref float speed, Character character, Vector3 dir)
        {
            float bonus = RPClassConfig.HunterFalconMoveSpeedBonus.Value;
            if (bonus <= 0f) return;
            if (character != null && character.IsSwimming())
            {
                speed += baseSpeed * bonus * 0.5f;
            }
            else
            {
                speed += baseSpeed * bonus;
            }
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (hitData.m_backstabBonus > 1f)
            {
                hitData.m_backstabBonus *= (1f + RPClassConfig.HunterStealthBackstabBonus.Value);
            }
        }
    }

    // ========================================================================
    // 5. SORCERER PASSIVE: "Архимаг" (+15% Magic Weapon Damage, +10% Status Damage)
    // ========================================================================
    public class SE_RPArchmage : StatusEffect, IRPPassiveSkillEffect
    {
        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildArchmageTooltip();
        }

        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            if (SE_RPClass.IsMagicSkill(skill))
            {
                SE_RPClass.ScaleCombatDamageOnly(ref hitData.m_damage, 1f + RPClassConfig.SorcererMagicDamageBonus.Value);
            }
        }
    }

    // ========================================================================
    // 6. BEGGAR PASSIVE: "Бездарность" (-20% Skill XP gain)
    // ========================================================================
    public class SE_RPIneptitude : StatusEffect, IRPPassiveSkillEffect
    {
        public override bool IsDone() { return false; }

        public override string GetIconText()
        {
            return "";
        }

        public override string GetTooltipString()
        {
            return RPClassConfig.BuildIneptitudeTooltip();
        }

        public override void ModifyRaiseSkill(Skills.SkillType skill, ref float value)
        {
            value = Mathf.Max(0.05f, value - RPClassConfig.BeggarSkillXPPenalty.Value);
        }
    }

    public static class RPStatusEffectRegistry
    {
        public const string BraveHeartEffectName = "SE_RP_BraveHeart";
        public const string UnwaveringEffectName = "SE_RP_Unwavering";
        public const string ShadowAscensionEffectName = "SE_RP_ShadowAscension";
        public const string FalconEyeEffectName = "SE_RP_FalconEye";
        public const string ArchmageEffectName = "SE_RP_Archmage";
        public const string IneptitudeEffectName = "SE_RP_Ineptitude";

        private static readonly Dictionary<RPClassType, SE_RPClass> s_classEffects = new Dictionary<RPClassType, SE_RPClass>();
        private static readonly Dictionary<RPClassType, StatusEffect> s_passiveEffects = new Dictionary<RPClassType, StatusEffect>();
        private static bool s_registered;

        public static string GetEffectName(RPClassType cls)
        {
            return "SE_RPClass_" + cls;
        }

        public static string GetPassiveEffectName(RPClassType cls)
        {
            switch (cls)
            {
                case RPClassType.Warrior: return BraveHeartEffectName;
                case RPClassType.Knight: return UnwaveringEffectName;
                case RPClassType.Thief: return ShadowAscensionEffectName;
                case RPClassType.Hunter: return FalconEyeEffectName;
                case RPClassType.Sorcerer: return ArchmageEffectName;
                case RPClassType.Beggar: return IneptitudeEffectName;
                default: return "";
            }
        }

        public static void RegisterAll()
        {
            if (s_registered) return;

            RPClassType[] all = new RPClassType[]
            {
                RPClassType.Warrior,
                RPClassType.Knight,
                RPClassType.Thief,
                RPClassType.Hunter,
                RPClassType.Sorcerer,
                RPClassType.Beggar
            };

            // 1) Hidden background class status effects (m_hidden = true so class name is NOT shown in buffs!)
            for (int i = 0; i < all.Length; i++)
            {
                RPClassType cls = all[i];
                SE_RPClass se = ScriptableObject.CreateInstance<SE_RPClass>();
                se.name = GetEffectName(cls);
                se.ClassType = cls;
                se.m_name = RPClassConfig.GetClassToken(cls);
                se.m_tooltip = "";
                se.m_icon = null;
                se.m_hidden = true;
                se.m_ttl = 0f;
                se.m_category = "RPClassHidden";

                s_classEffects[cls] = se;
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            }

            // 2) Visible Passive Skills for each of the 6 classes (rendered in separate bottom passive row!)
            RegisterPassive<SE_RPBraveHeart>(RPClassType.Warrior, BraveHeartEffectName, "$rp_se_braveheart", RPIcons.GetBraveHeartIcon());
            RegisterPassive<SE_RPUnwavering>(RPClassType.Knight, UnwaveringEffectName, "$rp_se_unwavering", RPIcons.GetUnwaveringIcon());
            RegisterPassive<SE_RPShadowAscension>(RPClassType.Thief, ShadowAscensionEffectName, "$rp_se_shadow_ascension", RPIcons.GetShadowAscensionIcon());
            RegisterPassive<SE_RPFalconEye>(RPClassType.Hunter, FalconEyeEffectName, "$rp_se_falcon_eye", RPIcons.GetFalconEyeIcon());
            RegisterPassive<SE_RPArchmage>(RPClassType.Sorcerer, ArchmageEffectName, "$rp_se_archmage", RPIcons.GetArchmageIcon());
            RegisterPassive<SE_RPIneptitude>(RPClassType.Beggar, IneptitudeEffectName, "$rp_se_ineptitude", RPIcons.GetIneptitudeIcon());

            s_registered = true;
        }

        private static void RegisterPassive<T>(RPClassType cls, string effectName, string nameToken, Sprite icon) where T : StatusEffect
        {
            T se = ScriptableObject.CreateInstance<T>();
            se.name = effectName;
            se.m_name = nameToken;
            se.m_tooltip = "";
            se.m_icon = icon;
            se.m_hidden = false;
            se.m_ttl = 0f;
            se.m_category = "RPClassPassive";
            s_passiveEffects[cls] = se;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
        }

        public static SE_RPClass GetClassEffect(RPClassType cls)
        {
            SE_RPClass se;
            s_classEffects.TryGetValue(cls, out se);
            return se;
        }

        public static StatusEffect GetPassiveEffect(RPClassType cls)
        {
            StatusEffect se;
            s_passiveEffects.TryGetValue(cls, out se);
            return se;
        }
    }
}
