using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using CoarstalsEnemyOverhaul;
using CoarstalsRPClasses;

namespace NewDualAxesMod
{
    /// <summary>
    /// Registers 3 Meadows Relic Parts + 5 Lore-Friendly Magic Staves (Meadows, Black Forest, Swamp, Mountain, Plains)
    /// and implements all custom spell projectiles, Blood-Dominion mind control, Swamp Resistance-Strip Miasma Cloud,
    /// Mountain Fixed-Charge Hitscan Railgun, and Plains Blood-Harvest Totem.
    /// </summary>
    public static class MagicStavesModule
    {
        // Prefab IDs
        public const string PartCrystalPrefab = "PartWindCrystal";
        public const string PartHandlePrefab = "PartCarvedHandle";
        public const string PartBindingPrefab = "PartRunicBinding";

        public const string MeadowsStaffPrefab = "StaffMeadows";
        public const string BlackForestStaffPrefab = "StaffBlackForest";
        public const string SwampStaffPrefab = "StaffSwamp";
        public const string MountainStaffPrefab = "StaffMountain";
        public const string PlainsStaffPrefab = "StaffPlains";

        // Config entries
        public static ConfigEntry<float> MeadowsChestDropChance;

        public static ConfigEntry<float> MeadowsSlashDamage;
        public static ConfigEntry<float> MeadowsSlashPerLevel;
        public static ConfigEntry<float> MeadowsKnockbackForce;
        public static ConfigEntry<float> MeadowsEitrCost;
        public static ConfigEntry<string> MeadowsUpgradeRecipe;

        public static ConfigEntry<float> BlackForestEitrCost;
        public static ConfigEntry<float> BlackForestCleanseRadius;
        public static ConfigEntry<float> BlackForestBaseHeal;
        public static ConfigEntry<float> BlackForestExtraHealPerLevel;
        public static ConfigEntry<string> BlackForestRecipe;

        public static ConfigEntry<float> SwampEitrCost;
        public static ConfigEntry<float> SwampCloudDuration;
        public static ConfigEntry<float> SwampCloudRadius;
        public static ConfigEntry<float> SwampTickDamage;
        public static ConfigEntry<float> SwampTickDamagePerLevel;
        public static ConfigEntry<string> SwampStaffRecipe;

        public static ConfigEntry<float> MountainDrawEitrPerSec;
        public static ConfigEntry<float> MountainDrawEitrExtraPerLevel;
        public static ConfigEntry<float> MountainLightningDamage;
        public static ConfigEntry<float> MountainLightningPerLevel;
        public static ConfigEntry<string> MountainStaffRecipe;

        public static ConfigEntry<float> PlainsEitrCost;
        public static ConfigEntry<float> PlainsHealthPercent;
        public static ConfigEntry<float> PlainsTotemDuration;
        public static ConfigEntry<float> PlainsTotemExtraDurationPerLevel;
        public static ConfigEntry<string> PlainsStaffRecipe;

        private static bool s_registered;

        // Cached custom meshes, materials, icons
        internal static Mesh s_partCrystalMesh;
        internal static Material s_partCrystalMat;
        internal static Sprite s_partCrystalIcon;

        internal static Mesh s_partHandleMesh;
        internal static Material s_partHandleMat;
        internal static Sprite s_partHandleIcon;

        internal static Mesh s_partBindingMesh;
        internal static Material s_partBindingMat;
        internal static Sprite s_partBindingIcon;

        internal static Mesh s_meadowsMesh;
        internal static Material s_meadowsMat;
        internal static Sprite s_meadowsIcon;

        internal static Mesh s_bfMesh;
        internal static Material s_bfMat;
        internal static Sprite s_bfIcon;

        internal static Mesh s_swampMesh;
        internal static Material s_swampMat;
        internal static Sprite s_swampIcon;

        internal static Mesh s_mountainMesh;
        internal static Material s_mountainMat;
        internal static Sprite s_mountainIcon;

        internal static Mesh s_plainsMesh;
        internal static Material s_plainsMat;
        internal static Sprite s_plainsIcon;

        internal static Mesh s_plainsTotemMesh;
        internal static Material s_plainsTotemMat;
        internal static Sprite s_plainsTotemIcon;

        // Shared additive/alpha line & particle material + procedural soft smoke puff & rain streak textures/materials
        private static Material s_additiveFxMat;
        private static Texture2D s_softSmokePuffTex;
        private static Material s_softSmokeParticleMat;
        private static Texture2D s_rainStreakTex;
        private static Material s_bloodRainParticleMat;
        private static Texture2D s_windFangTex;
        private static Material s_windFangMat;
        private static Mesh s_crescentBladeMesh;

        // Runtime projectile & networked zone prefabs (kept inactive under hidden root & registered in ZNetScene for full co-op visibility!)
        private static Transform s_templateRoot;
        private static GameObject s_windBladeProjPrefab;
        private static GameObject s_purificationTriggerProjPrefab;
        private static GameObject s_swampMiasmaProjPrefab;
        private static GameObject s_mountainDummyProjPrefab;
        private static GameObject s_plainsTotemProjPrefab;
        internal static GameObject s_purificationWaveNetPrefab;
        internal static GameObject s_swampMiasmaCloudPrefab;
        internal static GameObject s_mountainBeamNetPrefab;
        internal static GameObject s_plainsTotemZonePrefab;

        // ZDO keys for co-op synchronization of spell zones and beams
        internal const string ZdoKeyDuration = "cwp_dur";
        internal const string ZdoKeyQuality = "cwp_qual";
        internal const string ZdoKeyBeamStart = "cwp_bstart";
        internal const string ZdoKeyBeamEnd = "cwp_bend";
        internal const string ZdoKeyWaveRadius = "cwp_wrad";
        internal const string RpcPurifyPlayer = "CWP_RPC_PurifyPlayer";

        // Vanilla effects borrowed from ObjectDB / ZNetScene for authentic Valheim audio-visual feedback
        internal static EffectList s_vanillaFireHitEffects;
        internal static EffectList s_vanillaLightningHitEffects;
        internal static EffectList s_vanillaPoisonBombHitEffects;
        internal static EffectList s_vanillaBloodHitEffects;

        // Active Swamp Miasma resistance-strip registry (Character -> expiry Time.time)
        internal static readonly Dictionary<Character, float> s_miasmaAfflicted = new Dictionary<Character, float>();

        // Active Plains Blood-Harvest Totem tracker per caster Player (strictly max 1 active totem)
        internal static readonly Dictionary<long, List<PlainsBloodHarvestTotemZone>> s_totemsByPlayer = new Dictionary<long, List<PlainsBloodHarvestTotemZone>>();

        // Active Plains Totem Field aura registries (Character -> expiry Time.time)
        internal static readonly Dictionary<Character, float> s_plainsSlowedEnemies = new Dictionary<Character, float>();
        internal static readonly Dictionary<Character, float> s_plainsBuffedPlayers = new Dictionary<Character, float>();

        // Runtime ScriptableObject StatusEffect (`SE_PlainsBloodFieldBuff`) shown in top-right HUD inside the Totem field
        internal static StatusEffect s_plainsBuffStatusEffect;
        internal static int s_plainsBuffStatusHash;

        public const float PlainsTotemZoneRadius = 12.0f; // Diameter = 24.0m (was 21.0m -> +3.0m diameter!)
        public const float MeadowsWindFlightRange = 21.0f;

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };

            const string secMeadows = "03. Weapon Pack - 4. Meadows Staff (Whispering Wind)";
            const string secBF = "03. Weapon Pack - 5. Black Forest Staff (Purification)";
            const string secSwamp = "03. Weapon Pack - 6. Swamp Staff (Restless Spirit Censer)";
            const string secMtn = "03. Weapon Pack - 7. Mountain Staff (Thor Tuning Railgun)";
            const string secPlains = "03. Weapon Pack - 8. Plains Staff (Blood Harvest)";

            MeadowsChestDropChance = config.Bind(
                secMeadows,
                "MeadowsChestPartChance",
                0.55f,
                new ConfigDescription("Chance (0.0 - 1.0) for each abandoned Meadows chest to contain 1-2 Whispering Wind Staff relic parts.", null, adminAttr)
            );
            MeadowsSlashDamage = config.Bind(secMeadows, "SlashDamage", 35f, new ConfigDescription("Base Wind Blade Slash damage at level 1.", null, adminAttr));
            MeadowsSlashPerLevel = config.Bind(secMeadows, "SlashDamagePerLevel", 7f, new ConfigDescription("Extra Slash damage per staff upgrade level.", null, adminAttr));
            MeadowsKnockbackForce = config.Bind(secMeadows, "KnockbackForce", 100f, new ConfigDescription("Exact Wind Blade knockback force (100).", null, adminAttr));
            MeadowsEitrCost = config.Bind(secMeadows, "EitrCost", 18f, new ConfigDescription("Eitr cost per Wind Blade cast.", null, adminAttr));
            MeadowsUpgradeRecipe = config.Bind(
                secMeadows,
                "Recipe",
                "PartWindCrystal:1:0,PartCarvedHandle:1:0,PartRunicBinding:1:0,Flint:0:6,DeerHide:0:4,Resin:0:8",
                new ConfigDescription("Workbench recipe (Prefab:CraftAmount:UpgradePerLevel).", null, adminAttr)
            );

            BlackForestEitrCost = config.Bind(secBF, "EitrCost", 50f, new ConfigDescription("Eitr cost per Purification cast (default 50 Eitr, 0% HP cost).", null, adminAttr));
            BlackForestCleanseRadius = config.Bind(secBF, "CleanseRadiusMeters", 10f, new ConfigDescription("Radius in meters around the caster within which all players have debuffs cleansed and receive healing.", null, adminAttr));
            BlackForestBaseHeal = config.Bind(secBF, "BaseHealAmount", 25f, new ConfigDescription("Health restored at staff quality level 1 (+25 HP).", null, adminAttr));
            BlackForestExtraHealPerLevel = config.Bind(secBF, "ExtraHealPerLevel", 10f, new ConfigDescription("Additional health restored per staff upgrade level (+10 HP/lvl => 25 / 35 / 45 / 55 HP).", null, adminAttr));
            BlackForestRecipe = config.Bind(
                secBF,
                "Recipe",
                "Bronze:10:5,SurtlingCore:4:2,AncientSeed:5:2,TrophyGreydwarfShaman:1:0",
                new ConfigDescription("Forge level 1 recipe.", null, adminAttr)
            );

            SwampEitrCost = config.Bind(secSwamp, "EitrCost", 80f, new ConfigDescription("Eitr cost per Spirit Miasma cast.", null, adminAttr));
            SwampCloudDuration = config.Bind(secSwamp, "CloudDurationSeconds", 8.0f, new ConfigDescription("Duration of the resistance-stripping Spirit Miasma cloud.", null, adminAttr));
            SwampCloudRadius = config.Bind(secSwamp, "CloudRadiusMeters", 7.2f, new ConfigDescription("Radius of the Spirit Miasma cloud in meters.", null, adminAttr));
            SwampTickDamage = config.Bind(secSwamp, "DamagePerSecond", 28f, new ConfigDescription("Base cloud DPS scalar at level 1 (Spirit portion reduced by 50%).", null, adminAttr));
            SwampTickDamagePerLevel = config.Bind(secSwamp, "DamagePerSecondPerLevel", 7f, new ConfigDescription("Additional cloud DPS scalar per staff level.", null, adminAttr));
            SwampStaffRecipe = config.Bind(
                secSwamp,
                "Recipe",
                "Iron:18:8,ElderBark:15:6,Guck:8:4,Chain:2:1,WitheredBone:4:2",
                new ConfigDescription("Forge level 2 recipe.", null, adminAttr)
            );

            MountainDrawEitrPerSec = config.Bind(secMtn, "DrawEitrDrainPerSecond", 10f, new ConfigDescription("Base continuous Eitr drain per second while charging the Lightning Railgun at level 1.", null, adminAttr));
            MountainDrawEitrExtraPerLevel = config.Bind(secMtn, "DrawEitrDrainExtraPerLevel", 2f, new ConfigDescription("Additional Eitr drain per second added for each staff upgrade level (+2/s per level => 10, 12, 14, 16...).", null, adminAttr));
            MountainLightningDamage = config.Bind(secMtn, "LightningDamage", 150f, new ConfigDescription("Piercing Lightning damage at level 1 (3.0s charge).", null, adminAttr));
            MountainLightningPerLevel = config.Bind(secMtn, "LightningDamagePerLevel", 150f, new ConfigDescription("Extra Lightning damage per upgrade level (+150/lvl => 150/300/450/600; +0.5s charge time per level).", null, adminAttr));
            MountainStaffRecipe = config.Bind(
                secMtn,
                "Recipe",
                "Silver:20:10,Crystal:8:4,Obsidian:15:6,TrophyWolf:2:1",
                new ConfigDescription("Forge level 3 recipe.", null, adminAttr)
            );

