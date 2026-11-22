using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace CoarstalsRPClasses
{
    public enum RPClassType
    {
        None = 0,
        Warrior = 1,
        Knight = 2,
        Thief = 3,
        Hunter = 4,
        Sorcerer = 5,
        Beggar = 6
    }

    public static class RPClassConfig
    {
        // ====================================================================
        // 0. General & Hildir Respec
        // ====================================================================
        public static ConfigEntry<int> HildirScrollPrice;
        public static ConfigEntry<bool> ShowClassOverheadTag;
        public static ConfigEntry<bool> ShowHudArmorShield;
        public static ConfigEntry<bool> InvulnerableDuringInitialChoice;

        // ====================================================================
        // 1. Warrior (Воин) + Brave Heart (Храброе сердце)
        // ====================================================================
        public static ConfigEntry<float> WarriorBaseHP;
        public static ConfigEntry<float> WarriorMeleeDamageBonus;
        public static ConfigEntry<float> WarriorFullHPEnemyDamageBonus;
        public static ConfigEntry<float> WarriorDodgeStaminaReduction;
        public static ConfigEntry<float> WarriorBraveHeartDamageBonus;
        public static ConfigEntry<float> WarriorBraveHeartMoveSpeedBonus;
        public static ConfigEntry<float> WarriorBraveHeartHPThreshold;
        public static ConfigEntry<float> WarriorBloodlustAttackSpeedPerStack;
        public static ConfigEntry<float> WarriorBloodlustLifestealPerStack;
        public static ConfigEntry<int> WarriorBloodlustMaxStacks;
        public static ConfigEntry<float> WarriorBloodlustDuration;
        public static ConfigEntry<float> WarriorBlockPowerPenalty;
        public static ConfigEntry<float> WarriorRangedDamagePenalty;
        public static ConfigEntry<float> WarriorEitrRegenPenalty;

        // ====================================================================
        // 2. Knight (Рыцарь) + Unwavering (Непоколебимость)
        // ====================================================================
        public static ConfigEntry<float> KnightBaseHP;
        public static ConfigEntry<float> KnightBaseStamina;
        public static ConfigEntry<float> KnightShieldBlockBonus;
        public static ConfigEntry<float> KnightParriedEnemyDamageBonus;
        public static ConfigEntry<float> KnightFlatBaseArmor;
        public static ConfigEntry<float> KnightFullArmorBonus;
        public static ConfigEntry<float> KnightShieldSpeedBonus;
        public static ConfigEntry<float> KnightMoveSpeedPenalty;
        public static ConfigEntry<float> KnightDodgeStaminaPenalty;
        public static ConfigEntry<float> KnightSneakPenalty;
        public static ConfigEntry<float> KnightEitrRegenPenalty;

        // ====================================================================
        // 3. Thief (Вор) + Shadow Ascension (Восхождение в тени)
        // ====================================================================
        public static ConfigEntry<float> ThiefBaseStamina;
        public static ConfigEntry<float> ThiefKnifeDamageBonus;
        public static ConfigEntry<float> ThiefPositionalBackstabBonus;
        public static ConfigEntry<float> ThiefSneakStaminaReduction;
        public static ConfigEntry<float> ThiefDodgeStaminaReduction;
        public static ConfigEntry<float> ThiefSneakBonus;
        public static ConfigEntry<float> ThiefStealthBackstabBonus;
        public static ConfigEntry<float> ThiefNightDamageBonus;
        public static ConfigEntry<float> ThiefNightStaminaReduction;
        public static ConfigEntry<float> ThiefNightMoveSpeedBonus;
        public static ConfigEntry<float> ThiefArmorPenalty;
        public static ConfigEntry<float> ThiefBlockPowerPenalty;
        public static ConfigEntry<float> ThiefRangedDamagePenalty;
        public static ConfigEntry<float> ThiefEitrRegenPenalty;

        // ====================================================================
        // 4. Hunter (Охотник) + Falcon's Swiftness (Соколиная сноровка)
        // ====================================================================
        public static ConfigEntry<float> HunterBaseStamina;
        public static ConfigEntry<float> HunterRangedDamageBonus;
        public static ConfigEntry<float> HunterSneakBonus;
        public static ConfigEntry<float> HunterBowDrawStaminaReduction;
        public static ConfigEntry<float> HunterBowCrossbowSpeedBonus;
        public static ConfigEntry<float> HunterFalconMoveSpeedBonus;
        public static ConfigEntry<float> HunterStealthBackstabBonus;
        public static ConfigEntry<int> HunterComboHitsRequired;
        public static ConfigEntry<float> HunterComboDamageMultiplier;
        public static ConfigEntry<float> HunterHeavyMeleePenalty;
        public static ConfigEntry<float> HunterBlockPowerPenalty;
        public static ConfigEntry<float> HunterEitrRegenPenalty;

        // ====================================================================
        // 5. Sorcerer (Колдун) + Archmage (Архимаг)
        // ====================================================================
        public static ConfigEntry<float> SorcererBaseEitr;
        public static ConfigEntry<float> SorcererEitrRegenBonus;
        public static ConfigEntry<float> SorcererSkillXPBonus;
        public static ConfigEntry<float> SorcererGuardianDurationBonus;
        public static ConfigEntry<float> SorcererGuardianCooldownReduction;
        public static ConfigEntry<int> SorcererFreeCastEveryN;
        public static ConfigEntry<float> SorcererPhysicalWeaponPenalty;
        public static ConfigEntry<float> SorcererArmorPenalty;
        public static ConfigEntry<float> SorcererCarryWeightPenalty;
        public static ConfigEntry<float> SorcererHeavyArmorEitrPenalty;

        // ====================================================================
        // 6. Beggar (Нищий) + Ineptitude (Бездарность)
        // ====================================================================
        public static ConfigEntry<float> BeggarCarryWeightBonus;
        public static ConfigEntry<float> BeggarBaseStatsBonus;
        public static ConfigEntry<float> BeggarAllDamageBonus;
        public static ConfigEntry<float> BeggarMoveSpeedBonus;
        public static ConfigEntry<float> BeggarSneakBonus;
        public static ConfigEntry<float> BeggarFoodDurationBonus;
        public static ConfigEntry<float> BeggarFoodStatsBonus;
        public static ConfigEntry<float> BeggarSkillXPPenalty;

        public static void Bind(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            ConfigurationManagerAttributes clientAttr = new ConfigurationManagerAttributes { IsAdminOnly = false };

            // 0. General
            HildirScrollPrice = config.Bind(
                "01. RP Classes - 0. General",
                "HildirRebirthScrollPrice",
                1000,
                new ConfigDescription("Price in Coins for the Scroll of Rebirth sold by Hildir.", null, adminAttr));

            ShowClassOverheadTag = config.Bind(
                "01. RP Classes - 0. General",
                "ShowClassOverheadTag",
                true,
                new ConfigDescription("Show the player's RP class above their nickname in multiplayer.", null, clientAttr));

            ShowHudArmorShield = config.Bind(
                "01. RP Classes - 0. General",
                "ShowHudArmorIndicator",
                true,
                new ConfigDescription("Show a shield icon with current Armor Class (AC) next to the HUD health/food bars.", null, clientAttr));

            InvulnerableDuringInitialChoice = config.Bind(
                "01. RP Classes - 0. General",
                "InvulnerableDuringInitialChoice",
                true,
                new ConfigDescription("Protect newly spawned players without a class from taking damage while choosing their class.", null, adminAttr));

            // 1. Warrior
            WarriorBaseHP = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BaseHPBonus",
                25f,
                new ConfigDescription("Flat bonus added to base Health (+25).", null, adminAttr));
            WarriorMeleeDamageBonus = config.Bind(
                "01. RP Classes - 1. Warrior",
                "MeleeDamageBonus",
                0.20f,
                new ConfigDescription("Damage bonus with all melee weapons including knives (0.20 = +20%).", null, adminAttr));
            WarriorFullHPEnemyDamageBonus = config.Bind(
                "01. RP Classes - 1. Warrior",
                "FullHPEnemyDamageBonus",
                0.25f,
                new ConfigDescription("Bonus damage dealt to enemies at full health (0.25 = +25%).", null, adminAttr));
            WarriorDodgeStaminaReduction = config.Bind(
                "01. RP Classes - 1. Warrior",
                "DodgeStaminaReduction",
                0.15f,
                new ConfigDescription("Dodge roll stamina cost reduction (0.15 = -15% stamina cost).", null, adminAttr));
            WarriorBraveHeartDamageBonus = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartLowHPDamageBonus",
                0.20f,
                new ConfigDescription("Damage bonus from passive 'Brave Heart' when HP is below threshold (0.20 = +20%).", null, adminAttr));
            WarriorBraveHeartMoveSpeedBonus = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartMoveSpeedBonus",
                0.05f,
                new ConfigDescription("Movement speed bonus from passive 'Brave Heart' (0.05 = +5%).", null, adminAttr));
            WarriorBraveHeartHPThreshold = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartHPThreshold",
                0.50f,
                new ConfigDescription("Health fraction threshold for 'Brave Heart' low-HP damage activation (0.50 = below 50% HP).", null, adminAttr));
            WarriorBloodlustAttackSpeedPerStack = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartBloodlustAttackSpeedPerStack",
                0.04f,
                new ConfigDescription("Melee hit stacking attack speed bonus per stack from 'Brave Heart' (0.04 = +4% attack speed per hit).", null, adminAttr));
            WarriorBloodlustLifestealPerStack = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartBloodlustLifestealPerStack",
                0.025f,
                new ConfigDescription("Melee lifesteal fraction per 'Bloody Onslaught' stack (0.025 = +2.5% lifesteal per hit, up to 12.5% at 5 stacks).", null, adminAttr));
            WarriorBloodlustMaxStacks = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartBloodlustMaxStacks",
                5,
                new ConfigDescription("Maximum stacks for 'Brave Heart' melee onslaught (5 stacks = up to +20% attack speed).", null, adminAttr));
            WarriorBloodlustDuration = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BraveHeartBloodlustDuration",
                5f,
                new ConfigDescription("Duration in seconds of 'Brave Heart' melee onslaught stacks (5s).", null, adminAttr));
            WarriorBlockPowerPenalty = config.Bind(
                "01. RP Classes - 1. Warrior",
                "BlockPowerPenalty",
                0.10f,
                new ConfigDescription("Block power penalty (0.10 = -10%).", null, adminAttr));
            WarriorRangedDamagePenalty = config.Bind(
                "01. RP Classes - 1. Warrior",
                "RangedDamagePenalty",
                0.25f,
                new ConfigDescription("Damage penalty with Bows and Crossbows (0.25 = -25%).", null, adminAttr));
            WarriorEitrRegenPenalty = config.Bind(
                "01. RP Classes - 1. Warrior",
                "EitrRegenPenaltyV2",
                0.50f,
                new ConfigDescription("Eitr regeneration penalty (0.50 = -50%).", null, adminAttr));

            // 2. Knight
            KnightBaseHP = config.Bind(
                "01. RP Classes - 2. Knight",
                "BaseHPBonus",
                50f,
                new ConfigDescription("Flat bonus added to base Health (+50).", null, adminAttr));
            KnightBaseStamina = config.Bind(
                "01. RP Classes - 2. Knight",
                "BaseStaminaBonus",
                25f,
                new ConfigDescription("Flat bonus added to base Stamina (+25).", null, adminAttr));
            KnightShieldBlockBonus = config.Bind(
                "01. RP Classes - 2. Knight",
                "ShieldBlockPowerBonus",
                0.25f,
                new ConfigDescription("Shield block power bonus (0.25 = +25% stronger shield blocking).", null, adminAttr));
            KnightParriedEnemyDamageBonus = config.Bind(
                "01. RP Classes - 2. Knight",
                "StaggeredEnemyDamageBonus",
                0.30f,
                new ConfigDescription("Bonus damage dealt to parried/staggered enemies (0.30 = +30%).", null, adminAttr));
            KnightFlatBaseArmor = config.Bind(
                "01. RP Classes - 2. Knight",
                "UnwaveringFlatBaseArmor",
                15f,
                new ConfigDescription("Flat base armor granted by the passive 'Unwavering' buff (+15 armor).", null, adminAttr));
            KnightFullArmorBonus = config.Bind(
                "01. RP Classes - 2. Knight",
                "UnwaveringFullArmorBonus",
                0.25f,
                new ConfigDescription("Total armor bonus from 'Unwavering' while raising a shield / blocking (0.25 = +25%).", null, adminAttr));
            KnightShieldSpeedBonus = config.Bind(
                "01. RP Classes - 2. Knight",
                "UnwaveringShieldSpeedBonusV2",
                0.10f,
                new ConfigDescription("Movement speed bonus from 'Unwavering' while holding a shield (0.10 = +10%).", null, adminAttr));
            KnightMoveSpeedPenalty = config.Bind(
                "01. RP Classes - 2. Knight",
                "MoveSpeedPenalty",
                0.05f,
                new ConfigDescription("Base movement speed penalty (0.05 = -5%).", null, adminAttr));
            KnightDodgeStaminaPenalty = config.Bind(
                "01. RP Classes - 2. Knight",
                "DodgeStaminaPenalty",
                0.20f,
                new ConfigDescription("Additional stamina cost for dodging/rolling only (0.20 = +20%).", null, adminAttr));
            KnightSneakPenalty = config.Bind(
                "01. RP Classes - 2. Knight",
                "SneakSkillPenalty",
                30f,
                new ConfigDescription("Sneak skill penalty (-30) and increased stealth visibility.", null, adminAttr));
            KnightEitrRegenPenalty = config.Bind(
                "01. RP Classes - 2. Knight",
                "EitrRegenPenalty",
                1.00f,
                new ConfigDescription("Eitr regeneration penalty (1.00 = -100%).", null, adminAttr));

            // 3. Thief
            ThiefBaseStamina = config.Bind(
                "01. RP Classes - 3. Thief",
                "BaseStaminaBonus",
                50f,
                new ConfigDescription("Flat bonus added to base Stamina (+50).", null, adminAttr));
            ThiefKnifeDamageBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "KnifeDamageBonus",
                0.25f,
                new ConfigDescription("Damage bonus with Knives (0.25 = +25%).", null, adminAttr));
            ThiefPositionalBackstabBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "PositionalBackstabBonus",
                0.35f,
                new ConfigDescription("Bonus damage when striking an enemy from behind in combat (0.35 = +35%).", null, adminAttr));
            ThiefSneakStaminaReduction = config.Bind(
                "01. RP Classes - 3. Thief",
                "SneakStaminaReduction",
                0.25f,
                new ConfigDescription("Stamina cost reduction while sneaking (0.25 = -25%).", null, adminAttr));
            ThiefDodgeStaminaReduction = config.Bind(
                "01. RP Classes - 3. Thief",
                "DodgeStaminaReduction",
                0.25f,
                new ConfigDescription("Stamina cost reduction when dodging/rolling (0.25 = -25%).", null, adminAttr));
            ThiefSneakBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "ShadowAscensionSneakBonus",
                20f,
                new ConfigDescription("Bonus to Sneak skill from 'Shadow Ascension' (+20).", null, adminAttr));
            ThiefStealthBackstabBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "ShadowAscensionStealthBackstabBonus",
                0.50f,
                new ConfigDescription("Multiplier bonus to surprise stealth backstab from 'Shadow Ascension' (0.50 = +50%).", null, adminAttr));
            ThiefNightDamageBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "ShadowAscensionNightDamageBonus",
                0.25f,
                new ConfigDescription("All damage bonus at night from 'Shadow Ascension' (0.25 = +25%).", null, adminAttr));
            ThiefNightStaminaReduction = config.Bind(
                "01. RP Classes - 3. Thief",
                "NightStalkerStaminaReduction",
                0.25f,
                new ConfigDescription("All stamina usage reduction at night (0.25 = -25% stamina cost).", null, adminAttr));
            ThiefNightMoveSpeedBonus = config.Bind(
                "01. RP Classes - 3. Thief",
                "NightStalkerMoveSpeedBonus",
                0.10f,
                new ConfigDescription("Movement speed bonus at night (0.10 = +10%).", null, adminAttr));
            ThiefArmorPenalty = config.Bind(
                "01. RP Classes - 3. Thief",
                "ArmorPenalty",
                0.15f,
                new ConfigDescription("Total armor penalty (0.15 = -15%).", null, adminAttr));
            ThiefBlockPowerPenalty = config.Bind(
                "01. RP Classes - 3. Thief",
                "BlockPowerPenalty",
                0.20f,
                new ConfigDescription("Shield/weapon block power penalty (0.20 = -20%).", null, adminAttr));
            ThiefRangedDamagePenalty = config.Bind(
                "01. RP Classes - 3. Thief",
                "RangedDamagePenalty",
                0.10f,
                new ConfigDescription("Damage penalty with Bows and Crossbows (0.10 = -10%).", null, adminAttr));
            ThiefEitrRegenPenalty = config.Bind(
                "01. RP Classes - 3. Thief",
                "EitrRegenPenalty",
                0.35f,
                new ConfigDescription("Eitr regeneration penalty (0.35 = -35%).", null, adminAttr));

            // 4. Hunter
            HunterBaseStamina = config.Bind(
                "01. RP Classes - 4. Hunter",
                "BaseStaminaBonus",
                25f,
                new ConfigDescription("Flat bonus added to base Stamina (+25).", null, adminAttr));
            HunterRangedDamageBonus = config.Bind(
                "01. RP Classes - 4. Hunter",
                "RangedDamageBonus",
                0.20f,
                new ConfigDescription("Damage bonus with Bows, Crossbows, and thrown Spears (0.20 = +20%).", null, adminAttr));
            HunterSneakBonus = config.Bind(
                "01. RP Classes - 4. Hunter",
                "SneakSkillBonus",
                10f,
                new ConfigDescription("Bonus to Sneak skill (+10).", null, adminAttr));
            HunterBowDrawStaminaReduction = config.Bind(
                "01. RP Classes - 4. Hunter",
                "BowDrawStaminaReduction",
                0.15f,
                new ConfigDescription("Stamina drain reduction while drawing a bow (0.15 = -15%).", null, adminAttr));
            HunterBowCrossbowSpeedBonus = config.Bind(
                "01. RP Classes - 4. Hunter",
                "FalconEyeSpeedBonus",
                0.15f,
                new ConfigDescription("Faster bow draw and crossbow reload speed from 'Falcon's Swiftness' (0.15 = +15%).", null, adminAttr));
            HunterFalconMoveSpeedBonus = config.Bind(
                "01. RP Classes - 4. Hunter",
                "FalconMoveSpeedBonus",
                0.05f,
                new ConfigDescription("Movement speed bonus from 'Falcon's Swiftness' (0.05 = +5%).", null, adminAttr));
            HunterStealthBackstabBonus = config.Bind(
                "01. RP Classes - 4. Hunter",
                "FalconStealthBackstabBonus",
                0.50f,
                new ConfigDescription("Sneak attack multiplier bonus from 'Falcon's Swiftness' (0.50 = +50%).", null, adminAttr));
            HunterComboHitsRequired = config.Bind(
                "01. RP Classes - 4. Hunter",
                "FalconConsecutiveHitsRequired",
                3,
                new ConfigDescription("Consecutive ranged hits required to empower the next shot (3 hits -> 4th shot deals x2 damage).", null, adminAttr));
            HunterComboDamageMultiplier = config.Bind(
                "01. RP Classes - 4. Hunter",
                "FalconEmpoweredShotMultiplier",
                2.0f,
                new ConfigDescription("Damage multiplier of the empowered ranged shot after landing consecutive hits (2.0 = x2 damage).", null, adminAttr));
            HunterHeavyMeleePenalty = config.Bind(
                "01. RP Classes - 4. Hunter",
                "HeavyMeleeDamagePenaltyV2",
                0.25f,
                new ConfigDescription("Damage penalty with non-knife/non-spear melee weapons (0.25 = -25%).", null, adminAttr));
            HunterBlockPowerPenalty = config.Bind(
                "01. RP Classes - 4. Hunter",
                "BlockPowerPenalty",
                0.15f,
                new ConfigDescription("Block power penalty (0.15 = -15%).", null, adminAttr));
            HunterEitrRegenPenalty = config.Bind(
                "01. RP Classes - 4. Hunter",
                "EitrRegenPenalty",
                0.35f,
                new ConfigDescription("Eitr regeneration penalty (0.35 = -35%).", null, adminAttr));

            // 5. Sorcerer
            SorcererBaseEitr = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "BaseEitrBonusV2",
                50f,
                new ConfigDescription("Innate base Eitr pool (+50).", null, adminAttr));
            SorcererEitrRegenBonus = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "EitrRegenBonusV2",
                0.25f,
                new ConfigDescription("Eitr regeneration bonus (0.25 = +25%).", null, adminAttr));
            SorcererSkillXPBonus = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "SkillXPBonus",
                0.25f,
                new ConfigDescription("All skill experience gain bonus (0.25 = +25%).", null, adminAttr));
            SorcererGuardianDurationBonus = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "ArchmageForsakenDurationBonus",
                0.50f,
                new ConfigDescription("Forsaken Power buff duration bonus from 'Archmage' (0.50 = +50% duration -> 7.5 min instead of 5 min).", null, adminAttr));
            SorcererGuardianCooldownReduction = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "ArchmageForsakenCooldownReduction",
                0.25f,
                new ConfigDescription("Forsaken Power cooldown reduction from 'Archmage' (0.25 = -25% cooldown -> 15 min instead of 20 min).", null, adminAttr));
            SorcererFreeCastEveryN = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "ArchmageFreeEitrEveryNCasts",
                5,
                new ConfigDescription("Every Nth magic cast costs 0 Eitr (5 = every 5th cast is free).", null, adminAttr));
            SorcererPhysicalWeaponPenalty = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "PhysicalWeaponDamagePenaltyV2",
                0.35f,
                new ConfigDescription("Damage penalty with non-magic weapons (0.35 = -35%).", null, adminAttr));
            SorcererArmorPenalty = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "ArmorPenaltyV2",
                0.30f,
                new ConfigDescription("Total armor penalty (0.30 = -30%).", null, adminAttr));
            SorcererCarryWeightPenalty = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "CarryWeightPenaltyV2",
                50f,
                new ConfigDescription("Carry weight penalty (50 = -50 weight).", null, adminAttr));
            SorcererHeavyArmorEitrPenalty = config.Bind(
                "01. RP Classes - 5. Sorcerer",
                "HeavyArmorEitrRegenPenalty",
                1.00f,
                new ConfigDescription("Eitr regeneration penalty while wearing any Heavy Armor piece (1.00 = -100%).", null, adminAttr));

            // 6. Beggar
            BeggarCarryWeightBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "CarryWeightBonusV2",
                100f,
                new ConfigDescription("Carry weight bonus (+100).", null, adminAttr));
            BeggarBaseStatsBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "BaseStatsBonus",
                10f,
                new ConfigDescription("Flat bonus added to Base HP, Base Stamina, and Base Eitr (+10 each).", null, adminAttr));
            BeggarAllDamageBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "AllDamageBonus",
                0.05f,
                new ConfigDescription("All damage dealt bonus (0.05 = +5%).", null, adminAttr));
            BeggarMoveSpeedBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "MoveSpeedBonus",
                0.05f,
                new ConfigDescription("Movement speed bonus (0.05 = +5%).", null, adminAttr));
            BeggarSneakBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "SneakSkillBonusV2",
                5f,
                new ConfigDescription("Bonus to Sneak skill (+5).", null, adminAttr));
            BeggarFoodDurationBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "IronStomachFoodDurationBonus",
                0.30f,
                new ConfigDescription("Food duration bonus for Beggar's 'Iron Stomach' (0.30 = +30% longer food duration).", null, adminAttr));
            BeggarFoodStatsBonus = config.Bind(
                "01. RP Classes - 6. Beggar",
                "IronStomachFoodStatsBonus",
                0.15f,
                new ConfigDescription("Bonus Health, Stamina, and Eitr gained from food for Beggar's 'Iron Stomach' (0.15 = +15%).", null, adminAttr));
            BeggarSkillXPPenalty = config.Bind(
                "01. RP Classes - 6. Beggar",
                "IneptitudeSkillXPPenaltyV3",
                0.50f,
                new ConfigDescription("Skill experience gain penalty from the passive 'Ineptitude' debuff (0.50 = -50% XP).", null, adminAttr));
        }

        public static string GetClassToken(RPClassType cls)
        {
            switch (cls)
            {
                case RPClassType.Warrior: return "$rp_class_warrior";
                case RPClassType.Knight: return "$rp_class_knight";
                case RPClassType.Thief: return "$rp_class_thief";
                case RPClassType.Hunter: return "$rp_class_hunter";
                case RPClassType.Sorcerer: return "$rp_class_sorcerer";
                case RPClassType.Beggar: return "$rp_class_beggar";
                default: return "$rp_class_none";
            }
        }

        public static string GetClassRoleToken(RPClassType cls)
        {
            switch (cls)
            {
                case RPClassType.Warrior: return "$rp_role_warrior";
                case RPClassType.Knight: return "$rp_role_knight";
                case RPClassType.Thief: return "$rp_role_thief";
                case RPClassType.Hunter: return "$rp_role_hunter";
                case RPClassType.Sorcerer: return "$rp_role_sorcerer";
                case RPClassType.Beggar: return "$rp_role_beggar";
                default: return "";
            }
        }

        public static string GetClassLoreToken(RPClassType cls)
        {
            switch (cls)
            {
                case RPClassType.Warrior: return "$rp_lore_warrior";
                case RPClassType.Knight: return "$rp_lore_knight";
                case RPClassType.Thief: return "$rp_lore_thief";
                case RPClassType.Hunter: return "$rp_lore_hunter";
                case RPClassType.Sorcerer: return "$rp_lore_sorcerer";
                case RPClassType.Beggar: return "$rp_lore_beggar";
                default: return "";
            }
        }

        public static Color GetClassColor(RPClassType cls)
        {
            switch (cls)
            {
                case RPClassType.Warrior: return new Color(0.95f, 0.36f, 0.30f);  // Crimson Red
                case RPClassType.Knight: return new Color(0.78f, 0.86f, 0.96f);   // Silver-Steel
                case RPClassType.Thief: return new Color(0.74f, 0.46f, 0.96f);    // Shadow Violet
                case RPClassType.Hunter: return new Color(0.45f, 0.88f, 0.48f);   // Ranger Green
                case RPClassType.Sorcerer: return new Color(0.36f, 0.78f, 1.00f); // Arcane Cyan
                case RPClassType.Beggar: return new Color(0.84f, 0.68f, 0.44f);   // Burlap Ochre
                default: return Color.white;
            }
        }

        public static string GetClassHexColor(RPClassType cls)
        {
            Color32 c = GetClassColor(cls);
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.r, c.g, c.b);
        }

        private static string Loc(string token)
        {
            if (Localization.instance != null)
            {
                return Localization.instance.Localize(token);
            }
            return token;
        }

        private static int Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f);
        }

        private static void AppendGoodLine(StringBuilder sb, string label, string val, bool newline = true)
        {
            sb.AppendFormat("• {0}: <color=#80E878>{1}</color>", label, val);
            if (newline) sb.Append('\n');
        }

        private static void AppendBadLine(StringBuilder sb, string label, string val, bool newline = true)
        {
            sb.AppendFormat("• {0}: <color=#FF6E6E>{1}</color>", label, val);
            if (newline) sb.Append('\n');
        }

        private static void AppendSpecialPassiveLine(StringBuilder sb, string label, string val, bool newline = true)
        {
            // Highlighted in bright turquoise-gold so special passive mechanics stand out distinctly!
            sb.AppendFormat("• <color=#5CE6FF>{0}:</color> <color=#FFE066><b>{1}</b></color>", label, val);
            if (newline) sb.Append('\n');
        }

        private static void AppendSectionHeader(StringBuilder sb, string colorHex, string text, bool topGap = false)
        {
            if (topGap) sb.Append("<size=6>\n</size>");
            sb.AppendFormat("<color={0}><b>{1}:</b></color>\n", colorHex, text);
        }

        /// <summary>
        /// Strict unified standard across ALL 6 classes:
        /// 1. Advantages: (• Parameter: +Value)
        /// 2. Passive Skill — «Name»: (• Parameter: Value)
        /// 3. Weaknesses: (• Parameter: -Value) — omitted only for Beggar whose weakness IS his passive skill!
        /// </summary>
        public static string BuildClassTooltip(RPClassType cls, bool includeLore)
        {
            StringBuilder sb = new StringBuilder();
            if (includeLore)
            {
                sb.Append("<color=#D8C8A8><i>").Append(Loc(GetClassLoreToken(cls))).Append("</i></color>\n\n");
            }

            string passiveTitle = Loc("$rp_ui_passive");

            switch (cls)
            {
                case RPClassType.Warrior:
                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_base_hp"), "+" + Mathf.RoundToInt(WarriorBaseHP.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_melee_dmg"), "+" + Pct(WarriorMeleeDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_full_hp_enemy_dmg"), "+" + Pct(WarriorFullHPEnemyDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_dodge_stam"), "-" + Pct(WarriorDodgeStaminaReduction.Value) + "%");

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_braveheart") + "»", true);
                    AppendGoodLine(sb, string.Format(Loc("$rp_stat_low_hp_dmg"), Pct(WarriorBraveHeartHPThreshold.Value)), "+" + Pct(WarriorBraveHeartDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_move_speed"), "+" + Pct(WarriorBraveHeartMoveSpeedBonus.Value) + "%");
                    {
                        float lsPerStackPct = WarriorBloodlustLifestealPerStack.Value * 100f;
                        float lsMaxPct = lsPerStackPct * WarriorBloodlustMaxStacks.Value;
                        AppendSpecialPassiveLine(
                            sb,
                            Loc("$rp_stat_warrior_onslaught"),
                            string.Format(
                                Loc("$rp_stat_warrior_onslaught_val"),
                                Pct(WarriorBloodlustAttackSpeedPerStack.Value),
                                lsPerStackPct.ToString("0.##"),
                                Mathf.RoundToInt(WarriorBloodlustDuration.Value),
                                Pct(WarriorBloodlustAttackSpeedPerStack.Value * WarriorBloodlustMaxStacks.Value),
                                lsMaxPct.ToString("0.##")));
                    }

                    AppendSectionHeader(sb, "#FF6E6E", Loc("$rp_ui_debuffs"), true);
                    AppendBadLine(sb, Loc("$rp_stat_block_power"), "-" + Pct(WarriorBlockPowerPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_ranged_dmg"), "-" + Pct(WarriorRangedDamagePenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_eitr_regen"), "-" + Pct(WarriorEitrRegenPenalty.Value) + "%", false);
                    break;

                case RPClassType.Knight:
                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_base_hp"), "+" + Mathf.RoundToInt(KnightBaseHP.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_base_stamina"), "+" + Mathf.RoundToInt(KnightBaseStamina.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_shield_block"), "+" + Pct(KnightShieldBlockBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_parried_dmg"), "+" + Pct(KnightParriedEnemyDamageBonus.Value) + "%");

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_unwavering") + "»", true);
                    AppendGoodLine(sb, Loc("$rp_stat_base_armor"), "+" + Mathf.RoundToInt(KnightFlatBaseArmor.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_shield_move_speed"), "+" + Pct(KnightShieldSpeedBonus.Value) + "%");
                    AppendSpecialPassiveLine(
                        sb,
                        Loc("$rp_stat_knight_bulwark"),
                        string.Format(Loc("$rp_stat_knight_bulwark_val"), Pct(KnightFullArmorBonus.Value)));

                    AppendSectionHeader(sb, "#FF6E6E", Loc("$rp_ui_debuffs"), true);
                    AppendBadLine(sb, Loc("$rp_stat_move_speed"), "-" + Pct(KnightMoveSpeedPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_dodge_stam"), "+" + Pct(KnightDodgeStaminaPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_sneak"), "-" + Mathf.RoundToInt(KnightSneakPenalty.Value));
                    AppendBadLine(sb, Loc("$rp_stat_eitr_regen"), "-" + Pct(KnightEitrRegenPenalty.Value) + "%", false);
                    break;

                case RPClassType.Thief:
                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_base_stamina"), "+" + Mathf.RoundToInt(ThiefBaseStamina.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_knife_dmg"), "+" + Pct(ThiefKnifeDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_back_dmg"), "+" + Pct(ThiefPositionalBackstabBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_sneak_stam"), "-" + Pct(ThiefSneakStaminaReduction.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_dodge_stam"), "-" + Pct(ThiefDodgeStaminaReduction.Value) + "%");

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_shadow_ascension") + "»", true);
                    AppendGoodLine(sb, Loc("$rp_stat_sneak"), "+" + Mathf.RoundToInt(ThiefSneakBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_stealth_mult"), "+" + Pct(ThiefStealthBackstabBonus.Value) + "%");
                    AppendSpecialPassiveLine(
                        sb,
                        Loc("$rp_stat_thief_night_veil"),
                        string.Format(
                            Loc("$rp_stat_thief_night_veil_val"),
                            Pct(ThiefNightDamageBonus.Value),
                            Pct(ThiefNightStaminaReduction.Value),
                            Pct(ThiefNightMoveSpeedBonus.Value)));

                    AppendSectionHeader(sb, "#FF6E6E", Loc("$rp_ui_debuffs"), true);
                    AppendBadLine(sb, Loc("$rp_stat_total_armor"), "-" + Pct(ThiefArmorPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_block_power"), "-" + Pct(ThiefBlockPowerPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_ranged_dmg"), "-" + Pct(ThiefRangedDamagePenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_eitr_regen"), "-" + Pct(ThiefEitrRegenPenalty.Value) + "%", false);
                    break;

                case RPClassType.Hunter:
                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_base_stamina"), "+" + Mathf.RoundToInt(HunterBaseStamina.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_ranged_spear_dmg"), "+" + Pct(HunterRangedDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_sneak"), "+" + Mathf.RoundToInt(HunterSneakBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_bow_draw_stam"), "-" + Pct(HunterBowDrawStaminaReduction.Value) + "%");

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_falcon_eye") + "»", true);
                    AppendGoodLine(sb, Loc("$rp_stat_ranged_speed"), "+" + Pct(HunterBowCrossbowSpeedBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_move_speed"), "+" + Pct(HunterFalconMoveSpeedBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_stealth_mult"), "+" + Pct(HunterStealthBackstabBonus.Value) + "%");
                    AppendSpecialPassiveLine(
                        sb,
                        string.Format(Loc("$rp_stat_hunter_combo"), Mathf.Max(1, HunterComboHitsRequired.Value)),
                        string.Format(Loc("$rp_stat_hunter_combo_val"), HunterComboDamageMultiplier.Value.ToString("0.#")));

                    AppendSectionHeader(sb, "#FF6E6E", Loc("$rp_ui_debuffs"), true);
                    AppendBadLine(sb, Loc("$rp_stat_heavy_melee_dmg"), "-" + Pct(HunterHeavyMeleePenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_block_power"), "-" + Pct(HunterBlockPowerPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_eitr_regen"), "-" + Pct(HunterEitrRegenPenalty.Value) + "%", false);
                    break;

                case RPClassType.Sorcerer:
                {
                    float gpDurMin = 5f * (1f + Mathf.Max(0f, SorcererGuardianDurationBonus.Value));
                    float gpCdMin = 20f * (1f - Mathf.Clamp(SorcererGuardianCooldownReduction.Value, 0f, 0.90f));
                    string durMinStr = gpDurMin.ToString("0.#");
                    string cdMinStr = gpCdMin.ToString("0.#");

                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_base_eitr"), "+" + Mathf.RoundToInt(SorcererBaseEitr.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_eitr_regen"), "+" + Pct(SorcererEitrRegenBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_skill_xp"), "+" + Pct(SorcererSkillXPBonus.Value) + "%");

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_archmage") + "»", true);
                    AppendGoodLine(
                        sb,
                        Loc("$rp_stat_gp_duration"),
                        string.Format(Loc("$rp_stat_gp_duration_val"), Pct(SorcererGuardianDurationBonus.Value), durMinStr));
                    AppendGoodLine(
                        sb,
                        Loc("$rp_stat_gp_cooldown"),
                        string.Format(Loc("$rp_stat_gp_cooldown_val"), Pct(SorcererGuardianCooldownReduction.Value), cdMinStr));
                    AppendSpecialPassiveLine(
                        sb,
                        Loc("$rp_stat_sorcerer_echo"),
                        string.Format(Loc("$rp_stat_sorcerer_echo_val"), Mathf.Max(2, SorcererFreeCastEveryN.Value)));

                    AppendSectionHeader(sb, "#FF6E6E", Loc("$rp_ui_debuffs"), true);
                    AppendBadLine(sb, Loc("$rp_stat_phys_dmg"), "-" + Pct(SorcererPhysicalWeaponPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_total_armor"), "-" + Pct(SorcererArmorPenalty.Value) + "%");
                    AppendBadLine(sb, Loc("$rp_stat_carry_weight"), "-" + Mathf.RoundToInt(SorcererCarryWeightPenalty.Value));
                    AppendBadLine(sb, Loc("$rp_stat_heavy_armor_eitr"), "-" + Pct(SorcererHeavyArmorEitrPenalty.Value) + "%", false);
                    break;
                }

                case RPClassType.Beggar:
                    AppendSectionHeader(sb, "#80E878", Loc("$rp_ui_bonuses"), false);
                    AppendGoodLine(sb, Loc("$rp_stat_carry_weight"), "+" + Mathf.RoundToInt(BeggarCarryWeightBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_base_hp"), "+" + Mathf.RoundToInt(BeggarBaseStatsBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_base_stamina"), "+" + Mathf.RoundToInt(BeggarBaseStatsBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_base_eitr"), "+" + Mathf.RoundToInt(BeggarBaseStatsBonus.Value));
                    AppendGoodLine(sb, Loc("$rp_stat_all_dmg"), "+" + Pct(BeggarAllDamageBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_move_speed"), "+" + Pct(BeggarMoveSpeedBonus.Value) + "%");
                    AppendGoodLine(sb, Loc("$rp_stat_sneak"), "+" + Mathf.RoundToInt(BeggarSneakBonus.Value));
                    AppendSpecialPassiveLine(
                        sb,
                        Loc("$rp_stat_beggar_stomach"),
                        string.Format(
                            Loc("$rp_stat_beggar_stomach_val"),
                            Pct(BeggarFoodStatsBonus.Value),
                            Pct(BeggarFoodDurationBonus.Value)));

                    AppendSectionHeader(sb, "#FFD278", passiveTitle + " — «" + Loc("$rp_se_ineptitude") + "»", true);
                    AppendBadLine(sb, Loc("$rp_stat_skill_xp"), "-" + Pct(BeggarSkillXPPenalty.Value) + "%", false);
                    break;
            }
            return sb.ToString();
        }

        // ====================================================================
        // Passive Skill Tooltips (shown in HUD / Inventory Active Effects,
        // using the exact same unified parameter names!)
        // ====================================================================
        public static string BuildBraveHeartTooltip()
        {
            float lsPerStackPct = WarriorBloodlustLifestealPerStack.Value * 100f;
            float lsMaxPct = lsPerStackPct * WarriorBloodlustMaxStacks.Value;

            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_braveheart_lore")).Append("</i></color>\n\n");
            AppendGoodLine(sb, string.Format(Loc("$rp_stat_low_hp_dmg"), Pct(WarriorBraveHeartHPThreshold.Value)), "+" + Pct(WarriorBraveHeartDamageBonus.Value) + "%");
            AppendGoodLine(sb, Loc("$rp_stat_move_speed"), "+" + Pct(WarriorBraveHeartMoveSpeedBonus.Value) + "%");
            AppendSpecialPassiveLine(
                sb,
                Loc("$rp_stat_warrior_onslaught"),
                string.Format(
                    Loc("$rp_stat_warrior_onslaught_val"),
                    Pct(WarriorBloodlustAttackSpeedPerStack.Value),
                    lsPerStackPct.ToString("0.##"),
                    Mathf.RoundToInt(WarriorBloodlustDuration.Value),
                    Pct(WarriorBloodlustAttackSpeedPerStack.Value * WarriorBloodlustMaxStacks.Value),
                    lsMaxPct.ToString("0.##")),
                false);
            return sb.ToString();
        }

        public static string BuildUnwaveringTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_unwavering_lore")).Append("</i></color>\n\n");
            AppendGoodLine(sb, Loc("$rp_stat_base_armor"), "+" + Mathf.RoundToInt(KnightFlatBaseArmor.Value));
            AppendGoodLine(sb, Loc("$rp_stat_shield_move_speed"), "+" + Pct(KnightShieldSpeedBonus.Value) + "%");
            AppendSpecialPassiveLine(
                sb,
                Loc("$rp_stat_knight_bulwark"),
                string.Format(Loc("$rp_stat_knight_bulwark_val"), Pct(KnightFullArmorBonus.Value)),
                false);
            return sb.ToString();
        }

        public static string BuildShadowAscensionTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_shadow_ascension_lore")).Append("</i></color>\n\n");
            AppendGoodLine(sb, Loc("$rp_stat_sneak"), "+" + Mathf.RoundToInt(ThiefSneakBonus.Value));
            AppendGoodLine(sb, Loc("$rp_stat_stealth_mult"), "+" + Pct(ThiefStealthBackstabBonus.Value) + "%");
            AppendSpecialPassiveLine(
                sb,
                Loc("$rp_stat_thief_night_veil"),
                string.Format(
                    Loc("$rp_stat_thief_night_veil_val"),
                    Pct(ThiefNightDamageBonus.Value),
                    Pct(ThiefNightStaminaReduction.Value),
                    Pct(ThiefNightMoveSpeedBonus.Value)),
                false);
            return sb.ToString();
        }

        public static string BuildFalconEyeTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_falcon_eye_lore")).Append("</i></color>\n\n");
            AppendGoodLine(sb, Loc("$rp_stat_ranged_speed"), "+" + Pct(HunterBowCrossbowSpeedBonus.Value) + "%");
            AppendGoodLine(sb, Loc("$rp_stat_move_speed"), "+" + Pct(HunterFalconMoveSpeedBonus.Value) + "%");
            AppendGoodLine(sb, Loc("$rp_stat_stealth_mult"), "+" + Pct(HunterStealthBackstabBonus.Value) + "%");
            AppendSpecialPassiveLine(
                sb,
                string.Format(Loc("$rp_stat_hunter_combo"), Mathf.Max(1, HunterComboHitsRequired.Value)),
                string.Format(Loc("$rp_stat_hunter_combo_val"), HunterComboDamageMultiplier.Value.ToString("0.#")),
                false);
            return sb.ToString();
        }

        public static string BuildArchmageTooltip()
        {
            float gpDurMin = 5f * (1f + Mathf.Max(0f, SorcererGuardianDurationBonus.Value));
            float gpCdMin = 20f * (1f - Mathf.Clamp(SorcererGuardianCooldownReduction.Value, 0f, 0.90f));
            string durMinStr = gpDurMin.ToString("0.#");
            string cdMinStr = gpCdMin.ToString("0.#");

            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_archmage_lore")).Append("</i></color>\n\n");
            AppendGoodLine(
                sb,
                Loc("$rp_stat_gp_duration"),
                string.Format(Loc("$rp_stat_gp_duration_val"), Pct(SorcererGuardianDurationBonus.Value), durMinStr));
            AppendGoodLine(
                sb,
                Loc("$rp_stat_gp_cooldown"),
                string.Format(Loc("$rp_stat_gp_cooldown_val"), Pct(SorcererGuardianCooldownReduction.Value), cdMinStr));
            AppendSpecialPassiveLine(
                sb,
                Loc("$rp_stat_sorcerer_echo"),
                string.Format(Loc("$rp_stat_sorcerer_echo_val"), Mathf.Max(2, SorcererFreeCastEveryN.Value)),
                false);
            return sb.ToString();
        }

        public static string BuildIneptitudeTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#D8C8A8><i>").Append(Loc("$rp_se_ineptitude_lore")).Append("</i></color>\n\n");
            AppendBadLine(sb, Loc("$rp_stat_skill_xp"), "-" + Pct(BeggarSkillXPPenalty.Value) + "%", false);
            return sb.ToString();
        }
    }
}
