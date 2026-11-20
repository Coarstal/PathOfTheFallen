using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using FinalMod;

namespace CoarstalsEitrArmorMod
{
    public class CoarstalsEitrArmorPlugin
    {
        public const string PluginGUID = "com.coarstal.eitrarmor";
        public const string PluginName = "Coarstal's Eitr Armor";
        public const string PluginVersion = "1.1.1";

        // Keep exact Prefab IDs so existing spawned items, recipes & saves keep working seamlessly!
        // Set 1: Black Forest (Blue Recolor of Vanilla Eitr-Weave Set: HelmetMage, ArmorMageChest, ArmorMageLegs)
        public const string BFHoodPrefab = "ArmorForestShamanHood";
        public const string BFChestPrefab = "ArmorForestShamanChest";
        public const string BFLegsPrefab = "ArmorForestShamanLegs";
        public const string BFSetName = "ForestShamanEitrSet";
        public const string BFSetEffectName = "SE_ForestShamanSet";

        // Set 2: Mountains (Grey Recolor of Vanilla Embla Set: HelmetMage_Ashlands, ArmorMageChest_Ashlands, ArmorMageLegs_Ashlands)
        public const string MTHoodPrefab = "ArmorWolfMageHood";
        public const string MTChestPrefab = "ArmorWolfMageChest";
        public const string MTLegsPrefab = "ArmorWolfMageLegs";
        public const string MTSetName = "WolfRuneMageEitrSet";
        public const string MTSetEffectName = "SE_WolfRuneMageSet";

        internal static ManualLogSource ModLogger;

        // ====================================================================
        // BepInEx Config Entries — Set 1: Black Forest (+60% Total Eitr Regen)
        // ====================================================================
        public static ConfigEntry<float> BFArmorBase;
        public static ConfigEntry<float> BFArmorPerLevel;
        public static ConfigEntry<float> BFHoodEitrRegen;
        public static ConfigEntry<float> BFChestEitrRegen;
        public static ConfigEntry<float> BFLegsEitrRegen;
        public static ConfigEntry<float> BFSetBonusEitrRegen;
        public static ConfigEntry<int> BFMinStationLevel;
        public static ConfigEntry<string> BFHoodRecipe;
        public static ConfigEntry<string> BFChestRecipe;
        public static ConfigEntry<string> BFLegsRecipe;

        // ====================================================================
        // BepInEx Config Entries — Set 2: Mountains (+100% Total Eitr Regen)
        // ====================================================================
        public static ConfigEntry<float> MTArmorBase;
        public static ConfigEntry<float> MTArmorPerLevel;
        public static ConfigEntry<float> MTHoodEitrRegen;
        public static ConfigEntry<float> MTChestEitrRegen;
        public static ConfigEntry<float> MTLegsEitrRegen;
        public static ConfigEntry<float> MTSetBonusEitrRegen;
        public static ConfigEntry<bool> MTChestFrostResist;
        public static ConfigEntry<int> MTMinStationLevel;
        public static ConfigEntry<string> MTHoodRecipe;
        public static ConfigEntry<string> MTChestRecipe;
        public static ConfigEntry<string> MTLegsRecipe;

        private static bool s_registered;
        private static bool s_initialized;

        internal static SE_Stats s_bfSetStatusEffect;
        internal static SE_Stats s_mtSetStatusEffect;

        // Cache recolored materials & textures by original instanceID so Chest & Legs share identical recolored materials cleanly!
        private static readonly Dictionary<string, Material> s_recoloredMatCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> s_recoloredTexCache = new Dictionary<string, Texture2D>();

        private enum RecolorMode
        {
            BlackForestTrollBlue,
            MountainFrostGrey
        }

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            const string secBF = "04. Eitr Armor - 1. Black Forest Set";
            const string secMT = "04. Eitr Armor - 2. Mountain Set";

