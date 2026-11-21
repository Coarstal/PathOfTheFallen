using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace NewDualAxesMod
{
    /// <summary>
    /// Adds 6 Eitr foods across early-to-late pre-Mistlands progression:
    /// 1) Vanilla `NeckTailGrilled` -> adds +25 Eitr (keeps default HP/Stamina/BurnTime/Regen).
    /// 2) Vanilla `MushroomYellow`  -> adds +25 Eitr (keeps default HP/Stamina/BurnTime/Regen).
    /// 3) `FoodStuffedNeck` ("Mushroom-Stuffed Neck Tail") -> Cauldron lvl 1, x1: 40 Eitr / 15 HP / 20 Stamina, 20m, 2 hp/t.
    /// 4) `FoodNecroSoup` ("Necromancer's Brew") -> Cauldron lvl 2, x3: 50 Eitr / 15 HP / 40 Stamina, 20m, 3 hp/t.
    /// 5) `FoodSadAufer` ("Story of the Sad Aufer") -> Cauldron lvl 3, x2: 30 Eitr / 30 HP / 30 Stamina, 25m, 3 hp/t.
    /// 6) `FoodPlainsTart` ("Plains Shaman Tart") -> Cauldron lvl 4, x2: 65 Eitr / 20 HP / 10 Stamina, 30m, 4 hp/t.
    /// </summary>
    public static class EitrFoodsModule
    {
        public const string VanillaNeckTailGrilled = "NeckTailGrilled";
        public const string VanillaMushroomYellow = "MushroomYellow";

        public const string StuffedNeckPrefab = "FoodStuffedNeck";
        public const string NecroSoupPrefab = "FoodNecroSoup";
        public const string SadAuferPrefab = "FoodSadAufer";
        public const string PlainsTartPrefab = "FoodPlainsTart";

        public const string StuffedNeckPiecePrefab = "Piece_FoodStuffedNeck";
        public const string NecroSoupPiecePrefab = "Piece_FoodNecroSoup";
        public const string SadAuferPiecePrefab = "Piece_FoodSadAufer";
        public const string PlainsTartPiecePrefab = "Piece_FoodPlainsTart";

        // Config entries — Vanilla Eitr boosts & Visual Scale
        public static ConfigEntry<float> NeckTailGrilledEitr;
        public static ConfigEntry<float> MushroomYellowEitr;
        public static ConfigEntry<float> CustomFoodVisualScale;

        // Config entries — 3. Stuffed Neck
        public static ConfigEntry<float> StuffedNeckEitr;
        public static ConfigEntry<float> StuffedNeckHealth;
        public static ConfigEntry<float> StuffedNeckStamina;
        public static ConfigEntry<float> StuffedNeckDurationMinutes;
        public static ConfigEntry<float> StuffedNeckRegen;
        public static ConfigEntry<int> StuffedNeckCraftAmount;
        public static ConfigEntry<int> StuffedNeckMinCauldronLevel;
        public static ConfigEntry<string> StuffedNeckRecipe;

        // Config entries — 4. Necromancer's Soup
        public static ConfigEntry<float> NecroSoupEitr;
        public static ConfigEntry<float> NecroSoupHealth;
        public static ConfigEntry<float> NecroSoupStamina;
        public static ConfigEntry<float> NecroSoupDurationMinutes;
        public static ConfigEntry<float> NecroSoupRegen;
        public static ConfigEntry<int> NecroSoupCraftAmount;
        public static ConfigEntry<int> NecroSoupMinCauldronLevel;
        public static ConfigEntry<string> NecroSoupRecipe;

        // Config entries — 5. Story of the Sad Aufer
        public static ConfigEntry<float> SadAuferEitr;
        public static ConfigEntry<float> SadAuferHealth;
        public static ConfigEntry<float> SadAuferStamina;
        public static ConfigEntry<float> SadAuferDurationMinutes;
        public static ConfigEntry<float> SadAuferRegen;
        public static ConfigEntry<int> SadAuferCraftAmount;
        public static ConfigEntry<int> SadAuferMinCauldronLevel;
        public static ConfigEntry<string> SadAuferRecipe;

        // Config entries — 6. Plains Shaman Tart
        public static ConfigEntry<float> PlainsTartEitr;
        public static ConfigEntry<float> PlainsTartHealth;
        public static ConfigEntry<float> PlainsTartStamina;
        public static ConfigEntry<float> PlainsTartDurationMinutes;
        public static ConfigEntry<float> PlainsTartRegen;
        public static ConfigEntry<int> PlainsTartCraftAmount;
        public static ConfigEntry<int> PlainsTartMinCauldronLevel;
        public static ConfigEntry<string> PlainsTartRecipe;

        private static bool s_registered;
        private static bool s_trayPiecesRegistered;
        private static bool s_loggedVanillaPatchOnce;

        // Cached custom meshes, materials, icons
        internal static Mesh s_stuffedNeckMesh;
        internal static Material s_stuffedNeckMat;
        internal static Sprite s_stuffedNeckIcon;

        internal static Mesh s_necroSoupMesh;
        internal static Material s_necroSoupMat;
        internal static Sprite s_necroSoupIcon;

        internal static Mesh s_sadAuferMesh;
        internal static Material s_sadAuferMat;
        internal static Sprite s_sadAuferIcon;

        internal static Mesh s_plainsTartMesh;
        internal static Material s_plainsTartMat;
        internal static Sprite s_plainsTartIcon;

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            ConfigurationManagerAttributes clientAttr = new ConfigurationManagerAttributes { IsAdminOnly = false };

            const string secBoost = "03. Weapon Pack - 9. Eitr Foods (Vanilla Boosts)";
            const string secStuffed = "03. Weapon Pack - 10. Eitr Food (Stuffed Neck)";
            const string secNecro = "03. Weapon Pack - 11. Eitr Food (Necromancer Soup)";
            const string secAufer = "03. Weapon Pack - 12. Eitr Food (Sad Aufer)";
            const string secTart = "03. Weapon Pack - 13. Eitr Food (Plains Shaman Tart)";

            NeckTailGrilledEitr = config.Bind(
                secBoost,
                "NeckTailGrilledEitr",
                25f,
                new ConfigDescription("Eitr granted by vanilla Grilled Neck Tail (NeckTailGrilled). Default HP and Stamina remain unchanged.", null, adminAttr)
            );
            MushroomYellowEitr = config.Bind(
                secBoost,
                "MushroomYellowEitr",
                25f,
                new ConfigDescription("Eitr granted by vanilla Yellow Mushroom (MushroomYellow). Default HP and Stamina remain unchanged.", null, adminAttr)
            );
            CustomFoodVisualScale = config.Bind(
                secBoost,
                "CustomFoodVisualScale",
                1.0f,
                new ConfigDescription("Visual 3D model scale multiplier for the 4 custom Eitr dishes (1.0 = large ~0.55m-0.62m Valheim dish size).", null, clientAttr)
            );

            // 3) Mushroom-Stuffed Neck Tail
            StuffedNeckEitr = config.Bind(secStuffed, "Eitr", 40f, new ConfigDescription("Max Eitr granted.", null, adminAttr));
            StuffedNeckHealth = config.Bind(secStuffed, "Health", 15f, new ConfigDescription("Max Health granted (average of ingredients).", null, adminAttr));
            StuffedNeckStamina = config.Bind(secStuffed, "Stamina", 20f, new ConfigDescription("Max Stamina granted (average of ingredients).", null, adminAttr));
            StuffedNeckDurationMinutes = config.Bind(secStuffed, "DurationMinutes", 20f, new ConfigDescription("Food duration in minutes.", null, adminAttr));
            StuffedNeckRegen = config.Bind(secStuffed, "HealthRegen", 2f, new ConfigDescription("Health regen per tick.", null, adminAttr));
            StuffedNeckCraftAmount = config.Bind(secStuffed, "CraftAmount", 1, new ConfigDescription("Items produced per craft.", null, adminAttr));
            StuffedNeckMinCauldronLevel = config.Bind(secStuffed, "MinCauldronLevel", 1, new ConfigDescription("Minimum Cauldron level required.", null, adminAttr));
            StuffedNeckRecipe = config.Bind(
                secStuffed,
                "Recipe",
                "NeckTailGrilled:1,Mushroom:2,MushroomYellow:2",
                new ConfigDescription("Cauldron recipe (Prefab:Amount comma-separated).", null, adminAttr)
            );

            // 4) Necromancer's Brew
            NecroSoupEitr = config.Bind(secNecro, "Eitr", 50f, new ConfigDescription("Max Eitr granted.", null, adminAttr));
            NecroSoupHealth = config.Bind(secNecro, "Health", 15f, new ConfigDescription("Max Health granted.", null, adminAttr));
            NecroSoupStamina = config.Bind(secNecro, "Stamina", 40f, new ConfigDescription("Max Stamina granted.", null, adminAttr));
            NecroSoupDurationMinutes = config.Bind(secNecro, "DurationMinutes", 20f, new ConfigDescription("Food duration in minutes.", null, adminAttr));
            NecroSoupRegen = config.Bind(secNecro, "HealthRegen", 3f, new ConfigDescription("Health regen per tick.", null, adminAttr));
            NecroSoupCraftAmount = config.Bind(secNecro, "CraftAmount", 3, new ConfigDescription("Items produced per craft.", null, adminAttr));
            NecroSoupMinCauldronLevel = config.Bind(secNecro, "MinCauldronLevel", 2, new ConfigDescription("Minimum Cauldron level required.", null, adminAttr));
            NecroSoupRecipe = config.Bind(
                secNecro,
                "Recipe",
                "WitheredBone:1,Thistle:1,Entrails:2,Ooze:1",
                new ConfigDescription("Cauldron recipe (Prefab:Amount comma-separated).", null, adminAttr)
            );

            // 5) Story of the Sad Aufer
            SadAuferEitr = config.Bind(secAufer, "Eitr", 30f, new ConfigDescription("Max Eitr granted.", null, adminAttr));
            SadAuferHealth = config.Bind(secAufer, "Health", 30f, new ConfigDescription("Max Health granted.", null, adminAttr));
            SadAuferStamina = config.Bind(secAufer, "Stamina", 30f, new ConfigDescription("Max Stamina granted.", null, adminAttr));
            SadAuferDurationMinutes = config.Bind(secAufer, "DurationMinutes", 25f, new ConfigDescription("Food duration in minutes.", null, adminAttr));
            SadAuferRegen = config.Bind(secAufer, "HealthRegen", 3f, new ConfigDescription("Health regen per tick.", null, adminAttr));
            SadAuferCraftAmount = config.Bind(secAufer, "CraftAmount", 2, new ConfigDescription("Items produced per craft.", null, adminAttr));
            SadAuferMinCauldronLevel = config.Bind(secAufer, "MinCauldronLevel", 3, new ConfigDescription("Minimum Cauldron level required.", null, adminAttr));
            SadAuferRecipe = config.Bind(
                secAufer,
                "RecipeV2",
                "CookedWolfMeat:2,FreezeGland:2",
                new ConfigDescription("Cauldron recipe (Prefab:Amount comma-separated; FreezeGland = Drake Freeze Gland / Морозная железа).", null, adminAttr)
            );

            // 6) Plains Shaman Tart
            PlainsTartEitr = config.Bind(secTart, "Eitr", 65f, new ConfigDescription("Max Eitr granted.", null, adminAttr));
            PlainsTartHealth = config.Bind(secTart, "Health", 20f, new ConfigDescription("Max Health granted.", null, adminAttr));
            PlainsTartStamina = config.Bind(secTart, "Stamina", 10f, new ConfigDescription("Max Stamina granted.", null, adminAttr));
            PlainsTartDurationMinutes = config.Bind(secTart, "DurationMinutes", 30f, new ConfigDescription("Food duration in minutes.", null, adminAttr));
            PlainsTartRegen = config.Bind(secTart, "HealthRegen", 4f, new ConfigDescription("Health regen per tick.", null, adminAttr));
            PlainsTartCraftAmount = config.Bind(secTart, "CraftAmount", 2, new ConfigDescription("Items produced per craft.", null, adminAttr));
            PlainsTartMinCauldronLevel = config.Bind(secTart, "MinCauldronLevel", 4, new ConfigDescription("Minimum Cauldron level required.", null, adminAttr));
            PlainsTartRecipe = config.Bind(
                secTart,
                "Recipe",
                "Needle:2,Cloudberry:4,BarleyFlour:2",
                new ConfigDescription("Cauldron recipe (Prefab:Amount comma-separated).", null, adminAttr)
            );
        }

        public static void RegisterLocalizations()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string eng = "English";
            loc.AddTranslation(ref eng, new Dictionary<string, string>
            {
                { "item_food_stuffedneck", "Mushroom-Stuffed Neck Tail" },
                { "item_food_stuffedneck_desc", "A juicy grilled neck tail split along the ridge and stuffed with forest red mushrooms and glowing yellow cave-caps." },
                { "item_food_necrosoup", "Necromancer's Brew" },
                { "item_food_necrosoup_desc", "A bubbling bog-cauldron stew of withered bone, glowing thistle buds, entrails, and toxic ooze that awakens dark Eitr." },
                { "item_food_sadaufer", "Story of the Sad Aufer" },
                { "item_food_sadaufer_desc", "A sculpted figurine of a sorrowful sitting wolf molded from roasted wolf meat, glazed with icy drake freeze-gland essence onto an ashwood plate." },
                { "item_food_plainstart", "Plains Shaman Tart" },
                { "item_food_plainstart_desc", "A crisp barley-flour tart filled with golden cloudberry jam and crowned with two crossed emerald Deathsquito stingers." }
            });

            string rus = "Russian";
            loc.AddTranslation(ref rus, new Dictionary<string, string>
            {
                { "item_food_stuffedneck", "Никс, фаршированный грибами" },
                { "item_food_stuffedneck_desc", "Сочный жареный хвост никса, надрезанный вдоль гребня и щедро нашпигованный лесными красными и светящимися жёлтыми грибами." },
                { "item_food_necrosoup", "Похлёбка некроманта" },
                { "item_food_necrosoup_desc", "Густое варево из засохших костей, потрохов, болотной жижи и светящегося чертополоха, пробуждающее тёмный поток эйтра." },
                { "item_food_sadaufer", "История грустного ауфера" },
                { "item_food_sadaufer_desc", "Трогательное блюдо на деревянной тарелке: фигурка сидящего грустного волка из запечённого мяса, омытая ледяной эссенцией морозной железы дрейка." },
                { "item_food_plainstart", "Тарт Шамана Равнин" },
                { "item_food_plainstart_desc", "Хрустящая корзиночка из ячменной муки с густым морошковым джемом, украшенная двумя скрещёнными зелёными жалами комара смерти." }
            });
        }

        public static void RegisterAllEitrFoods()
        {
            if (!NewDualAxesPlugin.IsActive) return;
            try
            {
                ApplyVanillaEitrBoostsFromPrefabManager();

                if (s_registered && ItemManager.Instance.GetItem(StuffedNeckPrefab) != null)
                {
                    return;
                }

                Shader shader = FindFoodSurfaceShader();
                EnsureFoodAssetsLoaded(shader);

                string donorName = FindFoodDonorPrefabName();
                if (string.IsNullOrEmpty(donorName))
                {
                    if (NewDualAxesPlugin.ModLogger != null)
                    {
                        NewDualAxesPlugin.ModLogger.LogError("[EitrFoods] Could not find any vanilla cooked food donor prefab!");
                    }
                    return;
                }

                float scaleMult = Mathf.Clamp(CustomFoodVisualScale != null ? CustomFoodVisualScale.Value : 1.0f, 0.35f, 3.0f);

                // 3) Mushroom-Stuffed Neck Tail (Cauldron lvl 1 -> x1)
                RegisterCustomFood(
                    prefabName: StuffedNeckPrefab,
                    donorName: donorName,
                    nameToken: "$item_food_stuffedneck",
                    descToken: "$item_food_stuffedneck_desc",
                    icon: s_stuffedNeckIcon,
                    mesh: s_stuffedNeckMesh,
                    mat: s_stuffedNeckMat,
                    targetWidthMeters: 0.62f * scaleMult,
                    glowColor: new Color(1.0f, 0.88f, 0.35f, 1f),
                    eitr: StuffedNeckEitr.Value,
                    health: StuffedNeckHealth.Value,
                    stamina: StuffedNeckStamina.Value,
                    burnTimeSeconds: Mathf.Max(60f, StuffedNeckDurationMinutes.Value * 60f),
                    regen: StuffedNeckRegen.Value,
                    craftAmount: Mathf.Max(1, StuffedNeckCraftAmount.Value),
                    minCauldronLevel: Mathf.Max(1, StuffedNeckMinCauldronLevel.Value),
                    recipeStr: StuffedNeckRecipe.Value,
                    maxStackSize: 10,
                    weight: 1.0f
                );

                // 4) Necromancer's Brew (Cauldron lvl 2 -> x3)
                RegisterCustomFood(
                    prefabName: NecroSoupPrefab,
                    donorName: donorName,
                    nameToken: "$item_food_necrosoup",
                    descToken: "$item_food_necrosoup_desc",
                    icon: s_necroSoupIcon,
                    mesh: s_necroSoupMesh,
                    mat: s_necroSoupMat,
                    targetWidthMeters: 0.56f * scaleMult,
                    glowColor: new Color(0.32f, 0.96f, 0.68f, 1f),
                    eitr: NecroSoupEitr.Value,
                    health: NecroSoupHealth.Value,
                    stamina: NecroSoupStamina.Value,
                    burnTimeSeconds: Mathf.Max(60f, NecroSoupDurationMinutes.Value * 60f),
                    regen: NecroSoupRegen.Value,
                    craftAmount: Mathf.Max(1, NecroSoupCraftAmount.Value),
                    minCauldronLevel: Mathf.Max(1, NecroSoupMinCauldronLevel.Value),
                    recipeStr: NecroSoupRecipe.Value,
                    maxStackSize: 10,
                    weight: 1.0f
                );

                // 5) Story of the Sad Aufer (Cauldron lvl 3 -> x2)
                RegisterCustomFood(
                    prefabName: SadAuferPrefab,
                    donorName: donorName,
                    nameToken: "$item_food_sadaufer",
                    descToken: "$item_food_sadaufer_desc",
                    icon: s_sadAuferIcon,
                    mesh: s_sadAuferMesh,
                    mat: s_sadAuferMat,
                    targetWidthMeters: 0.55f * scaleMult,
                    glowColor: new Color(0.42f, 0.82f, 1.0f, 1f),
                    eitr: SadAuferEitr.Value,
                    health: SadAuferHealth.Value,
                    stamina: SadAuferStamina.Value,
                    burnTimeSeconds: Mathf.Max(60f, SadAuferDurationMinutes.Value * 60f),
                    regen: SadAuferRegen.Value,
                    craftAmount: Mathf.Max(1, SadAuferCraftAmount.Value),
                    minCauldronLevel: Mathf.Max(1, SadAuferMinCauldronLevel.Value),
                    recipeStr: SadAuferRecipe.Value,
                    maxStackSize: 10,
                    weight: 1.0f
                );

                // 6) Plains Shaman Tart (Cauldron lvl 4 -> x2)
                RegisterCustomFood(
                    prefabName: PlainsTartPrefab,
                    donorName: donorName,
                    nameToken: "$item_food_plainstart",
                    descToken: "$item_food_plainstart_desc",
                    icon: s_plainsTartIcon,
                    mesh: s_plainsTartMesh,
                    mat: s_plainsTartMat,
                    targetWidthMeters: 0.55f * scaleMult,
                    glowColor: new Color(0.95f, 0.42f, 0.78f, 1f),
                    eitr: PlainsTartEitr.Value,
                    health: PlainsTartHealth.Value,
                    stamina: PlainsTartStamina.Value,
                    burnTimeSeconds: Mathf.Max(60f, PlainsTartDurationMinutes.Value * 60f),
                    regen: PlainsTartRegen.Value,
                    craftAmount: Mathf.Max(1, PlainsTartCraftAmount.Value),
                    minCauldronLevel: Mathf.Max(1, PlainsTartMinCauldronLevel.Value),
                    recipeStr: PlainsTartRecipe.Value,
                    maxStackSize: 10,
                    weight: 1.0f
                );

                s_registered = true;
                RegisterServingTrayPieces(scaleMult);

                if (NewDualAxesPlugin.ModLogger != null)
                {
                    NewDualAxesPlugin.ModLogger.LogInfo("[EitrFoods] Patched 2 vanilla foods (+25 Eitr) and registered 4 custom Eitr dishes + Serving Tray pieces!");
                }
            }
            catch (Exception ex)
            {
                if (NewDualAxesPlugin.ModLogger != null)
                {
                    NewDualAxesPlugin.ModLogger.LogError("[EitrFoods] Failed to register Eitr foods: " + ex);
                }
            }
        }

        private static string FindFoodDonorPrefabName()
        {
            string[] candidates = new string[]
            {
                "SeekerAspic",
                "Salad",
                "CarrotSoup",
                "TurnipStew",
                "OnionSoup",
                "QueensJam",
                "WolfSkewer"
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (PrefabManager.Instance.GetPrefab(candidates[i]) != null)
                {
                    return candidates[i];
                }
            }
            return null;
        }

        private static Shader FindFoodSurfaceShader()
        {
            string[] foodCandidates = new string[]
            {
                "CarrotSoup",
                "TurnipStew",
                "Salad",
                "MeatPlatter",
                "LoxPie",
                "SeekerAspic"
            };
            for (int i = 0; i < foodCandidates.Length; i++)
            {
                GameObject p = PrefabManager.Instance.GetPrefab(foodCandidates[i]);
                if (p == null) continue;
                MeshRenderer[] mrs = p.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < mrs.Length; r++)
                {
                    MeshRenderer mr = mrs[r];
                    if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null)
                    {
                        string sName = mr.sharedMaterial.shader.name;
                        if (!sName.ToLowerInvariant().Contains("particle") && !sName.ToLowerInvariant().Contains("alpha"))
                        {
                            return mr.sharedMaterial.shader;
                        }
                    }
                }
            }
            return NewDualAxesPlugin.FindValheimStandardShader();
        }

        private static void EnsureFoodAssetsLoaded(Shader shader)
        {
            if (s_stuffedNeckMesh != null) return;

            s_stuffedNeckMesh = NewDualAxesPlugin.LoadEmbeddedMesh("FoodStuffedNeck.bin.gz", "FoodStuffedNeck");
            s_stuffedNeckIcon = NewDualAxesPlugin.LoadEmbeddedSprite("FoodStuffedNeck_Icon.png");
            s_stuffedNeckMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader,
                "Mat_FoodStuffedNeck",
                "FoodStuffedNeck_Albedo.png",
                "FoodStuffedNeck_Roughness.png",
                "FoodStuffedNeck_Normal.png",
                "FoodStuffedNeck_Emission.png",
                0.0f,
                Color.white * 1.45f
            );
            TuneFoodMaterialProperties(s_stuffedNeckMat);

            s_necroSoupMesh = NewDualAxesPlugin.LoadEmbeddedMesh("FoodNecroSoup.bin.gz", "FoodNecroSoup");
            s_necroSoupIcon = NewDualAxesPlugin.LoadEmbeddedSprite("FoodNecroSoup_Icon.png");
            s_necroSoupMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader,
                "Mat_FoodNecroSoup",
                "FoodNecroSoup_Albedo.png",
                "FoodNecroSoup_Roughness.png",
                "FoodNecroSoup_Normal.png",
                "FoodNecroSoup_Emission.png",
                0.04f,
                Color.white * 1.75f
            );
            TuneFoodMaterialProperties(s_necroSoupMat);

            s_sadAuferMesh = NewDualAxesPlugin.LoadEmbeddedMesh("FoodSadAufer.bin.gz", "FoodSadAufer");
            s_sadAuferIcon = NewDualAxesPlugin.LoadEmbeddedSprite("FoodSadAufer_Icon.png");
            s_sadAuferMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader,
                "Mat_FoodSadAufer",
                "FoodSadAufer_Albedo.png",
                "FoodSadAufer_Roughness.png",
                "FoodSadAufer_Normal.png",
                "FoodSadAufer_Emission.png",
                0.0f,
                Color.white * 1.65f
            );
            TuneFoodMaterialProperties(s_sadAuferMat);

            s_plainsTartMesh = NewDualAxesPlugin.LoadEmbeddedMesh("FoodPlainsTart.bin.gz", "FoodPlainsTart");
            s_plainsTartIcon = NewDualAxesPlugin.LoadEmbeddedSprite("FoodPlainsTart_Icon.png");
            s_plainsTartMat = NewDualAxesPlugin.BuildValheimMaterial(
                shader,
                "Mat_FoodPlainsTart",
                "FoodPlainsTart_Albedo.png",
                "FoodPlainsTart_Roughness.png",
                "FoodPlainsTart_Normal.png",
                "FoodPlainsTart_Emission.png",
                0.05f,
                Color.white * 1.55f
            );
            TuneFoodMaterialProperties(s_plainsTartMat);
        }

        private static void TuneFoodMaterialProperties(Material mat)
        {
            if (mat == null) return;
            // Ensure purely dielectric organic/wood diffuse response so indirect sky/fire light illuminates the dish warmly!
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
            if (mat.HasProperty("_UseGlossMap")) mat.SetFloat("_UseGlossMap", 0.0f);
            mat.DisableKeyword("_METALLICGLOSSMAP");
        }

        private static void RegisterCustomFood(
            string prefabName,
            string donorName,
            string nameToken,
            string descToken,
            Sprite icon,
            Mesh mesh,
            Material mat,
            float targetWidthMeters,
            Color glowColor,
            float eitr,
            float health,
            float stamina,
            float burnTimeSeconds,
            float regen,
            int craftAmount,
            int minCauldronLevel,
            string recipeStr,
            int maxStackSize,
            float weight)
        {
            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(prefabName, donorName);
            if (cloned == null) return;

            cloned.transform.localScale = Vector3.one;
            if (!cloned.activeSelf) cloned.SetActive(true);

            ApplyCustomFoodVisuals(cloned, prefabName, mesh, mat, targetWidthMeters, glowColor);

            ItemDrop id = cloned.GetComponent<ItemDrop>();
            if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null)
            {
                // Clear any donor piece toggle references so ItemDrop.Awake / MakePiece NEVER hides our custom dish model!
                id.m_pieceEnableObj = null;
                id.m_pieceDisabledObj = null;

                id.m_itemData.m_dropPrefab = cloned;
                ItemDrop.ItemData.SharedData sh = id.m_itemData.m_shared;
                sh.m_name = nameToken;
                sh.m_description = descToken;
                sh.m_dlc = "";
                sh.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                sh.m_attachOverride = ItemDrop.ItemData.ItemType.None;
                sh.m_maxQuality = 1;
                sh.m_maxStackSize = maxStackSize;
                sh.m_autoStack = true;
                sh.m_weight = weight;
                sh.m_teleportable = true;
                sh.m_isDrink = false;
                sh.m_consumeStatusEffect = null;

                sh.m_food = health;
                sh.m_foodStamina = stamina;
                sh.m_foodEitr = eitr;
                sh.m_foodBurnTime = burnTimeSeconds;
                sh.m_foodRegen = regen;
                if (sh.m_foodEatAnimTime <= 0f)
                {
                    sh.m_foodEatAnimTime = 1f;
                }

                if (icon != null)
                {
                    sh.m_icons = new Sprite[] { icon };
                }
            }

            // Configure native Valheim Bog Witch Serving Tray (`Feaster` / `_FeasterPieceTable`) components directly on the food prefab!
            // In Valheim Bog Witch, individual placed foods on the Serving Tray ARE the `ItemDrop` prefabs themselves:
            // `Player.PlacePiece` instantiates the food prefab and calls `ItemDrop.MakePiece()`, which destroys `Rigidbody`,
            // enables `WearNTear`, sets layer `"piece"`, and makes `[E]` call `ItemDrop.Eat()` on THIS exact food!
            ConfigureNativeServingTraySupportOnFoodPrefab(cloned, id, nameToken, descToken, icon);

            ItemConfig cfg = new ItemConfig();
            cfg.Name = nameToken;
            cfg.Description = descToken;
            cfg.Amount = craftAmount;
            cfg.CraftingStation = CraftingStations.Cauldron;
            cfg.MinStationLevel = minCauldronLevel;
            if (icon != null)
            {
                cfg.Icons = new Sprite[] { icon };
            }

            NewDualAxesPlugin.ParseAndAddRequirements(cfg, recipeStr);

            ItemManager.Instance.AddItem(new CustomItem(cloned, false, cfg));
        }

        private static void ApplyCustomFoodVisuals(
            GameObject prefab,
            string prefabName,
            Mesh mesh,
            Material mat,
            float targetWidthMeters,
            Color glowColor)
        {
            if (prefab == null || mesh == null || mat == null) return;

            // Compute uniform scale so the dish plate/bowl spans `targetWidthMeters` (~0.34m - 0.36m) in world space,
            // resting cleanly on Y = 0 at its base!
            Bounds b = mesh.bounds;
            float horizSpan = Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z));
            float worldScale = targetWidthMeters / horizSpan;
            Vector3 bottomCenterOffset = new Vector3(-b.center.x * worldScale, -b.min.y * worldScale, -b.center.z * worldScale);
            Mesh bakedDishMesh = NewDualAxesPlugin.BakeTransformedMesh(mesh, bottomCenterOffset, Quaternion.identity, worldScale);
            bakedDishMesh.name = prefabName + "_DishMesh";

            Transform attach = prefab.transform.Find("attach");

            // Strip any leftover donor particle systems or donor point lights so e.g. SeekerAspic mist doesn't tint other dishes
            ParticleSystem[] donorParticles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < donorParticles.Length; i++)
            {
                if (donorParticles[i] != null)
                {
                    donorParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    donorParticles[i].gameObject.SetActive(false);
                }
            }

            Light[] donorLights = prefab.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < donorLights.Length; i++)
            {
                if (donorLights[i] != null)
                {
                    donorLights[i].enabled = false;
                }
            }

            // 1) Disable ALL donor MeshRenderers/SkinnedMeshRenderers outside `attach` and create a fresh direct child `"EitrDishVisual"`
            // at local (0,0,0) with scale (1,1,1) so no donor parent offset/rotation/scale or hidden toggle can ever hide or shift our dish!
            SkinnedMeshRenderer[] donorSmrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < donorSmrs.Length; i++)
            {
                if (donorSmrs[i] != null) donorSmrs[i].enabled = false;
            }

            MeshFilter[] allFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < allFilters.Length; i++)
            {
                MeshFilter mf = allFilters[i];
                if (mf == null) continue;
                if (attach != null && mf.transform.IsChildOf(attach)) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
                mf.sharedMesh = null;
            }

            GameObject modelGo = new GameObject("EitrDishVisual");
            modelGo.transform.SetParent(prefab.transform, false);
            modelGo.transform.localPosition = Vector3.zero;
            modelGo.transform.localRotation = Quaternion.identity;
            modelGo.transform.localScale = Vector3.one;
            MeshFilter dishMf = modelGo.AddComponent<MeshFilter>();
            dishMf.sharedMesh = bakedDishMesh;
            MeshRenderer dishMr = modelGo.AddComponent<MeshRenderer>();
            dishMr.sharedMaterials = new Material[] { mat };
            dishMr.enabled = true;

            // Subtle warm Eitr glow above the top of the dish (higher so it illuminates the top surface rather than only inside)
            GameObject dropGlow = new GameObject("EitrFoodGlow");
            dropGlow.transform.SetParent(prefab.transform, false);
            dropGlow.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            Light dropLight = dropGlow.AddComponent<Light>();
            dropLight.type = LightType.Point;
            dropLight.color = glowColor;
            dropLight.range = 1.25f;
            dropLight.intensity = 0.35f;
            dropLight.shadows = LightShadows.None;

            // 2) Ensure `attach` child exists and carries the exact dish mesh + material so Item Stands & Serving Trays display the 3D dish!
            if (attach == null)
            {
                GameObject attachGo = new GameObject("attach");
                attachGo.transform.SetParent(prefab.transform, false);
                attach = attachGo.transform;
            }
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.identity;
            attach.localScale = Vector3.one;
            attach.gameObject.SetActive(false);

            MeshFilter[] attachFilters = attach.GetComponentsInChildren<MeshFilter>(true);
            bool replacedAttachMesh = false;
            for (int i = 0; i < attachFilters.Length; i++)
            {
                MeshFilter mf = attachFilters[i];
                if (mf == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                if (!replacedAttachMesh)
                {
                    float relScale = NewDualAxesPlugin.GetRelativeScaleMagnitude(mf.transform, attach);
                    if (relScale < 1e-5f) relScale = 1f;

                    mf.transform.localPosition = Vector3.zero;
                    mf.transform.localRotation = Quaternion.identity;
                    mf.transform.localScale = Vector3.one / relScale;
                    mf.sharedMesh = bakedDishMesh;
                    mr.sharedMaterials = new Material[] { mat };
                    mr.enabled = true;
                    MagicStavesModule.EnsureTransformChainActiveUpTo(mf.transform, attach);
                    replacedAttachMesh = true;
                }
                else
                {
                    mr.enabled = false;
                    mf.sharedMesh = null;
                }
            }

            if (!replacedAttachMesh)
            {
                GameObject attachModel = new GameObject("model");
                attachModel.transform.SetParent(attach, false);
                attachModel.transform.localPosition = Vector3.zero;
                attachModel.transform.localRotation = Quaternion.identity;
                attachModel.transform.localScale = Vector3.one;
                MeshFilter mf = attachModel.AddComponent<MeshFilter>();
                mf.sharedMesh = bakedDishMesh;
                MeshRenderer mr = attachModel.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { mat };
            }

            NewDualAxesPlugin.FitWorldDropColliders(prefab);

            // Ensure ALL BoxColliders (even on child objects like `"model"`) match the scaled dish bounds cleanly
            // so placement ghost height, table snapping, and `[E]` hover raycast work accurately!
            FitAllBoxCollidersToDishBounds(prefab, bakedDishMesh.bounds, attach);

            // Clear `DisableInPlacementGhost` object/renderer lists if present from donor so our 3D dish is 100% visible in the placement ghost!
            DisableInPlacementGhost[] ghostDisablers = prefab.GetComponentsInChildren<DisableInPlacementGhost>(true);
            for (int i = 0; i < ghostDisablers.Length; i++)
            {
                if (ghostDisablers[i] != null)
                {
                    if (ghostDisablers[i].m_objects != null) ghostDisablers[i].m_objects.Clear();
                    if (ghostDisablers[i].m_components != null) ghostDisablers[i].m_components.Clear();
                }
            }
        }

        private static void FitAllBoxCollidersToDishBounds(GameObject root, Bounds dishBounds, Transform skipSubtree)
        {
            if (root == null) return;
            BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
            bool foundAny = false;
            for (int i = 0; i < boxes.Length; i++)
            {
                BoxCollider bc = boxes[i];
                if (bc == null) continue;
                if (skipSubtree != null && bc.transform.IsChildOf(skipSubtree)) continue;

                foundAny = true;
                float relS = NewDualAxesPlugin.GetRelativeScaleMagnitude(bc.transform, root.transform);
                if (relS < 1e-5f) relS = 1f;
                if (bc.transform != root.transform)
                {
                    bc.transform.localPosition = Vector3.zero;
                    bc.transform.localRotation = Quaternion.identity;
                }
                bc.center = dishBounds.center / relS;
                bc.size = new Vector3(
                    Mathf.Max(0.24f, dishBounds.size.x) / relS,
                    Mathf.Max(0.12f, dishBounds.size.y) / relS,
                    Mathf.Max(0.24f, dishBounds.size.z) / relS
                );
            }

            if (!foundAny)
            {
                BoxCollider added = root.AddComponent<BoxCollider>();
                added.center = dishBounds.center;
                added.size = new Vector3(
                    Mathf.Max(0.24f, dishBounds.size.x),
                    Mathf.Max(0.12f, dishBounds.size.y),
                    Mathf.Max(0.24f, dishBounds.size.z)
                );
            }
        }

        private static void ConfigureNativeServingTraySupportOnFoodPrefab(
            GameObject foodPrefab,
            ItemDrop id,
            string nameToken,
            string descToken,
            Sprite icon)
        {
            if (foodPrefab == null || id == null) return;

            Piece piece = foodPrefab.GetComponent<Piece>();
            if (piece == null)
            {
                piece = foodPrefab.AddComponent<Piece>();
            }

            piece.m_name = nameToken;
            piece.m_description = descToken;
            piece.m_dlc = "";
            piece.m_category = Piece.PieceCategory.Food;
            piece.m_usage = Piece.UsageTagFlags.Food;
            piece.m_craftingStation = null;
            piece.m_canBeRemoved = true;
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            piece.m_cultivatedGroundOnly = false;
            piece.m_waterPiece = false;
            piece.m_noInWater = true;
            piece.m_notOnWood = false;
            piece.m_notOnTiltingSurface = false;
            piece.m_inCeilingOnly = false;
            piece.m_notOnFloor = false;
            piece.m_allowedInDungeons = true;
            piece.m_onlyInTeleportArea = false;
            piece.m_clipEverything = true;
            if (icon != null)
            {
                piece.m_icon = icon;
            }

            Piece.Requirement req = new Piece.Requirement();
            req.m_resItem = id;
            req.m_amount = 1;
            req.m_amountPerLevel = 0;
            req.m_recover = true;
            piece.m_resources = new Piece.Requirement[] { req };

            WearNTear wnt = foodPrefab.GetComponent<WearNTear>();
            if (wnt == null)
            {
                wnt = foodPrefab.AddComponent<WearNTear>();
                wnt.m_health = 10f;
                wnt.m_noRoofWear = false;
                wnt.m_noSupportWear = false;
                wnt.m_supports = false;
            }
            // Keep disabled on normal physics ItemDrop; Valheim's `ItemDrop.Awake()` caches `m_piece = GetComponent<Piece>()` and `m_wnt = GetComponent<WearNTear>()`,
            // and `ItemDrop.MakePiece()` enables `m_wnt.enabled = true` when placed via Serving Tray!
            wnt.enabled = false;

            id.m_pieceEnableObj = null;
            id.m_pieceDisabledObj = null;
        }

        /// <summary>
        /// Keeps legacy `Piece_Food*` prefabs registered in ZNetScene (with donor `ItemDrop` stripped and active 3D mesh + `Feast` fixed)
        /// ONLY for backwards-compatibility with any `Piece_Food*` objects already saved in a world, while the Serving Tray (`_FeasterPieceTable`)
        /// now places the authentic `FoodStuffedNeck` / `FoodNecroSoup` / `FoodSadAufer` / `FoodPlainsTart` prefabs directly!
        /// </summary>
        public static void RegisterServingTrayPieces(float scaleMult)
        {
            try
            {
                if (s_trayPiecesRegistered && PrefabManager.Instance.GetPrefab(StuffedNeckPiecePrefab) != null)
                {
                    return;
                }

                string donorPieceName = FindLegacyFeastDonorPieceName();
                if (string.IsNullOrEmpty(donorPieceName))
                {
                    return;
                }

                RegisterLegacySavedWorldPiece(
                    piecePrefabName: StuffedNeckPiecePrefab,
                    donorPieceName: donorPieceName,
                    foodItemPrefabName: StuffedNeckPrefab,
                    nameToken: "$item_food_stuffedneck",
                    descToken: "$item_food_stuffedneck_desc",
                    icon: s_stuffedNeckIcon,
                    mesh: s_stuffedNeckMesh,
                    mat: s_stuffedNeckMat,
                    targetWidthMeters: 0.62f * scaleMult,
                    glowColor: new Color(1.0f, 0.88f, 0.35f, 1f)
                );

                RegisterLegacySavedWorldPiece(
                    piecePrefabName: NecroSoupPiecePrefab,
                    donorPieceName: donorPieceName,
                    foodItemPrefabName: NecroSoupPrefab,
                    nameToken: "$item_food_necrosoup",
                    descToken: "$item_food_necrosoup_desc",
                    icon: s_necroSoupIcon,
                    mesh: s_necroSoupMesh,
                    mat: s_necroSoupMat,
                    targetWidthMeters: 0.56f * scaleMult,
                    glowColor: new Color(0.32f, 0.96f, 0.68f, 1f)
                );

                RegisterLegacySavedWorldPiece(
                    piecePrefabName: SadAuferPiecePrefab,
                    donorPieceName: donorPieceName,
                    foodItemPrefabName: SadAuferPrefab,
                    nameToken: "$item_food_sadaufer",
                    descToken: "$item_food_sadaufer_desc",
                    icon: s_sadAuferIcon,
                    mesh: s_sadAuferMesh,
                    mat: s_sadAuferMat,
                    targetWidthMeters: 0.55f * scaleMult,
                    glowColor: new Color(0.42f, 0.82f, 1.0f, 1f)
                );

                RegisterLegacySavedWorldPiece(
                    piecePrefabName: PlainsTartPiecePrefab,
                    donorPieceName: donorPieceName,
                    foodItemPrefabName: PlainsTartPrefab,
                    nameToken: "$item_food_plainstart",
                    descToken: "$item_food_plainstart_desc",
                    icon: s_plainsTartIcon,
                    mesh: s_plainsTartMesh,
                    mat: s_plainsTartMat,
                    targetWidthMeters: 0.55f * scaleMult,
                    glowColor: new Color(0.95f, 0.42f, 0.78f, 1f)
                );

                s_trayPiecesRegistered = true;
            }
            catch (Exception ex)
            {
                if (NewDualAxesPlugin.ModLogger != null)
                {
                    NewDualAxesPlugin.ModLogger.LogError("[EitrFoods] RegisterServingTrayPieces error: " + ex);
                }
            }
        }

        private static string FindLegacyFeastDonorPieceName()
        {
            string[] feastCandidates = new string[]
            {
                "piece_feastmeadows",
                "piece_feastblackforest",
                "piece_feastswamps",
                "piece_feastmountains",
                "piece_feastplains"
            };
            for (int i = 0; i < feastCandidates.Length; i++)
            {
                if (PrefabManager.Instance.GetPrefab(feastCandidates[i]) != null)
                {
                    return feastCandidates[i];
                }
            }
            return null;
        }

        private static void RegisterLegacySavedWorldPiece(
            string piecePrefabName,
            string donorPieceName,
            string foodItemPrefabName,
            string nameToken,
            string descToken,
            Sprite icon,
            Mesh mesh,
            Material mat,
            float targetWidthMeters,
            Color glowColor)
        {
            GameObject pieceGo = PrefabManager.Instance.CreateClonedPrefab(piecePrefabName, donorPieceName);
            if (pieceGo == null) return;

            pieceGo.transform.localScale = Vector3.one;
            if (!pieceGo.activeSelf) pieceGo.SetActive(true);

            // CRITICAL: Ensure NO donor `ItemDrop` or `Rigidbody` exists on `Piece_Food*` so `[E]` NEVER triggers a donor Mead/Food ItemDrop!
            ItemDrop[] leftoverDrops = pieceGo.GetComponentsInChildren<ItemDrop>(true);
            for (int i = 0; i < leftoverDrops.Length; i++)
            {
                if (leftoverDrops[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(leftoverDrops[i]);
                }
            }
            Rigidbody[] leftoverBodies = pieceGo.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < leftoverBodies.Length; i++)
            {
                if (leftoverBodies[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(leftoverBodies[i]);
                }
            }
            DisableInPlacementGhost[] ghostDisablers = pieceGo.GetComponentsInChildren<DisableInPlacementGhost>(true);
            for (int i = 0; i < ghostDisablers.Length; i++)
            {
                if (ghostDisablers[i] != null)
                {
                    if (ghostDisablers[i].m_objects != null) ghostDisablers[i].m_objects.Clear();
                    if (ghostDisablers[i].m_components != null) ghostDisablers[i].m_components.Clear();
                }
            }

            // Bake dish mesh resting on Y = 0
            Bounds b = mesh.bounds;
            float horizSpan = Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z));
            float worldScale = targetWidthMeters / horizSpan;
            Vector3 bottomCenterOffset = new Vector3(-b.center.x * worldScale, -b.min.y * worldScale, -b.center.z * worldScale);
            Mesh bakedDishMesh = NewDualAxesPlugin.BakeTransformedMesh(mesh, bottomCenterOffset, Quaternion.identity, worldScale);
            bakedDishMesh.name = piecePrefabName + "_TrayMesh";

            // Disable all donor renderers/particles/lights cleanly and create a fresh guaranteed-active `"model"` child!
            ParticleSystem[] donorParticles = pieceGo.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < donorParticles.Length; i++)
            {
                if (donorParticles[i] != null)
                {
                    donorParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    donorParticles[i].gameObject.SetActive(false);
                }
            }
            Light[] donorLights = pieceGo.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < donorLights.Length; i++)
            {
                if (donorLights[i] != null) donorLights[i].enabled = false;
            }
            MeshRenderer[] donorRenderers = pieceGo.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < donorRenderers.Length; i++)
            {
                if (donorRenderers[i] != null) donorRenderers[i].enabled = false;
            }
            MeshFilter[] donorFilters = pieceGo.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < donorFilters.Length; i++)
            {
                if (donorFilters[i] != null) donorFilters[i].sharedMesh = null;
            }

            GameObject modelGo = new GameObject("model");
            modelGo.transform.SetParent(pieceGo.transform, false);
            modelGo.transform.localPosition = Vector3.zero;
            modelGo.transform.localRotation = Quaternion.identity;
            modelGo.transform.localScale = Vector3.one;
            MeshFilter mf = modelGo.AddComponent<MeshFilter>();
            mf.sharedMesh = bakedDishMesh;
            MeshRenderer mr = modelGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { mat };
            mr.enabled = true;

            FitAllBoxCollidersToDishBounds(pieceGo, bakedDishMesh.bounds, null);

            GameObject trayGlow = new GameObject("EitrTrayGlow");
            trayGlow.transform.SetParent(pieceGo.transform, false);
            trayGlow.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            Light trayLight = trayGlow.AddComponent<Light>();
            trayLight.type = LightType.Point;
            trayLight.color = glowColor;
            trayLight.range = 1.25f;
            trayLight.intensity = 0.35f;
            trayLight.shadows = LightShadows.None;

            GameObject foodItemGo = PrefabManager.Instance.GetPrefab(foodItemPrefabName);
            ItemDrop foodItemDrop = (foodItemGo != null) ? foodItemGo.GetComponent<ItemDrop>() : null;

            Feast feast = pieceGo.GetComponent<Feast>();
            if (feast == null)
            {
                feast = pieceGo.AddComponent<Feast>();
            }
            feast.m_eatStacks = 1;
            if (foodItemDrop != null)
            {
                feast.m_foodItem = foodItemDrop;
            }
            if (feast.m_feastParts != null)
            {
                feast.m_feastParts.Clear();
            }

            Piece piece = pieceGo.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = nameToken;
                piece.m_description = descToken;
                piece.m_dlc = "";
                piece.m_category = Piece.PieceCategory.Food;
                piece.m_usage = Piece.UsageTagFlags.Food;
                piece.m_craftingStation = null;
                piece.m_canBeRemoved = true;
                if (icon != null)
                {
                    piece.m_icon = icon;
                }
                if (foodItemDrop != null)
                {
                    Piece.Requirement req = new Piece.Requirement();
                    req.m_resItem = foodItemDrop;
                    req.m_amount = 1;
                    req.m_recover = true;
                    piece.m_resources = new Piece.Requirement[] { req };
                }
            }

            PrefabManager.Instance.AddPrefab(pieceGo);
        }

        public static void EnsureServingTrayPiecesLinked(ObjectDB odb)
        {
            if (odb == null) return;

            string[] itemNames = new string[] { StuffedNeckPrefab, NecroSoupPrefab, SadAuferPrefab, PlainsTartPrefab };
            string[] legacyPieceNames = new string[] { StuffedNeckPiecePrefab, NecroSoupPiecePrefab, SadAuferPiecePrefab, PlainsTartPiecePrefab };

            // Find the Serving Tray PieceTable (`_FeasterPieceTable`) on `Feaster` in ObjectDB
            PieceTable trayTable = null;
            GameObject feasterGo = odb.GetItemPrefab("Feaster");
            if (feasterGo == null && PrefabManager.Instance != null)
            {
                feasterGo = PrefabManager.Instance.GetPrefab("Feaster");
            }
            if (feasterGo != null)
            {
                ItemDrop feasterDrop = feasterGo.GetComponent<ItemDrop>();
                if (feasterDrop != null && feasterDrop.m_itemData != null && feasterDrop.m_itemData.m_shared != null)
                {
                    trayTable = feasterDrop.m_itemData.m_shared.m_buildPieces;
                }
            }

            // Remove any legacy `Piece_Food*` entries from `_FeasterPieceTable` so only the authentic `Food*` prefabs appear in the Serving Tray!
            if (trayTable != null && trayTable.m_pieces != null)
            {
                for (int r = trayTable.m_pieces.Count - 1; r >= 0; r--)
                {
                    GameObject existing = trayTable.m_pieces[r];
                    if (existing == null) continue;
                    for (int k = 0; k < legacyPieceNames.Length; k++)
                    {
                        if (existing.name == legacyPieceNames[k])
                        {
                            trayTable.m_pieces.RemoveAt(r);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < itemNames.Length; i++)
            {
                GameObject foodGo = odb.GetItemPrefab(itemNames[i]);
                if (foodGo == null && PrefabManager.Instance != null)
                {
                    foodGo = PrefabManager.Instance.GetPrefab(itemNames[i]);
                }
                if (foodGo == null) continue;

                ItemDrop foodDrop = foodGo.GetComponent<ItemDrop>();
                if (foodDrop != null && foodDrop.m_itemData != null && foodDrop.m_itemData.m_shared != null)
                {
                    Sprite icon = (foodDrop.m_itemData.m_shared.m_icons != null && foodDrop.m_itemData.m_shared.m_icons.Length > 0)
                        ? foodDrop.m_itemData.m_shared.m_icons[0]
                        : null;
                    ConfigureNativeServingTraySupportOnFoodPrefab(
                        foodGo,
                        foodDrop,
                        foodDrop.m_itemData.m_shared.m_name,
                        foodDrop.m_itemData.m_shared.m_description,
                        icon
                    );
                }

                // Register the authentic `Food*` ItemDrop prefab directly in `_FeasterPieceTable` (exact vanilla Bog Witch architecture!)
                if (trayTable != null && trayTable.m_pieces != null && !trayTable.m_pieces.Contains(foodGo))
                {
                    trayTable.m_pieces.Add(foodGo);
                }

                // Also keep legacy `Piece_Food*` linked to `foodDrop` in case any were saved in the world previously
                GameObject legacyGo = null;
                if (ZNetScene.instance != null)
                {
                    legacyGo = ZNetScene.instance.GetPrefab(legacyPieceNames[i]);
                }
                if (legacyGo == null && PrefabManager.Instance != null)
                {
                    legacyGo = PrefabManager.Instance.GetPrefab(legacyPieceNames[i]);
                }
                if (legacyGo != null && foodDrop != null)
                {
                    Feast feast = legacyGo.GetComponent<Feast>();
                    if (feast != null)
                    {
                        feast.m_foodItem = foodDrop;
                        feast.m_eatStacks = 1;
                    }
                    Piece lpc = legacyGo.GetComponent<Piece>();
                    if (lpc != null)
                    {
                        Piece.Requirement req = new Piece.Requirement();
                        req.m_resItem = foodDrop;
                        req.m_amount = 1;
                        req.m_recover = true;
                        lpc.m_resources = new Piece.Requirement[] { req };
                    }
                }
            }
        }

        public static void ApplyVanillaEitrBoostsFromPrefabManager()
        {
            if (PrefabManager.Instance == null) return;
            PatchVanillaFoodPrefab(PrefabManager.Instance.GetPrefab(VanillaNeckTailGrilled), NeckTailGrilledEitr != null ? NeckTailGrilledEitr.Value : 25f);
            PatchVanillaFoodPrefab(PrefabManager.Instance.GetPrefab(VanillaMushroomYellow), MushroomYellowEitr != null ? MushroomYellowEitr.Value : 25f);
        }

        public static void EnsureFoodsReadyInObjectDB(ObjectDB odb)
        {
            if (odb == null || odb.m_items == null) return;

            float neckEitr = (NeckTailGrilledEitr != null) ? NeckTailGrilledEitr.Value : 25f;
            float yellowEitr = (MushroomYellowEitr != null) ? MushroomYellowEitr.Value : 25f;

            bool patchedNeck = PatchVanillaFoodPrefab(odb.GetItemPrefab(VanillaNeckTailGrilled), neckEitr);
            bool patchedYellow = PatchVanillaFoodPrefab(odb.GetItemPrefab(VanillaMushroomYellow), yellowEitr);

            EnsureServingTrayPiecesLinked(odb);

            if ((patchedNeck || patchedYellow) && !s_loggedVanillaPatchOnce && NewDualAxesPlugin.ModLogger != null)
            {
                s_loggedVanillaPatchOnce = true;
                NewDualAxesPlugin.ModLogger.LogInfo(string.Format(
                    "[EitrFoods] Applied vanilla Eitr boosts in ObjectDB: NeckTailGrilled Eitr={0}, MushroomYellow Eitr={1}",
                    neckEitr, yellowEitr));
            }
        }

        private static bool PatchVanillaFoodPrefab(GameObject prefab, float eitrAmount)
        {
            if (prefab == null) return false;
            ItemDrop id = prefab.GetComponent<ItemDrop>();
            if (id == null || id.m_itemData == null || id.m_itemData.m_shared == null) return false;

            id.m_itemData.m_shared.m_foodEitr = eitrAmount;
            return true;
        }

        public static void SyncInventoryVanillaFoodEitr(Inventory inv)
        {
            if (inv == null) return;
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            if (items == null) return;

            float neckEitr = (NeckTailGrilledEitr != null) ? NeckTailGrilledEitr.Value : 25f;
            float yellowEitr = (MushroomYellowEitr != null) ? MushroomYellowEitr.Value : 25f;

            for (int i = 0; i < items.Count; i++)
            {
                ItemDrop.ItemData it = items[i];
                if (it == null || it.m_shared == null) continue;
                string sharedName = it.m_shared.m_name;
                if (sharedName == "$item_necktailgrilled")
                {
                    it.m_shared.m_foodEitr = neckEitr;
                }
                else if (sharedName == "$item_mushroomyellow")
                {
                    it.m_shared.m_foodEitr = yellowEitr;
                }
            }
        }
    }
}
