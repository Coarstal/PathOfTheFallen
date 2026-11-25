using System;
using BepInEx.Configuration;
using Jotunn.Utils;

namespace CoarstalsEnemyOverhaul
{
    public enum InjuryType
    {
        None = 0,
        Bleeding = 1,       // Рваная рана (Кровотечение)
        Hamstrung = 2,      // Простреленное сухожилие (Хромота)
        ArmorSunder = 3,    // Раскол брони
        Winded = 4,         // Выбитое дыхание (Контузия)
        EitrBurn = 5,       // Ожог Эйтра (Манабёрн)
        Rot = 6             // Заражение (Гниение)
    }

    public enum ChampionPerk
    {
        None = 0,
        ElementalInfusion = 1,  // Стихийная аура и бонусный урон стихией биома
        BerserkFury = 2,        // На <40% HP: +25% скорости движения и атаки, повышенная устойчивость
        PackCaller = 3          // В бою один раз призывает 2 телохранителей своего вида
    }

    public static class EnemyOverhaulConfig
    {
        // ====================================================================
        // 1. Named Hunt Champions (Система Вендетты — каждые ~250 мобов)
        // ====================================================================
        public static ConfigEntry<bool> EnableChampions;
        public static ConfigEntry<bool> ShowMinimapChampionCounter;
        public static ConfigEntry<bool> ShowMinimapPing;
        public static ConfigEntry<int> HuntChampionMinKills;
        public static ConfigEntry<int> HuntChampionMaxKills;
        public static ConfigEntry<float> ChampionHPMultiplier;
        public static ConfigEntry<float> ChampionDamageMultiplier;
        public static ConfigEntry<float> ChampionScaleMultiplier;
        public static ConfigEntry<float> ChampionStaggerResistance;
        public static ConfigEntry<bool> ChampionOnlyKnightCanBlock;
        public static ConfigEntry<int> SmallChampionTrophyCount;
        public static ConfigEntry<int> LargeChampionTrophyCount;
        public static ConfigEntry<int> ChampionLootMultiplier;
        public static ConfigEntry<int> ChampionMinCoins;
        public static ConfigEntry<int> ChampionMaxCoins;

        // ====================================================================
        // 2. Combat Injuries (6 Боевых Травм)
        // ====================================================================
        public static ConfigEntry<bool> EnableInjuries;
        public static ConfigEntry<float> InjuryMinHealthLost;
        public static ConfigEntry<float> InjuryChanceZeroStar;
        public static ConfigEntry<float> InjuryChanceOneStarOrCursed;
        public static ConfigEntry<float> InjuryChanceTwoStarOrChampion;

        public static ConfigEntry<float> BleedingDuration;
        public static ConfigEntry<float> BleedingBaseDPS;
        public static ConfigEntry<float> BleedingSprintMultiplier;

        public static ConfigEntry<float> HamstrungDuration;
        public static ConfigEntry<float> HamstrungSpeedPenalty;
        public static ConfigEntry<float> HamstrungJumpDodgeStaminaPenalty;

        public static ConfigEntry<float> ArmorSunderDuration;
        public static ConfigEntry<float> ArmorSunderPenalty;

        public static ConfigEntry<float> WindedDuration;
        public static ConfigEntry<float> WindedStaminaRegenPenalty;

        public static ConfigEntry<float> EitrBurnDuration;
        public static ConfigEntry<float> EitrBurnRegenPenalty;
        public static ConfigEntry<float> EitrBurnHealthDamagePerEitr;

        public static ConfigEntry<float> RotDuration;
        public static ConfigEntry<float> RotHealingPenalty;

        // ====================================================================
        // 3. Cursed Sealed Dungeons (Проклятые Данжи)
        // ====================================================================
        public static ConfigEntry<bool> EnableCursedDungeons;
        public static ConfigEntry<float> CursedDungeonChance;
        public static ConfigEntry<float> CursedDungeonMobHPBonus;
        public static ConfigEntry<float> CursedDungeonMobDamageBonus;
        public static ConfigEntry<bool> CursedDungeonEnableFog;