            // 1. Black Forest Set (10% + 15% + 15% + 20% Set = 60% Eitr Regen, 5 Armor + 2/lvl)
            BFArmorBase = config.Bind(
                secBF,
                "BaseArmor",
                5f,
                new ConfigDescription("Base armor value at quality level 1 for each piece of the Black Forest Eitr set.", null, adminAttr)
            );
            BFArmorPerLevel = config.Bind(
                secBF,
                "ArmorPerLevel",
                2f,
                new ConfigDescription("Additional armor gained per upgrade level for each piece of the Black Forest Eitr set.", null, adminAttr)
            );
            BFHoodEitrRegen = config.Bind(
                secBF,
                "HoodEitrRegenBonus",
                0.10f,
                new ConfigDescription("Eitr regeneration bonus on Hood (0.10 = +10%).", null, adminAttr)
            );
            BFChestEitrRegen = config.Bind(
                secBF,
                "ChestEitrRegenBonus",
                0.15f,
                new ConfigDescription("Eitr regeneration bonus on Robe (0.15 = +15%).", null, adminAttr)
            );
            BFLegsEitrRegen = config.Bind(
                secBF,
                "LegsEitrRegenBonus",
                0.15f,
                new ConfigDescription("Eitr regeneration bonus on Trousers (0.15 = +15%).", null, adminAttr)
            );
            BFSetBonusEitrRegen = config.Bind(
                secBF,
                "SetBonusEitrRegen",
                0.20f,
                new ConfigDescription("3-piece Set Bonus Eitr regeneration for Black Forest set (0.20 = +20%, bringing full set total to +60%).", null, adminAttr)
            );
            BFMinStationLevel = config.Bind(
                secBF,
                "MinForgeLevel",
                1,
                new ConfigDescription("Minimum Forge level required to craft the Black Forest set.", null, adminAttr)
            );
            BFHoodRecipe = config.Bind(
                secBF,
                "HoodRecipe",
                "Copper:4:2,TrollHide:4:2,SurtlingCore:1:1,Thistle:4:2",
                new ConfigDescription("Crafting recipe for Black Forest Hood (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );
            BFChestRecipe = config.Bind(
                secBF,
                "ChestRecipe",
                "Copper:6:3,TrollHide:6:2,SurtlingCore:2:1,Thistle:6:2",
                new ConfigDescription("Crafting recipe for Black Forest Robe (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );
            BFLegsRecipe = config.Bind(
                secBF,
                "LegsRecipe",
                "Copper:5:2,TrollHide:5:2,SurtlingCore:1:1,Thistle:4:2",
                new ConfigDescription("Crafting recipe for Black Forest Trousers (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );

            // 2. Mountain Set (20% + 25% + 25% + 30% Set = 100% Eitr Regen, 8 Armor + 2/lvl, Frost Resist)
            MTArmorBase = config.Bind(
                secMT,
                "BaseArmor",
                8f,
                new ConfigDescription("Base armor value at quality level 1 for each piece of the Mountain Eitr set.", null, adminAttr)
            );
            MTArmorPerLevel = config.Bind(
                secMT,
                "ArmorPerLevel",
                2f,
                new ConfigDescription("Additional armor gained per upgrade level for each piece of the Mountain Eitr set.", null, adminAttr)
            );
            MTHoodEitrRegen = config.Bind(
                secMT,
                "HoodEitrRegenBonus",
                0.20f,
                new ConfigDescription("Eitr regeneration bonus on Mountain Hood (0.20 = +20%).", null, adminAttr)
            );
            MTChestEitrRegen = config.Bind(
                secMT,
                "ChestEitrRegenBonus",
                0.25f,
                new ConfigDescription("Eitr regeneration bonus on Mountain Robe (0.25 = +25%).", null, adminAttr)
            );
            MTLegsEitrRegen = config.Bind(
                secMT,
                "LegsEitrRegenBonus",
                0.25f,
                new ConfigDescription("Eitr regeneration bonus on Mountain Trousers (0.25 = +25%).", null, adminAttr)
            );
            MTSetBonusEitrRegen = config.Bind(
                secMT,
                "SetBonusEitrRegen",
                0.30f,
                new ConfigDescription("3-piece Set Bonus Eitr regeneration for Mountain set (0.30 = +30%, bringing full set total to +100%).", null, adminAttr)
            );
            MTChestFrostResist = config.Bind(
                secMT,
                "ChestGrantsFrostResistance",
                true,
                new ConfigDescription("Whether the Mountain Robe grants built-in Frost resistance (prevents Freezing in Mountains).", null, adminAttr)
            );
            MTMinStationLevel = config.Bind(
                secMT,
                "MinForgeLevel",
                2,
                new ConfigDescription("Minimum Forge level required to craft the Mountain set.", null, adminAttr)
            );
            MTHoodRecipe = config.Bind(
                secMT,
                "HoodRecipe",
                "Silver:5:2,WolfPelt:4:2,Obsidian:6:2,Crystal:2:1",
                new ConfigDescription("Crafting recipe for Mountain Hood (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );
            MTChestRecipe = config.Bind(
                secMT,
                "ChestRecipe",
                "Silver:8:3,WolfPelt:6:3,Obsidian:6:3,Crystal:4:2",
                new ConfigDescription("Crafting recipe for Mountain Robe (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );
            MTLegsRecipe = config.Bind(
                secMT,
                "LegsRecipe",
                "Silver:6:2,WolfPelt:5:2,Obsidian:6:2,Crystal:2:1",
                new ConfigDescription("Crafting recipe for Mountain Trousers (Format: Prefab:Amount:AmountPerLevel).", null, adminAttr)
            );

            MigrateLegacyFenrisHairInConfigs(config);
        }

        public static void Init(ManualLogSource logger)
        {
            if (s_initialized) return;
            ModLogger = logger;
            RegisterLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += RegisterAllEitrArmors;
            s_initialized = true;
            if (ModLogger != null) ModLogger.LogInfo("[PathOfTheFallen] EitrArmor module initialized.");
        }

        public static void Shutdown()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterAllEitrArmors;
            s_initialized = false;
        }

        private static void MigrateLegacyFenrisHairInConfigs(ConfigFile config)
        {
            bool changed = false;
            ConfigEntry<string>[] entries = new ConfigEntry<string>[] { MTHoodRecipe, MTChestRecipe, MTLegsRecipe };
            for (int i = 0; i < entries.Length; i++)
            {
                string val = entries[i].Value;
                if (!string.IsNullOrEmpty(val) && (val.Contains("WolfHairBundle") || val.Contains("FenrisHair")))
                {
                    string[] tokens = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    List<string> kept = new List<string>();
                    for (int t = 0; t < tokens.Length; t++)
                    {
                        string trimmed = tokens[t].Trim();
                        if (trimmed.StartsWith("WolfHairBundle:", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("FenrisHair:", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        kept.Add(trimmed);
                    }
                    entries[i].Value = string.Join(",", kept.ToArray());
                    changed = true;
                }
            }
            if (changed && config != null)
            {
                config.Save();
            }
        }

        private static void RegisterLocalizations()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string langEn = "English";
            loc.AddTranslation(ref langEn, new Dictionary<string, string>
            {
                { "item_forestshaman_hood", "Forest Eitr-Weave Hood" },
                { "item_forestshaman_hood_desc", "An azure mage hood woven with Black Forest troll hide and surtling essence, accelerating Eitr recovery." },
                { "item_forestshaman_chest", "Forest Eitr-Weave Robe" },
                { "item_forestshaman_chest_desc", "A blue Eitr-woven robe attuned to the ancient woods, pairing seamlessly with troll-hide cloaks." },
                { "item_forestshaman_legs", "Forest Eitr-Weave Trousers" },
                { "item_forestshaman_legs_desc", "Light azure mage trousers threaded with copper and forest runes for swift Eitr flow." },
                { "se_forestshaman_set", "Whisper of the Forest" },

                { "item_wolfmage_hood", "Frost Embla Hood" },
                { "item_wolfmage_hood_desc", "A storm-grey mountain mage hood forged with silver and crystal, focusing arcane power in the cold." },
                { "item_wolfmage_chest", "Frost Embla Robes" },
                { "item_wolfmage_chest_desc", "Silver-grey vestments modeled after Embla's attire, warding off mountain blizzards and surging with Eitr." },
                { "item_wolfmage_legs", "Frost Embla Trousers" },
                { "item_wolfmage_legs_desc", "Insulated grey mage trousers woven with wolf pelts and silver threads." },
                { "se_wolfmage_set", "Mountain Arcane Surge" }
            });

            string langRu = "Russian";
            loc.AddTranslation(ref langRu, new Dictionary<string, string>
            {
                // Black Forest Set — Синее сшитое из эйтра одеяние
                { "item_forestshaman_hood", "\u041b\u0435\u0441\u043d\u043e\u0439 \u043a\u0430\u043f\u044e\u0448\u043e\u043d \u044d\u0439\u0442\u0440\u0430" },
                { "item_forestshaman_hood_desc", "\u0421\u0438\u043d\u0438\u0439 \u043a\u0430\u043f\u044e\u0448\u043e\u043d \u0438\u0437 \u043f\u043b\u0435\u0442\u0435\u043d\u0438\u044f \u044d\u0439\u0442\u0440\u0430 \u0432 \u0442\u043e\u043d\u0430\u0445 \u0427\u0451\u0440\u043d\u043e\u0433\u043e \u043b\u0435\u0441\u0430, \u0443\u0441\u043a\u043e\u0440\u044f\u044e\u0449\u0438\u0439 \u0432\u043e\u0441\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0435 \u044d\u0439\u0442\u0440\u0430." },
                { "item_forestshaman_chest", "\u041b\u0435\u0441\u043d\u043e\u0435 \u0441\u0448\u0438\u0442\u043e\u0435 \u0438\u0437 \u044d\u0439\u0442\u0440\u0430 \u043e\u0434\u0435\u044f\u043d\u0438\u0435" },
                { "item_forestshaman_chest_desc", "\u0421\u0438\u043d\u0435\u0435 \u043c\u0430\u0433\u0438\u0447\u0435\u0441\u043a\u043e\u0435 \u043e\u0434\u0435\u044f\u043d\u0438\u0435 \u0438\u0437 \u0427\u0451\u0440\u043d\u043e\u0433\u043e \u043b\u0435\u0441\u0430, \u0438\u0434\u0435\u0430\u043b\u044c\u043d\u043e \u0441\u043e\u0447\u0435\u0442\u0430\u044e\u0449\u0435\u0435\u0441\u044f \u0441 \u043f\u043b\u0430\u0449\u043e\u043c \u0438\u0437 \u0448\u043a\u0443\u0440\u044b \u0442\u0440\u043e\u043b\u043b\u044f." },
                { "item_forestshaman_legs", "\u041b\u0435\u0441\u043d\u044b\u0435 \u0448\u0442\u0430\u043d\u044b \u044d\u0439\u0442\u0440\u0430" },
                { "item_forestshaman_legs_desc", "\u041b\u0451\u0433\u043a\u0438\u0435 \u0441\u0438\u043d\u0438\u0435 \u0448\u0442\u0430\u043d\u044b \u043c\u0430\u0433\u0430, \u043f\u0440\u043e\u0448\u0438\u0442\u044b\u0435 \u043c\u0435\u0434\u043d\u043e\u0439 \u043d\u0438\u0442\u044c\u044e \u0434\u043b\u044f \u0441\u0432\u043e\u0431\u043e\u0434\u043d\u043e\u0433\u043e \u0442\u043e\u043a\u0430 \u044d\u0439\u0442\u0440\u0430." },
                { "se_forestshaman_set", "\u0414\u044b\u0445\u0430\u043d\u0438\u0435 \u0427\u0451\u0440\u043d\u043e\u0433\u043e \u043b\u0435\u0441\u0430" },

                // Mountain Set — Серая одежда Эмблы
                { "item_wolfmage_hood", "\u0413\u043e\u0440\u043d\u044b\u0439 \u043a\u0430\u043f\u044e\u0448\u043e\u043d \u042d\u043c\u0431\u043b\u044b" },
                { "item_wolfmage_hood_desc", "\u041f\u0435\u043f\u0435\u043b\u044c\u043d\u043e-\u0441\u0435\u0440\u044b\u0439 \u043a\u0430\u043f\u044e\u0448\u043e\u043d \u043c\u0430\u0433\u0430 \u0441 \u0441\u0435\u0440\u0435\u0431\u0440\u044f\u043d\u043e\u0439 \u043e\u0442\u0434\u0435\u043b\u043a\u043e\u0439, \u043d\u0430\u043f\u043e\u043b\u043d\u044f\u044e\u0449\u0438\u0439 \u0440\u0430\u0437\u0443\u043c \u0441\u0438\u043b\u043e\u0439 \u0433\u043e\u0440\u043d\u044b\u0445 \u0432\u0435\u0440\u0448\u0438\u043d." },
                { "item_wolfmage_chest", "\u0413\u043e\u0440\u043d\u0430\u044f \u043e\u0434\u0435\u0436\u0434\u0430 \u042d\u043c\u0431\u043b\u044b" },
                { "item_wolfmage_chest_desc", "\u0421\u0435\u0440\u043e-\u0441\u0435\u0440\u0435\u0431\u0440\u0438\u0441\u0442\u043e\u0435 \u043e\u0431\u043b\u0430\u0447\u0435\u043d\u0438\u0435 \u042d\u043c\u0431\u043b\u044b, \u0437\u0430\u0449\u0438\u0449\u0430\u044e\u0449\u0435\u0435 \u043e\u0442 \u0433\u043e\u0440\u043d\u043e\u0439 \u0441\u0442\u0443\u0436\u0438 \u0438 \u043e\u0442\u043b\u0438\u0447\u043d\u043e \u0441\u043e\u0447\u0435\u0442\u0430\u044e\u0449\u0435\u0435\u0441\u044f \u0441 \u0432\u043e\u043b\u0447\u044c\u0438\u043c \u043f\u043b\u0430\u0449\u043e\u043c." },
                { "item_wolfmage_legs", "\u0413\u043e\u0440\u043d\u044b\u0435 \u0448\u0442\u0430\u043d\u044b \u042d\u043c\u0431\u043b\u044b" },
                { "item_wolfmage_legs_desc", "\u0423\u0442\u0435\u043f\u043b\u0451\u043d\u043d\u0430\u044f \u0441\u0435\u0440\u0430\u044f \u044e\u0431\u043a\u0430-\u0448\u0442\u0430\u043d\u044b \u042d\u043c\u0431\u043b\u044b, \u0441\u043e\u0442\u043a\u0430\u043d\u043d\u0430\u044f \u0441 \u0441\u0435\u0440\u0435\u0431\u0440\u043e\u043c \u0438 \u0432\u043e\u043b\u0447\u044c\u0438\u043c \u043c\u0435\u0445\u043e\u043c." },
                { "se_wolfmage_set", "\u041c\u043e\u0440\u043e\u0437\u043d\u044b\u0439 \u043f\u043e\u0442\u043e\u043a \u042d\u043c\u0431\u043b\u044b" }
            });
        }

        private static void RegisterAllEitrArmors()
        {
            if (s_registered || (FinalModulesConfig.EnableEitrArmor != null && !FinalModulesConfig.EnableEitrArmor.Value)) return;
            if (ItemManager.Instance.GetItem(BFHoodPrefab) != null || PrefabManager.Instance.GetPrefab(BFHoodPrefab) != null)
            {
                s_registered = true;
                return;
            }

            try
            {
                // 1) Resolve Vanilla Eitr-Weave Set (Mistlands: "Сшитое из эйтра одеяние")
                GameObject eitrHelmDonor  = PrefabManager.Instance.GetPrefab("HelmetMage");
                GameObject eitrChestDonor = PrefabManager.Instance.GetPrefab("ArmorMageChest");
                GameObject eitrLegsDonor  = PrefabManager.Instance.GetPrefab("ArmorMageLegs");

                // 2) Resolve Vanilla Embla Set (Ashlands: "Одежда Эмблы")
                GameObject emblaHelmDonor  = FindFirstValidPrefab("HelmetMage_Ashlands", "HelmetAshlandsMage", "HelmetMage");
                GameObject emblaChestDonor = FindFirstValidPrefab("ArmorMageChest_Ashlands", "ArmorMageAshlandsChest", "ArmorMageChest");
                GameObject emblaLegsDonor  = FindFirstValidPrefab("ArmorMageLegs_Ashlands", "ArmorMageAshlandsLegs", "ArmorMageLegs");

                if (eitrHelmDonor == null || eitrChestDonor == null || eitrLegsDonor == null ||
                    emblaHelmDonor == null || emblaChestDonor == null || emblaLegsDonor == null)
                {
                    if (ModLogger != null) ModLogger.LogError("Could not find vanilla Eitr-Weave or Embla armor donor prefabs!");
                    return;
                }

                if (ModLogger != null)
                {
                    ModLogger.LogInfo(string.Format(
                        "[EitrArmor] Vanilla donors resolved -> BlackForest(Blue): [{0}, {1}, {2}] | Mountains(Grey): [{3}, {4}, {5}]",
                        eitrHelmDonor.name, eitrChestDonor.name, eitrLegsDonor.name,
                        emblaHelmDonor.name, emblaChestDonor.name, emblaLegsDonor.name));
                }

                // Recolor original vanilla icons cleanly into Blue (Black Forest) & Frost Grey (Mountains)!
                Sprite bfHoodIcon  = CloneAndRecolorItemIcon(eitrHelmDonor,  BFHoodPrefab + "_Icon",  RecolorMode.BlackForestTrollBlue);
                Sprite bfChestIcon = CloneAndRecolorItemIcon(eitrChestDonor, BFChestPrefab + "_Icon", RecolorMode.BlackForestTrollBlue);
                Sprite bfLegsIcon  = CloneAndRecolorItemIcon(eitrLegsDonor,  BFLegsPrefab + "_Icon",  RecolorMode.BlackForestTrollBlue);

                Sprite mtHoodIcon  = CloneAndRecolorItemIcon(emblaHelmDonor,  MTHoodPrefab + "_Icon",  RecolorMode.MountainFrostGrey);
                Sprite mtChestIcon = CloneAndRecolorItemIcon(emblaChestDonor, MTChestPrefab + "_Icon", RecolorMode.MountainFrostGrey);
                Sprite mtLegsIcon  = CloneAndRecolorItemIcon(emblaLegsDonor,  MTLegsPrefab + "_Icon",  RecolorMode.MountainFrostGrey);

                // Create Set Bonus Status Effects (+20% -> 60% total in Black Forest; +30% -> 100% total in Mountains)
                s_bfSetStatusEffect = CreateSetEitrStatusEffect(
                    BFSetEffectName,
                    "$se_forestshaman_set",
                    bfHoodIcon,
                    BFSetBonusEitrRegen.Value
                );
                s_mtSetStatusEffect = CreateSetEitrStatusEffect(
                    MTSetEffectName,
                    "$se_wolfmage_set",
                    mtHoodIcon,
                    MTSetBonusEitrRegen.Value
                );

                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(s_bfSetStatusEffect, fixReference: false));
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(s_mtSetStatusEffect, fixReference: false));

                // ============================================================
                // SET 1: BLACK FOREST — BLUE EITR-WEAVE (+60% Total Eitr Regen)
                // ============================================================
                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: BFHoodPrefab,
                    donorPrefab: eitrHelmDonor,
                    mode: RecolorMode.BlackForestTrollBlue,
                    nameToken: "$item_forestshaman_hood",
                    descToken: "$item_forestshaman_hood_desc",
                    icon: bfHoodIcon,
                    armorBase: BFArmorBase.Value,
                    armorPerLvl: BFArmorPerLevel.Value,
                    eitrRegenBonus: BFHoodEitrRegen.Value,
                    grantFrostResist: false,
                    setName: BFSetName,
                    setEffect: s_bfSetStatusEffect,
                    minStationLvl: BFMinStationLevel.Value,
                    recipeStr: BFHoodRecipe.Value
                );

                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: BFChestPrefab,
                    donorPrefab: eitrChestDonor,
                    mode: RecolorMode.BlackForestTrollBlue,
                    nameToken: "$item_forestshaman_chest",
                    descToken: "$item_forestshaman_chest_desc",
                    icon: bfChestIcon,
                    armorBase: BFArmorBase.Value,
                    armorPerLvl: BFArmorPerLevel.Value,
                    eitrRegenBonus: BFChestEitrRegen.Value,
                    grantFrostResist: false,
                    setName: BFSetName,
                    setEffect: s_bfSetStatusEffect,
                    minStationLvl: BFMinStationLevel.Value,
                    recipeStr: BFChestRecipe.Value
                );

                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: BFLegsPrefab,
                    donorPrefab: eitrLegsDonor,
                    mode: RecolorMode.BlackForestTrollBlue,
                    nameToken: "$item_forestshaman_legs",
                    descToken: "$item_forestshaman_legs_desc",
                    icon: bfLegsIcon,
                    armorBase: BFArmorBase.Value,
                    armorPerLvl: BFArmorPerLevel.Value,
                    eitrRegenBonus: BFLegsEitrRegen.Value,
                    grantFrostResist: false,
                    setName: BFSetName,
                    setEffect: s_bfSetStatusEffect,
                    minStationLvl: BFMinStationLevel.Value,
                    recipeStr: BFLegsRecipe.Value
                );

                // ============================================================
                // SET 2: MOUNTAINS — GREY EMBLA (+100% Total Eitr Regen + Frost Resist)
                // ============================================================
                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: MTHoodPrefab,
                    donorPrefab: emblaHelmDonor,
                    mode: RecolorMode.MountainFrostGrey,
                    nameToken: "$item_wolfmage_hood",
                    descToken: "$item_wolfmage_hood_desc",
                    icon: mtHoodIcon,
                    armorBase: MTArmorBase.Value,
                    armorPerLvl: MTArmorPerLevel.Value,
                    eitrRegenBonus: MTHoodEitrRegen.Value,
                    grantFrostResist: false,
                    setName: MTSetName,
                    setEffect: s_mtSetStatusEffect,
                    minStationLvl: MTMinStationLevel.Value,
                    recipeStr: MTHoodRecipe.Value
                );

                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: MTChestPrefab,
                    donorPrefab: emblaChestDonor,
                    mode: RecolorMode.MountainFrostGrey,
                    nameToken: "$item_wolfmage_chest",
                    descToken: "$item_wolfmage_chest_desc",
                    icon: mtChestIcon,
                    armorBase: MTArmorBase.Value,
                    armorPerLvl: MTArmorPerLevel.Value,
                    eitrRegenBonus: MTChestEitrRegen.Value,
                    grantFrostResist: MTChestFrostResist.Value,
                    setName: MTSetName,
                    setEffect: s_mtSetStatusEffect,
                    minStationLvl: MTMinStationLevel.Value,
                    recipeStr: MTChestRecipe.Value
                );

                CloneRecolorAndRegisterVanillaArmor(
                    newPrefabName: MTLegsPrefab,
                    donorPrefab: emblaLegsDonor,
                    mode: RecolorMode.MountainFrostGrey,
                    nameToken: "$item_wolfmage_legs",
                    descToken: "$item_wolfmage_legs_desc",
                    icon: mtLegsIcon,
                    armorBase: MTArmorBase.Value,
                    armorPerLvl: MTArmorPerLevel.Value,
                    eitrRegenBonus: MTLegsEitrRegen.Value,
                    grantFrostResist: false,
                    setName: MTSetName,
                    setEffect: s_mtSetStatusEffect,
                    minStationLvl: MTMinStationLevel.Value,
                    recipeStr: MTLegsRecipe.Value
                );

                s_registered = true;
                if (ModLogger != null) ModLogger.LogInfo("Registered Blue Eitr-Weave (Black Forest) & Grey Embla (Mountains) sets cleanly v" + PluginVersion + "!");
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogError("Error registering recolored vanilla Eitr armors: " + ex);
            }
        }

        // ====================================================================
        // CLONE & RECOLOR VANILLA ARMOR PREFAB (100% NATIVE RIG, CLOTH & PIXEL GRAIN!)
        // ====================================================================
        private static void CloneRecolorAndRegisterVanillaArmor(
            string newPrefabName,
            GameObject donorPrefab,
            RecolorMode mode,
            string nameToken,
            string descToken,
            Sprite icon,
            float armorBase,
            float armorPerLvl,
            float eitrRegenBonus,
            bool grantFrostResist,
            string setName,
            StatusEffect setEffect,
            int minStationLvl,
            string recipeStr)
        {
            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, donorPrefab);
            if (cloned == null)
            {
                if (ModLogger != null) ModLogger.LogWarning("CreateClonedPrefab returned null for " + newPrefabName);
                return;
            }

            // 1) Recolor EVERY Renderer (`SkinnedMeshRenderer` under `attach_skin`, `MeshRenderer` under `attach`, and World-Drop renderers!)
            //    preserving 100% of vanilla meshes, bone weights, cloth physics, normal maps, metallic maps & pixel grain!
            Renderer[] allRenderers = cloned.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                Material[] newMats = new Material[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    newMats[m] = GetOrCreateRecoloredMaterial(mats[m], mode);
                }
                r.sharedMaterials = newMats;
            }

            // 2) Recolor the player body under-layer `m_armorMaterial` (`_ChestTex` & `_LegsTex`) AND configure exact stats!
            ItemDrop drop = cloned.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
            {
                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                shared.m_name = nameToken;
                shared.m_description = descToken;
                if (icon != null)
                {
                    shared.m_icons = new Sprite[] { icon };
                }

                if (shared.m_armorMaterial != null)
                {
                    shared.m_armorMaterial = GetOrCreateRecoloredMaterial(shared.m_armorMaterial, mode);
                }

                shared.m_maxQuality = 4;
                shared.m_armor = armorBase;
                shared.m_armorPerLevel = armorPerLvl;
                shared.m_movementModifier = 0f; // 0% speed penalty
                shared.m_eitrRegenModifier = eitrRegenBonus;
                shared.m_equipStatusEffect = null;
                shared.m_dlc = "";

                // Universal mage armor without weaknesses; Mountain chest grants Frost resistance!
                shared.m_damageModifiers = new List<HitData.DamageModPair>();
                if (grantFrostResist)
                {
                    shared.m_damageModifiers.Add(new HitData.DamageModPair
                    {
                        m_type = HitData.DamageType.Frost,
                        m_modifier = HitData.DamageModifier.Resistant
                    });
                }

                shared.m_setName = setName;
                shared.m_setSize = 3;
                shared.m_setStatusEffect = setEffect;
            }

            // 3) Register Forge recipe with Jotunn
            ItemConfig cfg = new ItemConfig();
            cfg.Name = nameToken;
            cfg.Description = descToken;
            cfg.Amount = 1;
            cfg.CraftingStation = CraftingStations.Forge;
            cfg.RepairStation = CraftingStations.Forge;
            cfg.MinStationLevel = Mathf.Max(1, minStationLvl);
            if (icon != null)
            {
                cfg.Icons = new Sprite[] { icon };
            }
            ParseAndAddRequirements(cfg, recipeStr);

            CustomItem customItem = new CustomItem(cloned, false, cfg);
            ItemManager.Instance.AddItem(customItem);
        }

        // ====================================================================
        // SMART HSV PIXEL-ART RECOLORING ENGINE (PRESERVES 100% OF VANILLA GRAIN & SHADING!)
        // ====================================================================
        private static Material GetOrCreateRecoloredMaterial(Material origMat, RecolorMode mode)
        {
            if (origMat == null) return null;
            string cacheKey = origMat.GetInstanceID() + "_" + mode.ToString();
            Material cached;
            if (s_recoloredMatCache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            Material cloned = new Material(origMat);
            cloned.name = origMat.name + "_" + mode.ToString();

            // Recolor every color diffuse/albedo/emission texture slot used by Valheim's item & player shaders!
            string[] texProps = new string[] { "_MainTex", "_ChestTex", "_LegsTex", "_EmissionMap" };
            for (int i = 0; i < texProps.Length; i++)
            {
                string prop = texProps[i];
                if (cloned.HasProperty(prop))
                {
                    Texture origTex = cloned.GetTexture(prop);
                    if (origTex != null)
                    {
                        Texture2D recolored = GetOrCreateRecoloredTexture(origTex, mode, isEmission: prop == "_EmissionMap");
                        if (recolored != null)
                        {
                            cloned.SetTexture(prop, recolored);
                            if (prop == "_MainTex") cloned.mainTexture = recolored;
                        }
                    }
                }
            }

            // Also recolor any flat tint `_Color` / `_EmissionColor` if the shader multiplies by a red/warm tint!
            if (cloned.HasProperty("_Color"))
            {
                Color c = cloned.GetColor("_Color");
                cloned.SetColor("_Color", TransformPixelColor(c, mode, isEmission: false));
            }
            if (cloned.HasProperty("_EmissionColor"))
            {
                Color ec = cloned.GetColor("_EmissionColor");
                if (ec.maxColorComponent > 0.01f)
                {
                    cloned.SetColor("_EmissionColor", TransformPixelColor(ec, mode, isEmission: true));
                }
            }

            s_recoloredMatCache[cacheKey] = cloned;
            return cloned;
        }

        private static Texture2D GetOrCreateRecoloredTexture(Texture srcTex, RecolorMode mode, bool isEmission)
        {
            if (srcTex == null) return null;
            string key = srcTex.GetInstanceID() + "_" + mode.ToString() + (isEmission ? "_EM" : "");
            Texture2D cached;
            if (s_recoloredTexCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            try
            {
                int w = Mathf.Max(4, srcTex.width);
                int h = Mathf.Max(4, srcTex.height);
                FilterMode origFilter = srcTex.filterMode;
                TextureWrapMode origWrap = srcTex.wrapMode;

                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.filterMode = FilterMode.Point;
                Graphics.Blit(srcTex, rt);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D readable = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
                readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color[] px = readable.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color c = px[i];
                    if (c.a > 0.002f || isEmission)
                    {
                        px[i] = TransformPixelColor(c, mode, isEmission);
                    }
                }

                readable.name = srcTex.name + "_" + mode.ToString();
                // Keep exact vanilla FilterMode.Point so every native Valheim pixel-art grain stays razor-sharp!
                readable.filterMode = origFilter;
                readable.wrapMode = origWrap;
                readable.SetPixels(px);
                readable.Apply(updateMipmaps: true, makeNoLongerReadable: false);

                s_recoloredTexCache[key] = readable;
                return readable;
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogWarning("Failed to recolor texture " + srcTex.name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Recolors individual pixels in HSV space while preserving 100% of Valheim's original hand-painted
        /// pixel grain, cloth weave, folds, shadows, highlights, and alpha masks!
        ///
        /// - `BlackForestTrollBlue` (Eitr-Weave -> Blue):
        ///   * Shifts crimson/red/burgundy/purple cloth (`hue < 0.07` or `hue > 0.80`) -> True Troll-Cape Blue (`hue ~ 0.56`, rich slate-azure)!
        ///   * Shifts warm gold/yellow brooches & trim (`hue in 0.07..0.19` with high V & S) -> Warm Black-Forest Copper/Bronze (`hue ~ 0.065`)!
        ///   * Neutral dark leather/boots/straps (`low saturation`) gain a subtle cool slate-blue tint!
        ///
        /// - `MountainFrostGrey` (Embla -> Grey):
        ///   * Desaturates colored cloth & warm bronze/green/red tones into sleek Storm-Wolf Grey / Ash-Silver (`hue ~ 0.58, sat * 0.14`)
        ///     so it pairs seamlessly with the Vanilla Wolf Fur Cape while keeping crisp light-to-dark contrast & silver highlights!
        /// </summary>
        private static Color TransformPixelColor(Color c, RecolorMode mode, bool isEmission)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);

            if (mode == RecolorMode.BlackForestTrollBlue)
            {
                if (isEmission)
                {
                    // Blue-cyan magical Eitr glow!
                    Color rgbEm = Color.HSVToRGB(0.55f, Mathf.Clamp01(s * 0.85f + 0.15f), v);
                    rgbEm.a = c.a;
                    return rgbEm;
                }

                // Check if pixel is crimson/red/magenta cloth (Eitr-Weave red cloth has H in [0.92..1.0] or [0.0..0.065]!)
                bool isRedCloth = (h >= 0.82f || h <= 0.068f) && s > 0.16f;
                // Check if pixel is bright gold/brass trim (`H in 0.068..0.18`, bright value)
                bool isGoldTrim = (h > 0.068f && h <= 0.19f) && s > 0.22f && v > 0.28f;

                if (isRedCloth)
                {
                    // Map crimson cloth -> exact Troll-Hide Cape Blue (`H = 0.555` = 200 deg Nordic Troll Blue!),
                    // slightly moderating saturation (`0.68x`) & boosting value (`1.12x`) so dark burgundy shadows become rich Troll-Cape slate-blue!
                    float newH = 0.555f;
                    float newS = Mathf.Clamp(s * 0.68f, 0.18f, 0.62f);
                    float newV = Mathf.Clamp01(v * 1.12f + 0.02f);
                    Color outC = Color.HSVToRGB(newH, newS, newV);
                    outC.a = c.a;
                    return outC;
                }
                else if (isGoldTrim)
                {
                    // Shift bright Mistlands gold slightly warmer toward Black-Forest Copper/Bronze (`H = 0.062`)!
                    Color outC = Color.HSVToRGB(0.062f, Mathf.Clamp01(s * 0.82f), Mathf.Clamp01(v * 0.96f));
                    outC.a = c.a;
                    return outC;
                }
                else
                {
                    // Brown leather trousers/straps/underlayers -> cool desaturated slate-leather to harmonize with blue cloth & Troll Cape!
                    float newH = 0.56f;
                    float newS = Mathf.Clamp(s * 0.35f, 0.05f, 0.28f);
                    Color outC = Color.HSVToRGB(newH, newS, v);
                    outC.a = c.a;
                    return outC;
                }
            }
            else // RecolorMode.MountainFrostGrey (Embla -> Sleek Mountain Wolf/Silver Grey!)
            {
                if (isEmission)
                {
                    // Cool Pale Frost-Silver / Ice Eitr glow!
                    Color rgbEm = Color.HSVToRGB(0.56f, 0.28f, v);
                    rgbEm.a = c.a;
                    return rgbEm;
                }

                // Transform Embla's warm/red/green/gold hues into a crisp, noble Wolf-Pelt & Silver Mountain Grey!
                // Bright trims (`v > 0.55, s > 0.20`) become gleaming Silver (`s = 0.06, v * 1.08`),
                // midtones become Ash-Wolf Grey (`H = 0.58, s = 0.07`), and dark cloth folds stay deep Charcoal-Grey!
                bool isBrightTrim = (s > 0.22f && v > 0.48f);
                float greyH = 0.58f; // Very subtle cool steel-grey undertone (matches Wolf Cape!)
                float greyS = isBrightTrim ? 0.05f : Mathf.Clamp(s * 0.12f, 0.02f, 0.09f);
                float greyV = isBrightTrim
                    ? Mathf.Clamp01(v * 1.10f + 0.04f)
                    : Mathf.Clamp01(v * 1.04f + 0.02f);

                Color outGrey = Color.HSVToRGB(greyH, greyS, greyV);
                outGrey.a = c.a;
                return outGrey;
            }
        }

        /// <summary>
        /// Extracts the exact sprite rectangle from the donor vanilla armor's `m_icons[0]` (even if packed inside a large UI atlas!)
        /// and recolors it into matching Blue (Black Forest) or Grey (Mountains) so inventory icons match the 3D armor 1:1!
        /// </summary>
        private static Sprite CloneAndRecolorItemIcon(GameObject donorPrefab, string iconName, RecolorMode mode)
        {
            ItemDrop drop = donorPrefab != null ? donorPrefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return null;
            Sprite[] icons = drop.m_itemData.m_shared.m_icons;
            if (icons == null || icons.Length == 0 || icons[0] == null) return null;

            Sprite origSprite = icons[0];
            Texture2D origTex = origSprite.texture;
            if (origTex == null) return origSprite;

            try
            {
                Rect tr = origSprite.textureRect;
                int fullW = origTex.width;
                int fullH = origTex.height;
                int rx = Mathf.Clamp(Mathf.RoundToInt(tr.x), 0, fullW - 1);
                int ry = Mathf.Clamp(Mathf.RoundToInt(tr.y), 0, fullH - 1);
                int rw = Mathf.Clamp(Mathf.RoundToInt(tr.width), 1, fullW - rx);
                int rh = Mathf.Clamp(Mathf.RoundToInt(tr.height), 1, fullH - ry);

                RenderTexture rt = RenderTexture.GetTemporary(fullW, fullH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(origTex, rt);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;

                // Note: In Unity OpenGL/DX11 ReadPixels on a RenderTexture, check Y orientation by reading full or rect;
                // reading the full atlas into a temporary Texture2D or reading `(rx, fullH - ry - rh, rw, rh)`?
                // Wait: `Texture2D.ReadPixels(new Rect(0, 0, fullW, fullH), 0, 0)` ALWAYS matches `Texture2D.GetPixels(rx, ry, rw, rh)` regardless of graphics API!
                Texture2D fullCopy = new Texture2D(fullW, fullH, TextureFormat.RGBA32, false, false);
                fullCopy.ReadPixels(new Rect(0, 0, fullW, fullH), 0, 0);
                fullCopy.Apply();

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color[] subPx = fullCopy.GetPixels(rx, ry, rw, rh);
                UnityEngine.Object.DestroyImmediate(fullCopy);

                for (int i = 0; i < subPx.Length; i++)
                {
                    if (subPx[i].a > 0.01f)
                    {
                        subPx[i] = TransformPixelColor(subPx[i], mode, isEmission: false);
                    }
                }

                Texture2D iconTex = new Texture2D(rw, rh, TextureFormat.RGBA32, false, false);
                iconTex.name = iconName;
                iconTex.filterMode = FilterMode.Bilinear;
                iconTex.wrapMode = TextureWrapMode.Clamp;
                iconTex.SetPixels(subPx);
                iconTex.Apply();

                return Sprite.Create(iconTex, new Rect(0f, 0f, rw, rh), new Vector2(0.5f, 0.5f), origSprite.pixelsPerUnit);
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogWarning("Icon recolor fallback for " + iconName + ": " + ex.Message);
                return origSprite;
            }
        }

        private static GameObject FindFirstValidPrefab(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                GameObject p = PrefabManager.Instance.GetPrefab(names[i]);
                if (p != null) return p;
            }
            return null;
        }

        private static SE_Stats CreateSetEitrStatusEffect(string effectName, string displayNameToken, Sprite icon, float bonusRegenFraction)
        {
            SE_Stats se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = effectName;
            se.m_name = displayNameToken;
            se.m_icon = icon;
            se.m_tooltip = "";
            se.m_eitrRegenMultiplier = 1.0f + Mathf.Max(0f, bonusRegenFraction);
            return se;
        }

        private static void ParseAndAddRequirements(ItemConfig cfg, string recipeStr)
        {
            if (string.IsNullOrEmpty(recipeStr)) return;
            string[] parts = recipeStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] tokens = parts[i].Trim().Split(':');
                if (tokens.Length >= 2)
                {
                    string resName = tokens[0].Trim();
                    int amount = 1;
                    int perLevel = 0;
                    int.TryParse(tokens[1].Trim(), out amount);
                    if (tokens.Length >= 3)
                    {
                        int.TryParse(tokens[2].Trim(), out perLevel);
                    }
                    if (!string.IsNullOrEmpty(resName) && (amount > 0 || perLevel > 0))
                    {
                        cfg.AddRequirement(new RequirementConfig(resName, amount, perLevel, true));
                    }
                }
            }
        }
    }
}