            PlainsEitrCost = config.Bind(secPlains, "EitrCost", 50f, new ConfigDescription("Eitr cost per Blood Totem deployment.", null, adminAttr));
            PlainsHealthPercent = config.Bind(secPlains, "HealthPercentageCost", 50f, new ConfigDescription("Current HP percentage cost per cast.", null, adminAttr));
            PlainsTotemDuration = config.Bind(secPlains, "TotemDurationSeconds", 15f, new ConfigDescription("Base lifetime of the deployed Blood Totem field in seconds at level 1.", null, adminAttr));
            PlainsTotemExtraDurationPerLevel = config.Bind(secPlains, "ExtraSecondsPerLevel", 5f, new ConfigDescription("Additional Blood Totem duration in seconds per upgrade level (+5s/lvl => 15/20/25/30s).", null, adminAttr));
            PlainsStaffRecipe = config.Bind(
                secPlains,
                "Recipe",
                "BlackMetal:25:10,GoblinTotem:1:1,LinenThread:15:6,LoxPelt:4:2",
                new ConfigDescription("Forge level 4 recipe.", null, adminAttr)
            );
        }

        internal static bool IsRussianLanguageSelected()
        {
            return Localization.instance != null && Localization.instance.GetSelectedLanguage() == "Russian";
        }

        public static void RegisterLocalizations()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string eng = "English";
            loc.AddTranslation(ref eng, new Dictionary<string, string>
            {
                { "item_part_windcrystal", "Wind Crystal Shard" },
                { "item_part_windcrystal_desc", "A sky-blue quartz shard humming with restless gusts. Found in abandoned Meadows dwellings; one of three parts of the Staff of Whispering Wind." },
                { "item_part_carvedhandle", "Carved Ashwood Handle" },
                { "item_part_carvedhandle_desc", "An ancient ashwood staff grip wrapped in weathered deer leather. Found in abandoned Meadows chests." },
                { "item_part_runicbinding", "Runic Antler Binding" },
                { "item_part_runicbinding_desc", "A braided leather binding set with Eikthyr-carved bone prongs designed to cradle a wind crystal." },

                { "item_staff_meadows", "Staff of Whispering Wind" },
                { "item_staff_meadows_desc", "Assembled from forgotten Meadows relics. Releases piercing crescent blades of compressed wind with powerful knockback (21m range)." },
                { "item_staff_blackforest", "Staff of Purification" },
                { "item_staff_blackforest_desc", "A shamanic Black Forest staff crowned with a living emerald heart. Releases a radiant 10m wave of forest vitality that cleanses all combat injuries and negative status effects before restoring health (+25/35/45/55 HP by level)." },
                { "item_staff_swamp", "Censer of Restless Spirit" },
                { "item_staff_swamp_desc", "A rusted iron censer forged from crypt chains and bog roots. Hurls spirit miasma that detonates into an 8-second cloud dealing damage and stripping ALL enemy damage resistances." },
                { "item_staff_mountain", "Thor's Thunder Tuning Staff" },
                { "item_staff_mountain_desc", "A silver-obsidian tuning fork etched with Thor's knot. Hold to charge and release a piercing hitscan lightning railgun beam through foes, trees, and structures." },
                { "item_staff_mountain_eitrhold", "Eitr drain" },
                { "item_staff_mountain_chargetime", "Charge time" },
                { "item_staff_plains", "Staff of Blood Harvest" },
                { "item_staff_plains_desc", "A Black Metal ritual staff that deploys a Blood Ritual Totem (15/20/25/30s by level, max 1 active totem) summoning a crimson rainstorm. Inside the blood rain, enemies move 40% slower while players gain +10% move speed, +50% HP & Eitr regen, and -30% to all stamina/eitr/health action costs." },

                { "se_plains_bloodfield_name", "Blood Totem Aura" },
                { "se_plains_bloodfield_tooltip", "Movement speed +10%\nHealth regeneration +50%\nEitr regeneration +50%\nAll stamina, eitr & health action costs -30%\nEnemies in field slowed by 40%" },

                { "msg_bf_cleansed", "Purified" },
                { "msg_mtn_fizzle", "Charge aborted! Hold until 100% charged" }
            });

            string rus = "Russian";
            loc.AddTranslation(ref rus, new Dictionary<string, string>
            {
                { "item_part_windcrystal", "Осколок Кристалла Ветра" },
                { "item_part_windcrystal_desc", "Небесно-голубой осколок кварца, в котором поёт ветер Лугов. Находится в сундуках заброшенных построек — одна из трёх частей Посоха Шепчущего Ветра." },
                { "item_part_carvedhandle", "Резная Ясеневая Рукоять" },
                { "item_part_carvedhandle_desc", "Древнее древко из ясеня, обмотанное выделанной оленьей кожей. Часть забытого посоха Лугов." },
                { "item_part_runicbinding", "Руническая Перевязь из Рога" },
                { "item_part_runicbinding_desc", "Плетёное крепление с роговыми зубцами и рунами ветра, удерживающее магический кристалл." },

                { "item_staff_meadows", "Посох Шепчущего Ветра" },
                { "item_staff_meadows_desc", "Собранный из древних реликвий Лугов посох магии стихий. Выпускает мощно отбрасывающие пронзающие серпы сжатого ветра на дальность 21 м." },
                { "item_staff_blackforest", "Посох Очищения" },
                { "item_staff_blackforest_desc", "Шаманский посох Чёрного Леса с живым изумрудным кристаллом на магии крови. Выпускает вокруг заклинателя целебную волну радиусом 10 м, снимающую все боевые травмы и негативные эффекты и восстанавливающую здоровье (+25/35/45/55 HP по уровню)." },
                { "item_staff_swamp", "Кадило Беспокойного Духа" },
                { "item_staff_swamp_desc", "Болотное кадило магии стихий на ржавых цепях. Взрывает духовную миазму, оставляя на 8 секунд широкое облако дыма, наносящее урон и полностью снимающее все сопротивления и иммунитеты к урону у врагов." },
                { "item_staff_mountain", "Громовой Камертон Тора" },
                { "item_staff_mountain_desc", "Серебряно-обсидиановый рейлган магии стихий со знаком Тора. При полной зарядке выпускает мощный луч молнии навылет сквозь врагов, деревья и постройки." },
                { "item_staff_mountain_eitrhold", "Расход эйтра" },
                { "item_staff_mountain_chargetime", "Время зарядки" },
                { "item_staff_plains", "Посох Кровавой Жатвы" },
                { "item_staff_plains_desc", "Ритуальный посох Равнин на магии крови (15/20/25/30 сек. по уровню, лимит: 1 тотем). Призывает кровавый дождь в области тотема: замедляет врагов на 40%, ускоряет игроков на 10%, снижает все затраты бега/атак/блока и др. на 30% и повышает регенерацию здоровья и эйтра на 50%." },

                { "se_plains_bloodfield_name", "Поле Кровавого Тотема" },
                { "se_plains_bloodfield_tooltip", "Скорость передвижения: +10%\nВосстановление здоровья: +50%\nВосстановление эйтра: +50%\nВсе затраты выносливости, эйтра и здоровья: -30%\nВраги в поле замедлены на 40%" },

                { "msg_bf_cleansed", "Очищение" },
                { "msg_mtn_fizzle", "Заряд сорвался! Удерживайте до полной зарядки" }
            });
        }

        public static void RegisterAllStavesAndParts()
        {
            if (!NewDualAxesPlugin.IsActive) return;
            try
            {
                if (s_registered && ItemManager.Instance.GetItem(MeadowsStaffPrefab) != null)
                {
                    InjectMeadowsChestDrops();
                    return;
                }

                Shader shader = NewDualAxesPlugin.FindValheimStandardShader();
                LoadAllStaffAssets(shader);
                EnsureSpellProjectileTemplates();

                // 1) Register 3 Meadows Relic Parts (found in Meadows chests)
                RegisterRelicPart(
                    PartCrystalPrefab,
                    "$item_part_windcrystal",
                    "$item_part_windcrystal_desc",
                    s_partCrystalMesh,
                    s_partCrystalMat,
                    s_partCrystalIcon,
                    0.26f,
                    0.5f
                );
                RegisterRelicPart(
                    PartHandlePrefab,
                    "$item_part_carvedhandle",
                    "$item_part_carvedhandle_desc",
                    s_partHandleMesh,
                    s_partHandleMat,
                    s_partHandleIcon,
                    0.58f,
                    1.0f
                );
                RegisterRelicPart(
                    PartBindingPrefab,
                    "$item_part_runicbinding",
                    "$item_part_runicbinding_desc",
                    s_partBindingMesh,
                    s_partBindingMat,
                    s_partBindingIcon,
                    0.22f,
                    0.5f
                );

                // 2) Register 5 Magic Staves
                string staffDonor = FindStaffDonorName();
                if (string.IsNullOrEmpty(staffDonor))
                {
                    if (NewDualAxesPlugin.ModLogger != null)
                        NewDualAxesPlugin.ModLogger.LogError("[MagicStaves] Could not find vanilla StaffFireball/StaffIceShards donor prefab!");
                    return;
                }

                // Meadows Staff — Whispering Wind (Elemental Magic, Workbench lvl 1)
                RegisterMagicStaff(
                    prefabName: MeadowsStaffPrefab,
                    donorName: staffDonor,
                    nameToken: "$item_staff_meadows",
                    descToken: "$item_staff_meadows_desc",
                    icon: s_meadowsIcon,
                    mesh: s_meadowsMesh,
                    mat: s_meadowsMat,
                    targetLengthMeters: 2.13f,
                    glowColor: new Color(0.40f, 0.92f, 1.0f, 1f),
                    glowYOffset: 1.00f,
                    skillType: Skills.SkillType.ElementalMagic,
                    station: CraftingStations.Workbench,
                    minStationLevel: 1,
                    recipeStr: MeadowsUpgradeRecipe.Value,
                    eitrCost: MeadowsEitrCost.Value,
                    drawEitrPerSec: 0f,
                    healthPctCost: 0f,
                    projectilePrefab: s_windBladeProjPrefab,
                    projectileVel: 25f,
                    slashDmg: MeadowsSlashDamage.Value,
                    slashPerLvl: MeadowsSlashPerLevel.Value,
                    lightningDmg: 0f,
                    lightningPerLvl: 0f,
                    spiritDmg: 0f,
                    spiritPerLvl: 0f,
                    poisonDmg: 0f,
                    poisonPerLvl: 0f,
                    isBowDrawRailgun: false,
                    maxQuality: 4,
                    attackForce: MeadowsKnockbackForce.Value,
                    toolTier: 0,
                    blockPower: 20f,
                    maxDurability: 160f
                );

                // Black Forest Staff — Staff of Purification (Blood Magic, Forge lvl 1, AoE Cleanse + Heal Wave)
                RegisterMagicStaff(
                    prefabName: BlackForestStaffPrefab,
                    donorName: staffDonor,
                    nameToken: "$item_staff_blackforest",
                    descToken: "$item_staff_blackforest_desc",
                    icon: s_bfIcon,
                    mesh: s_bfMesh,
                    mat: s_bfMat,
                    targetLengthMeters: 2.16f,
                    glowColor: new Color(0.24f, 0.96f, 0.50f, 1f),
                    glowYOffset: 1.02f,
                    skillType: Skills.SkillType.BloodMagic,
                    station: CraftingStations.Forge,
                    minStationLevel: 1,
                    recipeStr: BlackForestRecipe.Value,
                    eitrCost: BlackForestEitrCost.Value,
                    drawEitrPerSec: 0f,
                    healthPctCost: 0f,
                    projectilePrefab: s_purificationTriggerProjPrefab,
                    projectileVel: 1f,
                    slashDmg: 0f,
                    slashPerLvl: 0f,
                    lightningDmg: 0f,
                    lightningPerLvl: 0f,
                    spiritDmg: 0f,
                    spiritPerLvl: 0f,
                    poisonDmg: 0f,
                    poisonPerLvl: 0f,
                    isBowDrawRailgun: false,
                    maxQuality: 4,
                    attackForce: 0f,
                    toolTier: 1,
                    blockPower: 28f,
                    maxDurability: 180f
                );
                ApplyStaffShieldRadialCastAnimation(BlackForestStaffPrefab);

                // Swamp Staff — Censer of Restless Spirit (Elemental Magic, Forge lvl 2)
                RegisterMagicStaff(
                    prefabName: SwampStaffPrefab,
                    donorName: staffDonor,
                    nameToken: "$item_staff_swamp",
                    descToken: "$item_staff_swamp_desc",
                    icon: s_swampIcon,
                    mesh: s_swampMesh,
                    mat: s_swampMat,
                    targetLengthMeters: 2.20f,
                    glowColor: new Color(0.22f, 1.0f, 0.58f, 1f),
                    glowYOffset: 0.90f,
                    skillType: Skills.SkillType.ElementalMagic,
                    station: CraftingStations.Forge,
                    minStationLevel: 2,
                    recipeStr: SwampStaffRecipe.Value,
                    eitrCost: SwampEitrCost.Value,
                    drawEitrPerSec: 0f,
                    healthPctCost: 0f,
                    projectilePrefab: s_swampMiasmaProjPrefab,
                    projectileVel: 20f,
                    slashDmg: 0f,
                    slashPerLvl: 0f,
                    lightningDmg: 0f,
                    lightningPerLvl: 0f,
                    spiritDmg: SwampTickDamage.Value * 0.275f,
                    spiritPerLvl: SwampTickDamagePerLevel.Value * 0.275f,
                    poisonDmg: SwampTickDamage.Value * 0.45f,
                    poisonPerLvl: SwampTickDamagePerLevel.Value * 0.45f,
                    isBowDrawRailgun: false,
                    maxQuality: 4,
                    attackForce: 25f,
                    toolTier: 2,
                    blockPower: 34f,
                    maxDurability: 200f
                );

                // Mountain Staff — Thor's Thunder Tuning Railgun (Elemental Magic, Forge lvl 3, Dundr-style Hold & Charge with 10/s Eitr draw drain)
                string mountainDonor = FindDundrDonorName(staffDonor);
                RegisterMagicStaff(
                    prefabName: MountainStaffPrefab,
                    donorName: mountainDonor,
                    nameToken: "$item_staff_mountain",
                    descToken: "$item_staff_mountain_desc",
                    icon: s_mountainIcon,
                    mesh: s_mountainMesh,
                    mat: s_mountainMat,
                    targetLengthMeters: 2.28f,
                    glowColor: new Color(0.35f, 0.80f, 1.0f, 1f),
                    glowYOffset: 1.08f,
                    skillType: Skills.SkillType.ElementalMagic,
                    station: CraftingStations.Forge,
                    minStationLevel: 3,
                    recipeStr: MountainStaffRecipe.Value,
                    eitrCost: 0f,
                    drawEitrPerSec: MountainDrawEitrPerSec.Value,
                    healthPctCost: 0f,
                    projectilePrefab: s_mountainDummyProjPrefab,
                    projectileVel: 60f,
                    slashDmg: 0f,
                    slashPerLvl: 0f,
                    lightningDmg: MountainLightningDamage.Value,
                    lightningPerLvl: MountainLightningPerLevel.Value,
                    spiritDmg: 0f,
                    spiritPerLvl: 0f,
                    poisonDmg: 0f,
                    poisonPerLvl: 0f,
                    isBowDrawRailgun: true,
                    maxQuality: 4,
                    attackForce: 55f,
                    toolTier: 3,
                    blockPower: 40f,
                    maxDurability: 220f
                );

                // Plains Staff — Blood Ritual Totem Staff (Blood Magic, Forge lvl 4, Upgradable +5s/lvl, Max 1 Totem)
                RegisterMagicStaff(
                    prefabName: PlainsStaffPrefab,
                    donorName: staffDonor,
                    nameToken: "$item_staff_plains",
                    descToken: "$item_staff_plains_desc",
                    icon: s_plainsIcon,
                    mesh: s_plainsMesh,
                    mat: s_plainsMat,
                    targetLengthMeters: 2.26f,
                    glowColor: new Color(1.0f, 0.12f, 0.16f, 1f),
                    glowYOffset: 1.05f,
                    skillType: Skills.SkillType.BloodMagic,
                    station: CraftingStations.Forge,
                    minStationLevel: 4,
                    recipeStr: PlainsStaffRecipe.Value,
                    eitrCost: PlainsEitrCost.Value,
                    drawEitrPerSec: 0f,
                    healthPctCost: PlainsHealthPercent.Value,
                    projectilePrefab: s_plainsTotemProjPrefab,
                    projectileVel: 24f,
                    slashDmg: 0f,
                    slashPerLvl: 0f,
                    lightningDmg: 0f,
                    lightningPerLvl: 0f,
                    spiritDmg: 0f,
                    spiritPerLvl: 0f,
                    poisonDmg: 0f,
                    poisonPerLvl: 0f,
                    isBowDrawRailgun: false,
                    maxQuality: 4,
                    attackForce: 25f,
                    toolTier: 4,
                    blockPower: 48f,
                    maxDurability: 240f
                );

                InjectMeadowsChestDrops();

                s_registered = true;
                if (NewDualAxesPlugin.ModLogger != null)
                {
                    NewDualAxesPlugin.ModLogger.LogInfo("[MagicStaves] Registered 3 Meadows relic parts + 5 Magic Staves (v1.0.0)!");
                }
            }
            catch (Exception ex)
            {
                if (NewDualAxesPlugin.ModLogger != null)
                {
                    NewDualAxesPlugin.ModLogger.LogError("[MagicStaves] Failed to register staves: " + ex);
                }
            }
        }

        private static string FindStaffDonorName()
        {
            string[] candidates = new string[] { "StaffFireball", "StaffIceShards", "StaffClusterbomb", "StaffSkeleton" };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (PrefabManager.Instance.GetPrefab(candidates[i]) != null)
                {
                    return candidates[i];
                }
            }
            return null;
        }

        private static string FindDundrDonorName(string fallbackStaffDonor)
        {
            // "StaffLightning" IS vanilla Dundr ($item_staff_lightning — crossbow-style hold & "recharge" animation)!
            string[] candidates = new string[] { "StaffLightning", "CrossbowArbalest", "CrossbowRipper" };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (PrefabManager.Instance.GetPrefab(candidates[i]) != null)
                {
                    return candidates[i];
                }
            }
            return fallbackStaffDonor;
        }

        private static void LoadAllStaffAssets(Shader shader)
        {
            if (s_meadowsMesh != null) return;

            // 3 Meadows Parts
            s_partCrystalMesh = NewDualAxesPlugin.LoadEmbeddedMesh("Part_WindCrystal.bin.gz", "Part_WindCrystal");
            s_partCrystalIcon = NewDualAxesPlugin.LoadEmbeddedSprite("Part_WindCrystal_Icon.png");
            s_partCrystalMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_Part_WindCrystal",
                "Part_WindCrystal_Albedo.png", "Part_WindCrystal_Roughness.png",
                "Part_WindCrystal_Normal.png", "Part_WindCrystal_Emission.png",
                0.25f, new Color(0.55f, 0.95f, 1.0f, 1f) * 2.2f
            );

            s_partHandleMesh = NewDualAxesPlugin.LoadEmbeddedMesh("Part_CarvedHandle.bin.gz", "Part_CarvedHandle");
            s_partHandleIcon = NewDualAxesPlugin.LoadEmbeddedSprite("Part_CarvedHandle_Icon.png");
            s_partHandleMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_Part_CarvedHandle",
                "Part_CarvedHandle_Albedo.png", "Part_CarvedHandle_Roughness.png",
                "Part_CarvedHandle_Normal.png", "Part_CarvedHandle_Emission.png",
                0.15f, Color.white
            );

            s_partBindingMesh = NewDualAxesPlugin.LoadEmbeddedMesh("Part_RunicBinding.bin.gz", "Part_RunicBinding");
            s_partBindingIcon = NewDualAxesPlugin.LoadEmbeddedSprite("Part_RunicBinding_Icon.png");
            s_partBindingMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_Part_RunicBinding",
                "Part_RunicBinding_Albedo.png", "Part_RunicBinding_Roughness.png",
                "Part_RunicBinding_Normal.png", "Part_RunicBinding_Emission.png",
                0.20f, new Color(0.45f, 0.90f, 1.0f, 1f) * 1.5f
            );

            // 5 Staves + Totem
            s_meadowsMesh = NewDualAxesPlugin.LoadEmbeddedMesh("MeadowsStaff.bin.gz", "MeadowsStaff");
            s_meadowsIcon = NewDualAxesPlugin.LoadEmbeddedSprite("MeadowsStaff_Icon.png");
            s_meadowsMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_MeadowsStaff",
                "MeadowsStaff_Albedo.png", "MeadowsStaff_Roughness.png",
                "MeadowsStaff_Normal.png", "MeadowsStaff_Emission.png",
                0.25f, Color.white * 2.0f
            );

            s_bfMesh = NewDualAxesPlugin.LoadEmbeddedMesh("BlackForestStaff.bin.gz", "BlackForestStaff");
            BuildPurificationStaffMaterialAndIcon(shader, out s_bfMat, out s_bfIcon);

            s_swampMesh = NewDualAxesPlugin.LoadEmbeddedMesh("SwampStaff.bin.gz", "SwampStaff");
            s_swampIcon = NewDualAxesPlugin.LoadEmbeddedSprite("SwampStaff_Icon.png");
            s_swampMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_SwampStaff",
                "SwampStaff_Albedo.png", "SwampStaff_Roughness.png",
                "SwampStaff_Normal.png", "SwampStaff_Emission.png",
                0.68f, Color.white * 2.2f
            );

            s_mountainMesh = NewDualAxesPlugin.LoadEmbeddedMesh("MountainStaff.bin.gz", "MountainStaff");
            s_mountainIcon = NewDualAxesPlugin.LoadEmbeddedSprite("MountainStaff_Icon.png");
            s_mountainMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_MountainStaff",
                "MountainStaff_Albedo.png", "MountainStaff_Roughness.png",
                "MountainStaff_Normal.png", "MountainStaff_Emission.png",
                0.85f, Color.white * 2.4f
            );

            s_plainsMesh = NewDualAxesPlugin.LoadEmbeddedMesh("PlainsStaff.bin.gz", "PlainsStaff");
            s_plainsIcon = NewDualAxesPlugin.LoadEmbeddedSprite("PlainsStaff_Icon.png");
            s_plainsMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_PlainsStaff",
                "PlainsStaff_Albedo.png", "PlainsStaff_Roughness.png",
                "PlainsStaff_Normal.png", "PlainsStaff_Emission.png",
                0.80f, Color.white * 2.3f
            );

            s_plainsTotemMesh = NewDualAxesPlugin.LoadEmbeddedMesh("PlainsHarvestTotem.bin.gz", "PlainsHarvestTotem");
            s_plainsTotemIcon = NewDualAxesPlugin.LoadEmbeddedSprite("PlainsHarvestTotem_Icon.png");
            s_plainsTotemMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader, "Mat_PlainsHarvestTotem",
                "PlainsHarvestTotem_Albedo.png", "PlainsHarvestTotem_Roughness.png",
                "PlainsHarvestTotem_Normal.png", "PlainsHarvestTotem_Emission.png",
                0.75f, Color.white * 2.5f
            );
        }

        internal static Material GetAdditiveFxMaterial()
        {
            if (s_additiveFxMat != null) return s_additiveFxMat;
            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = NewDualAxesPlugin.FindValheimStandardShader();

            s_additiveFxMat = new Material(sh);
            s_additiveFxMat.name = "Mat_MagicStaves_FX";
            if (s_additiveFxMat.HasProperty("_Mode")) s_additiveFxMat.SetFloat("_Mode", 4f);
            if (s_additiveFxMat.HasProperty("_SrcBlend")) s_additiveFxMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (s_additiveFxMat.HasProperty("_DstBlend")) s_additiveFxMat.SetInt("_DstBlend", (int)BlendMode.One);
            if (s_additiveFxMat.HasProperty("_ZWrite")) s_additiveFxMat.SetInt("_ZWrite", 0);
            s_additiveFxMat.renderQueue = 3000;
            return s_additiveFxMat;
        }

        internal static Texture2D GetSoftSmokePuffTexture()
        {
            if (s_softSmokePuffTex != null) return s_softSmokePuffTex;

            const int size = 128;
            s_softSmokePuffTex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            s_softSmokePuffTex.name = "Tex_MagicStaves_SoftSmokePuff";
            s_softSmokePuffTex.wrapMode = TextureWrapMode.Clamp;
            s_softSmokePuffTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            float inv = 1f / (size - 1);

            for (int y = 0; y < size; y++)
            {
                float ny = (y * inv) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x * inv) * 2f - 1f;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    if (dist >= 1f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0f);
                        continue;
                    }

                    // Multi-octave organic billowing smoke edge & interior turbulence so puffs look like real mist, ZERO square edges!
                    float n1 = Mathf.PerlinNoise(nx * 2.1f + 4.3f, ny * 2.1f + 7.9f);
                    float n2 = Mathf.PerlinNoise(nx * 4.4f + 13.7f, ny * 4.4f + 2.5f);
                    float n3 = Mathf.PerlinNoise(nx * 8.5f + 21.1f, ny * 8.5f + 16.4f);
                    float cloudNoise = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f; // ~[0.15..0.85]

                    // Perturb radial distance slightly near outer rim while forcing strict 0 alpha well before UV border (dist >= 0.98)
                    float rimWarp = (cloudNoise - 0.5f) * 0.26f * Mathf.Clamp01(dist * 1.35f);
                    float warpedDist = Mathf.Clamp01(dist + rimWarp);
                    float edgeMask = 1f - Mathf.SmoothStep(0.0f, 0.96f, dist);
                    float radial = 1f - warpedDist;
                    float radialSoft = radial * radial * (3f - 2f * radial); // Smooth cubic falloff

                    float alpha = Mathf.Clamp01(radialSoft * edgeMask * Mathf.Lerp(0.62f, 1.22f, cloudNoise));
                    float lum = Mathf.Lerp(0.84f, 1.0f, cloudNoise);
                    pixels[y * size + x] = new Color(lum, 1.0f, lum * 0.96f, alpha);
                }
            }

            s_softSmokePuffTex.SetPixels(pixels);
            s_softSmokePuffTex.Apply(true, false);
            return s_softSmokePuffTex;
        }

        internal static Material GetSoftSmokeParticleMaterial()
        {
            if (s_softSmokeParticleMat != null) return s_softSmokeParticleMat;

            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = NewDualAxesPlugin.FindValheimStandardShader();

            s_softSmokeParticleMat = new Material(sh);
            s_softSmokeParticleMat.name = "Mat_MagicStaves_SoftSmoke";
            Texture2D puffTex = GetSoftSmokePuffTexture();
            s_softSmokeParticleMat.mainTexture = puffTex;
            if (s_softSmokeParticleMat.HasProperty("_MainTex")) s_softSmokeParticleMat.SetTexture("_MainTex", puffTex);

            // Alpha-blended (`SrcAlpha`, `OneMinusSrcAlpha`) instead of Additive so dense smoke stays lush green without blowing out to white squares!
            if (s_softSmokeParticleMat.HasProperty("_Mode")) s_softSmokeParticleMat.SetFloat("_Mode", 2f); // Fade / Alpha Blend in Particles/Standard Unlit
            if (s_softSmokeParticleMat.HasProperty("_ColorMode")) s_softSmokeParticleMat.SetFloat("_ColorMode", 0f); // Multiply vertex color
            if (s_softSmokeParticleMat.HasProperty("_SrcBlend")) s_softSmokeParticleMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (s_softSmokeParticleMat.HasProperty("_DstBlend")) s_softSmokeParticleMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (s_softSmokeParticleMat.HasProperty("_ZWrite")) s_softSmokeParticleMat.SetInt("_ZWrite", 0);
            if (s_softSmokeParticleMat.HasProperty("_Cull")) s_softSmokeParticleMat.SetInt("_Cull", (int)CullMode.Off);
            if (s_softSmokeParticleMat.HasProperty("_SoftParticlesEnabled"))
            {
                s_softSmokeParticleMat.SetFloat("_SoftParticlesEnabled", 1f);
                s_softSmokeParticleMat.EnableKeyword("_SOFTPARTICLES_ON");
            }
            s_softSmokeParticleMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            s_softSmokeParticleMat.DisableKeyword("_ALPHATEST_ON");
            s_softSmokeParticleMat.EnableKeyword("_ALPHABLEND_ON");
            s_softSmokeParticleMat.color = Color.white;
            if (s_softSmokeParticleMat.HasProperty("_Color")) s_softSmokeParticleMat.SetColor("_Color", Color.white);
            if (s_softSmokeParticleMat.HasProperty("_TintColor")) s_softSmokeParticleMat.SetColor("_TintColor", new Color(1f, 1f, 1f, 1f));
            s_softSmokeParticleMat.renderQueue = 3000;
            return s_softSmokeParticleMat;
        }

        internal static Texture2D GetRainStreakTexture()
        {
            if (s_rainStreakTex != null) return s_rainStreakTex;

            // Symmetric 64x64 high-opacity capsule/droplet texture so ParticleSystemRenderMode.Stretch
            // ALWAYS has a solid 100% opaque center along both U and V axes and smooth 0-alpha edges on all 4 borders!
            const int size = 64;
            s_rainStreakTex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            s_rainStreakTex.name = "Tex_MagicStaves_BloodRainStreak";
            s_rainStreakTex.wrapMode = TextureWrapMode.Clamp;
            s_rainStreakTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = ((float)y / (size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((float)x / (size - 1)) * 2f - 1f;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    if (dist >= 0.96f)
                    {
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0f);
                        continue;
                    }

                    // Smooth soft raindrop core (alpha peaks smoothly at center and tapers off cleanly, no harsh thick blob)
                    float falloff = 1.0f - Mathf.SmoothStep(0.08f, 0.94f, dist);
                    float a = Mathf.Clamp01(falloff * falloff);
                    float coreBrightness = Mathf.Lerp(0.82f, 1.0f, falloff);
                    pixels[y * size + x] = new Color(coreBrightness, coreBrightness, coreBrightness, a);
                }
            }

            s_rainStreakTex.SetPixels(pixels);
            s_rainStreakTex.Apply(true, false);
            return s_rainStreakTex;
        }

        internal static Material GetBloodRainParticleMaterial()
        {
            if (s_bloodRainParticleMat != null) return s_bloodRainParticleMat;

            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = NewDualAxesPlugin.FindValheimStandardShader();

            s_bloodRainParticleMat = new Material(sh);
            s_bloodRainParticleMat.name = "Mat_MagicStaves_BloodRain";
            Texture2D streakTex = GetRainStreakTexture();
            s_bloodRainParticleMat.mainTexture = streakTex;
            if (s_bloodRainParticleMat.HasProperty("_MainTex")) s_bloodRainParticleMat.SetTexture("_MainTex", streakTex);

            if (s_bloodRainParticleMat.HasProperty("_Mode")) s_bloodRainParticleMat.SetFloat("_Mode", 2f); // Alpha Blend
            if (s_bloodRainParticleMat.HasProperty("_ColorMode")) s_bloodRainParticleMat.SetFloat("_ColorMode", 0f);
            if (s_bloodRainParticleMat.HasProperty("_SrcBlend")) s_bloodRainParticleMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (s_bloodRainParticleMat.HasProperty("_DstBlend")) s_bloodRainParticleMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (s_bloodRainParticleMat.HasProperty("_ZWrite")) s_bloodRainParticleMat.SetInt("_ZWrite", 0);
            if (s_bloodRainParticleMat.HasProperty("_Cull")) s_bloodRainParticleMat.SetInt("_Cull", (int)CullMode.Off);
            s_bloodRainParticleMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            s_bloodRainParticleMat.DisableKeyword("_ALPHATEST_ON");
            s_bloodRainParticleMat.EnableKeyword("_ALPHABLEND_ON");
            s_bloodRainParticleMat.color = Color.white;
            if (s_bloodRainParticleMat.HasProperty("_Color")) s_bloodRainParticleMat.SetColor("_Color", Color.white);
            if (s_bloodRainParticleMat.HasProperty("_TintColor")) s_bloodRainParticleMat.SetColor("_TintColor", Color.white);
            s_bloodRainParticleMat.renderQueue = 3000;
            return s_bloodRainParticleMat;
        }

        internal static Texture2D GetWindFangGradientTexture()
        {
            if (s_windFangTex != null) return s_windFangTex;

            // U (x): 0..1 from left wingtip to right wingtip (tapers smoothly to 0 at both tips)
            // V (y): 0 = trailing inner wake, 1 = razor-sharp leading forward arc edge!
            const int w = 128;
            const int h = 64;
            s_windFangTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            s_windFangTex.name = "Tex_MagicStaves_WindFangArc";
            s_windFangTex.wrapMode = TextureWrapMode.Clamp;
            s_windFangTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                // Bright razor leading edge near v = 0.78..0.95, smooth wispy wake fading toward v = 0, soft anti-aliased front lip at v = 1
                float frontLip = 1.0f - Mathf.SmoothStep(0.92f, 1.0f, v);
                float wakeFade = Mathf.Pow(Mathf.SmoothStep(0.0f, 0.86f, v), 1.35f);
                float profileAlpha = wakeFade * frontLip;
                float edgeCore = Mathf.SmoothStep(0.60f, 0.90f, v) * frontLip;

                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float span = Mathf.Sin(u * Mathf.PI); // 0 at tips, 1 at center of the arc
                    float tipFade = Mathf.Pow(Mathf.Max(0f, span), 0.55f);

                    // Subtle wind-streak wisps along the trailing wake
                    float wisp = 0.85f + 0.15f * Mathf.Sin(u * 28.0f + v * 6.0f);
                    float a = Mathf.Clamp01(profileAlpha * tipFade * wisp * 1.15f);

                    Color c = Color.Lerp(
                        new Color(0.28f, 0.86f, 1.00f, 1f),
                        new Color(0.92f, 0.99f, 1.00f, 1f),
                        edgeCore * Mathf.Pow(Mathf.Max(0f, span), 0.35f)
                    );
                    c.a = a;
                    pixels[y * w + x] = c;
                }
            }

            s_windFangTex.SetPixels(pixels);
            s_windFangTex.Apply(true, false);
            return s_windFangTex;
        }

        internal static Material GetWindFangMaterial()
        {
            if (s_windFangMat != null) return s_windFangMat;

            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = NewDualAxesPlugin.FindValheimStandardShader();

            s_windFangMat = new Material(sh);
            s_windFangMat.name = "Mat_MagicStaves_WindFang";
            Texture2D tex = GetWindFangGradientTexture();
            s_windFangMat.mainTexture = tex;
            if (s_windFangMat.HasProperty("_MainTex")) s_windFangMat.SetTexture("_MainTex", tex);

            if (s_windFangMat.HasProperty("_Mode")) s_windFangMat.SetFloat("_Mode", 4f); // Additive glow
            if (s_windFangMat.HasProperty("_ColorMode")) s_windFangMat.SetFloat("_ColorMode", 0f);
            if (s_windFangMat.HasProperty("_SrcBlend")) s_windFangMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (s_windFangMat.HasProperty("_DstBlend")) s_windFangMat.SetInt("_DstBlend", (int)BlendMode.One);
            if (s_windFangMat.HasProperty("_ZWrite")) s_windFangMat.SetInt("_ZWrite", 0);
            if (s_windFangMat.HasProperty("_Cull")) s_windFangMat.SetInt("_Cull", (int)CullMode.Off);
            s_windFangMat.color = Color.white;
            if (s_windFangMat.HasProperty("_Color")) s_windFangMat.SetColor("_Color", Color.white);
            if (s_windFangMat.HasProperty("_TintColor")) s_windFangMat.SetColor("_TintColor", Color.white);
            s_windFangMat.renderQueue = 3000;
            return s_windFangMat;
        }

        internal static Mesh GetCrescentBladeMesh()
        {
            if (s_crescentBladeMesh != null) return s_crescentBladeMesh;

            // Single sleek horizontal "Wind Fang" crescent arc in XZ plane (+Z = forward apex, tips swept smoothly backward,
            // thickest at center and tapering to razor points at both wingtips, with soft vertical airfoil volume so it is
            // clearly visible even dead-on from the player's camera behind the projectile!)
            int segs = 32;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float outerR = 1.22f;
            float maxArcAngle = 1.08f; // ~124 deg total horizontal crescent arc -> ~2.15m wingspan

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float ang = Mathf.Lerp(-maxArcAngle, maxArcAngle, t);
                float span = Mathf.Sin(t * Mathf.PI); // 0 at tips, 1 at forward center
                float chord = 0.52f * Mathf.Pow(Mathf.Max(0f, span), 0.78f); // Tapers cleanly to 0.0 at both tips!
                float halfThickY = 0.065f * Mathf.Pow(Mathf.Max(0f, span), 0.70f); // Visible 3D crest from camera angle

                float sinA = Mathf.Sin(ang);
                float cosA = Mathf.Cos(ang);
                float zCenterShift = -0.82f;

                float rFront = outerR;
                float rCrest = outerR - chord * 0.26f;
                float rBack = outerR - chord;

                Vector3 pFront = new Vector3(sinA * rFront, 0f, cosA * rFront + zCenterShift);
                Vector3 pTop = new Vector3(sinA * rCrest, halfThickY, cosA * rCrest + zCenterShift);
                Vector3 pBack = new Vector3(sinA * rBack, 0f, cosA * rBack + zCenterShift);
                Vector3 pBot = new Vector3(sinA * rCrest, -halfThickY, cosA * rCrest + zCenterShift);

                verts.Add(pFront); norms.Add(Vector3.up); uvs.Add(new Vector2(t, 1.0f));
                verts.Add(pTop);   norms.Add(Vector3.up); uvs.Add(new Vector2(t, 0.76f));
                verts.Add(pBack);  norms.Add(Vector3.up); uvs.Add(new Vector2(t, 0.0f));
                verts.Add(pBot);   norms.Add(Vector3.down); uvs.Add(new Vector2(t, 0.76f));
            }

            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 4;
                int b1 = (i + 1) * 4;
                // Upper aerodynamic crescent surface
                tris.Add(b0 + 0); tris.Add(b1 + 0); tris.Add(b1 + 1);
                tris.Add(b0 + 0); tris.Add(b1 + 1); tris.Add(b0 + 1);
                tris.Add(b0 + 1); tris.Add(b1 + 1); tris.Add(b1 + 2);
                tris.Add(b0 + 1); tris.Add(b1 + 2); tris.Add(b0 + 2);
                // Lower aerodynamic crescent surface
                tris.Add(b0 + 0); tris.Add(b1 + 3); tris.Add(b1 + 0);
                tris.Add(b0 + 0); tris.Add(b0 + 3); tris.Add(b1 + 3);
                tris.Add(b0 + 3); tris.Add(b0 + 2); tris.Add(b1 + 2);
                tris.Add(b0 + 3); tris.Add(b1 + 2); tris.Add(b1 + 3);
            }

            s_crescentBladeMesh = new Mesh();
            s_crescentBladeMesh.name = "WindFangHorizontalArcMesh";
            s_crescentBladeMesh.vertices = verts.ToArray();
            s_crescentBladeMesh.normals = norms.ToArray();
            s_crescentBladeMesh.uv = uvs.ToArray();
            s_crescentBladeMesh.triangles = tris.ToArray();
            s_crescentBladeMesh.RecalculateBounds();
            return s_crescentBladeMesh;
        }

        private static GameObject CreateNetworkedSpellPrefab(string prefabName, bool syncPosition, bool syncRotation)
        {
            GameObject go = new GameObject(prefabName);
            go.transform.SetParent(s_templateRoot, false);

            ZNetView znv = go.AddComponent<ZNetView>();
            znv.m_persistent = false;
            znv.m_distant = false;
            znv.m_type = ZDO.ObjectType.Default;

            if (syncPosition || syncRotation)
            {
                ZSyncTransform zst = go.AddComponent<ZSyncTransform>();
                zst.m_syncPosition = syncPosition;
                zst.m_syncRotation = syncRotation;
                zst.m_syncScale = false;
                zst.m_syncBodyVelocity = false;
                zst.m_characterParentSync = false;
            }

            return go;
        }

        internal static void DestroyNetworkedOrLocal(GameObject go)
        {
            if (go == null) return;
            ZNetView znv = go.GetComponent<ZNetView>();
            if (znv != null && znv.IsValid())
            {
                if (znv.IsOwner() && ZNetScene.instance != null)
                {
                    ZNetScene.instance.Destroy(go);
                }
                return;
            }
            UnityEngine.Object.Destroy(go);
        }

        internal static double GetWorldTimeSeconds()
        {
            if (ZNet.instance != null)
            {
                return ZNet.instance.GetTimeSeconds();
            }
            return (double)Time.time;
        }

        private static void EnsureSpellProjectileTemplates()
        {
            if (s_templateRoot == null)
            {
                GameObject rootGo = new GameObject("MagicStaves_ProjectileTemplates");
                UnityEngine.Object.DontDestroyOnLoad(rootGo);
                rootGo.SetActive(false);
                s_templateRoot = rootGo.transform;
            }

            // Borrow vanilla hit/explosion effects if available
            GameObject staffFb = PrefabManager.Instance.GetPrefab("StaffFireball");
            if (staffFb != null)
            {
                ItemDrop id = staffFb.GetComponent<ItemDrop>();
                if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null &&
                    id.m_itemData.m_shared.m_attack != null && id.m_itemData.m_shared.m_attack.m_attackProjectile != null)
                {
                    Projectile p = id.m_itemData.m_shared.m_attack.m_attackProjectile.GetComponent<Projectile>();
                    if (p != null) s_vanillaFireHitEffects = p.m_hitEffects;
                }
            }

            GameObject lightningAtgeir = PrefabManager.Instance.GetPrefab("AtgeirHimminAfl");
            if (lightningAtgeir != null)
            {
                ItemDrop id = lightningAtgeir.GetComponent<ItemDrop>();
                if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null)
                {
                    s_vanillaLightningHitEffects = id.m_itemData.m_shared.m_hitEffect;
                }
            }

            GameObject oozeBomb = PrefabManager.Instance.GetPrefab("BombOoze");
            if (oozeBomb != null)
            {
                ItemDrop id = oozeBomb.GetComponent<ItemDrop>();
                if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null &&
                    id.m_itemData.m_shared.m_attack != null && id.m_itemData.m_shared.m_attack.m_attackProjectile != null)
                {
                    Projectile p = id.m_itemData.m_shared.m_attack.m_attackProjectile.GetComponent<Projectile>();
                    if (p != null) s_vanillaPoisonBombHitEffects = p.m_hitEffects;
                }
            }

            // Borrow genuine vanilla blood-magic / blood-splatter VFX & audio (StaffShield / StaffSkeleton / Deer)!
            if (s_vanillaBloodHitEffects == null)
            {
                string[] bloodDonors = new string[] { "StaffShield", "StaffSkeleton", "StaffRedTroll" };
                for (int i = 0; i < bloodDonors.Length; i++)
                {
                    GameObject bd = PrefabManager.Instance.GetPrefab(bloodDonors[i]);
                    if (bd == null) continue;
                    ItemDrop id = bd.GetComponent<ItemDrop>();
                    if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null)
                    {
                        if (id.m_itemData.m_shared.m_startEffect != null && id.m_itemData.m_shared.m_startEffect.HasEffects())
                        {
                            s_vanillaBloodHitEffects = id.m_itemData.m_shared.m_startEffect;
                            break;
                        }
                        if (id.m_itemData.m_shared.m_triggerEffect != null && id.m_itemData.m_shared.m_triggerEffect.HasEffects())
                        {
                            s_vanillaBloodHitEffects = id.m_itemData.m_shared.m_triggerEffect;
                            break;
                        }
                    }
                }
                if (s_vanillaBloodHitEffects == null)
                {
                    GameObject deer = PrefabManager.Instance.GetPrefab("Deer");
                    if (deer != null)
                    {
                        Character ch = deer.GetComponent<Character>();
                        if (ch != null && ch.m_hitEffects != null && ch.m_hitEffects.HasEffects())
                        {
                            s_vanillaBloodHitEffects = ch.m_hitEffects;
                        }
                    }
                }
            }

            if (s_windBladeProjPrefab == null)
            {
                s_windBladeProjPrefab = CreateNetworkedSpellPrefab("CWP_Proj_MeadowsWindBlade", true, true);
                s_windBladeProjPrefab.AddComponent<WindBladeProjectile>();
                PrefabManager.Instance.AddPrefab(s_windBladeProjPrefab);
            }

            if (s_purificationTriggerProjPrefab == null)
            {
                s_purificationTriggerProjPrefab = new GameObject("Proj_BlackForestPurificationTrigger");
                s_purificationTriggerProjPrefab.transform.SetParent(s_templateRoot, false);
                s_purificationTriggerProjPrefab.AddComponent<PurificationTriggerProjectile>();
            }

            if (s_swampMiasmaProjPrefab == null)
            {
                s_swampMiasmaProjPrefab = CreateNetworkedSpellPrefab("CWP_Proj_SwampMiasma", true, true);
                s_swampMiasmaProjPrefab.AddComponent<SwampMiasmaProjectile>();
                PrefabManager.Instance.AddPrefab(s_swampMiasmaProjPrefab);
            }

            if (s_mountainDummyProjPrefab == null)
            {
                s_mountainDummyProjPrefab = new GameObject("Proj_MountainRailgunTrigger");
                s_mountainDummyProjPrefab.transform.SetParent(s_templateRoot, false);
                s_mountainDummyProjPrefab.AddComponent<MountainRailgunTriggerProjectile>();
            }

            if (s_plainsTotemProjPrefab == null)
            {
                s_plainsTotemProjPrefab = new GameObject("Proj_PlainsHarvestTotem");
                s_plainsTotemProjPrefab.transform.SetParent(s_templateRoot, false);
                s_plainsTotemProjPrefab.AddComponent<PlainsTotemDeployProjectile>();
            }

            if (s_purificationWaveNetPrefab == null)
            {
                s_purificationWaveNetPrefab = CreateNetworkedSpellPrefab("CWP_FX_PurificationWave", false, false);
                s_purificationWaveNetPrefab.AddComponent<PurificationWaveVisual>();
                PrefabManager.Instance.AddPrefab(s_purificationWaveNetPrefab);
            }

            if (s_swampMiasmaCloudPrefab == null)
            {
                s_swampMiasmaCloudPrefab = CreateNetworkedSpellPrefab("CWP_Zone_SwampMiasmaCloud", false, false);
                s_swampMiasmaCloudPrefab.AddComponent<SwampMiasmaCloudZone>();
                PrefabManager.Instance.AddPrefab(s_swampMiasmaCloudPrefab);
            }

            if (s_mountainBeamNetPrefab == null)
            {
                s_mountainBeamNetPrefab = CreateNetworkedSpellPrefab("CWP_FX_MountainRailgunBeam", false, false);
                s_mountainBeamNetPrefab.AddComponent<LightningRailgunBeamVisual>();
                PrefabManager.Instance.AddPrefab(s_mountainBeamNetPrefab);
            }

            if (s_plainsTotemZonePrefab == null)
            {
                s_plainsTotemZonePrefab = CreateNetworkedSpellPrefab("CWP_Zone_PlainsHarvestTotem", false, false);
                s_plainsTotemZonePrefab.AddComponent<PlainsBloodHarvestTotemZone>();
                PrefabManager.Instance.AddPrefab(s_plainsTotemZonePrefab);
            }

            if (s_plainsBuffStatusEffect == null)
            {
                SE_Stats se = ScriptableObject.CreateInstance<SE_Stats>();
                se.name = "SE_PlainsBloodFieldBuff";
                se.m_name = "$se_plains_bloodfield_name";
                se.m_tooltip = "$se_plains_bloodfield_tooltip";
                se.m_icon = (s_plainsTotemIcon != null) ? s_plainsTotemIcon : s_plainsIcon;
                se.m_ttl = 0.65f;
                // +10% move speed, +50% HP regen, +50% Eitr regen, -30% ALL stamina action costs!
                se.m_speedModifier = 0.10f;
                se.m_swimSpeedModifier = 0.10f;
                se.m_healthRegenMultiplier = 1.50f;
                se.m_eitrRegenMultiplier = 1.50f;
                se.m_runStaminaDrainModifier = -0.30f;
                se.m_jumpStaminaUseModifier = -0.30f;
                se.m_attackStaminaUseModifier = -0.30f;
                se.m_blockStaminaUseModifier = -0.30f;
                se.m_dodgeStaminaUseModifier = -0.30f;
                se.m_swimStaminaUseModifier = -0.30f;
                se.m_homeItemStaminaUseModifier = -0.30f;
                se.m_sneakStaminaUseModifier = -0.30f;

                s_plainsBuffStatusEffect = se;
                s_plainsBuffStatusHash = StringExtensionMethods.GetStableHashCode(se.name);
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            }
        }

        private static void RegisterRelicPart(
            string prefabName,
            string nameToken,
            string descToken,
            Mesh mesh,
            Material mat,
            Sprite icon,
            float targetSizeMeters,
            float weight)
        {
            string donor = "Amber";
            if (PrefabManager.Instance.GetPrefab(donor) == null) donor = "Flint";

            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(prefabName, donor);
            if (cloned == null) return;

            cloned.transform.localScale = Vector3.one;
            if (!cloned.activeSelf) cloned.SetActive(true);

            MeshFilter mf = cloned.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer mr = cloned.GetComponentInChildren<MeshRenderer>(true);
            if (mf != null && mr != null && mesh != null && mat != null)
            {
                float relScale = NewDualAxesPlugin.GetRelativeScaleMagnitude(mf.transform, cloned.transform);
                if (relScale < 1e-5f) relScale = 1f;
                Bounds b = mesh.bounds;
                float maxSpan = Mathf.Max(0.05f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                float scale = (targetSizeMeters / maxSpan) / relScale;
                Vector3 offset = -b.center * scale;
                mf.sharedMesh = NewDualAxesPlugin.BakeTransformedMesh(mesh, offset, Quaternion.identity, scale);
                mr.sharedMaterials = new Material[] { mat };
                mr.enabled = true;
                EnsureTransformChainActiveUpTo(mf.transform, cloned.transform);
            }

            NewDualAxesPlugin.FitWorldDropColliders(cloned);

            ItemDrop id = cloned.GetComponent<ItemDrop>();
            if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null)
            {
                id.m_itemData.m_dropPrefab = cloned;
                ItemDrop.ItemData.SharedData sh = id.m_itemData.m_shared;
                sh.m_name = nameToken;
                sh.m_description = descToken;
                sh.m_itemType = ItemDrop.ItemData.ItemType.Material;
                sh.m_maxStackSize = 20;
                sh.m_weight = weight;
                sh.m_value = 0;
                sh.m_teleportable = true;
                if (icon != null) sh.m_icons = new Sprite[] { icon };
            }

            ItemConfig cfg = new ItemConfig();
            cfg.Name = nameToken;
            cfg.Description = descToken;
            if (icon != null) cfg.Icons = new Sprite[] { icon };

            ItemManager.Instance.AddItem(new CustomItem(cloned, false, cfg));
        }

        private static void RegisterMagicStaff(
            string prefabName,
            string donorName,
            string nameToken,
            string descToken,
            Sprite icon,
            Mesh mesh,
            Material mat,
            float targetLengthMeters,
            Color glowColor,
            float glowYOffset,
            Skills.SkillType skillType,
            string station,
            int minStationLevel,
            string recipeStr,
            float eitrCost,
            float drawEitrPerSec,
            float healthPctCost,
            GameObject projectilePrefab,
            float projectileVel,
            float slashDmg,
            float slashPerLvl,
            float lightningDmg,
            float lightningPerLvl,
            float spiritDmg,
            float spiritPerLvl,
            float poisonDmg,
            float poisonPerLvl,
            bool isBowDrawRailgun,
            int maxQuality,
            float attackForce,
            int toolTier,
            float blockPower,
            float maxDurability)
        {
            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(prefabName, donorName);
            if (cloned == null) return;

            cloned.transform.localScale = Vector3.one;
            if (!cloned.activeSelf) cloned.SetActive(true);

            ApplyCustomStaffVisuals(cloned, mesh, mat, targetLengthMeters, glowColor, glowYOffset);

            ItemDrop itemDrop = cloned.GetComponent<ItemDrop>();
            if (itemDrop != null && itemDrop.m_itemData != null && itemDrop.m_itemData.m_shared != null)
            {
                itemDrop.m_itemData.m_dropPrefab = cloned;
                ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
                shared.m_name = nameToken;
                shared.m_description = descToken;
                if (icon != null)
                {
                    shared.m_icons = new Sprite[] { icon };
                }

                shared.m_maxQuality = Mathf.Max(1, maxQuality);
                shared.m_weight = 2.0f;
                shared.m_toolTier = toolTier;
                shared.m_skillType = skillType;
                shared.m_scaleByQuality = 0f;
                shared.m_dlc = "";
                shared.m_eitrRegenModifier = -0.05f;

                HitData.DamageTypes dmg = new HitData.DamageTypes();
                dmg.m_slash = slashDmg;
                dmg.m_lightning = lightningDmg;
                dmg.m_spirit = spiritDmg;
                dmg.m_poison = poisonDmg;
                shared.m_damages = dmg;

                HitData.DamageTypes dmgPerLvl = new HitData.DamageTypes();
                dmgPerLvl.m_slash = slashPerLvl;
                dmgPerLvl.m_lightning = lightningPerLvl;
                dmgPerLvl.m_spirit = spiritPerLvl;
                dmgPerLvl.m_poison = poisonPerLvl;
                shared.m_damagesPerLevel = dmgPerLvl;

                shared.m_blockPower = blockPower;
                shared.m_blockPowerPerLevel = (maxQuality > 1) ? 2f : 0f;
                shared.m_deflectionForce = 20f;
                shared.m_deflectionForcePerLevel = (maxQuality > 1) ? 5f : 0f;
                shared.m_timedBlockBonus = 2f;
                shared.m_attackForce = attackForce;

                shared.m_useDurability = true;
                shared.m_maxDurability = maxDurability;
                shared.m_durabilityPerLevel = (maxQuality > 1) ? 50f : 0f;

                shared.m_attackStatusEffect = null;
                shared.m_attackStatusEffectChance = 0f;

                if (shared.m_attack != null)
                {
                    // Capture donor's exact reload/charge animation name BEFORE overriding (for `StaffLightning` / Dundr this is `"recharge"`!)
                    string donorReloadAnim = shared.m_attack.m_reloadAnimation;
                    string donorAttackAnim = shared.m_attack.m_attackAnimation;

                    shared.m_attack = shared.m_attack.Clone();
                    Attack atk = shared.m_attack;
                    atk.m_attackType = Attack.AttackType.Projectile;
                    atk.m_attackProjectile = projectilePrefab;
                    atk.m_projectileVel = projectileVel;
                    atk.m_projectileVelMin = projectileVel;
                    atk.m_projectileAccuracy = 0f;
                    atk.m_projectileAccuracyMin = 0f;
                    atk.m_projectiles = 1;
                    atk.m_projectileBursts = 1;
                    atk.m_launchAngle = 0f;
                    atk.m_attackStamina = 0f;
                    atk.m_attackEitr = eitrCost;
                    atk.m_attackHealth = 0f;
                    atk.m_attackHealthPercentage = healthPctCost;
                    atk.m_attackHealthLowBlockUse = (healthPctCost > 0f);
                    atk.m_requiresReload = false;
                    atk.m_reloadTime = 0f;
                    atk.m_reloadStaminaDrain = 0f;
                    atk.m_reloadEitrDrain = 0f;

                    if (isBowDrawRailgun)
                    {
                        // Clear any physical ammo requirement so `Attack.StartDraw` / `Attack.HaveAmmo` never blocks draw!
                        shared.m_ammoType = "";

                        // Dundr (`StaffLightning`) holding stance (`AnimationState.Crossbow` = 10) + Dundr `"recharge"` charging animation!
                        shared.m_animationState = ItemDrop.ItemData.AnimationState.Crossbow;
                        if (string.IsNullOrEmpty(donorAttackAnim))
                        {
                            atk.m_attackAnimation = "crossbow_fire";
                        }

                        // Enable hold-to-charge HUD circle & continuous bow-draw Eitr drain (`m_drawEitrDrain = 10/s`),
                        // AND drive Dundr's exact charging animation (`"recharge"`, with `"recharge_done"` triggered on full charge)!
                        atk.m_bowDraw = true;
                        atk.m_drawDurationMin = 3.0f;
                        atk.m_drawStaminaDrain = 0f;
                        atk.m_drawEitrDrain = drawEitrPerSec;
                        atk.m_drawAnimationState = !string.IsNullOrEmpty(donorReloadAnim) ? donorReloadAnim : "recharge";
                        shared.m_projectileToolTip = true;

                        if (NewDualAxesPlugin.ModLogger != null)
                        {
                            NewDualAxesPlugin.ModLogger.LogInfo(string.Format(
                                "[MagicStaves] Mountain Railgun donor='{0}', animState={1}, drawAnim='{2}', attackAnim='{3}', eitrDrain={4}/s",
                                donorName, shared.m_animationState, atk.m_drawAnimationState, atk.m_attackAnimation, atk.m_drawEitrDrain));
                        }
                    }
                    else
                    {
                        atk.m_bowDraw = false;
                        atk.m_drawEitrDrain = 0f;
                    }
                }

                if (shared.m_secondaryAttack != null)
                {
                    shared.m_secondaryAttack = new Attack();
                }
            }

            ItemConfig cfg = new ItemConfig();
            cfg.Name = nameToken;
            cfg.Description = descToken;
            cfg.Amount = 1;
            cfg.CraftingStation = station;
            cfg.RepairStation = station;
            cfg.MinStationLevel = minStationLevel;
            if (icon != null)
            {
                cfg.Icons = new Sprite[] { icon };
            }

            NewDualAxesPlugin.ParseAndAddRequirements(cfg, recipeStr);

            CustomItem customItem = new CustomItem(cloned, false, cfg);
            ItemManager.Instance.AddItem(customItem);
        }

        internal static void EnsureTransformChainActiveUpTo(Transform leaf, Transform stopExclusive)
        {
            Transform cur = leaf;
            while (cur != null && cur != stopExclusive)
            {
                if (!cur.gameObject.activeSelf)
                {
                    cur.gameObject.SetActive(true);
                }
                cur = cur.parent;
            }
        }

        private static void ApplyCustomStaffVisuals(
            GameObject prefab,
            Mesh staffMesh,
            Material staffMat,
            float targetLengthMeters,
            Color glowColor,
            float glowYOffset)
        {
            if (staffMesh == null || staffMat == null) return;

            Transform attach = prefab.transform.Find("attach");
            Transform attachBack = prefab.transform.Find("attach_back");
            Transform attachSkin = prefab.transform.Find("attach_skin");

            // CRITICAL: Look at `VisEquipment.AttachItem`: when `backAttach == false`, Valheim loops over direct children
            // of `prefab` and stops at the FIRST child named `"attach"` OR `"attach_skin"`!
            // If the donor prefab happens to have an `"attach_skin"` child, rename & disable it so `AttachItem` ALWAYS
            // selects our custom `"attach"` (which mounts directly onto `RightHand_attach` / `LeftHand_attach`)!
            if (attachSkin != null)
            {
                attachSkin.name = "attach_skin_unused";
                attachSkin.gameObject.SetActive(false);
                attachSkin = null;
            }

            // Disable particle systems / trails / skinned renderers inherited from StaffFireball (e.g. ember flames)
            ParticleSystem[] donorParticles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < donorParticles.Length; i++)
            {
                if (donorParticles[i] != null)
                {
                    donorParticles[i].gameObject.SetActive(false);
                }
            }

            TrailRenderer[] donorTrails = prefab.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < donorTrails.Length; i++)
            {
                if (donorTrails[i] != null)
                {
                    donorTrails[i].enabled = false;
                }
            }

            SkinnedMeshRenderer[] donorSmrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < donorSmrs.Length; i++)
            {
                if (donorSmrs[i] != null)
                {
                    donorSmrs[i].enabled = false;
                }
            }

            Light[] donorLights = prefab.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < donorLights.Length; i++)
            {
                if (donorLights[i] != null)
                {
                    donorLights[i].color = glowColor;
                    donorLights[i].intensity = 0.75f;
                    donorLights[i].range = 2.2f;
                }
            }

            // 1) World-drop MeshFilters (outside attach / attach_back)
            MeshFilter[] allFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter bestWorldMf = null;
            int bestWorldVerts = -1;
            for (int i = 0; i < allFilters.Length; i++)
            {
                MeshFilter mf = allFilters[i];
                if (mf == null) continue;
                if (attach != null && mf.transform.IsChildOf(attach)) continue;
                if (attachBack != null && mf.transform.IsChildOf(attachBack)) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                int vc = (mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 0;
                string mName = mf.name.ToLowerInvariant();
                if (mName.Contains("glow") || mName.Contains("plane") || mName.Contains("fx"))
                {
                    mr.enabled = false;
                    continue;
                }

                if (vc > bestWorldVerts)
                {
                    bestWorldVerts = vc;
                    bestWorldMf = mf;
                }
            }

            for (int i = 0; i < allFilters.Length; i++)
            {
                MeshFilter mf = allFilters[i];
                if (mf == null || mf == bestWorldMf) continue;
                if (attach != null && mf.transform.IsChildOf(attach)) continue;
                if (attachBack != null && mf.transform.IsChildOf(attachBack)) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            if (bestWorldMf != null)
            {
                MeshRenderer bestWorldMr = bestWorldMf.GetComponent<MeshRenderer>();
                Quaternion wRot;
                Vector3 wOff;
                float wScale;
                AlignAndBakeStaffIntoMeshFilter(bestWorldMf, bestWorldMr, prefab.transform, staffMesh, staffMat, targetLengthMeters, false, out wRot, out wOff, out wScale);
                EnsureTransformChainActiveUpTo(bestWorldMf.transform, prefab.transform);
            }
            else
            {
                // Fallback if donor had no world MeshFilter outside attach: create a clean world visual child!
                GameObject worldGo = new GameObject("CustomStaffWorldVisual");
                worldGo.transform.SetParent(prefab.transform, false);
                worldGo.transform.localPosition = Vector3.zero;
                worldGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                worldGo.transform.localScale = Vector3.one;
                MeshFilter wMf = worldGo.AddComponent<MeshFilter>();
                MeshRenderer wMr = worldGo.AddComponent<MeshRenderer>();
                float customLen = Mathf.Max(0.8f, staffMesh.bounds.size.y);
                float vScale = targetLengthMeters / customLen;
                wMf.sharedMesh = NewDualAxesPlugin.BakeTransformedMesh(staffMesh, -staffMesh.bounds.center * vScale, Quaternion.identity, vScale);
                wMr.sharedMaterials = new Material[] { staffMat };
                wMr.enabled = true;
            }

            NewDualAxesPlugin.FitWorldDropColliders(prefab);

            // 2) Equipped 'attach' group (Hand) — create if missing on donor!
            if (attach == null)
            {
                GameObject attachGo = new GameObject("attach");
                attachGo.transform.SetParent(prefab.transform, false);
                attachGo.transform.localPosition = Vector3.zero;
                attachGo.transform.localRotation = Quaternion.identity;
                attachGo.transform.localScale = Vector3.one;
                attachGo.SetActive(false);
                attach = attachGo.transform;
            }
            ReplaceStaffAttachGroup(attach, prefab.transform, staffMesh, staffMat, targetLengthMeters, glowColor, glowYOffset, true, prefab.name);

            // 3) Holstered 'attach_back' group (Back) — create if missing on donor!
            if (attachBack == null)
            {
                GameObject attachBackGo = new GameObject("attach_back");
                attachBackGo.transform.SetParent(prefab.transform, false);
                attachBackGo.transform.localPosition = Vector3.zero;
                attachBackGo.transform.localRotation = Quaternion.identity;
                attachBackGo.transform.localScale = Vector3.one;
                attachBackGo.SetActive(false);
                attachBack = attachBackGo.transform;
            }
            ReplaceStaffAttachGroup(attachBack, prefab.transform, staffMesh, staffMat, targetLengthMeters, glowColor, glowYOffset, false, prefab.name);
        }

        private static void ReplaceStaffAttachGroup(
            Transform groupRoot,
            Transform prefabRoot,
            Mesh staffMesh,
            Material staffMat,
            float targetLengthMeters,
            Color glowColor,
            float glowYOffset,
            bool isEquippedHand,
            string prefabName)
        {
            MeshFilter[] filters = groupRoot.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter primaryMf = null;
            int bestVerts = -1;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                string mName = mf.name.ToLowerInvariant();
                int vc = (mf.sharedMesh != null) ? mf.sharedMesh.vertexCount : 0;
                if (mName.Contains("glow") || mName.Contains("plane") || mName.Contains("fx") || vc < 16)
                {
                    mr.enabled = false;
                    continue;
                }

                if (vc > bestVerts)
                {
                    bestVerts = vc;
                    primaryMf = mf;
                }
            }

            // Disable any secondary MeshRenderers in the group so vanilla parts never overlap our custom staff
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null || mf == primaryMf) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            MeshRenderer primaryMr = null;
            if (primaryMf == null)
            {
                GameObject customGo = new GameObject("CustomStaffMesh");
                customGo.transform.SetParent(groupRoot, false);
                customGo.transform.localPosition = Vector3.zero;
                customGo.transform.localRotation = Quaternion.identity;
                customGo.transform.localScale = Vector3.one;
                primaryMf = customGo.AddComponent<MeshFilter>();
                primaryMr = customGo.AddComponent<MeshRenderer>();
            }
            else
            {
                primaryMr = primaryMf.GetComponent<MeshRenderer>();
                EnsureTransformChainActiveUpTo(primaryMf.transform, groupRoot);
            }

            // CRITICAL SCALE FIX:
            // Pass `prefabRoot` (whose `localScale == Vector3.one`) as `referenceRoot`!
            Quaternion bakedRot;
            Vector3 bakedOffset;
            float bakedScale;
            Vector3 crownLocalPos = AlignAndBakeStaffIntoMeshFilter(
                primaryMf,
                primaryMr,
                prefabRoot,
                staffMesh,
                staffMat,
                targetLengthMeters,
                true,
                out bakedRot,
                out bakedOffset,
                out bakedScale
            );

            // For Swamp Staff ("Censer of Restless Spirit"): split the hanging censer + upper 5-link chain AND the lower
            // skull-collar 5-link tail chain out of the static mesh into physics-simulated articulated chain hierarchies!
            Light dynamicCenserLight = null;
            if (prefabName == SwampStaffPrefab && staffMesh != null)
            {
                SetupSwampCenserDynamicChains(
                    primaryMf,
                    primaryMr,
                    staffMesh,
                    staffMat,
                    bakedOffset,
                    bakedRot,
                    bakedScale,
                    glowColor,
                    isEquippedHand,
                    out dynamicCenserLight
                );
            }

            // Attach runtime live calibrator (`StaffVisualCalibrator`) so when `VisEquipment.AttachItem` / `AttachBackItem`
            // parents the instance onto the player's skeleton (`m_rightHand`, `m_leftHand`, or `m_backTwohandedMelee`),
            // we verify and auto-correct exact world-space meter length (`targetLengthMeters`), layer, and culling!
            StaffVisualCalibrator cal = groupRoot.gameObject.GetComponent<StaffVisualCalibrator>();
            if (cal == null) cal = groupRoot.gameObject.AddComponent<StaffVisualCalibrator>();
            cal.PrefabName = prefabName;
            cal.TargetLengthMeters = targetLengthMeters;
            cal.IsBackHolster = !isEquippedHand;
            cal.TargetRenderer = primaryMr;
            cal.TargetFilter = primaryMf;

            if (isEquippedHand)
            {
                if (dynamicCenserLight == null)
                {
                    GameObject glowGo = new GameObject("CustomStaffCrownGlow");
                    glowGo.transform.SetParent(primaryMf.transform, false);
                    glowGo.transform.localPosition = crownLocalPos;
                    Light l = glowGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = glowColor;
                    l.range = 2.0f;
                    l.intensity = 0.65f;
                    l.shadows = LightShadows.None;

                    if (prefabName == MountainStaffPrefab)
                    {
                        MountainRailgunChargeVisual chargeVis = groupRoot.gameObject.GetComponent<MountainRailgunChargeVisual>();
                        if (chargeVis == null) chargeVis = groupRoot.gameObject.AddComponent<MountainRailgunChargeVisual>();
                        chargeVis.CrownAnchor = glowGo.transform;
                        chargeVis.CrownLight = l;
                    }
                }
            }
        }

        /// <summary>
        /// Aligns our custom staff mesh (where Unity `+Y` is shaft from pommel `[-0.95]` to crown `[+1.25]`, `(0,0,0)` is grip,
        /// and `-Z` is decorative front face) directly onto the vanilla donor `StaffFireball`'s exact shaft axis, crown direction,
        /// and hand grip origin! Returns the baked local position of the staff's glowing crown for attaching Light/VFX.
        /// </summary>
        private static Vector3 AlignAndBakeStaffIntoMeshFilter(
            MeshFilter mf,
            MeshRenderer mr,
            Transform referenceRoot,
            Mesh staffMesh,
            Material staffMat,
            float targetLengthMeters,
            bool isAttachVisual,
            out Quaternion outRot,
            out Vector3 outLocalOffset,
            out float outVertexScale)
        {
            float relScale = NewDualAxesPlugin.GetRelativeScaleMagnitude(mf.transform, referenceRoot);
            if (relScale < 1e-6f) relScale = 1.0f;

            float desiredLocalLen = targetLengthMeters / relScale;
            float customLen = Mathf.Max(0.8f, staffMesh.bounds.size.y);
            float vertexScale = desiredLocalLen / customLen;

            Quaternion rot = Quaternion.identity;
            Vector3 localOffset = Vector3.zero;

            Mesh donorMesh = mf.sharedMesh;
            if (donorMesh != null && donorMesh.bounds.size.sqrMagnitude > 1e-8f)
            {
                Bounds db = donorMesh.bounds;
                Vector3 sz = db.size;
                int shaftAxis = NewDualAxesPlugin.GetLongestAxis(sz);
                int secondAxis = NewDualAxesPlugin.GetSecondLongestAxis(sz, shaftAxis);

                // Determine which direction along `shaftAxis` is the TOP CROWN of the vanilla staff/Dundr:
                // Transform `db.min` and `db.max` along `shaftAxis` into `attach` root space where `(0,0,0)` is the hand grip!
                // The barrel / crown always extends further forward/upward from the hand grip `(0,0,0)` than the stock / pommel!
                Transform attachSpace = mf.transform.parent != null ? mf.transform.parent : referenceRoot;
                Matrix4x4 mfToAttach = attachSpace.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Vector3 axisDir = Vector3.zero;
                axisDir[shaftAxis] = 1.0f;
                Vector3 posEndInAttach = mfToAttach.MultiplyPoint3x4(db.center + axisDir * (0.5f * sz[shaftAxis]));
                Vector3 negEndInAttach = mfToAttach.MultiplyPoint3x4(db.center - axisDir * (0.5f * sz[shaftAxis]));

                float crownSign = 1.0f;
                if (isAttachVisual && Mathf.Abs(posEndInAttach.sqrMagnitude - negEndInAttach.sqrMagnitude) > 1e-4f)
                {
                    crownSign = (posEndInAttach.sqrMagnitude >= negEndInAttach.sqrMagnitude) ? 1.0f : -1.0f;
                }
                else if (Mathf.Abs(db.center[shaftAxis]) > 0.02f * sz[shaftAxis])
                {
                    crownSign = Mathf.Sign(db.center[shaftAxis]);
                }

                Vector3 targetShaftUp = Vector3.zero;
                targetShaftUp[shaftAxis] = crownSign;

                Vector3 perpForward = Vector3.zero;
                perpForward[secondAxis] = 1.0f;

                // Map our staff's +Y -> targetShaftUp, and our staff's +Z -> perpForward (so front -Z faces cleanly outward!)
                rot = Quaternion.LookRotation(perpForward, targetShaftUp);

                // Match the exact grip/stock proportion!
                // Note: If donor is shorter (like Dundr ~1.4m vs our 2.28m staff), aligning `0,0,0` (hand grip) to the donor's grip
                // (or keeping stock length <= donor stock length so the rear pommel never pokes too far behind the shoulder!) is super clean:
                Vector3 donorGripLocal = mfToAttach.inverse.MultiplyPoint3x4(Vector3.zero);
                if (isAttachVisual && sz[shaftAxis] < 1.75f * relScale)
                {
                    // Crossbow / Dundr-style donor (`StaffLightning`): anchor our staff's grip `(0, +0.22m, 0)` right at the donor's front/rear hand grip
                    // so the tuning-fork prongs aim forward like Dundr's barrel and the buttstock tucks cleanly under the arm!
                    Vector3 customGripShift = new Vector3(0f, 0.22f, 0f);
                    localOffset = donorGripLocal - rot * (customGripShift * vertexScale);
                }
                else
                {
                    Vector3 rotatedCustomCenter = rot * (staffMesh.bounds.center * vertexScale);
                    localOffset = db.center - rotatedCustomCenter;
                }
            }
            else if (isAttachVisual)
            {
                rot = Quaternion.identity;
                localOffset = Vector3.zero;
            }

            outRot = rot;
            outLocalOffset = localOffset;
            outVertexScale = vertexScale;

            Mesh bakedMesh = NewDualAxesPlugin.BakeTransformedMesh(staffMesh, localOffset, rot, vertexScale);
            // Expand bounds slightly so frustum culling never clips long two-handed staves near camera edges
            Bounds padBounds = bakedMesh.bounds;
            padBounds.Expand(0.5f);
            bakedMesh.bounds = padBounds;

            mf.sharedMesh = bakedMesh;
            mr.sharedMaterials = new Material[] { staffMat };
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.enabled = true;

            Vector3 crownInCustom = new Vector3(0f, staffMesh.bounds.max.y * 0.84f, 0f);
            return localOffset + rot * (crownInCustom * vertexScale);
        }

        // ====================================================================================================
        // SWAMP STAFF ("CENSER OF RESTLESS SPIRIT") DYNAMIC ARTICULATED CHAIN & CENSER SPLITTER
        // ====================================================================================================
        /// <summary>
        /// Splits `SwampStaff`'s raw mesh into:
        ///   - Group 0 (Static Staff): Twisted bogwood crook, skull, iron hook cap `(0.135, 1.120, 0)`, and lower skull collar.
        ///   - Groups 1..5 (Upper 5 Chain Links): Hanging down from hook `(0.135, 1.106, 0)` to `(0.135, 0.962, 0)`.
        ///   - Group 6 (Swinging Censer Lantern + Wisp Core): Hanging from bottom of upper chain `(0.135, 0.962, 0)` down to `Y = 0.615`.
        ///   - Groups 7..11 (Lower 5 Decorative Tail Links): Hanging below skull collar `(-0.056, 0.672, -0.004)` down to `Y = 0.510`.
        /// Parents them in real articulated GameObject chains driven by `SwampCenserChainSimulator` so both chains + censer swing, lag,
        /// and sway naturally with gravity, walking, running, turning, jumping, and spellcasting!
        /// </summary>
        private static void SetupSwampCenserDynamicChains(
            MeshFilter staffMf,
            MeshRenderer staffMr,
            Mesh rawStaffMesh,
            Material staffMat,
            Vector3 bakedOffset,
            Quaternion bakedRot,
            float bakedScale,
            Color glowColor,
            bool addCenserPointLight,
            out Light censerLight)
        {
            censerLight = null;
            if (staffMf == null || staffMr == null || rawStaffMesh == null || staffMat == null) return;

            Vector3[] verts = rawStaffMesh.vertices;
            Vector3[] norms = rawStaffMesh.normals;
            Vector2[] uvs = rawStaffMesh.uv;
            int[] tris = rawStaffMesh.triangles;
            if (verts == null || tris == null || verts.Length < 100 || tris.Length < 90) return;

            int triCount = tris.Length / 3;
            const int GroupCount = 12; // 0 = Static, 1..5 = Upper chain links, 6 = Censer lantern, 7..11 = Lower tail chain links
            List<int>[] groupTris = new List<int>[GroupCount];
            for (int g = 0; g < GroupCount; g++)
            {
                groupTris[g] = new List<int>(g == 0 ? tris.Length : 512);
            }

            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3];
                int i1 = tris[t * 3 + 1];
                int i2 = tris[t * 3 + 2];
                Vector3 c = (verts[i0] + verts[i1] + verts[i2]) * (1.0f / 3.0f);

                int g = 0; // Default: static staff body (shaft, crook, skull, hook cap at Y >= 1.106)
                if (c.x > 0.042f && c.y >= 0.600f && c.y < 1.105f)
                {
                    // Hanging Censer + Upper 5 Chain Links on the right side of the crook (X ~ 0.135)
                    if (c.y >= 0.962f)
                    {
                        if (c.y >= 1.077f) g = 1;
                        else if (c.y >= 1.049f) g = 2;
                        else if (c.y >= 1.021f) g = 3;
                        else if (c.y >= 0.993f) g = 4;
                        else g = 5;
                    }
                    else
                    {
                        g = 6; // Entire Censer Lantern + Glowing Spirit Wisp!
                    }
                }
                else if (c.x < -0.042f && c.y >= 0.495f && c.y < 0.676f && Mathf.Abs(c.z) < 0.032f)
                {
                    // Hanging 5-link Tail Chain below the skull iron collar (X ~ -0.06..-0.09, Y ~ 0.51..0.675)
                    if (c.y >= 0.638f) g = 7;
                    else if (c.y >= 0.608f) g = 8;
                    else if (c.y >= 0.577f) g = 9;
                    else if (c.y >= 0.546f) g = 10;
                    else g = 11;
                }

                List<int> dst = groupTris[g];
                dst.Add(i0);
                dst.Add(i1);
                dst.Add(i2);
            }

            // Verify we cleanly isolated the censer & chain links before replacing the static mesh
            if (groupTris[6].Count < 300 || groupTris[1].Count < 30) return;

            Matrix4x4 customToMf = Matrix4x4.TRS(bakedOffset, bakedRot, Vector3.one * bakedScale);

            // 1) Rebuild Group 0 as the static staff body (without the frozen censer & chains!)
            Mesh staticStaffOnly = BuildSubGroupMesh("SwampStaff_StaticBody", verts, norms, uvs, groupTris[0], customToMf, Vector3.zero);
            Bounds padB = staticStaffOnly.bounds;
            padB.Expand(0.50f);
            staticStaffOnly.bounds = padB;
            staffMf.sharedMesh = staticStaffOnly;

            // Exact custom-space pivot positions for the articulated upper chain + censer (top-down along X = 0.135):
            Vector3[] upperPivotsCustom = new Vector3[]
            {
                new Vector3(0.135f, 1.106f, 0.000f), // Link 1 top hook pivot (group 1)
                new Vector3(0.135f, 1.077f, 0.000f), // Link 2 pivot (group 2)
                new Vector3(0.135f, 1.049f, 0.000f), // Link 3 pivot (group 3)
                new Vector3(0.135f, 1.021f, 0.000f), // Link 4 pivot (group 4)
                new Vector3(0.135f, 0.993f, 0.000f), // Link 5 pivot (group 5)
                new Vector3(0.135f, 0.962f, 0.000f)  // Censer top ring pivot (group 6)
            };

            Transform[] upperChainNodes = new Transform[6];
            Transform parentT = staffMf.transform;
            Vector3 prevPivotMf = Vector3.zero;

            for (int k = 0; k < 6; k++)
            {
                int grpIdx = k + 1;
                Vector3 pivotMf = customToMf.MultiplyPoint3x4(upperPivotsCustom[k]);
                Vector3 localPos = (k == 0) ? pivotMf : (pivotMf - prevPivotMf);

                GameObject nodeGo = new GameObject(k < 5 ? ("SwampCenserChainLink_" + (k + 1)) : "SwampCenserLantern_Dynamic");
                nodeGo.transform.SetParent(parentT, false);
                nodeGo.transform.localPosition = localPos;
                nodeGo.transform.localRotation = Quaternion.identity;
                nodeGo.transform.localScale = Vector3.one;

                if (groupTris[grpIdx].Count >= 3)
                {
                    Mesh partMesh = BuildSubGroupMesh(nodeGo.name + "_Mesh", verts, norms, uvs, groupTris[grpIdx], customToMf, pivotMf);
                    Bounds pb = partMesh.bounds;
                    pb.Expand(0.35f);
                    partMesh.bounds = pb;

                    MeshFilter pMf = nodeGo.AddComponent<MeshFilter>();
                    pMf.sharedMesh = partMesh;
                    MeshRenderer pMr = nodeGo.AddComponent<MeshRenderer>();
                    pMr.sharedMaterials = new Material[] { staffMat };
                    pMr.shadowCastingMode = ShadowCastingMode.On;
                    pMr.receiveShadows = true;
                }

                upperChainNodes[k] = nodeGo.transform;
                parentT = nodeGo.transform;
                prevPivotMf = pivotMf;
            }

            // Attach point light directly inside the swinging censer lantern so the eerie turquoise glow swings with the censer!
            if (addCenserPointLight && upperChainNodes[5] != null)
            {
                Vector3 wispCenterMf = customToMf.MultiplyPoint3x4(new Vector3(0.135f, 0.825f, 0.000f));
                Vector3 censerPivotMf = customToMf.MultiplyPoint3x4(upperPivotsCustom[5]);

                GameObject glowGo = new GameObject("SwampCenserSwingingGlow");
                glowGo.transform.SetParent(upperChainNodes[5], false);
                glowGo.transform.localPosition = wispCenterMf - censerPivotMf;
                Light l = glowGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = glowColor;
                l.range = 2.4f;
                l.intensity = 0.78f;
                l.shadows = LightShadows.None;
                censerLight = l;
            }

            // 3) Articulated 5-link decorative tail chain hanging under the skull collar:
            Vector3[] tailPivotsCustom = new Vector3[]
            {
                new Vector3(-0.056f, 0.673f, -0.004f), // Tail Link 1 top collar pivot (group 7)
                new Vector3(-0.062f, 0.641f, -0.005f), // Tail Link 2 pivot (group 8)
                new Vector3(-0.069f, 0.610f, -0.005f), // Tail Link 3 pivot (group 9)
                new Vector3(-0.075f, 0.579f, -0.005f), // Tail Link 4 pivot (group 10)
                new Vector3(-0.082f, 0.548f, -0.006f)  // Tail Link 5 pivot (group 11)
            };

            List<Transform> tailNodesList = new List<Transform>(5);
            Transform tailParent = staffMf.transform;
            Vector3 prevTailPivotMf = Vector3.zero;

            for (int k = 0; k < 5; k++)
            {
                int grpIdx = 7 + k;
                if (groupTris[grpIdx].Count < 3) break;

                Vector3 pivotMf = customToMf.MultiplyPoint3x4(tailPivotsCustom[k]);
                Vector3 localPos = (k == 0) ? pivotMf : (pivotMf - prevTailPivotMf);

                GameObject tGo = new GameObject("SwampSkullTailChain_" + (k + 1));
                tGo.transform.SetParent(tailParent, false);
                tGo.transform.localPosition = localPos;
                tGo.transform.localRotation = Quaternion.identity;
                tGo.transform.localScale = Vector3.one;

                Mesh tMesh = BuildSubGroupMesh(tGo.name + "_Mesh", verts, norms, uvs, groupTris[grpIdx], customToMf, pivotMf);
                Bounds tb = tMesh.bounds;
                tb.Expand(0.25f);
                tMesh.bounds = tb;

                MeshFilter tMf = tGo.AddComponent<MeshFilter>();
                tMf.sharedMesh = tMesh;
                MeshRenderer tMr = tGo.AddComponent<MeshRenderer>();
                tMr.sharedMaterials = new Material[] { staffMat };
                tMr.shadowCastingMode = ShadowCastingMode.On;
                tMr.receiveShadows = true;

                tailNodesList.Add(tGo.transform);
                tailParent = tGo.transform;
                prevTailPivotMf = pivotMf;
            }

            // Add real-time damped pendulum physics simulator onto `staffMf.gameObject`!
            Vector3 staffDownInMf = (bakedRot * Vector3.down).normalized;
            SwampCenserChainSimulator sim = staffMf.gameObject.GetComponent<SwampCenserChainSimulator>();
            if (sim == null) sim = staffMf.gameObject.AddComponent<SwampCenserChainSimulator>();
            sim.RestDownDirLocal = staffDownInMf;
            sim.UpperChainNodes = upperChainNodes;
            sim.TailChainNodes = tailNodesList.ToArray();
        }

        private static Mesh BuildSubGroupMesh(
            string name,
            Vector3[] srcVerts,
            Vector3[] srcNorms,
            Vector2[] srcUvs,
            List<int> triIndices,
            Matrix4x4 customToMf,
            Vector3 pivotInMf)
        {
            Dictionary<int, int> remap = new Dictionary<int, int>(triIndices.Count / 2 + 8);
            List<Vector3> outV = new List<Vector3>(triIndices.Count / 2 + 8);
            List<Vector3> outN = new List<Vector3>(triIndices.Count / 2 + 8);
            List<Vector2> outUV = new List<Vector2>(triIndices.Count / 2 + 8);
            int[] outT = new int[triIndices.Count];

            Matrix4x4 nMat = customToMf.inverse.transpose;
            bool flip = customToMf.determinant < 0f;

            for (int i = 0; i < triIndices.Count; i++)
            {
                int oldIdx = triIndices[i];
                int newIdx;
                if (!remap.TryGetValue(oldIdx, out newIdx))
                {
                    newIdx = outV.Count;
                    remap[oldIdx] = newIdx;
                    Vector3 pMf = customToMf.MultiplyPoint3x4(srcVerts[oldIdx]);
                    outV.Add(pMf - pivotInMf);
                    outN.Add(nMat.MultiplyVector(srcNorms[oldIdx]).normalized);
                    outUV.Add(srcUvs != null && oldIdx < srcUvs.Length ? srcUvs[oldIdx] : Vector2.zero);
                }
                outT[i] = newIdx;
            }

            if (flip)
            {
                for (int i = 0; i < outT.Length; i += 3)
                {
                    int tmp = outT[i + 1];
                    outT[i + 1] = outT[i + 2];
                    outT[i + 2] = tmp;
                }
            }

            Mesh m = new Mesh();
            m.name = name;
            m.vertices = outV.ToArray();
            m.normals = outN.ToArray();
            m.uv = outUV.ToArray();
            m.triangles = outT;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        // ====================================================================================================
        // MEADOWS CHEST LOOT INJECTION (Abandoned Houses / Towers / Buried Meadows Chests)
        // ====================================================================================================
        internal static void InjectMeadowsChestDrops()
        {
            GameObject pCrystal = PrefabManager.Instance.GetPrefab(PartCrystalPrefab);
            GameObject pHandle = PrefabManager.Instance.GetPrefab(PartHandlePrefab);
            GameObject pBinding = PrefabManager.Instance.GetPrefab(PartBindingPrefab);
            if (pCrystal == null || pHandle == null || pBinding == null) return;

            string[] meadowsChestNames = new string[]
            {
                "TreasureChest_meadows",
                "TreasureChest_meadows_buried"
            };

            int injectedCount = 0;
            for (int i = 0; i < meadowsChestNames.Length; i++)
            {
                GameObject chestPrefab = PrefabManager.Instance.GetPrefab(meadowsChestNames[i]);
                if (chestPrefab == null) continue;
                Container cont = chestPrefab.GetComponent<Container>();
                if (cont == null || cont.m_defaultItems == null || cont.m_defaultItems.m_drops == null) continue;

                AddDropIfMissing(cont.m_defaultItems.m_drops, pCrystal, 0.75f);
                AddDropIfMissing(cont.m_defaultItems.m_drops, pHandle, 0.75f);
                AddDropIfMissing(cont.m_defaultItems.m_drops, pBinding, 0.75f);
                injectedCount++;
            }

            if (injectedCount > 0 && NewDualAxesPlugin.ModLogger != null)
            {
                NewDualAxesPlugin.ModLogger.LogInfo("[MagicStaves] Injected Wind Staff relic parts into " + injectedCount + " Meadows chest DropTables.");
            }
        }

        private static void AddDropIfMissing(List<DropTable.DropData> drops, GameObject itemPrefab, float weight)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                if (drops[i].m_item == itemPrefab) return;
            }
            DropTable.DropData dd = new DropTable.DropData();
            dd.m_item = itemPrefab;
            dd.m_stackMin = 1;
            dd.m_stackMax = 1;
            dd.m_weight = weight;
            dd.m_dontScale = true;
            drops.Add(dd);
        }

        // ====================================================================================================
        // HELPER: Weapon Identification & Mountain Fixed Charge Math
        // ====================================================================================================
        internal static bool IsMountainRailgun(ItemDrop.ItemData weapon)
        {
            if (weapon == null || weapon.m_shared == null) return false;
            if (weapon.m_shared.m_name == "$item_staff_mountain") return true;
            if (weapon.m_dropPrefab != null && weapon.m_dropPrefab.name == MountainStaffPrefab) return true;
            return false;
        }

        internal static float GetMountainRequiredChargeSecondsForQuality(int quality)
        {
            int q = Mathf.Max(1, quality);
            // Level 1 = 3.0s, Level 2 = 3.5s, Level 3 = 4.0s, Level 4+ = 4.5s (cap charge time at 4.5s so quality 5+ from Forge of Potential increases damage without making charge slower!)
            int chargeTier = Mathf.Clamp(q, 1, 4);
            return 3.0f + 0.5f * (chargeTier - 1);
        }

        internal static float GetMountainRequiredChargeSeconds(ItemDrop.ItemData weapon)
        {
            int q = (weapon != null) ? Mathf.Max(1, weapon.m_quality) : 1;
            return GetMountainRequiredChargeSecondsForQuality(q);
        }

        internal static float GetMountainDrawEitrPerSecForQuality(int quality)
        {
            int q = Mathf.Max(1, quality);
            float baseDrain = (MountainDrawEitrPerSec != null) ? MountainDrawEitrPerSec.Value : 10f;
            float perLvl = (MountainDrawEitrExtraPerLevel != null) ? MountainDrawEitrExtraPerLevel.Value : 2f;
            return Mathf.Max(0f, baseDrain + perLvl * (q - 1));
        }

        internal static float GetMountainDrawEitrPerSec(ItemDrop.ItemData weapon)
        {
            int q = (weapon != null) ? Mathf.Max(1, weapon.m_quality) : 1;
            return GetMountainDrawEitrPerSecForQuality(q);
        }

        // ====================================================================================================
        // HELPER: Swamp Miasma Resistance Stripper
        // ====================================================================================================
        internal static void MarkCharacterMiasmaAfflicted(Character c, float graceSeconds)
        {
            if (c == null) return;
            float exp = Time.time + graceSeconds;
            float cur;
            if (!s_miasmaAfflicted.TryGetValue(c, out cur) || exp > cur)
            {
                s_miasmaAfflicted[c] = exp;
            }
        }

        internal static bool IsCharacterMiasmaAfflicted(Character c)
        {
            if (c == null || s_miasmaAfflicted.Count == 0) return false;
            float exp;
            if (s_miasmaAfflicted.TryGetValue(c, out exp))
            {
                if (Time.time <= exp) return true;
                s_miasmaAfflicted.Remove(c);
            }
            return false;
        }

        internal static HitData.DamageModifier StripResistModifier(HitData.DamageModifier mod)
        {
            // Strip Resistant, VeryResistant, SlightlyResistant, and Immune -> Normal! Keep Weak / VeryWeak / SlightlyWeak intact!
            if (mod == HitData.DamageModifier.Resistant ||
                mod == HitData.DamageModifier.VeryResistant ||
                mod == HitData.DamageModifier.SlightlyResistant ||
                mod == HitData.DamageModifier.Immune)
            {
                return HitData.DamageModifier.Normal;
            }
            return mod;
        }

        // ====================================================================================================
        // HELPER: Black Forest Purification Staff — Recolor, Radial Cast Animation & AoE Cleanse + Heal
        // ====================================================================================================
        private static readonly int[] s_vanillaCleanseDebuffHashes = new int[]
        {
            "Burning".GetStableHashCode(),
            "Poison".GetStableHashCode(),
            "Frost".GetStableHashCode(),
            "Tared".GetStableHashCode(),
            "Lightning".GetStableHashCode(),
            "Spirit".GetStableHashCode(),
            "Smoked".GetStableHashCode(),
            "Wet".GetStableHashCode()
        };

        private static Color RecolorRubyToShamanEmerald(Color src, float emissionStrength, bool isEmissionTex)
        {
            float h, s, v;
            Color.RGBToHSV(src, out h, out s, out v);

            bool isRubyHue = (h <= 0.048f || h >= 0.90f) && s >= 0.42f && src.r > src.g * 1.55f;
            float mask = Mathf.Max(Mathf.Clamp01(emissionStrength * 1.45f), isRubyHue ? Mathf.Clamp01((s - 0.40f) * 2.5f) : 0f);
            if (mask <= 0.01f)
            {
                return src;
            }

            // Emerald-gold palette of Greydwarf Shaman healing magic: rich forest emerald shifting to warm golden-lime at bright highlights!
            Color emerald = new Color(0.14f, 0.92f, 0.46f, src.a);
            Color goldGlow = new Color(0.86f, 1.00f, 0.48f, src.a);
            float highlight = Mathf.Clamp01((Mathf.Max(v, emissionStrength) - 0.52f) / 0.42f);
            Color targetTone = Color.Lerp(emerald, goldGlow, highlight * 0.65f);

            if (isEmissionTex)
            {
                Color outEm = targetTone * Mathf.Max(v, emissionStrength);
                outEm.a = src.a;
                return Color.Lerp(src, outEm, mask);
            }

            Color tinted = targetTone * Mathf.Clamp(v * 1.12f, 0.08f, 1.0f);
            tinted.a = src.a;
            return Color.Lerp(src, tinted, mask);
        }

        private static void BuildPurificationStaffMaterialAndIcon(Shader shader, out Material mat, out Sprite icon)
        {
            mat = new Material(shader);
            mat.name = "Mat_BlackForestPurificationStaff";

            Texture2D albedo = NewDualAxesPlugin.LoadEmbeddedTexture("BlackForestStaff_Albedo.png", false, FilterMode.Point);
            Texture2D rough = NewDualAxesPlugin.LoadEmbeddedTexture("BlackForestStaff_Roughness.png", true, FilterMode.Point);
            Texture2D normal = NewDualAxesPlugin.LoadEmbeddedNormalMap("BlackForestStaff_Normal.png", FilterMode.Point);
            Texture2D emiss = NewDualAxesPlugin.LoadEmbeddedTexture("BlackForestStaff_Emission.png", false, FilterMode.Point);

            if (albedo != null && emiss != null && albedo.width == emiss.width && albedo.height == emiss.height)
            {
                Color[] aPx = albedo.GetPixels();
                Color[] ePx = emiss.GetPixels();
                for (int i = 0; i < aPx.Length; i++)
                {
                    float emStr = Mathf.Max(ePx[i].r, Mathf.Max(ePx[i].g, ePx[i].b));
                    aPx[i] = RecolorRubyToShamanEmerald(aPx[i], emStr, false);
                    ePx[i] = RecolorRubyToShamanEmerald(ePx[i], emStr, true);
                }
                albedo.SetPixels(aPx);
                albedo.Apply();
                emiss.SetPixels(ePx);
                emiss.Apply();
            }

            Texture2D metGloss = NewDualAxesPlugin.BuildMetallicGlossMap(albedo, rough, 0.65f);

            mat.color = Color.white;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (albedo != null)
            {
                mat.mainTexture = albedo;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            if (metGloss != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metGloss);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.65f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.55f);
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (emiss != null)
            {
                Color emBoost = new Color(0.75f, 1.0f, 0.78f, 1f) * 2.2f;
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emiss);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emBoost);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emBoost);
                mat.EnableKeyword("_EMISSION");
            }

            Texture2D iconTex = NewDualAxesPlugin.LoadEmbeddedTexture("BlackForestStaff_Icon.png", false, FilterMode.Bilinear);
            if (iconTex != null)
            {
                Color[] iPx = iconTex.GetPixels();
                for (int i = 0; i < iPx.Length; i++)
                {
                    if (iPx[i].a > 0.01f)
                    {
                        iPx[i] = RecolorRubyToShamanEmerald(iPx[i], 0f, false);
                    }
                }
                iconTex.SetPixels(iPx);
                iconTex.Apply();
                icon = Sprite.Create(iconTex, new Rect(0f, 0f, iconTex.width, iconTex.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                icon = null;
            }
        }

        private static void ApplyStaffShieldRadialCastAnimation(string staffPrefabName)
        {
            CustomItem ci = ItemManager.Instance.GetItem(staffPrefabName);
            GameObject staffGo = (ci != null) ? ci.ItemPrefab : PrefabManager.Instance.GetPrefab(staffPrefabName);
            if (staffGo == null) return;

            ItemDrop id = staffGo.GetComponent<ItemDrop>();
            if (id == null || id.m_itemData == null || id.m_itemData.m_shared == null || id.m_itemData.m_shared.m_attack == null) return;

            Attack atk = id.m_itemData.m_shared.m_attack;
            GameObject shieldDonor = PrefabManager.Instance.GetPrefab("StaffShield");
            if (shieldDonor != null)
            {
                ItemDrop shieldId = shieldDonor.GetComponent<ItemDrop>();
                if (shieldId != null && shieldId.m_itemData != null && shieldId.m_itemData.m_shared != null)
                {
                    ItemDrop.ItemData.SharedData sShared = shieldId.m_itemData.m_shared;
                    id.m_itemData.m_shared.m_animationState = sShared.m_animationState;
                    if (sShared.m_attack != null && !string.IsNullOrEmpty(sShared.m_attack.m_attackAnimation))
                    {
                        atk.m_attackAnimation = sShared.m_attack.m_attackAnimation;
                    }
                    else
                    {
                        atk.m_attackAnimation = "staff_shield";
                    }
                    if (sShared.m_startEffect != null && sShared.m_startEffect.HasEffects())
                    {
                        id.m_itemData.m_shared.m_startEffect = sShared.m_startEffect;
                    }
                    if (sShared.m_triggerEffect != null && sShared.m_triggerEffect.HasEffects())
                    {
                        id.m_itemData.m_shared.m_triggerEffect = sShared.m_triggerEffect;
                    }
                    return;
                }
            }
            atk.m_attackAnimation = "staff_shield";
        }

        public static float GetPurificationHealForQuality(int qualityLevel)
        {
            int q = Mathf.Max(1, qualityLevel);
            float baseHeal = BlackForestBaseHeal != null ? BlackForestBaseHeal.Value : 25f;
            float extraHeal = BlackForestExtraHealPerLevel != null ? BlackForestExtraHealPerLevel.Value : 10f;
            return Mathf.Max(0f, baseHeal + (q - 1) * extraHeal);
        }

        public static void ApplyPurificationAndHealLocal(Player target, float healAmount)
        {
            if (target == null || target.IsDead()) return;

            // 1. Cleanse all 6 Combat Injuries FIRST (including Rot -50% healing penalty so the heal applies at 100% strength!)
            if (EnemyOverhaulModule.IsActive)
            {
                EnemyInjuryManager.ClearAllInjuries(target);
            }

            // 2. Cleanse vanilla combat/weather debuffs (Burning, Poison, Frost, Tared, Lightning, Spirit, Smoked, Wet)
            //    without touching potion cooldowns or positive buffs!
            SEMan seman = target.GetSEMan();
            if (seman != null)
            {
                for (int i = 0; i < s_vanillaCleanseDebuffHashes.Length; i++)
                {
                    int h = s_vanillaCleanseDebuffHashes[i];
                    if (seman.HaveStatusEffect(h))
                    {
                        seman.RemoveStatusEffect(h, false);
                    }
                }
            }

            // 3. Apply full heal (+25 / +35 / +45 / +55 HP)
            if (healAmount > 0.01f)
            {
                target.Heal(healAmount, true);
            }
        }

        internal static void ExecutePurificationWave(Character owner, ItemDrop.ItemData weapon)
        {
            if (owner == null) return;

            int quality = (weapon != null) ? Mathf.Max(1, weapon.m_quality) : 1;
            float radius = BlackForestCleanseRadius != null ? Mathf.Max(1f, BlackForestCleanseRadius.Value) : 10f;
            float healAmount = GetPurificationHealForQuality(quality);
            Vector3 center = owner.transform.position;

            // Spawn networked emerald-gold expanding wave FX so all co-op players see the 10m ritual wave!
            if (s_purificationWaveNetPrefab != null)
            {
                GameObject waveGo = UnityEngine.Object.Instantiate(s_purificationWaveNetPrefab, center + Vector3.up * 0.12f, Quaternion.identity);
                PurificationWaveVisual vis = waveGo.GetComponent<PurificationWaveVisual>();
                if (vis != null)
                {
                    vis.InitOwner(radius);
                }
            }

            // Cleanse & heal every living player within 10m (including the caster)
            float rSq = radius * radius;
            List<Player> players = Player.GetAllPlayers();
            int affectedCount = 0;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    Player p = players[i];
                    if (p == null || p.IsDead()) continue;
                    if ((p.transform.position - center).sqrMagnitude <= rSq)
                    {
                        ZNetView nv = p.GetComponent<ZNetView>();
                        if (nv != null && nv.IsValid() && !nv.IsOwner())
                        {
                            // Atomic single RPC so remote ally cleanses Rot BEFORE applying Heal!
                            nv.InvokeRPC(RpcPurifyPlayer, healAmount);
                        }
                        else
                        {
                            ApplyPurificationAndHealLocal(p, healAmount);
                        }
                        affectedCount++;
                    }
                }
            }

            if (affectedCount > 0)
            {
                owner.RaiseSkill(Skills.SkillType.BloodMagic, 1.5f);
            }
        }

        /// <summary>
        /// Computes extra damage added onto `weapon.GetDamage()` by external enchantment/attribute mods
        /// (such as `WeaponAttributes` +Lightning/+Fire/+Frost/+Poison/+Spirit/+Physical) beyond the weapon's own `m_shared` base + per-level stats.
        /// </summary>
        internal static HitData.DamageTypes GetBonusEnchantDamages(ItemDrop.ItemData weapon)
        {
            HitData.DamageTypes bonus = new HitData.DamageTypes();
            if (weapon == null || weapon.m_shared == null) return bonus;

            int q = Mathf.Max(1, weapon.m_quality);
            HitData.DamageTypes actual = weapon.GetDamage();

            HitData.DamageTypes rawBase = weapon.m_shared.m_damages;
            if (q > 1)
            {
                rawBase.Add(weapon.m_shared.m_damagesPerLevel, q - 1);
            }
            if (weapon.m_worldLevel > 0 && Game.instance != null)
            {
                rawBase.IncreaseEqually(weapon.m_worldLevel * Game.instance.m_worldLevelGearBaseDamage, true);
            }

            bonus.m_damage = Mathf.Max(0f, actual.m_damage - rawBase.m_damage);
            bonus.m_blunt = Mathf.Max(0f, actual.m_blunt - rawBase.m_blunt);
            bonus.m_slash = Mathf.Max(0f, actual.m_slash - rawBase.m_slash);
            bonus.m_pierce = Mathf.Max(0f, actual.m_pierce - rawBase.m_pierce);
            bonus.m_chop = Mathf.Max(0f, actual.m_chop - rawBase.m_chop);
            bonus.m_pickaxe = Mathf.Max(0f, actual.m_pickaxe - rawBase.m_pickaxe);
            bonus.m_fire = Mathf.Max(0f, actual.m_fire - rawBase.m_fire);
            bonus.m_frost = Mathf.Max(0f, actual.m_frost - rawBase.m_frost);
            bonus.m_lightning = Mathf.Max(0f, actual.m_lightning - rawBase.m_lightning);
            bonus.m_poison = Mathf.Max(0f, actual.m_poison - rawBase.m_poison);
            bonus.m_spirit = Mathf.Max(0f, actual.m_spirit - rawBase.m_spirit);
            return bonus;
        }

        // ====================================================================================================
        // HELPER: Plains Blood-Harvest Totem Cap Management (Strictly max 1 active totem) & Field Registries
        // ====================================================================================================
        internal static void RegisterDeployedTotem(Character owner, PlainsBloodHarvestTotemZone newTotem)
        {
            if (newTotem == null) return;
            Player p = owner as Player;
            long pid = (p != null) ? p.GetPlayerID() : 0L;

            List<PlainsBloodHarvestTotemZone> list;
            if (!s_totemsByPlayer.TryGetValue(pid, out list))
            {
                list = new List<PlainsBloodHarvestTotemZone>();
                s_totemsByPlayer[pid] = list;
            }

            // Prune destroyed or inactive totems
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null || !list[i].IsTotemAlive)
                {
                    list.RemoveAt(i);
                }
            }

            const int MaxTotems = 1; // Strictly 1 active totem per player!
            while (list.Count >= MaxTotems && list.Count > 0)
            {
                PlainsBloodHarvestTotemZone oldest = list[0];
                list.RemoveAt(0);
                if (oldest != null && oldest.IsTotemAlive)
                {
                    oldest.DespawnReplaced();
                }
            }

            list.Add(newTotem);
        }

        internal static void MarkEnemyPlainsSlowed(Character enemy, float graceSeconds)
        {
            if (enemy == null) return;
            float exp = Time.time + graceSeconds;
            float cur;
            if (!s_plainsSlowedEnemies.TryGetValue(enemy, out cur) || exp > cur)
            {
                s_plainsSlowedEnemies[enemy] = exp;
            }
        }

        internal static bool IsEnemyPlainsSlowed(Character enemy)
        {
            if (enemy == null || s_plainsSlowedEnemies.Count == 0) return false;
            float exp;
            if (s_plainsSlowedEnemies.TryGetValue(enemy, out exp))
            {
                if (Time.time <= exp) return true;
                s_plainsSlowedEnemies.Remove(enemy);
            }
            return false;
        }

        internal static void MarkPlayerPlainsBuffed(Character player, float graceSeconds)
        {
            if (player == null) return;
            float exp = Time.time + graceSeconds;
            float cur;
            if (!s_plainsBuffedPlayers.TryGetValue(player, out cur) || exp > cur)
            {
                s_plainsBuffedPlayers[player] = exp;
            }

            SEMan sem = player.GetSEMan();
            if (sem != null && s_plainsBuffStatusEffect != null)
            {
                StatusEffect active = sem.GetStatusEffect(s_plainsBuffStatusHash);
                if (active != null)
                {
                    active.ResetTime();
                }
                else
                {
                    sem.AddStatusEffect(s_plainsBuffStatusEffect, true, 0, 0f);
                }
            }
        }

        internal static bool IsPlayerPlainsBuffed(Character player)
        {
            if (player == null || s_plainsBuffedPlayers.Count == 0) return false;
            float exp;
            if (s_plainsBuffedPlayers.TryGetValue(player, out exp))
            {
                if (Time.time <= exp) return true;
                s_plainsBuffedPlayers.Remove(player);
            }
            return false;
        }

        internal static void SpawnImpactBurstLight(Vector3 pos, Color color, float range, float intensity, float duration)
        {
            GameObject go = new GameObject("SpellImpactFlash");
            go.transform.position = pos;
            TransientFlashLight fl = go.AddComponent<TransientFlashLight>();
            fl.Init(color, range, intensity, duration);
        }
    }

    // ========================================================================================================
    // HARMONY PATCHES FOR STAVES & MEADOWS CHESTS
    // ========================================================================================================

    /// <summary>
    /// Guarantees Meadows abandoned structure chests additionally have a configurable roll for Wind Staff relic parts
    /// even if a world/location uses custom Meadows chest prefabs.
    /// </summary>
    [HarmonyPatch(typeof(Container), "AddDefaultItems")]
    public static class Container_AddDefaultItems_MeadowsParts_Patch
    {
        private static void Postfix(Container __instance)
        {
            try
            {
                if (__instance == null || __instance.GetInventory() == null) return;
                string pName = __instance.name != null ? __instance.name.ToLowerInvariant() : "";
                if (!pName.Contains("treasurechest_meadows")) return;

                Inventory inv = __instance.GetInventory();
                // Check if DropTable already rolled one of our 3 parts
                List<ItemDrop.ItemData> items = inv.GetAllItems();
                for (int i = 0; i < items.Count; i++)
                {
                    string iname = items[i].m_shared != null ? items[i].m_shared.m_name : "";
                    if (iname == "$item_part_windcrystal" || iname == "$item_part_carvedhandle" || iname == "$item_part_runicbinding")
                    {
                        return; // Already received a part from DropTable!
                    }
                }

                float chance = MagicStavesModule.MeadowsChestDropChance != null ? MagicStavesModule.MeadowsChestDropChance.Value : 0.55f;
                if (UnityEngine.Random.value <= chance && ObjectDB.instance != null)
                {
                    string[] partNames = new string[]
                    {
                        MagicStavesModule.PartCrystalPrefab,
                        MagicStavesModule.PartHandlePrefab,
                        MagicStavesModule.PartBindingPrefab
                    };
                    string chosen = partNames[UnityEngine.Random.Range(0, partNames.Length)];
                    GameObject prefab = ObjectDB.instance.GetItemPrefab(chosen);
                    if (prefab != null)
                    {
                        inv.AddItem(prefab, 1);
                    }
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Enforces strictly fixed Mountain Staff charge duration per weapon quality level (3.0 / 3.5 / 4.0 / 4.5s)
    /// with ZERO skill haste reduction, driving Valheim's native crosshair charge circle smoothly from 0% to 100%!
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), "GetAttackDrawPercentage")]
    public static class Humanoid_GetAttackDrawPercentage_Mountain_Patch
    {
        private static bool Prefix(Humanoid __instance, float ___m_attackDrawTime, ref float __result)
        {
            if (__instance == null) return true;
            ItemDrop.ItemData weapon = __instance.GetCurrentWeapon();
            if (!MagicStavesModule.IsMountainRailgun(weapon)) return true;

            if (___m_attackDrawTime <= 0f)
            {
                __result = 0f;
                return false;
            }

            float required = MagicStavesModule.GetMountainRequiredChargeSeconds(weapon);
            __result = Mathf.Clamp01(___m_attackDrawTime / required);
            return false;
        }
    }

    /// <summary>
    /// Ensures the Mountain Railgun drains 10 Eitr/sec at level 1 + 2 Eitr/sec per upgrade level (configurable) while charging,
    /// AND stops/interrupts the charge immediately if Eitr runs out mid-charge (just like running out of stamina on a bow)!
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetDrawEitrDrain")]
    public static class ItemData_GetDrawEitrDrain_Mountain_Patch
    {
        private static bool Prefix(ItemDrop.ItemData __instance, ref float __result)
        {
            if (!MagicStavesModule.IsMountainRailgun(__instance)) return true;

            // RP Sorcerer "Arcane Resonance" synergy: every 5th spell cast is FREE (0 Eitr/sec during Railgun charge)!
            if (RPClassesModule.IsActive && Player.m_localPlayer != null &&
                RPPlayerClassManager.GetPlayerClass(Player.m_localPlayer) == RPClassType.Sorcerer &&
                RPHarmonyPatches.SorcererFreeCastTracker.IsNextCastFree)
            {
                __result = 0f;
                return false;
            }

            __result = MagicStavesModule.GetMountainDrawEitrPerSec(__instance);
            return false;
        }
    }

    /// <summary>
    /// Displays Mountain Railgun's quality-scaled Eitr drain (`Eitr drain: X/s`) and Charge time (`Charge time: Y s`)
    /// directly inside the orange item stats section (not in the flavor description), automatically updating on Crafting/Upgrade preview!
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    public static class ItemData_GetTooltip_MountainStats_Patch
    {
        private static void Postfix(ItemDrop.ItemData item, int qualityLevel, ref string __result)
        {
            if (item == null || __result == null || !MagicStavesModule.IsMountainRailgun(item))
            {
                return;
            }

            float drain = MagicStavesModule.GetMountainDrawEitrPerSecForQuality(qualityLevel);
            float chargeSec = MagicStavesModule.GetMountainRequiredChargeSecondsForQuality(qualityLevel);
            bool isRu = MagicStavesModule.IsRussianLanguageSelected();

            string drainStat = isRu
                ? string.Format(
                    "\n$item_staff_mountain_eitrhold: <color=orange>{0:0.#}</color>/с\n$item_staff_mountain_chargetime: <color=orange>{1:0.0}</color> с",
                    drain,
                    chargeSec
                )
                : string.Format(
                    "\n$item_staff_mountain_eitrhold: <color=orange>{0:0.#}</color>/s\n$item_staff_mountain_chargetime: <color=orange>{1:0.0}</color> s",
                    drain,
                    chargeSec
                );

            // Insert cleanly in the primary combat stats block right before Block / Knockback (exact same spot as vanilla `$item_staminahold`)!
            int insertIdx = __result.IndexOf("\n$item_blockpower:", StringComparison.Ordinal);
            if (insertIdx < 0)
            {
                insertIdx = __result.IndexOf("\n$item_blockarmor:", StringComparison.Ordinal);
            }
            if (insertIdx < 0)
            {
                insertIdx = __result.IndexOf("\n$item_knockback:", StringComparison.Ordinal);
            }
            if (insertIdx < 0)
            {
                insertIdx = __result.IndexOf("\n$item_eitrregen_modifier:", StringComparison.Ordinal);
            }

            if (insertIdx >= 0)
            {
                __result = __result.Insert(insertIdx, drainStat);
            }
            else
            {
                __result += drainStat;
            }
        }
    }

    /// <summary>
    /// Dundr's `"recharge"` state on Animator Layer 1 has the `"minoraction"` tag (`Player.s_animatorTagMinorAction`).
    /// In vanilla Valheim, `Player.UpdateAttackBowDraw` and `Humanoid.StartAttack` immediately abort/refuse if `InMinorAction()` is true!
    /// For Mountain Railgun (`m_requiresReload == false`), real minor actions (equipping/unequipping gear) sit in `Player.m_actionQueue`.
    /// When `m_actionQueue` is empty, Layer 1 `"recharge"` is OUR bow-draw charge animation — NOT a blocking minor action!
    /// (Note: `Player.InMinorActionSlowdown()` is separate and untouched, so Dundr's authentic charging walk-slowdown still works!)
    /// </summary>
    [HarmonyPatch(typeof(Player), "InMinorAction")]
    public static class Player_InMinorAction_MountainRailgun_Patch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (!__result || __instance == null) return;
            if (__instance.GetActionQueueCount() > 0) return;

            ItemDrop.ItemData weapon = __instance.GetCurrentWeapon();
            if (MagicStavesModule.IsMountainRailgun(weapon))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
    public static class Player_UpdateAttackBowDraw_MountainEitrGate_Patch
    {
        private static bool s_firedReadyAnim;

        private static bool Prefix(Player __instance, ItemDrop.ItemData weapon, ref float ___m_attackDrawTime, bool ___m_attackHold, ZSyncAnimation ___m_zanim)
        {
            if (__instance == null || !MagicStavesModule.IsMountainRailgun(weapon)) return true;

            if (___m_attackDrawTime <= 0f)
            {
                s_firedReadyAnim = false;
            }

            string drawAnim = (weapon.m_shared != null && weapon.m_shared.m_attack != null)
                ? weapon.m_shared.m_attack.m_drawAnimationState
                : null;

            // When releasing LMB after charging (`!m_attackHold && m_attackDrawTime > 0f`), clear Layer 1 `"recharge"` bool
            // BEFORE vanilla `UpdateAttackBowDraw` calls `StartAttack(null, false)` so Layer 1 exits `"recharge"` cleanly
            // and never masks Dundr's shot animation trigger!
            if (!___m_attackHold && ___m_attackDrawTime > 0f)
            {
                if (___m_zanim != null && !string.IsNullOrEmpty(drawAnim))
                {
                    ___m_zanim.SetBool(drawAnim, false);
                }
                return true;
            }

            float drain = weapon.GetDrawEitrDrain();
            if (drain > 0f && !__instance.HaveEitr(0f))
            {
                if (___m_attackDrawTime > 0f)
                {
                    if (__instance == Player.m_localPlayer && Hud.instance != null)
                    {
                        Hud.instance.EitrBarEmptyFlash();
                    }
                    // Clear Dundr charging bool cleanly before interrupting draw
                    if (___m_zanim != null && !string.IsNullOrEmpty(drawAnim))
                    {
                        ___m_zanim.SetBool(drawAnim, false);
                    }
                    // Interrupt draw: If < 100% charged, Attack.Start will fizzle; if >= 100% charged, fire the fully charged bolt before releasing!
                    __instance.StartAttack(null, false);
                    ___m_attackDrawTime = -1f;
                    s_firedReadyAnim = false;
                    return false;
                }
                else if (___m_attackDrawTime == 0f && __instance.InAttack() == false)
                {
                    // Prevent starting draw with 0 Eitr (vanilla UpdateAttackBowDraw only gated hold loop on HaveStamina, not HaveEitr!)
                    if (ZInput.GetButton("Attack") || ZInput.GetButton("JoyAttack"))
                    {
                        if (__instance == Player.m_localPlayer && Hud.instance != null)
                        {
                            Hud.instance.EitrBarEmptyFlash();
                        }
                        ___m_attackDrawTime = -1f;
                        return false;
                    }
                }
            }
            return true;
        }

        private static void Postfix(Player __instance, ItemDrop.ItemData weapon, float ___m_attackDrawTime, ZSyncAnimation ___m_zanim)
        {
            if (__instance == null || ___m_zanim == null || !MagicStavesModule.IsMountainRailgun(weapon)) return;
            if (weapon.m_shared == null || weapon.m_shared.m_attack == null) return;

            string drawAnim = weapon.m_shared.m_attack.m_drawAnimationState;
            if (string.IsNullOrEmpty(drawAnim)) return;

            // Once Mountain Railgun reaches 100% charge, transition from Dundr's `recharge` cranking loop into Dundr's
            // aimed-ready stance (`recharge` = false + `recharge_done` trigger) while keeping the railgun armed!
            if (___m_attackDrawTime > 0f)
            {
                float pct = __instance.GetAttackDrawPercentage();
                if (pct >= 0.999f)
                {
                    ___m_zanim.SetBool(drawAnim, false);
                    if (!s_firedReadyAnim)
                    {
                        s_firedReadyAnim = true;
                        ___m_zanim.SetTrigger(drawAnim + "_done");
                    }
                }
            }
            else
            {
                s_firedReadyAnim = false;
            }
        }
    }

    /// <summary>
    /// Enforces the strict Mountain Railgun release rule:
    /// If the player releases LMB before reaching 100% of the level's fixed charge time (3.0/3.5/4.0/4.5s),
    /// the shot FIZZLES with zero damage!
    /// </summary>
    [HarmonyPatch(typeof(Attack), "Start")]
    public static class Attack_Start_MountainRailgun_Patch
    {
        private static bool Prefix(Humanoid character, ItemDrop.ItemData weapon, float attackDrawPercentage, ref bool __result)
        {
            if (!MagicStavesModule.IsMountainRailgun(weapon)) return true;

            if (attackDrawPercentage < 0.995f)
            {
                Player p = character as Player;
                if (p != null && p == Player.m_localPlayer)
                {
                    p.Message(MessageHud.MessageType.Center, "$msg_mtn_fizzle", 0, null);
                    MagicStavesModule.SpawnImpactBurstLight(
                        p.transform.position + Vector3.up * 1.5f + p.GetLookDir() * 0.6f,
                        new Color(0.4f, 0.8f, 1.0f, 1f),
                        3.0f,
                        1.4f,
                        0.25f
                    );
                }
                __result = false;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Strips ALL damage resistances and immunities (Blunt, Slash, Pierce, Fire, Frost, Lightning, Poison, Spirit -> Normal)
    /// on any enemy or boss standing inside (or just hit by) the Swamp Staff's Spirit Miasma Cloud!
    /// Leaves Weak / VeryWeak vulnerabilities untouched and never alters Chop / Pickaxe.
    /// </summary>
    [HarmonyPatch(typeof(Character), "GetDamageModifiers")]
    public static class Character_GetDamageModifiers_SwampMiasma_Patch
    {
        private static void Postfix(Character __instance, ref HitData.DamageModifiers __result)
        {
            if (__instance == null || !MagicStavesModule.IsCharacterMiasmaAfflicted(__instance)) return;

            __result.m_blunt = MagicStavesModule.StripResistModifier(__result.m_blunt);
            __result.m_slash = MagicStavesModule.StripResistModifier(__result.m_slash);
            __result.m_pierce = MagicStavesModule.StripResistModifier(__result.m_pierce);
            __result.m_fire = MagicStavesModule.StripResistModifier(__result.m_fire);
            __result.m_frost = MagicStavesModule.StripResistModifier(__result.m_frost);
            __result.m_lightning = MagicStavesModule.StripResistModifier(__result.m_lightning);
            __result.m_poison = MagicStavesModule.StripResistModifier(__result.m_poison);
            __result.m_spirit = MagicStavesModule.StripResistModifier(__result.m_spirit);
        }
    }

    /// <summary>
    /// Also allows Poison & Spirit status effects in Character.RPC_Damage to apply even if the prefab's raw
    /// `m_damageModifiers.m_poison` was `Immune` (such as Bonemass or Skeletons) while inside the Swamp Miasma Cloud!
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class Character_RPCDamage_SwampMiasmaImmunityBypass_Patch
    {
        private static void Prefix(Character __instance, out HitData.DamageModifiers __state)
        {
            __state = __instance.m_damageModifiers;
            if (__instance != null && MagicStavesModule.IsCharacterMiasmaAfflicted(__instance))
            {
                __instance.m_damageModifiers.m_blunt = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_blunt);
                __instance.m_damageModifiers.m_slash = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_slash);
                __instance.m_damageModifiers.m_pierce = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_pierce);
                __instance.m_damageModifiers.m_fire = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_fire);
                __instance.m_damageModifiers.m_frost = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_frost);
                __instance.m_damageModifiers.m_lightning = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_lightning);
                __instance.m_damageModifiers.m_poison = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_poison);
                __instance.m_damageModifiers.m_spirit = MagicStavesModule.StripResistModifier(__instance.m_damageModifiers.m_spirit);
            }
        }

        private static void Postfix(Character __instance, HitData.DamageModifiers __state)
        {
            if (__instance != null)
            {
                __instance.m_damageModifiers = __state;
            }
        }
    }

    /// <summary>
    /// Registers the atomic co-op RPC for the Black Forest Staff of Purification on every spawned Player
    /// so cleansing injuries/debuffs and healing (+25..+55 HP) execute in strict sequence on remote allies!
    /// </summary>
    [HarmonyPatch(typeof(Character), "Awake")]
    public static class Character_Awake_PurificationRpc_Patch
    {
        private static void Postfix(Character __instance)
        {
            Player p = __instance as Player;
            if (p == null) return;
            ZNetView nv = p.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;

            nv.Register<float>(MagicStavesModule.RpcPurifyPlayer, delegate(long sender, float healAmount)
            {
                MagicStavesModule.ApplyPurificationAndHealLocal(p, healAmount);
            });
        }
    }

    /// <summary>
    /// Slows enemies inside the Plains Blood Totem field by 40% (`speed *= 0.60f`).
    /// Note: Player +10% move speed, +50% HP & Eitr regen, and -30% ALL stamina costs are handled natively by `SE_PlainsBloodFieldBuff` (`SE_Stats`)!
    /// </summary>
    [HarmonyPatch(typeof(SEMan), "ApplyStatusEffectSpeedMods")]
    public static class SEMan_ApplyStatusEffectSpeedMods_PlainsFieldSlow_Patch
    {
        private static void Postfix(Character ___m_character, ref float speed)
        {
            if (___m_character != null && MagicStavesModule.IsEnemyPlainsSlowed(___m_character))
            {
                speed *= 0.60f;
            }
        }
    }

    /// <summary>
    /// Reduces ALL Eitr & Health action costs by 30% (`* 0.70f`) for players standing inside the Plains Blood Totem field
    /// (complementing the -30% stamina modifiers on `SE_PlainsBloodFieldBuff` so literally EVERY resource expenditure is -30%!).
    /// </summary>
    [HarmonyPatch(typeof(Character), "UseEitr")]
    public static class Character_UseEitr_PlainsFieldDiscount_Patch
    {
        private static void Prefix(Character __instance, ref float eitr)
        {
            if (eitr > 0f && __instance != null && MagicStavesModule.IsPlayerPlainsBuffed(__instance))
            {
                eitr *= 0.70f;
            }
        }
    }

    [HarmonyPatch(typeof(Character), "TryUseEitr")]
    public static class Character_TryUseEitr_PlainsFieldDiscount_Patch
    {
        private static void Prefix(Character __instance, ref float eitrUse)
        {
            if (eitrUse > 0f && __instance != null && MagicStavesModule.IsPlayerPlainsBuffed(__instance))
            {
                eitrUse *= 0.70f;
            }
        }
    }

    [HarmonyPatch(typeof(Character), "UseHealth")]
    public static class Character_UseHealth_PlainsFieldDiscount_Patch
    {
        private static void Prefix(Character __instance, ref float hp)
        {
            if (hp > 0f && __instance != null && MagicStavesModule.IsPlayerPlainsBuffed(__instance))
            {
                hp *= 0.70f;
            }
        }
    }

    // ========================================================================================================
    // 1. MEADOWS STAFF SPELL: Piercing Crescent Wind Blade (`WindBladeProjectile`)
    // ========================================================================================================
    public class WindBladeProjectile : MonoBehaviour, IProjectile
    {
        private ZNetView m_nview;
        private bool m_visualsBuilt;
        private Character m_owner;
        private Vector3 m_vel;
        private HitData m_baseHit;
        private float m_traveled;
        private const float MaxFlightDistance = MagicStavesModule.MeadowsWindFlightRange; // 21.0m = Staff 5 totem zone diameter
        private readonly HashSet<Character> m_hitTargets = new HashSet<Character>();
        private int m_envMask;
        private int m_charMask;
        private ParticleSystem m_wakePs;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid())
            {
                // Build visuals immediately on BOTH owner and remote co-op clients!
                BuildWindVisuals();
            }
        }

        public void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            m_owner = owner;
            Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : (owner != null ? owner.GetLookDir() : transform.forward);
            if (owner is Player)
            {
                dir = ((Player)owner).GetLookDir();
            }
            float speed = Mathf.Max(22f, velocity.magnitude);
            m_vel = dir * speed;
            m_baseHit = hitData;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            m_envMask = LayerMask.GetMask(new string[] { "Default", "static_solid", "terrain", "piece" });
            m_charMask = LayerMask.GetMask(new string[] { "character", "character_net", "character_ghost", "hitbox", "character_noenv" });

            BuildWindVisuals();
        }

        public string GetTooltipString(int itemQuality)
        {
            if (MagicStavesModule.IsRussianLanguageSelected())
            {
                return string.Format("\n<color=#88eeff>Дальность полёта клинка: {0:F0} м</color>", MaxFlightDistance);
            }
            return string.Format("\n<color=#88eeff>Blade flight range: {0:F0} m</color>", MaxFlightDistance);
        }

        private void BuildWindVisuals()
        {
            if (m_visualsBuilt) return;
            m_visualsBuilt = true;

            Material fangMat = MagicStavesModule.GetWindFangMaterial();

            // Single strictly horizontal crescent "Wind Fang" arc (`Getsuga` / Wind Scar style — NO X-cross, NO wingtip triangles!)
            GameObject arc = new GameObject("WindFangHorizontalArc");
            arc.transform.SetParent(transform, false);
            arc.transform.localRotation = Quaternion.identity; // Strictly horizontal (0° roll/pitch)!
            arc.transform.localScale = new Vector3(1.12f, 1.0f, 1.08f);
            MeshFilter mf = arc.AddComponent<MeshFilter>();
            mf.sharedMesh = MagicStavesModule.GetCrescentBladeMesh();
            MeshRenderer mr = arc.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fangMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Smooth curved razor leading-edge highlight following the exact horizontal crescent contour
            GameObject edgeGo = new GameObject("WindFangLeadingEdgeGlow");
            edgeGo.transform.SetParent(arc.transform, false);
            LineRenderer edgeLr = edgeGo.AddComponent<LineRenderer>();
            edgeLr.useWorldSpace = false;
            edgeLr.loop = false;
            int edgePts = 29;
            edgeLr.positionCount = edgePts;
            edgeLr.material = MagicStavesModule.GetSoftSmokeParticleMaterial();
            edgeLr.widthCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.01f),
                new Keyframe(0.20f, 0.09f),
                new Keyframe(0.50f, 0.16f),
                new Keyframe(0.80f, 0.09f),
                new Keyframe(1.0f, 0.01f)
            );
            Gradient edgeGrad = new Gradient();
            edgeGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.40f, 0.90f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.95f, 1.00f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.40f, 0.90f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0.95f, 0.50f),
                    new GradientAlphaKey(0.85f, 0.75f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            edgeLr.colorGradient = edgeGrad;
            edgeLr.shadowCastingMode = ShadowCastingMode.Off;
            edgeLr.receiveShadows = false;

            float outerR = 1.22f;
            float maxArcAngle = 1.08f;
            float zCenterShift = -0.82f;
            for (int i = 0; i < edgePts; i++)
            {
                float t = (float)i / (edgePts - 1);
                float ang = Mathf.Lerp(-maxArcAngle, maxArcAngle, t);
                edgeLr.SetPosition(i, new Vector3(Mathf.Sin(ang) * outerR, 0f, Mathf.Cos(ang) * outerR + zCenterShift));
            }

            // Soft airy wind-mist wake trailing behind the horizontal crescent arc (zero sharp triangles/squares!)
            GameObject wakeGo = new GameObject("WindFangAirWake");
            wakeGo.transform.SetParent(transform, false);
            wakeGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            ParticleSystem wakePs = wakeGo.AddComponent<ParticleSystem>();
            m_wakePs = wakePs;
            wakePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule wMain = wakePs.main;
            wMain.loop = true;
            wMain.playOnAwake = false;
            wMain.simulationSpace = ParticleSystemSimulationSpace.World;
            wMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
            wMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            wMain.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            wMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            wMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.94f, 1.0f, 0.22f),
                new Color(0.85f, 0.99f, 1.0f, 0.34f)
            );
            wMain.maxParticles = 60;

            ParticleSystem.EmissionModule wEm = wakePs.emission;
            wEm.rateOverTime = 75f;

            ParticleSystem.ShapeModule wShape = wakePs.shape;
            wShape.shapeType = ParticleSystemShapeType.Box;
            wShape.scale = new Vector3(1.75f, 0.06f, 0.18f); // Horizontal wind wake strip

            ParticleSystem.ColorOverLifetimeModule wCol = wakePs.colorOverLifetime;
            wCol.enabled = true;
            Gradient wakeGrad = new Gradient();
            wakeGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.80f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.38f, 0.86f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.32f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            wCol.color = new ParticleSystem.MinMaxGradient(wakeGrad);

            ParticleSystemRenderer wPsr = wakeGo.GetComponent<ParticleSystemRenderer>();
            if (wPsr != null)
            {
                wPsr.sharedMaterial = MagicStavesModule.GetSoftSmokeParticleMaterial();
                wPsr.renderMode = ParticleSystemRenderMode.Billboard;
                wPsr.shadowCastingMode = ShadowCastingMode.Off;
                wPsr.receiveShadows = false;
            }
            wakePs.Play();

            Light l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 0.92f, 1.0f, 1f);
            l.range = 4.0f;
            l.intensity = 1.05f;
            l.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (m_wakePs != null)
            {
                m_wakePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_wakePs.transform.SetParent(null, true);
                Destroy(m_wakePs.gameObject, 0.5f);
                m_wakePs = null;
            }
        }

        private void DespawnFang()
        {
            if (m_wakePs != null)
            {
                m_wakePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_wakePs.transform.SetParent(null, true);
                Destroy(m_wakePs.gameObject, 0.5f);
                m_wakePs = null;
            }
            MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
        }

        private void Update()
        {
            if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
            {
                // Remote co-op client: position & rotation are smoothly replicated by `ZSyncTransform`!
                return;
            }

            float dt = Time.deltaTime;
            float remainingRange = MaxFlightDistance - m_traveled;
            if (remainingRange <= 0.001f)
            {
                DespawnFang();
                return;
            }

            Vector3 prev = transform.position;
            Vector3 desiredStep = m_vel * dt;
            float stepLen = desiredStep.magnitude;
            bool reachedMaxRange = false;
            if (stepLen >= remainingRange)
            {
                desiredStep = desiredStep.normalized * remainingRange;
                stepLen = remainingRange;
                reachedMaxRange = true;
            }

            Vector3 next = prev + desiredStep;
            m_traveled += stepLen;

            if (stepLen > 0.0001f)
            {
                // Check solid obstacle impact
                RaycastHit envHit;
                if (Physics.Raycast(prev, desiredStep.normalized, out envHit, stepLen, m_envMask, QueryTriggerInteraction.Ignore))
                {
                    MagicStavesModule.SpawnImpactBurstLight(envHit.point, new Color(0.5f, 0.95f, 1.0f, 1f), 3.5f, 1.5f, 0.25f);
                    DespawnFang();
                    return;
                }

                // Wide crescent sweep (radius 1.15m) piercing all enemies along its flight!
                Collider[] cols = Physics.OverlapSphere(next, 1.15f, m_charMask, QueryTriggerInteraction.Collide);
                bool raisedSkill = false;
                for (int i = 0; i < cols.Length; i++)
                {
                    IDestructible destr = cols[i].GetComponentInParent<IDestructible>();
                    Character ch = destr as Character;
                    if (ch == null || ch == m_owner || ch.IsDead()) continue;
                    if (m_owner != null && !BaseAI.IsEnemy(m_owner, ch)) continue;
                    if (m_hitTargets.Contains(ch)) continue;

                    m_hitTargets.Add(ch);
                    if (m_baseHit != null)
                    {
                        HitData hit = m_baseHit.Clone();
                        hit.m_point = ch.GetCenterPoint();
                        hit.m_dir = m_vel.normalized;
                        float kb = MagicStavesModule.MeadowsKnockbackForce != null ? MagicStavesModule.MeadowsKnockbackForce.Value : 100f;
                        hit.m_pushForce = kb;
                        ch.Damage(hit);

                        if (!raisedSkill && m_owner != null)
                        {
                            m_owner.RaiseSkill(Skills.SkillType.ElementalMagic, 1.0f);
                            raisedSkill = true;
                        }
                    }

                    MagicStavesModule.SpawnImpactBurstLight(ch.GetCenterPoint(), new Color(0.55f, 0.98f, 1.0f, 1f), 3.2f, 1.4f, 0.2f);
                }
            }

            transform.position = next;
            if (reachedMaxRange)
            {
                DespawnFang();
            }
        }
    }

    // ========================================================================================================
    // 2. BLACK FOREST STAFF SPELL: Purification & Healing Wave (10m AoE Debuff/Injury Cleanse + Heal)
    // ========================================================================================================
    public class PurificationTriggerProjectile : MonoBehaviour, IProjectile
    {
        public void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            MagicStavesModule.ExecutePurificationWave(owner, item);
            Destroy(gameObject);
        }

        public string GetTooltipString(int itemQuality)
        {
            float rad = MagicStavesModule.BlackForestCleanseRadius != null ? MagicStavesModule.BlackForestCleanseRadius.Value : 10f;
            float heal = MagicStavesModule.GetPurificationHealForQuality(itemQuality);
            if (MagicStavesModule.IsRussianLanguageSelected())
            {
                return string.Format(
                    "\n<color=#55ff99>Волна очищения (радиус {0:F0} м):\n• Снимает все боевые травмы и негативные эффекты\n• Восстанавливает здоровье: +{1:F0} HP</color>",
                    rad,
                    heal
                );
            }
            return string.Format(
                "\n<color=#55ff99>Purification Wave ({0:F0} m radius):\n• Cleanses all combat injuries & negative status effects\n• Restores health: +{1:F0} HP</color>",
                rad,
                heal
            );
        }
    }

    public class PurificationWaveVisual : MonoBehaviour
    {
        private ZNetView m_nview;
        private bool m_visualsBuilt;
        private float m_maxRadius = 10f;
        private float m_elapsed;
        private const float WaveDuration = 0.90f;
        private LineRenderer m_outerRing;
        private LineRenderer m_innerRing;
        private Light m_light;
        private ParticleSystem m_sparksPs;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                float r = m_nview.GetZDO().GetFloat(MagicStavesModule.ZdoKeyWaveRadius, 0f);
                if (r > 0.5f)
                {
                    m_maxRadius = r;
                    BuildWaveVisuals();
                }
            }
        }

        public void InitOwner(float maxRadius)
        {
            m_maxRadius = Mathf.Max(1f, maxRadius);
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                m_nview.GetZDO().Set(MagicStavesModule.ZdoKeyWaveRadius, m_maxRadius);
            }
            BuildWaveVisuals();
        }

        private void BuildWaveVisuals()
        {
            if (m_visualsBuilt) return;
            m_visualsBuilt = true;

            Material fxMat = new Material(MagicStavesModule.GetAdditiveFxMaterial());
            Color emerald = new Color(0.22f, 0.98f, 0.52f, 0.92f);
            Color gold = new Color(0.92f, 1.00f, 0.52f, 0.88f);
            fxMat.color = Color.white;
            if (fxMat.HasProperty("_Color")) fxMat.SetColor("_Color", Color.white);
            if (fxMat.HasProperty("_TintColor")) fxMat.SetColor("_TintColor", Color.white);

            GameObject outerGo = new GameObject("PurifyOuterRing");
            outerGo.transform.SetParent(transform, false);
            m_outerRing = outerGo.AddComponent<LineRenderer>();
            m_outerRing.useWorldSpace = false;
            m_outerRing.loop = true;
            m_outerRing.positionCount = 48;
            m_outerRing.startWidth = 0.22f;
            m_outerRing.endWidth = 0.22f;
            m_outerRing.material = fxMat;
            m_outerRing.startColor = emerald;
            m_outerRing.endColor = emerald;

            GameObject innerGo = new GameObject("PurifyInnerGoldRing");
            innerGo.transform.SetParent(transform, false);
            m_innerRing = innerGo.AddComponent<LineRenderer>();
            m_innerRing.useWorldSpace = false;
            m_innerRing.loop = true;
            m_innerRing.positionCount = 48;
            m_innerRing.startWidth = 0.12f;
            m_innerRing.endWidth = 0.12f;
            m_innerRing.material = fxMat;
            m_innerRing.startColor = gold;
            m_innerRing.endColor = gold;

            UpdateRings(0.5f, 1.0f);

            // Rising emerald-gold shamanic healing motes across the cleansed area
            GameObject spGo = new GameObject("PurifyHealingMotes");
            spGo.transform.SetParent(transform, false);
            spGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            m_sparksPs = spGo.AddComponent<ParticleSystem>();
            m_sparksPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = m_sparksPs.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.duration = 0.85f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);
            main.startColor = new ParticleSystem.MinMaxGradient(emerald, gold);
            main.maxParticles = 64;
            main.gravityModifier = -0.35f;

            ParticleSystem.EmissionModule em = m_sparksPs.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 48) });

            ParticleSystem.ShapeModule sh = m_sparksPs.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = m_maxRadius * 0.72f;
            sh.radiusThickness = 0.85f;
            sh.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colMod = m_sparksPs.colorOverLifetime;
            colMod.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.90f, 1.00f, 0.55f), 0.0f),
                    new GradientColorKey(new Color(0.20f, 0.96f, 0.50f), 0.55f),
                    new GradientColorKey(new Color(0.10f, 0.75f, 0.38f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.90f, 0.20f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colMod.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystemRenderer psr = spGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = MagicStavesModule.GetSoftSmokeParticleMaterial();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.shadowCastingMode = ShadowCastingMode.Off;
                psr.receiveShadows = false;
            }
            m_sparksPs.Play();

            m_light = gameObject.AddComponent<Light>();
            m_light.type = LightType.Point;
            m_light.color = new Color(0.32f, 1.00f, 0.56f, 1f);
            m_light.range = m_maxRadius * 1.35f;
            m_light.intensity = 2.4f;
            m_light.shadows = LightShadows.None;
        }

        private void UpdateRings(float currentRadius, float alpha)
        {
            if (m_outerRing != null)
            {
                Color cOut = new Color(0.22f, 0.98f, 0.52f, 0.92f * alpha);
                m_outerRing.startColor = cOut;
                m_outerRing.endColor = cOut;
                m_outerRing.startWidth = 0.26f * alpha;
                m_outerRing.endWidth = 0.26f * alpha;
                for (int i = 0; i < 48; i++)
                {
                    float a = (i / 48f) * Mathf.PI * 2f;
                    m_outerRing.SetPosition(i, new Vector3(Mathf.Cos(a) * currentRadius, 0.18f, Mathf.Sin(a) * currentRadius));
                }
            }
            if (m_innerRing != null)
            {
                float rIn = Mathf.Max(0.2f, currentRadius * 0.82f);
                Color cIn = new Color(0.92f, 1.00f, 0.52f, 0.85f * alpha);
                m_innerRing.startColor = cIn;
                m_innerRing.endColor = cIn;
                m_innerRing.startWidth = 0.14f * alpha;
                m_innerRing.endWidth = 0.14f * alpha;
                for (int i = 0; i < 48; i++)
                {
                    float a = (i / 48f) * Mathf.PI * 2f;
                    m_innerRing.SetPosition(i, new Vector3(Mathf.Cos(a) * rIn, 0.28f, Mathf.Sin(a) * rIn));
                }
            }
        }

        private void Update()
        {
            if (!m_visualsBuilt)
            {
                if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
                {
                    float r = m_nview.GetZDO().GetFloat(MagicStavesModule.ZdoKeyWaveRadius, 10f);
                    m_maxRadius = Mathf.Max(1f, r);
                }
                BuildWaveVisuals();
            }

            m_elapsed += Time.deltaTime;
            if (m_elapsed >= WaveDuration)
            {
                if (m_sparksPs != null)
                {
                    m_sparksPs.transform.SetParent(null, true);
                    Destroy(m_sparksPs.gameObject, 0.8f);
                    m_sparksPs = null;
                }
                MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                return;
            }

            float t = Mathf.Clamp01(m_elapsed / WaveDuration);
            float easeOut = 1.0f - (1.0f - t) * (1.0f - t);
            float curRadius = Mathf.Lerp(0.6f, m_maxRadius, easeOut);
            float alpha = 1.0f - Mathf.Pow(t, 1.6f);

            UpdateRings(curRadius, alpha);
            if (m_light != null)
            {
                m_light.intensity = 2.4f * alpha;
            }
        }
    }



    // ========================================================================================================
    // 3. SWAMP STAFF SPELL: Spirit Miasma Projectile & 5s Resistance-Stripping Cloud
    // ========================================================================================================
    public class SwampMiasmaProjectile : MonoBehaviour, IProjectile
    {
        private ZNetView m_nview;
        private bool m_visualsBuilt;
        private Character m_owner;
        private Vector3 m_vel;
        private int m_quality = 1;
        private float m_skillFactor;
        private HitData.DamageTypes m_bonusEnchant;
        private float m_life;
        private int m_hitMask;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid())
            {
                BuildMiasmaProjectileVisuals();
            }
        }

        public void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            m_owner = owner;
            Vector3 dir = (owner is Player) ? ((Player)owner).GetLookDir() : (velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward);
            m_vel = dir * Mathf.Max(20f, velocity.magnitude) + Vector3.up * 2.2f;
            m_quality = (item != null) ? Mathf.Max(1, item.m_quality) : 1;
            m_skillFactor = (owner != null) ? owner.GetSkillFactor(Skills.SkillType.ElementalMagic) : 0f;
            m_bonusEnchant = MagicStavesModule.GetBonusEnchantDamages(item);
            m_hitMask = LayerMask.GetMask(new string[] { "Default", "static_solid", "terrain", "piece", "character", "character_net", "character_ghost", "hitbox", "character_noenv" });

            BuildMiasmaProjectileVisuals();
        }

        private void BuildMiasmaProjectileVisuals()
        {
            if (m_visualsBuilt) return;
            m_visualsBuilt = true;

            Material fxMat = new Material(MagicStavesModule.GetAdditiveFxMaterial());
            Color col = new Color(0.25f, 1.0f, 0.62f, 0.9f);
            fxMat.color = col;
            if (fxMat.HasProperty("_Color")) fxMat.SetColor("_Color", col);
            if (fxMat.HasProperty("_TintColor")) fxMat.SetColor("_TintColor", col);

            GameObject glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider c = glob.GetComponent<Collider>();
            if (c != null) Destroy(c);
            glob.transform.SetParent(transform, false);
            glob.transform.localScale = Vector3.one * 0.30f;
            glob.GetComponent<MeshRenderer>().sharedMaterial = fxMat;

            TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.30f;
            tr.startWidth = 0.28f;
            tr.endWidth = 0.04f;
            tr.material = fxMat;
            tr.startColor = col;
            tr.endColor = new Color(0.1f, 0.6f, 0.3f, 0f);

            Light l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = col;
            l.range = 4.5f;
            l.intensity = 1.3f;
        }

        public string GetTooltipString(int itemQuality)
        {
            float dur = MagicStavesModule.SwampCloudDuration != null ? MagicStavesModule.SwampCloudDuration.Value : 8.0f;
            float rad = MagicStavesModule.SwampCloudRadius != null ? MagicStavesModule.SwampCloudRadius.Value : 7.2f;
            if (MagicStavesModule.IsRussianLanguageSelected())
            {
                return string.Format("\n<color=#55ffaa>Облако духа ({0:F0} сек., радиус {1:F1} м): снимает ВСЕ сопротивления урону у врагов</color>", dur, rad);
            }
            return string.Format("\n<color=#55ffaa>Spirit cloud ({0:F0} s, radius {1:F1} m): strips ALL enemy damage resistances</color>", dur, rad);
        }

        private void Update()
        {
            if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
            {
                return;
            }

            float dt = Time.deltaTime;
            m_life += dt;
            m_vel += Vector3.down * (9.5f * dt); // Gentle censer lob arc

            if (m_life >= 3.5f)
            {
                SpawnCloud(transform.position);
                MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                return;
            }

            Vector3 prev = transform.position;
            Vector3 next = prev + m_vel * dt;
            Vector3 step = next - prev;
            float dist = step.magnitude;

            if (dist > 0.0001f)
            {
                RaycastHit hit;
                if (Physics.SphereCast(prev, 0.22f, step.normalized, out hit, dist, m_hitMask, QueryTriggerInteraction.Collide))
                {
                    Character ch = hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
                    if (ch != null && ch == m_owner)
                    {
                        transform.position = next;
                        return;
                    }
                    if (hit.collider != null && (!hit.collider.isTrigger || ch != null))
                    {
                        SpawnCloud(hit.point);
                        MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                        return;
                    }
                }
            }

            transform.position = next;
        }

        private void SpawnCloud(Vector3 pos)
        {
            Vector3 impactPt = pos;

            // Snap close to ground if slightly airborne
            RaycastHit gh;
            int gMask = LayerMask.GetMask(new string[] { "terrain", "Default", "static_solid", "piece" });
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out gh, 5.0f, gMask, QueryTriggerInteraction.Ignore))
            {
                pos = gh.point + Vector3.up * 0.15f;
            }

            GameObject cloudGo;
            if (MagicStavesModule.s_swampMiasmaCloudPrefab != null)
            {
                cloudGo = UnityEngine.Object.Instantiate(MagicStavesModule.s_swampMiasmaCloudPrefab, pos, Quaternion.identity);
            }
            else
            {
                cloudGo = new GameObject("SwampSpiritMiasmaCloud");
                cloudGo.transform.position = pos;
                cloudGo.AddComponent<SwampMiasmaCloudZone>();
            }
            SwampMiasmaCloudZone zone = cloudGo.GetComponent<SwampMiasmaCloudZone>();
            if (zone != null)
            {
                zone.Init(m_owner, m_quality, m_skillFactor, m_bonusEnchant, impactPt);
            }
        }

        internal static void SpawnImpactDetonation(Vector3 hitPos, Vector3 groundPos, bool spawnNetworkedEffectList)
        {
            // Authentic Valheim explosion sound & splash particles (BombOoze / fallback StaffFireball)
            if (spawnNetworkedEffectList)
            {
                if (MagicStavesModule.s_vanillaPoisonBombHitEffects != null && MagicStavesModule.s_vanillaPoisonBombHitEffects.HasEffects())
                {
                    MagicStavesModule.s_vanillaPoisonBombHitEffects.Create(hitPos, Quaternion.identity, null, 1.15f, -1);
                }
                else if (MagicStavesModule.s_vanillaFireHitEffects != null && MagicStavesModule.s_vanillaFireHitEffects.HasEffects())
                {
                    MagicStavesModule.s_vanillaFireHitEffects.Create(hitPos, Quaternion.identity, null, 0.85f, -1);
                }
            }

            // Bright emerald-green explosion flash light
            MagicStavesModule.SpawnImpactBurstLight(hitPos + Vector3.up * 0.35f, new Color(0.28f, 1.0f, 0.56f, 1f), 9.5f, 3.6f, 0.38f);

            // Custom expanding emerald smoke blast + spirit ember burst (zero square particles!)
            GameObject blastGo = new GameObject("SwampMiasmaImpactBlastFX");
            blastGo.transform.position = groundPos + Vector3.up * 0.25f;

            Material smokeMat = MagicStavesModule.GetSoftSmokeParticleMaterial();

            // 1) Fast radial smoke shockwave puffing outward from impact center
            ParticleSystem burstSmoke = blastGo.AddComponent<ParticleSystem>();
            burstSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule smMain = burstSmoke.main;
            smMain.loop = false;
            smMain.playOnAwake = false;
            smMain.simulationSpace = ParticleSystemSimulationSpace.World;
            smMain.duration = 0.8f;
            smMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            smMain.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
            smMain.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.5f);
            smMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            smMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.26f, 0.96f, 0.52f, 0.72f),
                new Color(0.42f, 1.00f, 0.68f, 0.85f)
            );
            smMain.maxParticles = 60;
            smMain.gravityModifier = -0.15f;

            ParticleSystem.EmissionModule smEm = burstSmoke.emission;
            smEm.rateOverTime = 0f;
            smEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 34) });

            ParticleSystem.ShapeModule smShape = burstSmoke.shape;
            smShape.shapeType = ParticleSystemShapeType.Hemisphere;
            smShape.radius = 0.35f;
            smShape.scale = new Vector3(1.0f, 0.45f, 1.0f);

            ParticleSystem.LimitVelocityOverLifetimeModule smLim = burstSmoke.limitVelocityOverLifetime;
            smLim.enabled = true;
            smLim.limit = 1.6f;
            smLim.dampen = 0.28f;

            ParticleSystem.ColorOverLifetimeModule smCol = burstSmoke.colorOverLifetime;
            smCol.enabled = true;
            Gradient blastGrad = new Gradient();
            blastGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.45f, 1.0f, 0.68f), 0.0f),
                    new GradientColorKey(new Color(0.20f, 0.85f, 0.44f), 0.45f),
                    new GradientColorKey(new Color(0.12f, 0.62f, 0.30f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.48f, 0.45f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            smCol.color = new ParticleSystem.MinMaxGradient(blastGrad);

            ParticleSystem.SizeOverLifetimeModule smSize = burstSmoke.sizeOverLifetime;
            smSize.enabled = true;
            smSize.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.45f),
                new Keyframe(0.35f, 1.05f),
                new Keyframe(1.0f, 1.45f)
            ));

            ParticleSystemRenderer smPsr = blastGo.GetComponent<ParticleSystemRenderer>();
            if (smPsr != null)
            {
                smPsr.sharedMaterial = smokeMat;
                smPsr.renderMode = ParticleSystemRenderMode.Billboard;
                smPsr.sortMode = ParticleSystemSortMode.Distance;
                smPsr.shadowCastingMode = ShadowCastingMode.Off;
                smPsr.receiveShadows = false;
            }
            burstSmoke.Play();

            // 2) Glowing green spirit droplets/sparks flying out of the blast
            GameObject sparksGo = new GameObject("MiasmaBlastSparks");
            sparksGo.transform.SetParent(blastGo.transform, false);
            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            sparksPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule spMain = sparksPs.main;
            spMain.loop = false;
            spMain.playOnAwake = false;
            spMain.simulationSpace = ParticleSystemSimulationSpace.World;
            spMain.duration = 0.8f;
            spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            spMain.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 11.5f);
            spMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
            spMain.startColor = new Color(0.55f, 1.0f, 0.72f, 0.95f);
            spMain.maxParticles = 45;
            spMain.gravityModifier = 1.35f;

            ParticleSystem.EmissionModule spEm = sparksPs.emission;
            spEm.rateOverTime = 0f;
            spEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 28) });

            ParticleSystem.ShapeModule spShape = sparksPs.shape;
            spShape.shapeType = ParticleSystemShapeType.Hemisphere;
            spShape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule spCol = sparksPs.colorOverLifetime;
            spCol.enabled = true;
            spCol.color = new ParticleSystem.MinMaxGradient(blastGrad);

            ParticleSystemRenderer spPsr = sparksGo.GetComponent<ParticleSystemRenderer>();
            if (spPsr != null)
            {
                spPsr.sharedMaterial = smokeMat;
                spPsr.renderMode = ParticleSystemRenderMode.Stretch;
                spPsr.lengthScale = 1.8f;
                spPsr.velocityScale = 0.06f;
                spPsr.shadowCastingMode = ShadowCastingMode.Off;
            }
            sparksPs.Play();

            Destroy(blastGo, 1.6f);
        }
    }

    public class SwampMiasmaCloudZone : MonoBehaviour
    {
        private ZNetView m_nview;
        private bool m_visualsBuilt;
        private Character m_owner;
        private float m_duration = 8.0f;
        private float m_radius = 7.2f;
        private float m_dps;
        private HitData.DamageTypes m_bonusEnchantDps;
        private float m_elapsed;
        private float m_tickTimer;
        private const float TickInterval = 0.5f;
        private int m_charMask;
        private ParticleSystem m_groundFogPs;
        private ParticleSystem m_billowSmokePs;
        private bool m_stoppedEmission;
        private Light m_light;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
            {
                m_duration = m_nview.GetZDO().GetFloat(MagicStavesModule.ZdoKeyDuration, MagicStavesModule.SwampCloudDuration != null ? MagicStavesModule.SwampCloudDuration.Value : 8.0f);
                m_radius = MagicStavesModule.SwampCloudRadius != null ? MagicStavesModule.SwampCloudRadius.Value : 7.2f;
                m_charMask = LayerMask.GetMask(new string[] { "character", "character_net", "character_ghost", "hitbox", "character_noenv" });
                SwampMiasmaProjectile.SpawnImpactDetonation(transform.position + Vector3.up * 0.25f, transform.position, false);
                BuildCloudVisuals();
            }
        }

        public void Init(Character owner, int quality, float skillFactor, HitData.DamageTypes bonusEnchant, Vector3 impactPt)
        {
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            m_owner = owner;
            m_duration = MagicStavesModule.SwampCloudDuration != null ? MagicStavesModule.SwampCloudDuration.Value : 8.0f;
            m_radius = MagicStavesModule.SwampCloudRadius != null ? MagicStavesModule.SwampCloudRadius.Value : 7.2f;
            if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                m_nview.GetZDO().Set(MagicStavesModule.ZdoKeyDuration, m_duration);
            }

            float skillMult = 1.0f + 0.5f * skillFactor;
            float baseDps = (MagicStavesModule.SwampTickDamage != null ? MagicStavesModule.SwampTickDamage.Value : 28f)
                          + (MagicStavesModule.SwampTickDamagePerLevel != null ? MagicStavesModule.SwampTickDamagePerLevel.Value : 7f) * (Mathf.Max(1, quality) - 1);
            m_dps = baseDps * skillMult;
            m_bonusEnchantDps = bonusEnchant;
            m_bonusEnchantDps.Modify(skillMult);
            m_charMask = LayerMask.GetMask(new string[] { "character", "character_net", "character_ghost", "hitbox", "character_noenv" });

            SwampMiasmaProjectile.SpawnImpactDetonation(impactPt, transform.position, true);
            BuildCloudVisuals();
            // Immediate initial tick on impact!
            DoMiasmaTick();
        }

        private static ParticleSystem.MinMaxGradient CreateSmokeFadeGradient(Color peakColor, Color midColor)
        {
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(peakColor.r, peakColor.g, peakColor.b), 0.0f),
                    new GradientColorKey(new Color(midColor.r, midColor.g, midColor.b), 0.60f),
                    new GradientColorKey(new Color(midColor.r * 0.75f, midColor.g * 0.85f, midColor.b * 0.75f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(peakColor.a, 0.22f),
                    new GradientAlphaKey(midColor.a, 0.68f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            return new ParticleSystem.MinMaxGradient(grad);
        }

        private void BuildCloudVisuals()
        {
            if (m_visualsBuilt) return;
            m_visualsBuilt = true;

            Material smokeMat = MagicStavesModule.GetSoftSmokeParticleMaterial();

            float areaRatio = Mathf.Clamp((m_radius * m_radius) / (4.2f * 4.2f), 1f, 4f);
            float sizeScale = Mathf.Clamp(m_radius / 4.2f, 1f, 1.55f);

            // 1) Dense ground-hugging green smoke blanket covering the ENTIRE area (`m_radius`) with ZERO line rings and ZERO square quads!
            GameObject groundFogGo = new GameObject("MiasmaGroundSmokeBlanket");
            groundFogGo.transform.SetParent(transform, false);
            groundFogGo.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            m_groundFogPs = groundFogGo.AddComponent<ParticleSystem>();
            m_groundFogPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule gMain = m_groundFogPs.main;
            gMain.loop = true;
            gMain.playOnAwake = false;
            gMain.simulationSpace = ParticleSystemSimulationSpace.World;
            gMain.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.3f);
            gMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            gMain.startSize = new ParticleSystem.MinMaxCurve(2.4f * sizeScale, 3.6f * sizeScale);
            gMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            gMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.14f, 0.72f, 0.35f, 0.36f),
                new Color(0.22f, 0.84f, 0.46f, 0.46f)
            );
            gMain.maxParticles = Mathf.RoundToInt(185f * Mathf.Sqrt(areaRatio));
            gMain.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule gEm = m_groundFogPs.emission;
            gEm.rateOverTime = 45f * Mathf.Sqrt(areaRatio);
            gEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, (short)Mathf.RoundToInt(36f * Mathf.Sqrt(areaRatio))) });

            ParticleSystem.ShapeModule gShape = m_groundFogPs.shape;
            gShape.shapeType = ParticleSystemShapeType.Circle;
            gShape.radius = Mathf.Max(0.8f, m_radius - 0.55f);
            gShape.radiusThickness = 1.0f; // Uniformly fills the entire disk from center out to the edge!
            gShape.rotation = new Vector3(90f, 0f, 0f); // Lie flat in XZ plane!

            ParticleSystem.ColorOverLifetimeModule gCol = m_groundFogPs.colorOverLifetime;
            gCol.enabled = true;
            gCol.color = CreateSmokeFadeGradient(new Color(0.19f, 0.80f, 0.42f, 0.50f), new Color(0.12f, 0.64f, 0.32f, 0.35f));

            ParticleSystem.SizeOverLifetimeModule gSize = m_groundFogPs.sizeOverLifetime;
            gSize.enabled = true;
            AnimationCurve growCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.72f),
                new Keyframe(0.45f, 1.05f),
                new Keyframe(1.0f, 1.28f)
            );
            gSize.size = new ParticleSystem.MinMaxCurve(1.0f, growCurve);

            ParticleSystem.RotationOverLifetimeModule gRot = m_groundFogPs.rotationOverLifetime;
            gRot.enabled = true;
            gRot.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

            ParticleSystemRenderer gPsr = groundFogGo.GetComponent<ParticleSystemRenderer>();
            if (gPsr != null)
            {
                gPsr.sharedMaterial = smokeMat;
                gPsr.renderMode = ParticleSystemRenderMode.Billboard;
                gPsr.sortMode = ParticleSystemSortMode.Distance;
                gPsr.shadowCastingMode = ShadowCastingMode.Off;
                gPsr.receiveShadows = false;
            }
            m_groundFogPs.Play();

            // 2) Rising volumetric billowing green spirit smoke plumes across the whole cloud area
            GameObject billowGo = new GameObject("MiasmaBillowingSmoke");
            billowGo.transform.SetParent(transform, false);
            billowGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            m_billowSmokePs = billowGo.AddComponent<ParticleSystem>();
            m_billowSmokePs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule bMain = m_billowSmokePs.main;
            bMain.loop = true;
            bMain.playOnAwake = false;
            bMain.simulationSpace = ParticleSystemSimulationSpace.World;
            bMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);
            bMain.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
            bMain.startSize = new ParticleSystem.MinMaxCurve(1.8f * sizeScale, 3.0f * sizeScale);
            bMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            bMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.17f, 0.76f, 0.39f, 0.28f),
                new Color(0.28f, 0.88f, 0.54f, 0.37f)
            );
            bMain.maxParticles = Mathf.RoundToInt(135f * Mathf.Sqrt(areaRatio));
            bMain.gravityModifier = -0.06f;

            ParticleSystem.EmissionModule bEm = m_billowSmokePs.emission;
            bEm.rateOverTime = 30f * Mathf.Sqrt(areaRatio);
            bEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, (short)Mathf.RoundToInt(22f * Mathf.Sqrt(areaRatio))) });

            ParticleSystem.ShapeModule bShape = m_billowSmokePs.shape;
            bShape.shapeType = ParticleSystemShapeType.Hemisphere;
            bShape.radius = Mathf.Max(0.8f, m_radius - 0.65f);
            bShape.radiusThickness = 1.0f;
            bShape.scale = new Vector3(1.0f, 0.32f, 1.0f); // Wide low dome across the whole zone

            ParticleSystem.VelocityOverLifetimeModule bVel = m_billowSmokePs.velocityOverLifetime;
            bVel.enabled = true;
            bVel.space = ParticleSystemSimulationSpace.Local;
            bVel.orbitalY = new ParticleSystem.MinMaxCurve(0.22f, 0.48f); // Subtle organic swirling drift!
            bVel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);

            ParticleSystem.ColorOverLifetimeModule bCol = m_billowSmokePs.colorOverLifetime;
            bCol.enabled = true;
            bCol.color = CreateSmokeFadeGradient(new Color(0.23f, 0.84f, 0.48f, 0.40f), new Color(0.13f, 0.66f, 0.34f, 0.25f));

            ParticleSystem.SizeOverLifetimeModule bSize = m_billowSmokePs.sizeOverLifetime;
            bSize.enabled = true;
            bSize.size = new ParticleSystem.MinMaxCurve(1.0f, growCurve);

            ParticleSystem.RotationOverLifetimeModule bRot = m_billowSmokePs.rotationOverLifetime;
            bRot.enabled = true;
            bRot.z = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);

            ParticleSystemRenderer bPsr = billowGo.GetComponent<ParticleSystemRenderer>();
            if (bPsr != null)
            {
                bPsr.sharedMaterial = smokeMat;
                bPsr.renderMode = ParticleSystemRenderMode.Billboard;
                bPsr.sortMode = ParticleSystemSortMode.Distance;
                bPsr.shadowCastingMode = ShadowCastingMode.Off;
                bPsr.receiveShadows = false;
            }
            m_billowSmokePs.Play();

            m_light = gameObject.AddComponent<Light>();
            m_light.type = LightType.Point;
            m_light.color = new Color(0.18f, 0.85f, 0.45f, 1f);
            m_light.range = m_radius * 1.75f;
            m_light.intensity = 0.95f;
            m_light.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (m_groundFogPs != null)
            {
                m_groundFogPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_groundFogPs.transform.SetParent(null, true);
                Destroy(m_groundFogPs.gameObject, 2.2f);
                m_groundFogPs = null;
            }
            if (m_billowSmokePs != null)
            {
                m_billowSmokePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_billowSmokePs.transform.SetParent(null, true);
                Destroy(m_billowSmokePs.gameObject, 2.2f);
                m_billowSmokePs = null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_elapsed += dt;

            // Stop emitting new smoke 0.8s before expiration so the cloud dissipates naturally instead of popping out!
            if (!m_stoppedEmission && m_elapsed >= Mathf.Max(0.2f, m_duration - 0.85f))
            {
                m_stoppedEmission = true;
                if (m_groundFogPs != null) m_groundFogPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (m_billowSmokePs != null) m_billowSmokePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (m_elapsed >= m_duration)
            {
                // Detach lingering smoke particles for 1.8s so already-spawned puffs finish fading smoothly to 0 alpha
                if (m_groundFogPs != null)
                {
                    m_groundFogPs.transform.SetParent(null, true);
                    Destroy(m_groundFogPs.gameObject, 2.2f);
                    m_groundFogPs = null;
                }
                if (m_billowSmokePs != null)
                {
                    m_billowSmokePs.transform.SetParent(null, true);
                    Destroy(m_billowSmokePs.gameObject, 2.2f);
                    m_billowSmokePs = null;
                }
                MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                return;
            }

            if (m_light != null)
            {
                float fade = Mathf.Clamp01((m_duration - m_elapsed) / 0.85f);
                m_light.intensity = 0.95f * fade;
            }

            // Continuously keep all enemies inside the cloud stripped of resistances EVERY frame on ALL co-op clients
            // so co-op allies attacking enemies inside your Swamp Miasma Cloud ALSO deal full resistance-stripped damage!
            RefreshResistanceStripInZone(0.35f);

            // Periodic DoT ticks are executed strictly on the owner client so enemies don't take N-times damage!
            if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
            {
                return;
            }

            m_tickTimer += dt;
            if (m_tickTimer >= TickInterval)
            {
                m_tickTimer -= TickInterval;
                DoMiasmaTick();
            }
        }

        private void RefreshResistanceStripInZone(float grace)
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, m_radius, m_charMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < cols.Length; i++)
            {
                Character ch = cols[i].GetComponentInParent<Character>();
                if (ch == null || ch == m_owner || ch.IsDead()) continue;
                if (m_owner != null && !BaseAI.IsEnemy(m_owner, ch)) continue;
                MagicStavesModule.MarkCharacterMiasmaAfflicted(ch, grace);
            }
        }

        private void DoMiasmaTick()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, m_radius, m_charMask, QueryTriggerInteraction.Collide);
            HashSet<Character> hitOnce = new HashSet<Character>();
            float dmgThisTick = m_dps * TickInterval;
            HitData.DamageTypes bonusThisTick = m_bonusEnchantDps;
            bonusThisTick.Modify(TickInterval);

            for (int i = 0; i < cols.Length; i++)
            {
                Character ch = cols[i].GetComponentInParent<Character>();
                if (ch == null || ch == m_owner || ch.IsDead()) continue;
                if (m_owner != null && !BaseAI.IsEnemy(m_owner, ch)) continue;
                if (hitOnce.Contains(ch)) continue;
                hitOnce.Add(ch);

                // Strip ALL resistances BEFORE applying damage so even Spirit-immune / Poison-immune enemies take full damage!
                MagicStavesModule.MarkCharacterMiasmaAfflicted(ch, TickInterval + 0.45f);

                HitData hit = new HitData();
                // Direct spiritual miasma damage: Spirit reduced by 50% (0.275x) + Poison (0.45x) + any bonus WeaponAttributes enchantments!
                hit.m_damage.m_spirit = dmgThisTick * 0.275f;
                hit.m_damage.m_poison = dmgThisTick * 0.45f;
                hit.m_damage.Add(bonusThisTick, 1);
                hit.m_point = ch.GetCenterPoint();
                hit.m_dir = (ch.transform.position - transform.position).normalized;
                hit.m_skill = Skills.SkillType.ElementalMagic;
                if (m_owner != null)
                {
                    hit.SetAttacker(m_owner);
                    SEMan seman = m_owner.GetSEMan();
                    if (seman != null)
                    {
                        seman.ModifyAttack(Skills.SkillType.ElementalMagic, ref hit);
                    }
                }
                ch.Damage(hit);
            }

            if (hitOnce.Count > 0 && m_owner != null)
            {
                m_owner.RaiseSkill(Skills.SkillType.ElementalMagic, 0.35f);
            }
        }
    }

    // ========================================================================================================
    // 4. MOUNTAIN STAFF SPELL: Fixed-Charge Pinpoint Hitscan Lightning Railgun
    // ========================================================================================================
    public class MountainRailgunChargeVisual : MonoBehaviour
    {
        public Transform CrownAnchor;
        public Light CrownLight;

        private Player m_ownerPlayer;
        private Animator m_ownerAnimator;
        private float m_remoteVisualCharge;
        private LineRenderer m_arcRenderer;

        private void Start()
        {
            m_ownerPlayer = GetComponentInParent<Player>();
            m_ownerAnimator = (m_ownerPlayer != null) ? m_ownerPlayer.GetComponentInChildren<Animator>() : null;
            if (CrownAnchor != null)
            {
                GameObject arcGo = new GameObject("TuningProngElectricArc");
                arcGo.transform.SetParent(CrownAnchor, false);
                Material fxMat = new Material(MagicStavesModule.GetAdditiveFxMaterial());
                Color c = new Color(0.55f, 0.90f, 1.0f, 0.95f);
                fxMat.color = c;
                if (fxMat.HasProperty("_Color")) fxMat.SetColor("_Color", c);
                if (fxMat.HasProperty("_TintColor")) fxMat.SetColor("_TintColor", c);

                m_arcRenderer = arcGo.AddComponent<LineRenderer>();
                m_arcRenderer.useWorldSpace = false;
                m_arcRenderer.positionCount = 8;
                m_arcRenderer.startWidth = 0.03f;
                m_arcRenderer.endWidth = 0.03f;
                m_arcRenderer.material = fxMat;
                m_arcRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (m_ownerPlayer == null)
            {
                m_ownerPlayer = GetComponentInParent<Player>();
                if (m_ownerPlayer != null) m_ownerAnimator = m_ownerPlayer.GetComponentInChildren<Animator>();
            }
            if (m_ownerPlayer == null) return;

            float pct = 0f;
            if (m_ownerPlayer == Player.m_localPlayer)
            {
                ItemDrop.ItemData w = m_ownerPlayer.GetCurrentWeapon();
                if (!MagicStavesModule.IsMountainRailgun(w))
                {
                    if (m_arcRenderer != null) m_arcRenderer.enabled = false;
                    return;
                }
                pct = m_ownerPlayer.GetAttackDrawPercentage();
            }
            else
            {
                // Remote co-op player: `GetCurrentWeapon()` is null on non-local players (`VisEquipment` instantiated this visual directly!),
                // and `ZSyncAnimation` syncs the `"recharge"` charging bool on Animator Layer 1!
                if (m_ownerAnimator == null) m_ownerAnimator = m_ownerPlayer.GetComponentInChildren<Animator>();
                bool remoteCharging = (m_ownerAnimator != null && m_ownerAnimator.GetBool("recharge"));
                if (remoteCharging)
                {
                    m_remoteVisualCharge = Mathf.MoveTowards(m_remoteVisualCharge, 1.0f, Time.deltaTime / 3.2f);
                }
                else
                {
                    m_remoteVisualCharge = Mathf.MoveTowards(m_remoteVisualCharge, 0.0f, Time.deltaTime * 6.0f);
                }
                pct = m_remoteVisualCharge;
            }
            if (pct > 0.01f)
            {
                if (CrownLight != null)
                {
                    CrownLight.intensity = Mathf.Lerp(0.75f, 3.2f, pct);
                    CrownLight.range = Mathf.Lerp(2.0f, 4.8f, pct);
                    CrownLight.color = pct >= 0.995f
                        ? new Color(0.85f, 0.98f, 1.0f, 1f)
                        : new Color(0.35f, 0.80f, 1.0f, 1f);
                }
                if (m_arcRenderer != null)
                {
                    m_arcRenderer.enabled = true;
                    float wArc = Mathf.Lerp(0.018f, 0.055f, pct);
                    m_arcRenderer.startWidth = wArc;
                    m_arcRenderer.endWidth = wArc;
                    for (int i = 0; i < 8; i++)
                    {
                        float t = i / 7f;
                        float x = Mathf.Lerp(-0.11f, 0.11f, t);
                        float jitter = (i == 0 || i == 7) ? 0f : UnityEngine.Random.Range(-0.045f, 0.045f) * (0.5f + pct);
                        m_arcRenderer.SetPosition(i, new Vector3(x, jitter, UnityEngine.Random.Range(-0.03f, 0.03f)));
                    }
                }
            }
            else
            {
                if (CrownLight != null)
                {
                    CrownLight.intensity = 0.65f;
                    CrownLight.range = 2.0f;
                }
                if (m_arcRenderer != null) m_arcRenderer.enabled = false;
            }
        }
    }

    public class MountainRailgunTriggerProjectile : MonoBehaviour, IProjectile
    {
        public void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            FirePinpointRailgunBeam(owner, hitData, item);
            Destroy(gameObject);
        }

        public string GetTooltipString(int itemQuality)
        {
            return "";
        }

        private static bool IsBlockedModifier(HitData.DamageModifier mod)
        {
            return mod == HitData.DamageModifier.Immune || mod == HitData.DamageModifier.Ignore;
        }

        /// <summary>
        /// Returns true if `destr` is an indestructible world/fortress piece (e.g. Charred Fortress walls requiring Siege Ram tier > 4,
        /// invulnerable boss/dungeon structures with `m_health <= 0` or immune to all damage).
        /// </summary>
        private static bool IsIndestructibleBlocker(IDestructible destr)
        {
            if (destr == null) return true;
            if (destr is Character) return false;

            TreeBase tb = destr as TreeBase;
            if (tb != null) return tb.m_minToolTier > 4;

            TreeLog tl = destr as TreeLog;
            if (tl != null) return tl.m_minToolTier > 4;

            MineRock mr = destr as MineRock;
            if (mr != null) return mr.m_minToolTier > 4 || IsBlockedModifier(mr.m_damageModifiers.m_pickaxe);

            MineRock5 mr5 = destr as MineRock5;
            if (mr5 != null) return mr5.m_minToolTier > 4 || IsBlockedModifier(mr5.m_damageModifiers.m_pickaxe);

            WearNTear wnt = destr as WearNTear;
            if (wnt != null)
            {
                if (wnt.m_health <= 0f || wnt.m_health >= 50000f || wnt.m_minToolTier > 4) return true;
                HitData.DamageModifiers dm = wnt.m_damages;
                if (IsBlockedModifier(dm.m_lightning) && IsBlockedModifier(dm.m_chop) && IsBlockedModifier(dm.m_pickaxe) && IsBlockedModifier(dm.m_blunt))
                {
                    return true;
                }
                return false;
            }

            Destructible d = destr as Destructible;
            if (d != null)
            {
                if (d.m_health <= 0f || d.m_health >= 50000f || d.m_minToolTier > 4) return true;
                HitData.DamageModifiers dm = d.m_damages;
                if (IsBlockedModifier(dm.m_lightning) && IsBlockedModifier(dm.m_chop) && IsBlockedModifier(dm.m_pickaxe) && IsBlockedModifier(dm.m_slash) && IsBlockedModifier(dm.m_blunt))
                {
                    return true;
                }
                return false;
            }

            return false;
        }

        private static void PopulateRailgunDamageForTarget(IDestructible destr, HitData beamHit, float dmg, HitData.DamageTypes bonusEnchant)
        {
            beamHit.m_damage = new HitData.DamageTypes();
            beamHit.m_toolTier = 4; // Cleaves up to Plains/Mistlands trees, logs, ores & structures, while Fortress walls (>4) remain safe!

            if (destr is Character)
            {
                beamHit.m_damage.m_lightning = dmg;
                beamHit.m_damage.Add(bonusEnchant, 1);
                return;
            }

            // For environmental targets (trees, logs, rocks, structures), fold any enchantment bonus damage into the effective tool/elemental damage!
            float totalWithBonus = dmg + bonusEnchant.GetTotalDamage();

            if (destr is TreeBase || destr is TreeLog)
            {
                beamHit.m_damage.m_chop = totalWithBonus;
                return;
            }

            if (destr is MineRock || destr is MineRock5)
            {
                beamHit.m_damage.m_pickaxe = totalWithBonus;
                return;
            }

            WearNTear wnt = destr as WearNTear;
            if (wnt != null)
            {
                if (!IsBlockedModifier(wnt.m_damages.m_lightning)) beamHit.m_damage.m_lightning = totalWithBonus;
                else if (!IsBlockedModifier(wnt.m_damages.m_chop)) beamHit.m_damage.m_chop = totalWithBonus;
                else if (!IsBlockedModifier(wnt.m_damages.m_pickaxe)) beamHit.m_damage.m_pickaxe = totalWithBonus;
                else beamHit.m_damage.m_blunt = totalWithBonus;
                return;
            }

            Destructible d = destr as Destructible;
            if (d != null)
            {
                if (d.GetDestructibleType() == DestructibleType.Tree || (!IsBlockedModifier(d.m_damages.m_chop) && IsBlockedModifier(d.m_damages.m_lightning)))
                {
                    beamHit.m_damage.m_chop = totalWithBonus;
                }
                else if (!IsBlockedModifier(d.m_damages.m_lightning))
                {
                    beamHit.m_damage.m_lightning = totalWithBonus;
                }
                else if (!IsBlockedModifier(d.m_damages.m_pickaxe))
                {
                    beamHit.m_damage.m_pickaxe = totalWithBonus;
                }
                else
                {
                    beamHit.m_damage.m_slash = totalWithBonus;
                }
                return;
            }

            beamHit.m_damage.m_lightning = dmg;
            beamHit.m_damage.Add(bonusEnchant, 1);
        }

        private static void FirePinpointRailgunBeam(Character owner, HitData hitData, ItemDrop.ItemData weapon)
        {
            if (owner == null) return;

            Vector3 rayOrigin = owner.GetEyePoint();
            Vector3 rayDir = owner.GetLookDir();

            // Use GameCamera exact crosshair ray for pinpoint sniper accuracy when cast by local player!
            if (owner == Player.m_localPlayer && GameCamera.instance != null)
            {
                Transform camT = GameCamera.instance.transform;
                rayOrigin = camT.position;
                rayDir = camT.forward;
            }

            float maxRange = 95f;
            int terrainLayer = LayerMask.NameToLayer("terrain");
            int mask = LayerMask.GetMask(new string[]
            {
                "Default", "static_solid", "Default_small", "piece", "terrain",
                "character", "character_net", "character_ghost", "hitbox", "character_noenv", "vehicle"
            });

            Vector3 muzzlePos = owner.GetCenterPoint() + Vector3.up * 0.45f + owner.GetLookDir() * 0.75f;

            // 1) Centerline RaycastAll to find the exact stop distance at Terrain or Indestructible Fortress/World geometry!
            RaycastHit[] centerHits = Physics.RaycastAll(rayOrigin, rayDir, maxRange, mask, QueryTriggerInteraction.Collide);
            Array.Sort(centerHits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });

            float stopDist = maxRange;
            Vector3 endPoint = rayOrigin + rayDir * maxRange;

            for (int i = 0; i < centerHits.Length; i++)
            {
                Collider col = centerHits[i].collider;
                if (col == null) continue;
                if (Vector3.Dot(centerHits[i].point - muzzlePos, rayDir) < -0.2f) continue;

                Character ch = col.GetComponentInParent<Character>();
                if (ch != null && ch == owner) continue;

                // Stop immediately on terrain / ground heightmap!
                if (col.gameObject.layer == terrainLayer || col.GetComponentInParent<Heightmap>() != null)
                {
                    stopDist = centerHits[i].distance;
                    endPoint = centerHits[i].point;
                    break;
                }

                IDestructible destr = col.GetComponentInParent<IDestructible>();
                if (destr != null)
                {
                    if (IsIndestructibleBlocker(destr))
                    {
                        stopDist = centerHits[i].distance;
                        endPoint = centerHits[i].point;
                        break;
                    }
                    // Otherwise destructible (enemy, tree, log, building, prop, rock) -> PIERCE THROUGH!
                    continue;
                }

                // Pure static non-trigger world collider without IDestructible (dungeon walls, crypts, boss stones, fortresses) -> STOP!
                if (!col.isTrigger)
                {
                    stopDist = centerHits[i].distance;
                    endPoint = centerHits[i].point;
                    break;
                }
            }

            // 2) Thicker piercing SphereCastAll (radius 0.28m) up to `stopDist` to damage ALL enemies, trees, logs & structures along the beam!
            if (hitData != null)
            {
                int q = (weapon != null) ? Mathf.Max(1, weapon.m_quality) : 1;
                float baseLtg = (MagicStavesModule.MountainLightningDamage != null ? MagicStavesModule.MountainLightningDamage.Value : 150f)
                              + (MagicStavesModule.MountainLightningPerLevel != null ? MagicStavesModule.MountainLightningPerLevel.Value : 150f) * (q - 1);
                float skillFactor = owner.GetSkillFactor(Skills.SkillType.ElementalMagic);
                // Level 1 = 150, Level 2 = 300, Level 3 = 450, Level 4 = 600 (+ gentle ElementalMagic skill bonus)
                float skillMult = 1.0f + 0.20f * skillFactor;
                float totalDamage = baseLtg * skillMult;

                // Preserve any extra attribute/enchantment damage added onto `weapon.GetDamage()` (e.g. WeaponAttributes +50 Lightning / Fire / Frost / Poison / Spirit / Physical)!
                HitData.DamageTypes bonusEnchant = MagicStavesModule.GetBonusEnchantDamages(weapon);
                bonusEnchant.Modify(skillMult);

                RaycastHit[] pierceHits = Physics.SphereCastAll(rayOrigin, 0.28f, rayDir, stopDist + 0.15f, mask, QueryTriggerInteraction.Collide);
                Array.Sort(pierceHits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });

                HashSet<object> damagedTargets = new HashSet<object>();
                bool hitAnyTarget = false;

                for (int i = 0; i < pierceHits.Length; i++)
                {
                    Collider col = pierceHits[i].collider;
                    if (col == null) continue;
                    Vector3 hitPt = pierceHits[i].point;
                    if (hitPt.sqrMagnitude < 0.001f) hitPt = col.bounds.center;
                    if (Vector3.Dot(hitPt - muzzlePos, rayDir) < -0.2f) continue;
                    if (pierceHits[i].distance > stopDist + 0.25f) continue;
                    if (col.gameObject.layer == terrainLayer || col.GetComponentInParent<Heightmap>() != null) continue;

                    IDestructible destr = col.GetComponentInParent<IDestructible>();
                    if (destr == null) continue;

                    Character ch = destr as Character;
                    if (ch != null && (ch == owner || ch.IsDead())) continue;

                    if (IsIndestructibleBlocker(destr)) continue;

                    // Deduplicate per root IDestructible (so multi-collider enemies, trees, and walls take exact 1x railgun hit!)
                    if (damagedTargets.Contains(destr)) continue;
                    damagedTargets.Add(destr);

                    HitData beamHit = hitData.Clone();
                    PopulateRailgunDamageForTarget(destr, beamHit, totalDamage, bonusEnchant);
                    if (destr is Character && owner.GetSEMan() != null)
                    {
                        owner.GetSEMan().ModifyAttack(Skills.SkillType.ElementalMagic, ref beamHit);
                    }
                    beamHit.m_itemWorldLevel = (byte)Game.m_worldLevel;
                    beamHit.m_point = hitPt;
                    beamHit.m_dir = rayDir;
                    beamHit.m_hitCollider = col;
                    beamHit.m_pushForce = 55f;
                    beamHit.m_staggerMultiplier = 2.0f;
                    beamHit.SetAttacker(owner);

                    destr.Damage(beamHit);
                    hitAnyTarget = true;

                    MagicStavesModule.SpawnImpactBurstLight(hitPt, new Color(0.55f, 0.92f, 1.0f, 1f), 4.5f, 2.0f, 0.25f);
                }

                if (hitAnyTarget)
                {
                    owner.RaiseSkill(Skills.SkillType.ElementalMagic, 2.0f);
                }
            }

            // Spawn vanilla lightning hit VFX/SFX at terminal impact point if available
            if (MagicStavesModule.s_vanillaLightningHitEffects != null && MagicStavesModule.s_vanillaLightningHitEffects.HasEffects())
            {
                MagicStavesModule.s_vanillaLightningHitEffects.Create(endPoint, Quaternion.LookRotation(-rayDir), null, 1.5f, -1);
            }

            // Spawn thicker blinding 3D Jagged Lightning Railgun Beam as a ZNetScene-networked FX object so ALL co-op players see the railgun shot!
            GameObject beamGo;
            if (MagicStavesModule.s_mountainBeamNetPrefab != null)
            {
                beamGo = UnityEngine.Object.Instantiate(MagicStavesModule.s_mountainBeamNetPrefab, muzzlePos, Quaternion.identity);
            }
            else
            {
                beamGo = new GameObject("MountainRailgunBoltFX");
                beamGo.transform.position = muzzlePos;
                beamGo.AddComponent<LightningRailgunBeamVisual>();
            }
            LightningRailgunBeamVisual beamVis = beamGo.GetComponent<LightningRailgunBeamVisual>();
            if (beamVis != null)
            {
                beamVis.Init(muzzlePos, endPoint);
            }
        }
    }

    public class LightningRailgunBeamVisual : MonoBehaviour
    {
        private ZNetView m_nview;
        private bool m_initialized;
        private LineRenderer m_outerGlow;
        private LineRenderer m_coreBeam;
        private LineRenderer m_jaggedArc;
        private float m_life;
        private const float Duration = 0.45f;
        private Vector3 m_start;
        private Vector3 m_end;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
        }

        private void Start()
        {
            if (!m_initialized && m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                Vector3 s = m_nview.GetZDO().GetVec3(MagicStavesModule.ZdoKeyBeamStart, transform.position);
                Vector3 e = m_nview.GetZDO().GetVec3(MagicStavesModule.ZdoKeyBeamEnd, transform.position + transform.forward * 30f);
                BuildBeamRenderers(s, e);
            }
        }

        public void Init(Vector3 start, Vector3 end)
        {
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                m_nview.GetZDO().Set(MagicStavesModule.ZdoKeyBeamStart, start);
                m_nview.GetZDO().Set(MagicStavesModule.ZdoKeyBeamEnd, end);
            }
            BuildBeamRenderers(start, end);
        }

        private void BuildBeamRenderers(Vector3 start, Vector3 end)
        {
            if (m_initialized) return;
            m_initialized = true;
            m_start = start;
            m_end = end;

            Material fxMat = new Material(MagicStavesModule.GetAdditiveFxMaterial());
            Color col = new Color(0.65f, 0.92f, 1.0f, 1.0f);
            fxMat.color = col;
            if (fxMat.HasProperty("_Color")) fxMat.SetColor("_Color", col);
            if (fxMat.HasProperty("_TintColor")) fxMat.SetColor("_TintColor", col);

            // Wide soft plasma aura around the thicker railgun beam
            GameObject glowGo = new GameObject("OuterPlasmaAura");
            glowGo.transform.SetParent(transform, false);
            m_outerGlow = glowGo.AddComponent<LineRenderer>();
            m_outerGlow.useWorldSpace = true;
            m_outerGlow.positionCount = 2;
            m_outerGlow.SetPosition(0, start);
            m_outerGlow.SetPosition(1, end);
            m_outerGlow.startWidth = 0.56f;
            m_outerGlow.endWidth = 0.46f;
            m_outerGlow.material = fxMat;
            m_outerGlow.startColor = new Color(0.28f, 0.75f, 1.0f, 0.42f);
            m_outerGlow.endColor = new Color(0.22f, 0.68f, 1.0f, 0.35f);

            // Thicker straight blinding white-cyan railgun core (was 0.16 -> now 0.30)
            GameObject coreGo = new GameObject("CoreRailBeam");
            coreGo.transform.SetParent(transform, false);
            m_coreBeam = coreGo.AddComponent<LineRenderer>();
            m_coreBeam.useWorldSpace = true;
            m_coreBeam.positionCount = 2;
            m_coreBeam.SetPosition(0, start);
            m_coreBeam.SetPosition(1, end);
            m_coreBeam.startWidth = 0.30f;
            m_coreBeam.endWidth = 0.24f;
            m_coreBeam.material = fxMat;
            m_coreBeam.startColor = Color.white;
            m_coreBeam.endColor = new Color(0.50f, 0.90f, 1.0f, 1f);

            // Thicker outer jagged crackling lightning bolt (was 0.09 -> now 0.16)
            GameObject jagGo = new GameObject("JaggedLightningArc");
            jagGo.transform.SetParent(transform, false);
            m_jaggedArc = jagGo.AddComponent<LineRenderer>();
            m_jaggedArc.useWorldSpace = true;
            m_jaggedArc.material = fxMat;
            m_jaggedArc.startColor = new Color(0.40f, 0.85f, 1.0f, 0.95f);
            m_jaggedArc.endColor = new Color(0.60f, 0.94f, 1.0f, 0.95f);
            RebuildJaggedPoints(1.0f);

            MagicStavesModule.SpawnImpactBurstLight(end, new Color(0.50f, 0.90f, 1.0f, 1f), 10.0f, 3.8f, 0.42f);
        }

        private void RebuildJaggedPoints(float ampScale)
        {
            float dist = Vector3.Distance(m_start, m_end);
            int segs = Mathf.Clamp(Mathf.RoundToInt(dist * 1.6f), 8, 56);
            m_jaggedArc.positionCount = segs + 1;
            Vector3 fwd = (m_end - m_start).normalized;
            Vector3 right = Vector3.Cross(fwd, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(right, fwd).normalized;

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                Vector3 p = Vector3.Lerp(m_start, m_end, t);
                if (i > 0 && i < segs)
                {
                    float jitter = 0.30f * ampScale;
                    p += right * UnityEngine.Random.Range(-jitter, jitter) + up * UnityEngine.Random.Range(-jitter, jitter);
                }
                m_jaggedArc.SetPosition(i, p);
            }
        }

        private void Update()
        {
            if (!m_initialized)
            {
                if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
                {
                    Vector3 s = m_nview.GetZDO().GetVec3(MagicStavesModule.ZdoKeyBeamStart, Vector3.zero);
                    Vector3 e = m_nview.GetZDO().GetVec3(MagicStavesModule.ZdoKeyBeamEnd, Vector3.zero);
                    if ((e - s).sqrMagnitude > 0.01f)
                    {
                        BuildBeamRenderers(s, e);
                    }
                }
                if (!m_initialized) return;
            }

            m_life += Time.deltaTime;
            if (m_life >= Duration)
            {
                MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                return;
            }

            float alpha = 1.0f - (m_life / Duration);
            if (m_outerGlow != null)
            {
                m_outerGlow.startWidth = 0.56f * alpha;
                m_outerGlow.endWidth = 0.46f * alpha;
            }
            if (m_coreBeam != null)
            {
                m_coreBeam.startWidth = 0.30f * alpha;
                m_coreBeam.endWidth = 0.24f * alpha;
            }
            if (m_jaggedArc != null)
            {
                m_jaggedArc.startWidth = 0.16f * alpha;
                m_jaggedArc.endWidth = 0.12f * alpha;
                RebuildJaggedPoints(alpha);
            }
        }
    }

    // ========================================================================================================
    // 5. PLAINS STAFF SPELL: Blood Ritual Totem Field (Enemy -40% Move Speed, Player +10% Move Speed,
    //    -30% All Stamina/Eitr/Health Action Costs, +50% HP & Eitr Regen, Max 1 Active Totem, Non-Upgradable)
    // ========================================================================================================
    public class PlainsTotemDeployProjectile : MonoBehaviour, IProjectile
    {
        public void Setup(Character owner, Vector3 velocity, float hitNoise, HitData hitData, ItemDrop.ItemData item, ItemDrop.ItemData ammo)
        {
            DeployTotemAtAim(owner, item);
            Destroy(gameObject);
        }

        public static float GetTotemDurationForQuality(int itemQuality)
        {
            int q = Mathf.Max(1, itemQuality);
            float baseDur = MagicStavesModule.PlainsTotemDuration != null ? MagicStavesModule.PlainsTotemDuration.Value : 15f;
            float extraPerLvl = MagicStavesModule.PlainsTotemExtraDurationPerLevel != null ? MagicStavesModule.PlainsTotemExtraDurationPerLevel.Value : 5f;
            return baseDur + (q - 1) * extraPerLvl;
        }

        public string GetTooltipString(int itemQuality)
        {
            float dur = GetTotemDurationForQuality(itemQuality);
            if (MagicStavesModule.IsRussianLanguageSelected())
            {
                return string.Format(
                    "\n<color=#ff4455>Поле Тотема ({0:F0} сек., лимит: 1 тотем):\n• Враги: -40% к скорости передвижения\n• Игроки: +10% к скорости, +50% к регенерации HP и эйтра, -30% ко всем расходам</color>",
                    dur
                );
            }
            return string.Format(
                "\n<color=#ff4455>Totem Aura ({0:F0} s, max 1 totem):\n• Enemies: -40% movement speed\n• Players: +10% move speed, +50% HP & Eitr regen, -30% to all action costs</color>",
                dur
            );
        }

        private static void DeployTotemAtAim(Character owner, ItemDrop.ItemData weapon)
        {
            if (owner == null) return;

            Vector3 rayOrigin = owner.GetEyePoint();
            Vector3 rayDir = owner.GetLookDir();
            if (owner == Player.m_localPlayer && GameCamera.instance != null)
            {
                rayOrigin = GameCamera.instance.transform.position;
                rayDir = GameCamera.instance.transform.forward;
            }

            int envMask = LayerMask.GetMask(new string[] { "terrain", "Default", "static_solid", "piece" });
            Vector3 deployPos = owner.transform.position + owner.transform.forward * 4.5f;

            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDir, out hit, 20f, envMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(hit.point - owner.transform.position, owner.GetLookDir()) > 0.2f)
                {
                    deployPos = hit.point;
                }
            }

            // Snap cleanly onto ground surface
            RaycastHit groundHit;
            if (Physics.Raycast(deployPos + Vector3.up * 3.0f, Vector3.down, out groundHit, 10.0f, envMask, QueryTriggerInteraction.Ignore))
            {
                deployPos = groundHit.point;
            }

            Quaternion totemRot = Quaternion.identity;
            Vector3 faceDir = owner.transform.position - deployPos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.01f)
            {
                totemRot = Quaternion.LookRotation(-faceDir.normalized, Vector3.up);
            }

            int quality = (weapon != null) ? Mathf.Max(1, weapon.m_quality) : 1;
            float duration = GetTotemDurationForQuality(quality);

            // Spawn ZNetScene-networked Totem Zone prefab so ALL co-op players see the 3D Totem + Crimson Rainstorm AND receive the buff!
            GameObject totemGo;
            if (MagicStavesModule.s_plainsTotemZonePrefab != null)
            {
                totemGo = UnityEngine.Object.Instantiate(MagicStavesModule.s_plainsTotemZonePrefab, deployPos, totemRot);
            }
            else
            {
                totemGo = new GameObject("PlainsBloodHarvestTotem_Deployed");
                totemGo.transform.position = deployPos;
                totemGo.transform.rotation = totemRot;
                totemGo.AddComponent<PlainsBloodHarvestTotemZone>();
            }

            PlainsBloodHarvestTotemZone totem = totemGo.GetComponent<PlainsBloodHarvestTotemZone>();
            if (totem != null)
            {
                totem.Init(owner, duration);
                MagicStavesModule.RegisterDeployedTotem(owner, totem);
            }

            owner.RaiseSkill(Skills.SkillType.BloodMagic, 1.5f);
        }
    }

    public class PlainsBloodHarvestTotemZone : MonoBehaviour
    {
        public bool IsTotemAlive { get { return m_alive && this != null && gameObject != null; } }

        private ZNetView m_nview;
        private bool m_visualsBuilt;
        private bool m_alive = true;
        private Character m_owner;
        private float m_duration = 15f;
        private float m_radius = MagicStavesModule.PlainsTotemZoneRadius;
        private float m_elapsed;
        private float m_auraTimer;
        private const float AuraRefreshInterval = 0.15f;
        private int m_charMask;

        private Transform m_visualMeshTransform;
        private Vector3 m_orbWorldOffset = new Vector3(0f, 0.98f, 0f);
        private ParticleSystem m_bloodRainPs;
        private ParticleSystem m_bloodSplashMistPs;
        private bool m_stoppedRain;
        private Light m_orbLight;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
            {
                m_alive = true;
                m_duration = m_nview.GetZDO().GetFloat(MagicStavesModule.ZdoKeyDuration, MagicStavesModule.PlainsTotemDuration != null ? MagicStavesModule.PlainsTotemDuration.Value : 15f);
                m_charMask = LayerMask.GetMask(new string[] { "character", "character_net", "character_ghost", "hitbox", "character_noenv" });
                BuildTotem3DVisuals();
                RefreshFieldAura();
                MagicStavesModule.SpawnImpactBurstLight(transform.position + Vector3.up * 0.9f, new Color(1.0f, 0.10f, 0.16f, 1f), 7.0f, 2.5f, 0.35f);
            }
        }

        public void Init(Character owner, float durationSeconds)
        {
            if (m_nview == null) m_nview = GetComponent<ZNetView>();
            m_alive = true;
            m_owner = owner;
            m_duration = Mathf.Max(1f, durationSeconds);
            if (m_nview != null && m_nview.IsValid() && m_nview.GetZDO() != null)
            {
                m_nview.GetZDO().Set(MagicStavesModule.ZdoKeyDuration, m_duration);
            }
            m_charMask = LayerMask.GetMask(new string[] { "character", "character_net", "character_ghost", "hitbox", "character_noenv" });

            BuildTotem3DVisuals();
            RefreshFieldAura();
            MagicStavesModule.SpawnImpactBurstLight(transform.position + Vector3.up * 0.9f, new Color(1.0f, 0.10f, 0.16f, 1f), 7.0f, 2.5f, 0.35f);
        }

        public void DespawnReplaced()
        {
            if (!m_alive) return;
            m_alive = false;
            DetachRainGracefully();
            MagicStavesModule.SpawnImpactBurstLight(transform.position + Vector3.up * 0.8f, new Color(0.95f, 0.12f, 0.18f, 1f), 5.0f, 1.8f, 0.25f);
            MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
        }

        private void OnDestroy()
        {
            m_alive = false;
            DetachRainGracefully();
        }

        private void DetachRainGracefully()
        {
            if (m_bloodRainPs != null)
            {
                m_bloodRainPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_bloodRainPs.transform.SetParent(null, true);
                Destroy(m_bloodRainPs.gameObject, 1.5f);
                m_bloodRainPs = null;
            }
            if (m_bloodSplashMistPs != null)
            {
                m_bloodSplashMistPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_bloodSplashMistPs.transform.SetParent(null, true);
                Destroy(m_bloodSplashMistPs.gameObject, 2.0f);
                m_bloodSplashMistPs = null;
            }
        }

        private void BuildTotem3DVisuals()
        {
            if (m_visualsBuilt) return;
            m_visualsBuilt = true;

            // Render our custom Blender-baked 3D `PlainsHarvestTotem` model!
            GameObject modelGo = new GameObject("PlainsHarvestTotem_Model");
            modelGo.transform.SetParent(transform, false);
            m_visualMeshTransform = modelGo.transform;

            if (MagicStavesModule.s_plainsTotemMesh != null && MagicStavesModule.s_plainsTotemMat != null)
            {
                MeshFilter mf = modelGo.AddComponent<MeshFilter>();
                mf.sharedMesh = MagicStavesModule.s_plainsTotemMesh;
                MeshRenderer mr = modelGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MagicStavesModule.s_plainsTotemMat;
            }

            // 1) Heavy Crimson Blood Rain falling across the ENTIRE `m_radius` (10.5m) zone — ZERO line circles & ZERO red squares!
            GameObject rainGo = new GameObject("TotemBloodRain");
            rainGo.transform.SetParent(transform, false);
            rainGo.transform.localPosition = new Vector3(0f, 11.0f, 0f);
            m_bloodRainPs = rainGo.AddComponent<ParticleSystem>();
            m_bloodRainPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule rMain = m_bloodRainPs.main;
            rMain.loop = true;
            rMain.playOnAwake = false;
            rMain.simulationSpace = ParticleSystemSimulationSpace.World;
            rMain.startLifetime = new ParticleSystem.MinMaxCurve(0.58f, 0.75f); // Falls ~11.0m straight to ground!
            rMain.startSpeed = 0f; // Velocity controlled explicitly by VelocityOverLifetime
            rMain.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.15f);
            rMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.04f, 0.08f, 0.42f),
                new Color(0.86f, 0.10f, 0.14f, 0.56f)
            );
            rMain.maxParticles = 1550;
            rMain.gravityModifier = 0f;

            ParticleSystem.EmissionModule rEm = m_bloodRainPs.emission;
            rEm.rateOverTime = 890f;
            rEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 235) });

            ParticleSystem.ShapeModule rShape = m_bloodRainPs.shape;
            rShape.shapeType = ParticleSystemShapeType.Circle;
            rShape.radius = m_radius; // Exact 10.5m field radius!
            rShape.radiusThickness = 1.0f; // Uniformly fills the entire circle from center to edge
            rShape.rotation = new Vector3(90f, 0f, 0f); // Horizontal overhead disk

            ParticleSystem.VelocityOverLifetimeModule rVel = m_bloodRainPs.velocityOverLifetime;
            rVel.enabled = true;
            rVel.space = ParticleSystemSimulationSpace.World;
            rVel.x = new ParticleSystem.MinMaxCurve(-1.4f, -0.6f); // Slight natural storm slant
            rVel.y = new ParticleSystem.MinMaxCurve(-20.0f, -16.5f);
            rVel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            ParticleSystem.ColorOverLifetimeModule rCol = m_bloodRainPs.colorOverLifetime;
            rCol.enabled = true;
            Gradient rainGrad = new Gradient();
            rainGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.85f, 0.09f, 0.13f), 0.0f),
                    new GradientColorKey(new Color(0.75f, 0.05f, 0.09f), 0.78f),
                    new GradientColorKey(new Color(0.58f, 0.03f, 0.06f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.62f, 0.12f),
                    new GradientAlphaKey(0.55f, 0.86f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            rCol.color = new ParticleSystem.MinMaxGradient(rainGrad);

            ParticleSystemRenderer rPsr = rainGo.GetComponent<ParticleSystemRenderer>();
            if (rPsr != null)
            {
                rPsr.sharedMaterial = MagicStavesModule.GetBloodRainParticleMaterial();
                rPsr.renderMode = ParticleSystemRenderMode.Stretch;
                rPsr.velocityScale = 0.055f; // Sleek semi-transparent crimson rain streaks!
                rPsr.lengthScale = 3.6f;
                rPsr.shadowCastingMode = ShadowCastingMode.Off;
                rPsr.receiveShadows = false;
            }
            m_bloodRainPs.Play();

            // 2) Subtle low crimson rain-splash mist skimming the ground across the zone (uses soft smoke texture -> zero squares!)
            GameObject splashGo = new GameObject("TotemBloodRainGroundMist");
            splashGo.transform.SetParent(transform, false);
            splashGo.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            m_bloodSplashMistPs = splashGo.AddComponent<ParticleSystem>();
            m_bloodSplashMistPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule sMain = m_bloodSplashMistPs.main;
            sMain.loop = true;
            sMain.playOnAwake = false;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.35f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(1.9f, 3.0f);
            sMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.70f, 0.05f, 0.09f, 0.18f),
                new Color(0.86f, 0.10f, 0.14f, 0.26f)
            );
            sMain.maxParticles = 180;
            sMain.gravityModifier = -0.04f;

            ParticleSystem.EmissionModule sEm = m_bloodSplashMistPs.emission;
            sEm.rateOverTime = 50f;

            ParticleSystem.ShapeModule sShape = m_bloodSplashMistPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Circle;
            sShape.radius = Mathf.Max(1f, m_radius - 0.6f);
            sShape.radiusThickness = 1.0f;
            sShape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule sCol = m_bloodSplashMistPs.colorOverLifetime;
            sCol.enabled = true;
            Gradient mistGrad = new Gradient();
            mistGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.82f, 0.08f, 0.12f), 0.0f),
                    new GradientColorKey(new Color(0.60f, 0.04f, 0.07f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.24f, 0.30f),
                    new GradientAlphaKey(0.16f, 0.72f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            sCol.color = new ParticleSystem.MinMaxGradient(mistGrad);

            ParticleSystemRenderer sPsr = splashGo.GetComponent<ParticleSystemRenderer>();
            if (sPsr != null)
            {
                sPsr.sharedMaterial = MagicStavesModule.GetSoftSmokeParticleMaterial();
                sPsr.renderMode = ParticleSystemRenderMode.Billboard;
                sPsr.sortMode = ParticleSystemSortMode.Distance;
                sPsr.shadowCastingMode = ShadowCastingMode.Off;
                sPsr.receiveShadows = false;
            }
            m_bloodSplashMistPs.Play();

            // Crimson orb light
            GameObject lightGo = new GameObject("TotemBloodOrbLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = m_orbWorldOffset;
            m_orbLight = lightGo.AddComponent<Light>();
            m_orbLight.type = LightType.Point;
            m_orbLight.color = new Color(1.0f, 0.12f, 0.18f, 1f);
            m_orbLight.range = m_radius * 1.25f;
            m_orbLight.intensity = 1.65f;
            m_orbLight.shadows = LightShadows.None;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            m_elapsed += dt;

            // Rise-from-earth intro animation over first 0.25s
            if (m_visualMeshTransform != null)
            {
                float riseT = Mathf.Clamp01(m_elapsed / 0.25f);
                m_visualMeshTransform.localPosition = new Vector3(0f, Mathf.Lerp(-0.7f, 0f, riseT), 0f);
            }

            if (!m_stoppedRain && m_elapsed >= Mathf.Max(0.2f, m_duration - 0.65f))
            {
                m_stoppedRain = true;
                if (m_bloodRainPs != null) m_bloodRainPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (m_bloodSplashMistPs != null) m_bloodSplashMistPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (m_orbLight != null)
            {
                float fade = Mathf.Clamp01((m_duration - m_elapsed) / 0.75f);
                m_orbLight.intensity = 1.65f * fade;
            }

            if (m_elapsed >= m_duration)
            {
                m_alive = false;
                DetachRainGracefully();
                MagicStavesModule.SpawnImpactBurstLight(transform.position + Vector3.up * 0.9f, new Color(1.0f, 0.10f, 0.16f, 1f), 7.5f, 2.2f, 0.35f);
                MagicStavesModule.DestroyNetworkedOrLocal(gameObject);
                return;
            }

            m_auraTimer += dt;
            if (m_auraTimer >= AuraRefreshInterval)
            {
                m_auraTimer -= AuraRefreshInterval;
                RefreshFieldAura();
            }
        }

        private void RefreshFieldAura()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, m_radius, m_charMask, QueryTriggerInteraction.Collide);
            HashSet<Character> processed = new HashSet<Character>();

            for (int i = 0; i < cols.Length; i++)
            {
                Character ch = cols[i].GetComponentInParent<Character>();
                if (ch == null || ch.IsDead() || processed.Contains(ch)) continue;
                processed.Add(ch);

                if (ch.IsPlayer())
                {
                    // Only apply `AddStatusEffect` + resource-cost discount onto `Player.m_localPlayer` on EACH client
                    // so every co-op player inside the crimson rainstorm receives the exact buff locally without cross-peer SEMan conflicts!
                    if (ch == Player.m_localPlayer)
                    {
                        MagicStavesModule.MarkPlayerPlainsBuffed(ch, 0.45f);
                    }
                }
                else if ((m_owner != null && BaseAI.IsEnemy(m_owner, ch)) || (m_owner == null && Player.m_localPlayer != null && BaseAI.IsEnemy(Player.m_localPlayer, ch)))
                {
                    // Debuff enemy: -40% movement speed on whichever client currently simulates this enemy!
                    MagicStavesModule.MarkEnemyPlainsSlowed(ch, 0.45f);
                }
            }
        }
    }

    public class TransientFlashLight : MonoBehaviour
    {
        private Light m_light;
        private float m_duration = 0.3f;
        private float m_startIntensity = 1.5f;
        private float m_t;

        public void Init(Color c, float range, float intensity, float duration)
        {
            m_duration = Mathf.Max(0.05f, duration);
            m_startIntensity = intensity;
            m_light = gameObject.AddComponent<Light>();
            m_light.type = LightType.Point;
            m_light.color = c;
            m_light.range = range;
            m_light.intensity = intensity;
            m_light.shadows = LightShadows.None;
        }

        private void Update()
        {
            m_t += Time.deltaTime;
            if (m_t >= m_duration)
            {
                Destroy(gameObject);
                return;
            }
            if (m_light != null)
            {
                m_light.intensity = m_startIntensity * (1.0f - (m_t / m_duration));
            }
        }
    }

    /// <summary>
    /// Live runtime calibrator attached to every custom staff's `attach` and `attach_back` GameObjects.
    /// Invoked in `VisEquipment_AttachItem_Patch.Postfix`, `VisEquipment_AttachBackItem_Patch.Postfix`, and `Start()` (so ItemStands & ArmorStands are also covered!).
    /// Guarantees:
    ///   1) `TargetRenderer` and its transform chain are active, enabled, and assigned to the same visual layer as the character's `m_bodyModel` (never culled by cameras!).
    ///   2) World-space rendered length of the staff mesh is strictly equal to `TargetLengthMeters` (`~2.13m - 2.28m`) even if a modded skeleton or parent bone
    ///      altered `lossyScale` during `SetParent(joint)`!
    /// </summary>
    public class StaffVisualCalibrator : MonoBehaviour
    {
        public string PrefabName;
        public float TargetLengthMeters = 2.20f;
        public float boundsPadMargin = 0.50f;
        public bool IsBackHolster;
        public MeshRenderer TargetRenderer;
        public MeshFilter TargetFilter;

        private bool m_calibrated;

        private void Start()
        {
            if (!m_calibrated)
            {
                VisEquipment vis = GetComponentInParent<VisEquipment>();
                CalibrateForVisEquipment(vis);
            }
        }

        public void CalibrateForVisEquipment(VisEquipment vis)
        {
            if (m_calibrated) return;
            m_calibrated = true;

            if (TargetFilter == null) TargetFilter = GetComponentInChildren<MeshFilter>(true);
            if (TargetRenderer == null && TargetFilter != null) TargetRenderer = TargetFilter.GetComponent<MeshRenderer>();
            if (TargetFilter == null || TargetRenderer == null || TargetFilter.sharedMesh == null) return;

            MagicStavesModule.EnsureTransformChainActiveUpTo(TargetFilter.transform, transform);
            TargetRenderer.enabled = true;

            // Match visual layer to player body model so custom cameras / helmet / inventory / world cameras always render the staff!
            if (vis != null && vis.m_bodyModel != null)
            {
                int targetLayer = vis.m_bodyModel.gameObject.layer;
                SetLayerRecursively(gameObject, targetLayer);
            }

            // Measure actual world-space length of our staff mesh along its 3 local axes using `TargetFilter.transform.lossyScale`
            // (ignoring padded bounds margin by reading raw vertex span or unpadded bounds)
            Vector3 ls = TargetFilter.transform.lossyScale;
            Bounds mb = TargetFilter.sharedMesh.bounds;
            // Subtract the frustum-cull padding we added (`boundsPadMargin`)
            Vector3 rawLocalSize = new Vector3(
                Mathf.Max(0.01f, mb.size.x - boundsPadMargin),
                Mathf.Max(0.01f, mb.size.y - boundsPadMargin),
                Mathf.Max(0.01f, mb.size.z - boundsPadMargin)
            );
            Vector3 worldSpan = new Vector3(
                Mathf.Abs(ls.x) * rawLocalSize.x,
                Mathf.Abs(ls.y) * rawLocalSize.y,
                Mathf.Abs(ls.z) * rawLocalSize.z
            );
            float actualWorldLen = Mathf.Max(worldSpan.x, Mathf.Max(worldSpan.y, worldSpan.z));

            if (actualWorldLen > 1e-6f && TargetLengthMeters > 0.3f)
            {
                float ratio = TargetLengthMeters / actualWorldLen;
                // Auto-correct if world length differs by more than 4% (e.g., if bone lossyScale was 100 or 0.01!)
                if (ratio < 0.96f || ratio > 1.04f)
                {
                    transform.localScale = transform.localScale * ratio;
                }
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            Transform t = obj.transform;
            int count = t.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform c = t.GetChild(i);
                if (c != null) SetLayerRecursively(c.gameObject, layer);
            }
        }
    }

    /// <summary>
    /// Real-time 3D spherical pendulum & articulated chain simulator for the Swamp Staff ("Censer of Restless Spirit")!
    /// Simulates:
    ///   1) True world-space gravity (so tilting the staff while running, blocking, casting, or holstering on the back naturally hangs the censer downward!).
    ///   2) Inertial acceleration lag from hand/staff movement (walking bob, sprinting, jumping, turning, attack swings).
    ///   3) Subtle eerie spirit-breeze micro-sway so the censer never looks frozen even when standing still!
    ///   4) Progressive multi-link curvature across all 5 upper chain links + heavy censer lantern, plus an independent
    ///      lighter, faster-swinging simulation for the 5-link skull collar tail chain!
    /// </summary>
    public class SwampCenserChainSimulator : MonoBehaviour
    {
        public Vector3 RestDownDirLocal = Vector3.down;
        public Transform[] UpperChainNodes;
        public Transform[] TailChainNodes;

        private bool m_initialized;
        private Vector3 m_prevAnchorWorldPos;
        private Vector3 m_prevAnchorVel;

        // Primary heavy censer spherical pendulum state (world-space unit direction & angular velocity)
        private Vector3 m_censerDirWorld = Vector3.down;
        private Vector3 m_censerVelWorld = Vector3.zero;

        // Secondary lighter skull-collar tail chain state
        private Vector3 m_tailDirWorld = Vector3.down;
        private Vector3 m_tailVelWorld = Vector3.zero;

        private float m_phaseSeed;

        private void OnEnable()
        {
            m_initialized = false;
            m_phaseSeed = UnityEngine.Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (UpperChainNodes == null || UpperChainNodes.Length == 0 || UpperChainNodes[0] == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;
            if (dt > 0.05f) dt = 0.05f;

            Transform hookAnchor = UpperChainNodes[0];
            Vector3 anchorPos = hookAnchor.position;

            Vector3 restDownWorld = transform.TransformDirection(RestDownDirLocal);
            if (restDownWorld.sqrMagnitude < 1e-6f) restDownWorld = Vector3.down;
            else restDownWorld.Normalize();

            if (!m_initialized || (anchorPos - m_prevAnchorWorldPos).sqrMagnitude > 9.0f)
            {
                m_initialized = true;
                m_prevAnchorWorldPos = anchorPos;
                m_prevAnchorVel = Vector3.zero;
                m_censerDirWorld = Vector3.Slerp(restDownWorld, Vector3.down, 0.75f).normalized;
                m_censerVelWorld = Vector3.zero;
                m_tailDirWorld = Vector3.Slerp(restDownWorld, Vector3.down, 0.75f).normalized;
                m_tailVelWorld = Vector3.zero;
            }

            // Compute hand/crook world velocity & acceleration (clamped so teleports/equip snaps don't explode)
            Vector3 anchorVel = (anchorPos - m_prevAnchorWorldPos) / dt;
            Vector3 anchorAccel = (anchorVel - m_prevAnchorVel) / dt;
            m_prevAnchorWorldPos = anchorPos;
            m_prevAnchorVel = anchorVel;

            if (anchorAccel.sqrMagnitude > 900f)
            {
                anchorAccel = anchorAccel.normalized * 30f;
            }

            // Gentle ghostly censer micro-sway so even when standing idle, the censer breathes & sways softly
            float t = Time.time + m_phaseSeed;
            Vector3 idleBreeze = new Vector3(
                Mathf.Sin(t * 2.1f) * 0.42f + Mathf.Sin(t * 3.7f) * 0.18f,
                0f,
                Mathf.Cos(t * 1.75f) * 0.42f + Mathf.Sin(t * 2.9f) * 0.18f
            );

            // 1) Simulate heavy Censer + Upper 5-link Chain (`L_eff ~ 0.28m`, natural swinging frequency + smooth damping)
            Vector3 effGravityCenser = new Vector3(0f, -15.5f, 0f) - anchorAccel * 0.58f + idleBreeze;
            // Project acceleration perpendicular to current pendulum rod `m_censerDirWorld`
            Vector3 tangentForceCenser = effGravityCenser - m_censerDirWorld * Vector3.Dot(effGravityCenser, m_censerDirWorld);
            m_censerVelWorld += tangentForceCenser * (3.6f * dt);
            m_censerVelWorld *= Mathf.Exp(-3.4f * dt); // Smooth air + chain friction damping

            m_censerDirWorld = (m_censerDirWorld + m_censerVelWorld * dt).normalized;
            // Clamp maximum swing cone (`maxAngle = 52 deg`) relative to `restDownWorld` so the censer never clips through the bogwood shaft!
            m_censerDirWorld = ClampConeDirection(m_censerDirWorld, restDownWorld, 52f, ref m_censerVelWorld);

            // Distribute total bend from `restDownWorld -> m_censerDirWorld` smoothly across the 5 chain links + censer body!
            // Because each link is parented to the previous link, setting incremental `localRotation` creates a real curved catenary/whip arc!
            Quaternion totalCenserSwingWorld = Quaternion.FromToRotation(restDownWorld, m_censerDirWorld);
            Quaternion invBaseWorldRot = Quaternion.Inverse(transform.rotation);
            Quaternion totalSwingInMf = invBaseWorldRot * totalCenserSwingWorld * transform.rotation;

            // Add subtle secondary twist & lag on the censer lantern itself
            float twistDeg = Mathf.Sin(t * 1.6f) * 4.5f + Mathf.Clamp(Vector3.Dot(m_censerVelWorld, transform.right) * 5.0f, -14f, 14f);

            int upperCount = UpperChainNodes.Length;
            float prevFrac = 0f;
            for (int i = 0; i < upperCount; i++)
            {
                Transform node = UpperChainNodes[i];
                if (node == null) continue;

                // Progressive curve fractions: top link bends ~24%, middle links build up to ~92%, censer lantern adds final follow-through to 108%!
                float targetFrac = (i < upperCount - 1)
                    ? Mathf.Lerp(0.26f, 0.92f, (float)i / Mathf.Max(1, upperCount - 2))
                    : 1.06f;

                Quaternion prevQ = Quaternion.SlerpUnclamped(Quaternion.identity, totalSwingInMf, prevFrac);
                Quaternion curQ = Quaternion.SlerpUnclamped(Quaternion.identity, totalSwingInMf, targetFrac);
                Quaternion stepLocalRot = Quaternion.Inverse(prevQ) * curQ;

                if (i == upperCount - 1)
                {
                    stepLocalRot = stepLocalRot * Quaternion.AngleAxis(twistDeg, RestDownDirLocal);
                }

                node.localRotation = stepLocalRot;
                prevFrac = targetFrac;
            }

            // 2) Simulate lighter, livelier 5-link Skull Tail Chain
            if (TailChainNodes != null && TailChainNodes.Length > 0)
            {
                Vector3 tailBreeze = new Vector3(
                    Mathf.Cos(t * 3.1f + 1.3f) * 0.55f,
                    0f,
                    Mathf.Sin(t * 2.7f + 0.7f) * 0.55f
                );
                Vector3 effGravityTail = new Vector3(0f, -18.0f, 0f) - anchorAccel * 0.75f + tailBreeze;
                Vector3 tangentForceTail = effGravityTail - m_tailDirWorld * Vector3.Dot(effGravityTail, m_tailDirWorld);
                m_tailVelWorld += tangentForceTail * (5.2f * dt);
                m_tailVelWorld *= Mathf.Exp(-4.2f * dt);

                m_tailDirWorld = (m_tailDirWorld + m_tailVelWorld * dt).normalized;
                m_tailDirWorld = ClampConeDirection(m_tailDirWorld, restDownWorld, 45f, ref m_tailVelWorld);

                Quaternion totalTailSwingWorld = Quaternion.FromToRotation(restDownWorld, m_tailDirWorld);
                Quaternion totalTailInMf = invBaseWorldRot * totalTailSwingWorld * transform.rotation;

                float prevTailFrac = 0f;
                for (int i = 0; i < TailChainNodes.Length; i++)
                {
                    Transform tNode = TailChainNodes[i];
                    if (tNode == null) continue;
                    float frac = Mathf.Lerp(0.28f, 1.12f, (float)i / Mathf.Max(1, TailChainNodes.Length - 1));
                    Quaternion pQ = Quaternion.SlerpUnclamped(Quaternion.identity, totalTailInMf, prevTailFrac);
                    Quaternion cQ = Quaternion.SlerpUnclamped(Quaternion.identity, totalTailInMf, frac);
                    tNode.localRotation = Quaternion.Inverse(pQ) * cQ;
                    prevTailFrac = frac;
                }
            }
        }

        private static Vector3 ClampConeDirection(Vector3 dir, Vector3 axis, float maxDegrees, ref Vector3 vel)
        {
            float angle = Vector3.Angle(axis, dir);
            if (angle > maxDegrees && angle > 0.001f)
            {
                dir = Vector3.Slerp(axis, dir, maxDegrees / angle).normalized;
                // Damp outward radial velocity against the cone boundary
                Vector3 radial = (dir - axis * Vector3.Dot(dir, axis)).normalized;
                float outSpeed = Vector3.Dot(vel, radial);
                if (outSpeed > 0f)
                {
                    vel -= radial * (outSpeed * 1.25f);
                }
            }
            return dir;
        }
    }
}