        public static void Bind(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            ConfigurationManagerAttributes clientAttr = new ConfigurationManagerAttributes { IsAdminOnly = false };

            // 1. Named Hunt Champions
            const string secChamp = "05. Enemy Overhaul - 1. Hunt Champions";
            const string secInj = "05. Enemy Overhaul - 2. Combat Injuries";
            const string secDung = "05. Enemy Overhaul - 3. Cursed Dungeons";

            EnableChampions = config.Bind(
                secChamp,
                "EnableChampions",
                true,
                new ConfigDescription("Enable the server-wide Hunt Champion ambush system (~every 25-35 slain enemies, spawning the last killed creature).", null, adminAttr));

            ShowMinimapChampionCounter = config.Bind(
                secChamp,
                "ShowMinimapChampionCounter",
                true,
                new ConfigDescription("Display a small remaining-kills-until-Champion counter in the bottom-right corner of the minimap.", null, clientAttr));

            ShowMinimapPing = config.Bind(
                secChamp,
                "ShowMinimapPing",
                true,
                new ConfigDescription("Display server ping (ping: X ms) in the top-left corner of the minimap.", null, clientAttr));

            HuntChampionMinKills = config.Bind(
                secChamp,
                "HuntChampionMinKillsV2",
                25,
                new ConfigDescription("Minimum global hostile mob kills across all species before a Named Hunt Champion ambushes (~25-35).", null, adminAttr));

            HuntChampionMaxKills = config.Bind(
                secChamp,
                "HuntChampionMaxKillsV2",
                35,
                new ConfigDescription("Maximum global hostile mob kills across all species before a Named Hunt Champion is guaranteed to ambush (~25-35).", null, adminAttr));

            ChampionHPMultiplier = config.Bind(
                secChamp,
                "ChampionHPMultiplierV3",
                3.0f,
                new ConfigDescription("Health multiplier of a Named Champion relative to a 0-star creature (3.0x = exact vanilla 2-star creature HP).", null, adminAttr));

            ChampionDamageMultiplier = config.Bind(
                secChamp,
                "ChampionDamageMultiplierV3",
                2.0f,
                new ConfigDescription("Base damage multiplier of a Named Champion relative to a 0-star creature (2.0x = exact vanilla 2-star creature damage, plus guaranteed biome elemental damage).", null, adminAttr));

            ChampionScaleMultiplier = config.Bind(
                secChamp,
                "ChampionScaleMultiplier",
                1.25f,
                new ConfigDescription("Visual model scale multiplier of a Named Champion (1.25 = +25% size).", null, adminAttr));

            ChampionStaggerResistance = config.Bind(
                secChamp,
                "ChampionStaggerDamageReduction",
                0.55f,
                new ConfigDescription("Stagger buildup reduction on Named Champions so they cannot be trivially stunlocked (0.55 = -55% stagger taken).", null, adminAttr));

            ChampionOnlyKnightCanBlock = config.Bind(
                secChamp,
                "ChampionOnlyKnightCanBlock",
                true,
                new ConfigDescription("When RP Classes is enabled, only the Knight class can block or parry a Champion's attacks; any other class attempting to block or parry has their stamina completely drained and guard shattered.", null, adminAttr));

            SmallChampionTrophyCount = config.Bind(
                secChamp,
                "SmallChampionGuaranteedTrophies",
                3,
                new ConfigDescription("Guaranteed mob trophies dropped by a slain common-species Champion (synergizes with WeaponAttributes enchanting!).", null, adminAttr));

            LargeChampionTrophyCount = config.Bind(
                secChamp,
                "LargeChampionGuaranteedTrophies",
                2,
                new ConfigDescription("Guaranteed mob trophies dropped by a slain large-species Champion.", null, adminAttr));

            ChampionLootMultiplier = config.Bind(
                secChamp,
                "ChampionLootMultiplier",
                3,
                new ConfigDescription("Multiplier for regular resource drops from a slain Champion (3 = x3 loot).", null, adminAttr));

            ChampionMinCoins = config.Bind(
                secChamp,
                "ChampionMinCoins",
                25,
                new ConfigDescription("Minimum bonus Coins dropped by a slain Champion (scales up with biome tier).", null, adminAttr));

            ChampionMaxCoins = config.Bind(
                secChamp,
                "ChampionMaxCoins",
                65,
                new ConfigDescription("Maximum base bonus Coins dropped by a slain Champion (scales up with biome tier).", null, adminAttr));

            // 2. Combat Injuries
            EnableInjuries = config.Bind(
                secInj,
                "EnableInjuries",
                true,
                new ConfigDescription("Enable the 6 tactical combat injury debuffs inflicted by unparried/unblocked enemy hits.", null, adminAttr));

            InjuryMinHealthLost = config.Bind(
                secInj,
                "MinHealthDamageForInjury",
                7.5f,
                new ConfigDescription("Minimum unblocked health lost from a single hit required to trigger any Combat Injury debuff (default 7.5 HP).", null, adminAttr));

            InjuryChanceZeroStar = config.Bind(
                secInj,
                "ChanceZeroStar",
                0.14f,
                new ConfigDescription("Chance (0.14 = 14%) for an unblocked hit from a normal 0-star enemy to inflict an injury.", null, adminAttr));

            InjuryChanceOneStarOrCursed = config.Bind(
                secInj,
                "ChanceOneStarOrCursedMob",
                0.32f,
                new ConfigDescription("Chance (0.32 = 32%) for an unblocked hit from a 1-star enemy or Cursed Dungeon enemy to inflict an injury.", null, adminAttr));

            InjuryChanceTwoStarOrChampion = config.Bind(
                secInj,
                "ChanceTwoStarBossOrChampion",
                0.48f,
                new ConfigDescription("Chance (0.48 = 48%) for an unblocked hit from a 2-star enemy, Boss, or Named Champion to inflict an injury.", null, adminAttr));

            BleedingDuration = config.Bind(
                secInj,
                "BleedingDuration",
                12f,
                new ConfigDescription("Duration in seconds of Laceration / Bleeding (Рваная рана).", null, adminAttr));

            BleedingBaseDPS = config.Bind(
                secInj,
                "BleedingBaseDPS",
                3.0f,
                new ConfigDescription("Base bleeding damage per second while standing/walking (scales slightly with player max HP).", null, adminAttr));

            BleedingSprintMultiplier = config.Bind(
                secInj,
                "BleedingSprintRollMultiplier",
                2.0f,
                new ConfigDescription("Damage multiplier to Bleeding ticks while sprinting or dodge-rolling (2.0 = x2 bleed damage while running).", null, adminAttr));

            HamstrungDuration = config.Bind(
                secInj,
                "HamstrungDuration",
                8f,
                new ConfigDescription("Duration in seconds of Pierced Tendon / Hamstrung (Простреленное сухожилие).", null, adminAttr));

            HamstrungSpeedPenalty = config.Bind(
                secInj,
                "HamstrungMoveSpeedPenalty",
                0.25f,
                new ConfigDescription("Movement speed reduction while Hamstrung (0.25 = -25% speed).", null, adminAttr));

            HamstrungJumpDodgeStaminaPenalty = config.Bind(
                secInj,
                "HamstrungJumpDodgeStaminaPenalty",
                0.30f,
                new ConfigDescription("Additional stamina cost for jumping and dodge-rolling while Hamstrung (0.30 = +30% stamina cost).", null, adminAttr));

            ArmorSunderDuration = config.Bind(
                secInj,
                "ArmorSunderDuration",
                15f,
                new ConfigDescription("Duration in seconds of Sundered Armor (Раскол брони).", null, adminAttr));

            ArmorSunderPenalty = config.Bind(
                secInj,
                "ArmorSunderPenalty",
                0.30f,
                new ConfigDescription("Total armor reduction during Sundered Armor (0.30 = -30% total armor).", null, adminAttr));

            WindedDuration = config.Bind(
                secInj,
                "WindedDuration",
                12f,
                new ConfigDescription("Duration in seconds of Concussion / Winded (Выбитое дыхание).", null, adminAttr));

            WindedStaminaRegenPenalty = config.Bind(
                secInj,
                "WindedStaminaRegenPenalty",
                0.50f,
                new ConfigDescription("Stamina regeneration reduction while Winded (0.50 = -50% stamina regen).", null, adminAttr));

            EitrBurnDuration = config.Bind(
                secInj,
                "EitrBurnDuration",
                10f,
                new ConfigDescription("Duration in seconds of Eitr Burn / Manaburn (Ожог Эйтра).", null, adminAttr));

            EitrBurnRegenPenalty = config.Bind(
                secInj,
                "EitrBurnRegenPenalty",
                0.40f,
                new ConfigDescription("Eitr regeneration reduction during Eitr Burn (0.40 = -40% Eitr regen).", null, adminAttr));

            EitrBurnHealthDamagePerEitr = config.Bind(
                secInj,
                "EitrBurnHealthDamageRatio",
                0.50f,
                new ConfigDescription("Pure health damage suffered per point of Eitr spent while afflicted by Eitr Burn (0.50 = 50% of spent Eitr burned from HP).", null, adminAttr));

            RotDuration = config.Bind(
                secInj,
                "RotDuration",
                15f,
                new ConfigDescription("Duration in seconds of Putrid Rot / Infection (Заражение).", null, adminAttr));

            RotHealingPenalty = config.Bind(
                secInj,
                "RotHealingPenalty",
                0.50f,
                new ConfigDescription("Reduction to all incoming healing (meads, food regen, lifesteal) while afflicted by Rot (0.50 = -50% healing).", null, adminAttr));

            // 3. Cursed Dungeons
            EnableCursedDungeons = config.Bind(
                secDung,
                "EnableCursedDungeons",
                true,
                new ConfigDescription("Enable Cursed Dungeons that seal the exit until the Dungeon Champion is slain.", null, adminAttr));

            CursedDungeonChance = config.Bind(
                secDung,
                "CursedDungeonChance",
                0.10f,
                new ConfigDescription("Chance (0.10 = 10%) for any generated dungeon to be permanently Cursed until cleansed.", null, adminAttr));

            CursedDungeonMobHPBonus = config.Bind(
                secDung,
                "CursedMobHPBonus",
                0.30f,
                new ConfigDescription("Health bonus granted to regular enemies inside an active Cursed Dungeon (0.30 = +30% HP).", null, adminAttr));

            CursedDungeonMobDamageBonus = config.Bind(
                secDung,
                "CursedMobDamageBonus",
                0.30f,
                new ConfigDescription("Damage bonus granted to regular enemies inside an active Cursed Dungeon (0.30 = +30% damage).", null, adminAttr));

            CursedDungeonEnableFog = config.Bind(
                secDung,
                "EnableCursedFog",
                true,
                new ConfigDescription("Render thick colored mystical curse fog inside active Cursed Dungeons.", null, clientAttr));
        }
    }
}
