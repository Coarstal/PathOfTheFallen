using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace WeaponAttributesMod
{
    public static class SoulExtractionSystem
    {
        public const string EmptyCrystalPrefab = "SoulCrystalEmpty";
        public const string FilledCrystalPrefab = "SoulCrystalFilled";
        public const string SoulCauldronPrefab = "piece_soul_cauldron";

        public const string DATA_BOSS_KEY = "SoulCrystal_Boss";
        public const string DATA_LEVEL_KEY = "SoulCrystal_Level";

        public const string CAULDRON_RECIPE_PREFIX = "Recipe_SoulCauldron_";

        public static ConfigEntry<int> SoulCrystalTraderPrice;
        public static ConfigEntry<float> SoulCauldronMobReturnPercent;
        public static ConfigEntry<float> SoulCauldronBossReturnChance;

        internal static Sprite s_emptyCrystalIcon;
        internal static Sprite s_filledCrystalIcon;
        internal static Sprite s_soulCauldronIcon;
        internal static GameObject s_soulCauldronPrefabObj;

        private static bool s_itemsRegistered;
        private static bool s_cauldronRegistered;

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            SoulCrystalTraderPrice = config.Bind(
                "02. Weapon Attributes",
                "SoulCrystalTraderPrice",
                999,
                new ConfigDescription("Price in Coins for the Empty Soul Crystal sold exclusively by Hildir (default 999).", null, adminAttr));
            SoulCauldronMobReturnPercent = config.Bind(
                "02. Weapon Attributes",
                "SoulCauldronMobReturnPercent",
                50f,
                new ConfigDescription("Percentage (0-100%) of total invested mob trophies returned when breaking a filled Soul Crystal in the Soul Extraction Cauldron (default 50% => 1 / 3 / 8 / 16 / 26 across Levels I..V).", null, adminAttr));
            SoulCauldronBossReturnChance = config.Bind(
                "02. Weapon Attributes",
                "SoulCauldronBossReturnChance",
                25f,
                new ConfigDescription("Percentage chance (0-100%) to recover the Boss Trophy when breaking a filled Soul Crystal in the Soul Extraction Cauldron (default 25%).", null, adminAttr));
        }

        public static void Init()
        {
            PrefabManager.OnVanillaPrefabsAvailable += RegisterSoulCrystalItems;
            PrefabManager.OnPrefabsRegistered += RegisterSoulCauldronPiece;
        }

        public static void Shutdown()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterSoulCrystalItems;
            PrefabManager.OnPrefabsRegistered -= RegisterSoulCauldronPiece;
            SoulExtractionUI.DestroyUI();
            s_itemsRegistered = false;
            s_cauldronRegistered = false;
            s_soulCauldronPrefabObj = null;
        }

        public static string ToRoman(int level)
        {
            switch (Mathf.Clamp(level, 1, 5))
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                default: return "I";
            }
        }

        public static int GetTotalInvestedMobTrophies(int level)
        {
            int clamped = Mathf.Clamp(level, 1, 5);
            int sum = 0;
            for (int lv = 1; lv <= clamped; lv++)
            {
                sum += WeaponAttributesPlugin.GetMobTrophyCountForLevel(lv);
            }
            return sum; // 1, 6, 16, 31, 51
        }

        public static int GetReturnedMobTrophyCount(int level)
        {
            float pct = (SoulCauldronMobReturnPercent != null ? Mathf.Clamp(SoulCauldronMobReturnPercent.Value, 0f, 100f) : 50f) * 0.01f;
            if (pct <= 0f) return 0;
            float raw = (float)GetTotalInvestedMobTrophies(level) * pct;
            return Mathf.Max(1, Mathf.FloorToInt(raw + 0.5f)); // 50% => 1, 3, 8, 16, 26
        }

        public static float GetBossReturnChancePercent()
        {
            return SoulCauldronBossReturnChance != null ? Mathf.Clamp(SoulCauldronBossReturnChance.Value, 0f, 100f) : 25f;
        }

        public static bool IsEmptySoulCrystal(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_dropPrefab != null && string.Equals(item.m_dropPrefab.name, EmptyCrystalPrefab, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return string.Equals(item.m_shared.m_name, "$wa_item_soul_crystal_empty", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFilledSoulCrystal(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_dropPrefab != null && string.Equals(item.m_dropPrefab.name, FilledCrystalPrefab, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return string.Equals(item.m_shared.m_name, "$wa_item_soul_crystal_filled", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFilledCrystalBossKey(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return "eikthyr";
            string val;
            if (item.m_customData.TryGetValue(DATA_BOSS_KEY, out val) && !string.IsNullOrEmpty(val))
            {
                return val.Trim().ToLower();
            }
            return "eikthyr";
        }

        public static int GetFilledCrystalLevel(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return 1;
            string val;
            if (item.m_customData.TryGetValue(DATA_LEVEL_KEY, out val))
            {
                int lv;
                if (int.TryParse(val, out lv))
                {
                    return Mathf.Clamp(lv, 1, 5);
                }
            }
            return 1;
        }

        public static string InferBossKeyFromWeapon(ItemDrop.ItemData weapon)
        {
            string bossKey = WeaponAttributesPlugin.GetEnchantBossKey(weapon);
            if (!string.IsNullOrEmpty(bossKey)) return bossKey.Trim().ToLower();

            string enchantStr = WeaponAttributesPlugin.GetAttributeEnchantString(weapon);
            if (!string.IsNullOrEmpty(enchantStr))
            {
                string low = enchantStr.ToLower();
                if (low.Contains("fire") && low.Contains("spirit")) return "fader";
                if (low.Contains("poison") && low.Contains("lightning")) return "queen";
                if (low.Contains("lightning")) return "eikthyr";
                if (low.Contains("physical")) return "elder";
                if (low.Contains("poison")) return "bonemass";
                if (low.Contains("frost")) return "moder";
                if (low.Contains("fire")) return "yagluth";
            }
            return "eikthyr";
        }

        public static string GetSoulTitleTemplate(string bossKey)
        {
            string k = bossKey != null ? bossKey.Trim().ToLower() : "eikthyr";
            switch (k)
            {
                case "eikthyr": return WeaponAttributesLocalization.Loc("$wa_soul_title_eikthyr");
                case "elder": return WeaponAttributesLocalization.Loc("$wa_soul_title_elder");
                case "bonemass": return WeaponAttributesLocalization.Loc("$wa_soul_title_bonemass");
                case "moder": return WeaponAttributesLocalization.Loc("$wa_soul_title_moder");
                case "yagluth": return WeaponAttributesLocalization.Loc("$wa_soul_title_yagluth");
                case "queen": return WeaponAttributesLocalization.Loc("$wa_soul_title_queen");
                case "fader": return WeaponAttributesLocalization.Loc("$wa_soul_title_fader");
                default: return WeaponAttributesLocalization.Loc("$wa_soul_title_eikthyr");
            }
        }

        public static string GetReadableBossColorHex(string bossKey)
        {
            string hex = WeaponAttributesPlugin.GetBossEnchantColor(bossKey, null);
            if (string.IsNullOrEmpty(hex)) hex = "#FFD700";
            if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase)) return "#B0BEB0";
            if (string.Equals(bossKey, "fader", StringComparison.OrdinalIgnoreCase)) return "#E2843C";
            return hex;
        }

        public static string GetFilledCrystalDisplayName(string bossKey, int level, bool withRichColor)
        {
            string roman = ToRoman(level);
            string rawTitle = string.Format(GetSoulTitleTemplate(bossKey), roman);
            if (!withRichColor) return rawTitle;
            string hex = GetReadableBossColorHex(bossKey);
            return string.Format("<color={0}>{1}</color>", hex, rawTitle);
        }

        public static string GetLocalizedItemPrefabName(string prefabName, string fallback)
        {
            if (!string.IsNullOrEmpty(prefabName) && ObjectDB.instance != null)
            {
                GameObject obj = ObjectDB.instance.GetItemPrefab(prefabName);
                ItemDrop id = obj != null ? obj.GetComponent<ItemDrop>() : null;
                if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null && !string.IsNullOrEmpty(id.m_itemData.m_shared.m_name))
                {
                    return Localization.instance.Localize(id.m_itemData.m_shared.m_name);
                }
            }
            return fallback;
        }

        public static string BuildFilledCrystalTooltipAppendix(ItemDrop.ItemData item)
        {
            string bossKey = GetFilledCrystalBossKey(item);
            int level = GetFilledCrystalLevel(item);
            int mobReturn = GetReturnedMobTrophyCount(level);
            float bossChance = GetBossReturnChancePercent();

            string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);
            string bossPrefab;
            WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bossKey, out bossPrefab);

            string mobName = GetLocalizedItemPrefabName(mobPrefab, mobPrefab ?? "");
            string bossName = GetLocalizedItemPrefabName(bossPrefab, Patch_InventoryGui_UpdateRecipeList.GetShortBossLabel(bossKey));
            string coloredTitle = GetFilledCrystalDisplayName(bossKey, level, true);

            StringBuilder sb = new StringBuilder();
            sb.Append("\n\n");
            sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_crystal_tooltip_sealed"), coloredTitle));
            sb.Append("\n");
            sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_crystal_tooltip_mob_return"), mobReturn, mobName, Mathf.RoundToInt(SoulCauldronMobReturnPercent != null ? SoulCauldronMobReturnPercent.Value : 50f)));
            sb.Append("\n");
            sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_crystal_tooltip_boss_chance"), bossName, Mathf.RoundToInt(bossChance)));
            return sb.ToString();
        }

        public static bool IsSoulCauldronStation(CraftingStation station)
        {
            if (station == null) return false;
            string sName = station.name != null ? station.name.ToLowerInvariant() : "";
            string rawTok = station.m_name ?? "";
            return sName.Contains("piece_soul_cauldron") || rawTok == "$wa_piece_soul_cauldron";
        }

        private static void RegisterSoulCrystalItems()
        {
            if (!WeaponAttributesPlugin.IsActive) return;
            try
            {
                if (s_itemsRegistered && PrefabManager.Instance.GetPrefab(EmptyCrystalPrefab) != null)
                {
                    return;
                }

                string[] donors = new string[] { "Ruby", "AmberPearl", "Crystal", "Amber" };
                string donorName = null;
                for (int i = 0; i < donors.Length; i++)
                {
                    if (PrefabManager.Instance.GetPrefab(donors[i]) != null)
                    {
                        donorName = donors[i];
                        break;
                    }
                }
                if (string.IsNullOrEmpty(donorName)) return;

                if (s_emptyCrystalIcon == null) s_emptyCrystalIcon = CustomEnchantingTable.LoadEmbeddedSprite("SoulCrystalEmptyIcon.png");
                if (s_filledCrystalIcon == null) s_filledCrystalIcon = CustomEnchantingTable.LoadEmbeddedSprite("SoulCrystalFilledIcon.png");

                Shader stdShader = Shader.Find("Standard");

                // 1) Empty Soul Crystal (Consumable -> Right-click opens weapon selection UI; sold at Haldor & Hildir for 999 Coins)
                if (PrefabManager.Instance.GetPrefab(EmptyCrystalPrefab) == null)
                {
                    GameObject emptyObj = PrefabManager.Instance.CreateClonedPrefab(EmptyCrystalPrefab, donorName);
                    if (emptyObj != null)
                    {
                        ReplaceWorldDropMesh(emptyObj, "SoulCrystalEmptyMesh.bin.gz", stdShader, false);
                        ItemDrop drop = emptyObj.GetComponent<ItemDrop>();
                        if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                        {
                            ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared;
                            sh.m_name = "$wa_item_soul_crystal_empty";
                            sh.m_description = "$wa_item_soul_crystal_empty_desc";
                            if (s_emptyCrystalIcon != null) sh.m_icons = new Sprite[] { s_emptyCrystalIcon };
                            sh.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                            sh.m_maxStackSize = 10;
                            sh.m_weight = 0.5f;
                            sh.m_value = 0;
                            sh.m_food = 0f;
                            sh.m_foodStamina = 0f;
                            sh.m_foodEitr = 0f;
                            sh.m_foodBurnTime = 0f;
                            sh.m_foodRegen = 0f;
                            sh.m_consumeStatusEffect = null;
                            sh.m_teleportable = true;
                        }

                        ItemConfig emptyCfg = new ItemConfig();
                        emptyCfg.Name = "$wa_item_soul_crystal_empty";
                        emptyCfg.Description = "$wa_item_soul_crystal_empty_desc";
                        if (s_emptyCrystalIcon != null) emptyCfg.Icons = new Sprite[] { s_emptyCrystalIcon };
                        emptyCfg.Enabled = false; // Sold exclusively by Haldor & Hildir
                        ItemManager.Instance.AddItem(new CustomItem(emptyObj, false, emptyCfg));
                    }
                }

                // 2) Filled Soul Crystal (Material, stack size 1 so distinct Boss+Level crystals never overwrite customData!)
                if (PrefabManager.Instance.GetPrefab(FilledCrystalPrefab) == null)
                {
                    GameObject filledObj = PrefabManager.Instance.CreateClonedPrefab(FilledCrystalPrefab, donorName);
                    if (filledObj != null)
                    {
                        ReplaceWorldDropMesh(filledObj, "SoulCrystalFilledMesh.bin.gz", stdShader, true);
                        ItemDrop drop = filledObj.GetComponent<ItemDrop>();
                        if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                        {
                            ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared;
                            sh.m_name = "$wa_item_soul_crystal_filled";
                            sh.m_description = "$wa_item_soul_crystal_filled_desc";
                            if (s_filledCrystalIcon != null) sh.m_icons = new Sprite[] { s_filledCrystalIcon };
                            sh.m_itemType = ItemDrop.ItemData.ItemType.Material;
                            sh.m_maxStackSize = 1;
                            sh.m_weight = 0.5f;
                            sh.m_value = 0;
                            sh.m_teleportable = true;
                        }

                        ItemConfig filledCfg = new ItemConfig();
                        filledCfg.Name = "$wa_item_soul_crystal_filled";
                        filledCfg.Description = "$wa_item_soul_crystal_filled_desc";
                        if (s_filledCrystalIcon != null) filledCfg.Icons = new Sprite[] { s_filledCrystalIcon };
                        filledCfg.Enabled = false;
                        ItemManager.Instance.AddItem(new CustomItem(filledObj, false, filledCfg));
                    }
                }

                s_itemsRegistered = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[WeaponAttributes] Failed registering Soul Crystal items: " + ex);
            }
        }

        private static void ReplaceWorldDropMesh(GameObject itemObj, string meshResName, Shader baseShader, bool addSoulLight)
        {
            if (itemObj == null) return;
            Shader sh = baseShader;
            foreach (Renderer rnd in itemObj.GetComponentsInChildren<Renderer>(true))
            {
                if (rnd == null) continue;
                if (sh == null && rnd.sharedMaterial != null) sh = rnd.sharedMaterial.shader;
                rnd.enabled = false;
                MeshFilter mf = rnd.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = null;
            }

            GameObject modelRoot = new GameObject("SoulCrystalModel");
            modelRoot.layer = itemObj.layer;
            modelRoot.transform.SetParent(itemObj.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            CustomEnchantingTable.LoadEmbeddedMeshIntoRoot(modelRoot, meshResName, sh, itemObj.layer);

            if (addSoulLight)
            {
                GameObject lGo = new GameObject("SoulCrystalGlow");
                lGo.transform.SetParent(modelRoot.transform, false);
                lGo.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                Light pt = lGo.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.color = new Color(0.72f, 0.24f, 1.0f, 1f);
                pt.range = 2.4f;
                pt.intensity = 1.15f;
            }
        }

        public static void RegisterSoulCauldronPiece()
        {
            if (!WeaponAttributesPlugin.IsActive) return;
            if (s_cauldronRegistered && s_soulCauldronPrefabObj != null) return;

            try
            {
                GameObject cauldron = PrefabManager.Instance.CreateClonedPrefab(SoulCauldronPrefab, "piece_cauldron");
                if (cauldron == null)
                {
                    cauldron = PrefabManager.Instance.CreateClonedPrefab(SoulCauldronPrefab, "piece_artisanstation");
                }
                if (cauldron == null) return;

                Shader stdShader = Shader.Find("Standard");
                if (stdShader == null)
                {
                    Renderer anyR = cauldron.GetComponentInChildren<Renderer>(true);
                    stdShader = (anyR != null && anyR.sharedMaterial != null) ? anyR.sharedMaterial.shader : null;
                }

                // Force solid Valheim "piece" layer (never inherit piece_cauldron's "piece_nonsolid" layer, which lets players walk through it!)
                int pieceLayer = LayerMask.NameToLayer("piece");
                if (pieceLayer < 0)
                {
                    GameObject wb = PrefabManager.Instance.GetPrefab("piece_workbench");
                    pieceLayer = wb != null ? wb.layer : 10;
                }
                cauldron.layer = pieceLayer;

                foreach (Collider col in cauldron.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;
                    if (col.isTrigger || col.GetComponent<EffectArea>() != null) continue;
                    col.enabled = false;
                }

                Transform ghostOnly = cauldron.transform.Find("_GhostOnly");
                if (ghostOnly != null)
                {
                    UnityEngine.Object.DestroyImmediate(ghostOnly.gameObject);
                }

                // Hide original cauldron meshes & vanilla fire requirement objects so it doesn't demand a campfire underneath
                foreach (Renderer rnd in cauldron.GetComponentsInChildren<Renderer>(true))
                {
                    if (rnd != null && rnd.gameObject != cauldron)
                    {
                        rnd.enabled = false;
                        MeshFilter mf = rnd.GetComponent<MeshFilter>();
                        if (mf != null) mf.sharedMesh = null;
                    }
                }
                foreach (ParticleSystem ps in cauldron.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps != null && ps.gameObject != cauldron)
                    {
                        ps.gameObject.SetActive(false);
                    }
                }

                if (s_soulCauldronIcon == null)
                {
                    s_soulCauldronIcon = CustomEnchantingTable.LoadEmbeddedSprite("SoulCauldronIcon.png");
                }

                CraftingStation station = cauldron.GetComponent<CraftingStation>();
                if (station != null)
                {
                    station.m_name = "$wa_piece_soul_cauldron";
                    station.m_discoverRange = 4f;
                    station.m_rangeBuild = 10f;
                    station.m_useDistance = 2.6f;
                    station.m_craftRequireRoof = false;
                    station.m_craftRequireFire = false; // Self-fueled by Surtling Cores in its stone hearth!
                    if (s_soulCauldronIcon != null)
                    {
                        station.m_icon = s_soulCauldronIcon;
                    }
                }

                Piece basePiece = cauldron.GetComponent<Piece>();
                if (basePiece != null)
                {
                    basePiece.m_randomInitBuildRotation = false;
                    if (s_soulCauldronIcon != null)
                    {
                        basePiece.m_icon = s_soulCauldronIcon;
                    }
                }

                // Build custom Blender 3D Soul Cauldron + Animated Self-Stirring Ladle!
                GameObject modelRoot = new GameObject("SoulExtractionCauldronModel");
                modelRoot.layer = pieceLayer;
                modelRoot.transform.SetParent(cauldron.transform, false);
                modelRoot.transform.localPosition = Vector3.zero;
                modelRoot.transform.localRotation = Quaternion.identity;
                modelRoot.transform.localScale = Vector3.one;

                BoxCollider potCol = modelRoot.AddComponent<BoxCollider>();
                potCol.center = new Vector3(0f, 0.54f, 0f);
                potCol.size = new Vector3(1.18f, 1.08f, 1.18f);
                potCol.isTrigger = false;
                potCol.enabled = true;

                BoxCollider rootCol = cauldron.GetComponent<BoxCollider>();
                if (rootCol == null) rootCol = cauldron.AddComponent<BoxCollider>();
                rootCol.center = new Vector3(0f, 0.54f, 0f);
                rootCol.size = new Vector3(1.18f, 1.08f, 1.18f);
                rootCol.isTrigger = false;
                rootCol.enabled = true;

                WearNTear wnt = cauldron.GetComponent<WearNTear>();
                if (wnt != null)
                {
                    wnt.m_new = modelRoot;
                    wnt.m_worn = modelRoot;
                    wnt.m_broken = modelRoot;
                }

                // Purple-cyan brew glow light + warm hearth ember glow light (toned down for crisp non-blooming visuals)
                GameObject brewLightObj = new GameObject("SoulBrewGlowLight");
                brewLightObj.transform.SetParent(modelRoot.transform, false);
                brewLightObj.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                Light brewLight = brewLightObj.AddComponent<Light>();
                brewLight.type = LightType.Point;
                brewLight.color = new Color(0.58f, 0.22f, 0.96f, 1f);
                brewLight.range = 3.5f;
                brewLight.intensity = 0.80f;

                GameObject emberLightObj = new GameObject("HearthEmberLight");
                emberLightObj.transform.SetParent(modelRoot.transform, false);
                emberLightObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                Light emberLight = emberLightObj.AddComponent<Light>();
                emberLight.type = LightType.Point;
                emberLight.color = new Color(1.0f, 0.36f, 0.12f, 1f);
                emberLight.range = 2.0f;
                emberLight.intensity = 0.50f;

                CustomEnchantingTable.LoadEmbeddedMeshIntoRoot(modelRoot, "SoulCauldronMesh.bin.gz", stdShader, pieceLayer);

                PieceConfig cfg = new PieceConfig();
                cfg.PieceTable = "Hammer";
                cfg.Category = "Crafting";
                cfg.CraftingStation = "piece_workbench";
                cfg.Name = "$wa_piece_soul_cauldron";
                cfg.Description = "$wa_piece_soul_cauldron_desc";
                if (s_soulCauldronIcon != null)
                {
                    cfg.Icon = s_soulCauldronIcon;
                }
                // Black Forest tier recipe: Tin x10, Copper x5, SurtlingCore x3, BoneFragments x10
                cfg.AddRequirement(new RequirementConfig("Tin", 10, 0, true));
                cfg.AddRequirement(new RequirementConfig("Copper", 5, 0, true));
                cfg.AddRequirement(new RequirementConfig("SurtlingCore", 3, 0, true));
                cfg.AddRequirement(new RequirementConfig("BoneFragments", 10, 0, true));

                PieceManager.Instance.AddPiece(new CustomPiece(cauldron, false, cfg));
                s_soulCauldronPrefabObj = cauldron;
                s_cauldronRegistered = true;
                Debug.Log("[WeaponAttributes] Registered piece_soul_cauldron with animated stirring ladle!");
            }
            catch (Exception ex)
            {
                Debug.LogError("[WeaponAttributes] Failed registering piece_soul_cauldron: " + ex);
            }
        }

        public static void GiveFilledSoulCrystal(Player player, string bossKey, int level)
        {
            if (player == null || ObjectDB.instance == null) return;
            GameObject prefabObj = ObjectDB.instance.GetItemPrefab(FilledCrystalPrefab);
            if (prefabObj == null) prefabObj = PrefabManager.Instance.GetPrefab(FilledCrystalPrefab);
            if (prefabObj == null) return;

            ItemDrop id = prefabObj.GetComponent<ItemDrop>();
            if (id == null || id.m_itemData == null) return;

            ItemDrop.ItemData clone = id.m_itemData.Clone();
            clone.m_dropPrefab = prefabObj;
            clone.m_stack = 1;
            if (clone.m_customData == null)
            {
                clone.m_customData = new Dictionary<string, string>();
            }
            clone.m_customData[DATA_BOSS_KEY] = string.IsNullOrEmpty(bossKey) ? "eikthyr" : bossKey.Trim().ToLower();
            clone.m_customData[DATA_LEVEL_KEY] = Mathf.Clamp(level, 1, 5).ToString();

            Inventory inv = player.GetInventory();
            if (inv == null || !inv.AddItem(clone))
            {
                Vector3 dropPos = player.transform.position + player.transform.forward * 1.05f + Vector3.up * 0.85f;
                ItemDrop.DropItem(clone, 1, dropPos, Quaternion.identity);
            }
        }

        public static void GiveOrDropItems(Player player, string prefabName, int totalAmount)
        {
            if (player == null || string.IsNullOrEmpty(prefabName) || totalAmount <= 0 || ObjectDB.instance == null) return;
            GameObject prefabObj = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefabObj == null) return;

            ItemDrop id = prefabObj.GetComponent<ItemDrop>();
            if (id == null || id.m_itemData == null) return;

            Inventory inv = player.GetInventory();
            int overflow = 0;
            for (int i = 0; i < totalAmount; i++)
            {
                if (inv != null && inv.CanAddItem(prefabObj, 1) && inv.AddItem(prefabObj, 1))
                {
                    continue;
                }
                overflow++;
            }

            if (overflow > 0)
            {
                Vector3 dropPos = player.transform.position + player.transform.forward * 1.15f + Vector3.up * 0.85f;
                int maxStack = (id.m_itemData.m_shared != null && id.m_itemData.m_shared.m_maxStackSize > 1)
                    ? id.m_itemData.m_shared.m_maxStackSize
                    : 20;
                while (overflow > 0)
                {
                    int batch = Mathf.Min(overflow, maxStack);
                    ItemDrop.ItemData dropClone = id.m_itemData.Clone();
                    dropClone.m_dropPrefab = prefabObj;
                    dropClone.m_stack = batch;
                    ItemDrop.DropItem(dropClone, batch, dropPos, Quaternion.identity);
                    overflow -= batch;
                }
            }
        }

        private static EffectList s_cachedUpgraderBreakEffects;

        /// <summary>
        /// Plays the exact weapon-break VFX & SFX from Valheim's Forge of Potential (`CraftingStation` where `m_upgrader == true`,
        /// `m_craftItemDoneFailEffects`), falling back cleanly if unavailable.
        /// </summary>
        public static void PlayForgeOfPotentialBreakEffect(Player player, ItemDrop.ItemData brokenWeapon)
        {
            if (player == null) return;

            if (s_cachedUpgraderBreakEffects == null || !s_cachedUpgraderBreakEffects.HasEffects())
            {
                if (ZNetScene.instance != null && ZNetScene.instance.m_prefabs != null)
                {
                    List<GameObject> prefabs = ZNetScene.instance.m_prefabs;
                    EffectList anyFailEffects = null;
                    for (int i = 0; i < prefabs.Count; i++)
                    {
                        GameObject p = prefabs[i];
                        if (p == null) continue;
                        CraftingStation cs = p.GetComponent<CraftingStation>();
                        if (cs == null || cs.m_craftItemDoneFailEffects == null || !cs.m_craftItemDoneFailEffects.HasEffects()) continue;

                        if (cs.m_upgrader)
                        {
                            s_cachedUpgraderBreakEffects = cs.m_craftItemDoneFailEffects;
                            break;
                        }
                        if (anyFailEffects == null)
                        {
                            anyFailEffects = cs.m_craftItemDoneFailEffects;
                        }
                    }
                    if (s_cachedUpgraderBreakEffects == null && anyFailEffects != null)
                    {
                        s_cachedUpgraderBreakEffects = anyFailEffects;
                    }
                }
            }

            Vector3 fxPos = player.GetCenterPoint();
            if (s_cachedUpgraderBreakEffects != null && s_cachedUpgraderBreakEffects.HasEffects())
            {
                s_cachedUpgraderBreakEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1);
            }
            else if (brokenWeapon != null && brokenWeapon.m_shared != null &&
                     brokenWeapon.m_shared.m_destroyEffect != null && brokenWeapon.m_shared.m_destroyEffect.HasEffects())
            {
                brokenWeapon.m_shared.m_destroyEffect.Create(fxPos, Quaternion.identity, null, 1f, -1);
            }
        }

        /// <summary>
        /// Inflicts a 3-second elemental/physical backlash debuff matching the extracted Boss Soul's damage type!
        /// </summary>
        public static void ApplySoulExtractionDebuff(Player player, string bossKey, float durationSeconds)
        {
            if (player == null || player.IsDead()) return;
            SEMan seman = player.GetSEMan();
            if (seman == null) return;

            float dur = Mathf.Max(0.5f, durationSeconds);
            string k = !string.IsNullOrEmpty(bossKey) ? bossKey.Trim().ToLowerInvariant() : "eikthyr";
            float safeMaxTotalDot = Mathf.Min(4.5f, Mathf.Max(0f, player.GetHealth() - 2f));

            switch (k)
            {
                case "eikthyr":
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectLightning, dur, safeMaxTotalDot, false);
                    break;

                case "elder":
                    // Physical damage -> 3s Physical Bleeding ("Рваная рана"), falling back to Tared root slow if Injuries disabled
                    bool appliedBleed = CoarstalsEnemyOverhaul.EnemyInjuryManager.ApplyInjuryWithDuration(
                        player,
                        CoarstalsEnemyOverhaul.InjuryType.Bleeding,
                        dur);
                    if (!appliedBleed)
                    {
                        ApplyTimedStatusHash(seman, SEMan.s_statusEffectTared, dur, 0f, false);
                    }
                    break;

                case "bonemass":
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectPoison, dur, safeMaxTotalDot, false);
                    break;

                case "moder":
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectFrost, dur, 0f, false);
                    break;

                case "yagluth":
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectBurning, dur, safeMaxTotalDot, false);
                    break;

                case "queen":
                    // Poison + Lightning
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectPoison, dur, safeMaxTotalDot, false);
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectLightning, dur, 0f, false);
                    break;

                case "fader":
                    // Fire + Spirit
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectBurning, dur, safeMaxTotalDot * 0.5f, false);
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectSpirit, dur, safeMaxTotalDot * 0.5f, true);
                    break;

                default:
                    ApplyTimedStatusHash(seman, SEMan.s_statusEffectLightning, dur, 0f, false);
                    break;
            }
        }

        private static void ApplyTimedStatusHash(SEMan seman, int hash, float duration, float totalDotDamage, bool isSpiritBurn)
        {
            if (seman == null || hash == 0) return;
            StatusEffect se = seman.GetStatusEffect(hash);
            if (se == null)
            {
                se = seman.AddStatusEffect(hash, true, 0, 0f);
            }
            if (se == null) return;

            SE_Burning burn = se as SE_Burning;
            if (burn != null)
            {
                // Set m_ttl to duration BEFORE AddFireDamage/AddSpiritDamage so per-tick calculation divides over exact `duration`!
                se.m_ttl = duration;
                float safeDmg = Mathf.Max(0f, totalDotDamage);
                if (safeDmg > 0.25f)
                {
                    if (isSpiritBurn)
                    {
                        burn.AddSpiritDamage(safeDmg);
                    }
                    else
                    {
                        burn.AddFireDamage(safeDmg);
                    }
                }
                se.m_ttl = duration;
                se.ResetTime();
                return;
            }

            SE_Poison poison = se as SE_Poison;
            if (poison != null)
            {
                float safePoison = Mathf.Max(0f, totalDotDamage);
                if (safePoison > 0.25f)
                {
                    poison.AddDamage(safePoison);
                }
                se.m_ttl = duration;
                se.ResetTime();
                return;
            }

            se.m_ttl = duration;
            se.ResetTime();
        }
    }

    /// <summary>
    /// Smoothly animates the enchanted ladle inside the Soul Extraction Cauldron in a continuous conical stirring orbit!
    /// Speeds up briefly when a Soul Crystal is shattered inside the cauldron.
    /// </summary>
    public class SoulCauldronLadleAnimator : MonoBehaviour
    {
        private static readonly List<SoulCauldronLadleAnimator> s_instances = new List<SoulCauldronLadleAnimator>();

        public Vector3 m_pivotPos = new Vector3(0f, 0.78f, 0f);
        public float m_orbitRadius = 0.145f;
        public float m_tiltDegrees = 16.5f;
        public float m_baseAngularSpeed = 1.85f;

        private float m_angle;
        private float m_burstTimer;

        private void OnEnable()
        {
            if (!s_instances.Contains(this)) s_instances.Add(this);
            m_angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnDisable()
        {
            s_instances.Remove(this);
        }

        public static void BoostNearestCauldronStir(Vector3 worldPos)
        {
            float bestDistSq = 36f;
            SoulCauldronLadleAnimator best = null;
            for (int i = 0; i < s_instances.Count; i++)
            {
                SoulCauldronLadleAnimator inst = s_instances[i];
                if (inst == null) continue;
                float dSq = (inst.transform.position - worldPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = inst;
                }
            }
            if (best != null)
            {
                best.m_burstTimer = 2.4f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float speedMult = 1.0f;
            if (m_burstTimer > 0f)
            {
                m_burstTimer = Mathf.Max(0f, m_burstTimer - dt);
                speedMult = 1.0f + 2.8f * Mathf.Clamp01(m_burstTimer / 1.2f);
            }

            m_angle += m_baseAngularSpeed * speedMult * dt;
            if (m_angle > Mathf.PI * 200f) m_angle -= Mathf.PI * 200f;

            float ca = Mathf.Cos(m_angle);
            float sa = Mathf.Sin(m_angle);
            float bobY = Mathf.Sin(m_angle * 2.0f) * 0.014f;

            // Orbit around the cauldron center while leaning the upper handle smoothly along the stir cone!
            transform.localPosition = new Vector3(
                m_pivotPos.x + ca * m_orbitRadius,
                m_pivotPos.y + bobY,
                m_pivotPos.z + sa * m_orbitRadius);

            transform.localRotation = Quaternion.Euler(
                sa * m_tiltDegrees,
                -m_angle * Mathf.Rad2Deg * 0.25f,
                -ca * m_tiltDegrees);
        }
    }

    public class SoulCauldronBrewAnimator : MonoBehaviour
    {
        public int m_mode; // 0 = swirling brew surface, 1 = rising/pulsing soul bubbles
        public Vector3 m_basePos;
        private Material m_mat;
        private float m_phase;

        private void Start()
        {
            m_phase = UnityEngine.Random.Range(0f, 6.28f);
            Renderer r = GetComponent<Renderer>();
            if (r != null) m_mat = r.material;
        }

        private void Update()
        {
            float t = Time.time + m_phase;
            if (m_mode == 0)
            {
                transform.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
                float waveY = Mathf.Sin(t * 2.2f) * 0.008f;
                transform.localPosition = new Vector3(m_basePos.x, m_basePos.y + waveY, m_basePos.z);
                if (m_mat != null && m_mat.HasProperty("_MainTex"))
                {
                    m_mat.mainTextureOffset = new Vector2(t * 0.06f, t * 0.04f);
                }
            }
            else
            {
                transform.Rotate(0f, -22f * Time.deltaTime, 0f, Space.Self);
                float bob = Mathf.Sin(t * 3.4f) * 0.015f;
                float pulse = 1.0f + Mathf.Sin(t * 4.2f) * 0.06f;
                transform.localPosition = new Vector3(m_basePos.x, m_basePos.y + bob, m_basePos.z);
                transform.localScale = new Vector3(pulse, 1.0f, pulse);
            }
        }
    }

    // ====================================================================================================
    // RIGHT-CLICK SOUL EXTRACTION UI (Select Enchanted Weapon -> Seal Soul into Crystal & Break Weapon to 0)
    // ====================================================================================================
    public static class SoulExtractionUI
    {
        private class WeaponRowEntry
        {
            public ItemDrop.ItemData Weapon;
            public string BossKey;
            public int EnchantLevel;
            public Image BorderImg;
            public Image BgImg;
        }

        private static GameObject s_canvasRootObj;
        private static GameObject s_overlayObj;
        private static Text s_titleText;
        private static Text s_subtitleText;
        private static Text s_warningText;
        private static RectTransform s_listContentRt;
        private static Button s_extractBtn;
        private static Text s_extractBtnText;
        private static Button s_cancelBtn;
        private static Text s_cancelBtnText;

        private static readonly List<WeaponRowEntry> s_rows = new List<WeaponRowEntry>();
        private static int s_selectedIndex = -1;
        private static bool s_isOpen;

        public static bool IsOpen
        {
            get { return s_isOpen && s_overlayObj != null && s_overlayObj.activeSelf; }
        }

        public static void OpenForPlayer(Player player)
        {
            if (player == null || GUIManager.IsHeadless() || GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                return;
            }

            Inventory inv = player.GetInventory();
            if (inv == null) return;

            List<ItemDrop.ItemData> enchantedWeapons = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData it = all[i];
                if (it != null && WeaponAttributesPlugin.IsWeapon(it) && WeaponAttributesPlugin.GetEnchantLevel(it) > 0)
                {
                    enchantedWeapons.Add(it);
                }
            }

            if (enchantedWeapons.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_no_enchanted_weapons"));
                return;
            }

            EnsureBuilt();
            if (s_overlayObj == null) return;

            RebuildWeaponRows(enchantedWeapons);
            SelectIndex(0);

            if (InventoryGui.IsVisible() && InventoryGui.instance != null)
            {
                InventoryGui.instance.Hide();
            }

            s_canvasRootObj.SetActive(true);
            s_overlayObj.SetActive(true);
            s_isOpen = true;
            Canvas.ForceUpdateCanvases();

            GUIManager.BlockInput(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Close()
        {
            if (s_overlayObj != null) s_overlayObj.SetActive(false);
            if (s_canvasRootObj != null) s_canvasRootObj.SetActive(false);
            if (s_isOpen)
            {
                s_isOpen = false;
                GUIManager.BlockInput(false);
            }
        }

        public static void DestroyUI()
        {
            if (s_isOpen)
            {
                GUIManager.BlockInput(false);
                s_isOpen = false;
            }
            if (s_canvasRootObj != null)
            {
                UnityEngine.Object.Destroy(s_canvasRootObj);
                s_canvasRootObj = null;
                s_overlayObj = null;
                s_listContentRt = null;
            }
            s_rows.Clear();
        }

        public static void UpdateTick()
        {
            if (!IsOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private static void EnsureBuilt()
        {
            if (s_overlayObj != null) return;

            s_canvasRootObj = new GameObject("Coarstal_SoulExtractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(s_canvasRootObj);
            if (GUIManager.CustomGUIFront != null)
            {
                s_canvasRootObj.layer = GUIManager.CustomGUIFront.layer;
            }

            Canvas canvas = s_canvasRootObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 2550;

            CanvasScaler scaler = s_canvasRootObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

            s_overlayObj = new GameObject("SoulExtractionBackdrop", typeof(RectTransform), typeof(Image));
            s_overlayObj.transform.SetParent(s_canvasRootObj.transform, false);
            RectTransform ovRt = s_overlayObj.GetComponent<RectTransform>();
            ovRt.anchorMin = Vector2.zero;
            ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = Vector2.zero;
            ovRt.offsetMax = Vector2.zero;
            s_overlayObj.GetComponent<Image>().color = new Color(0.03f, 0.02f, 0.04f, 0.84f);

            GameObject panel = GUIManager.Instance.CreateWoodpanel(
                s_overlayObj.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                840f,
                650f,
                false);

            Font norseBold = GUIManager.Instance.NorseBold != null ? GUIManager.Instance.NorseBold : GUIManager.Instance.AveriaSerifBold;
            Font averia = GUIManager.Instance.AveriaSerif != null ? GUIManager.Instance.AveriaSerif : Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject titleGo = GUIManager.Instance.CreateText(
                "",
                panel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                norseBold,
                32,
                new Color(1.0f, 0.82f, 0.36f, 1f),
                true,
                Color.black,
                780f,
                42f,
                false);
            s_titleText = titleGo.GetComponent<Text>();
            s_titleText.alignment = TextAnchor.MiddleCenter;

            GameObject subGo = GUIManager.Instance.CreateText(
                "",
                panel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -72f),
                averia,
                16,
                new Color(0.88f, 0.84f, 0.92f, 1f),
                true,
                Color.black,
                780f,
                28f,
                false);
            s_subtitleText = subGo.GetComponent<Text>();
            s_subtitleText.alignment = TextAnchor.MiddleCenter;

            // Scrollable weapon list viewport
            GameObject viewportObj = new GameObject("WeaponScrollViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            viewportObj.transform.SetParent(panel.transform, false);
            RectTransform vpRt = viewportObj.GetComponent<RectTransform>();
            vpRt.anchorMin = new Vector2(0.5f, 0.5f);
            vpRt.anchorMax = new Vector2(0.5f, 0.5f);
            vpRt.pivot = new Vector2(0.5f, 0.5f);
            vpRt.anchoredPosition = new Vector2(0f, 6f);
            vpRt.sizeDelta = new Vector2(760f, 400f);
            Image vpImg = viewportObj.GetComponent<Image>();
            vpImg.color = new Color(0.06f, 0.05f, 0.08f, 0.65f);
            Mask vpMask = viewportObj.GetComponent<Mask>();
            vpMask.showMaskGraphic = true;

            GameObject contentObj = new GameObject("WeaponScrollContent", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            s_listContentRt = contentObj.GetComponent<RectTransform>();
            s_listContentRt.anchorMin = new Vector2(0f, 1f);
            s_listContentRt.anchorMax = new Vector2(1f, 1f);
            s_listContentRt.pivot = new Vector2(0.5f, 1f);
            s_listContentRt.anchoredPosition = Vector2.zero;
            s_listContentRt.sizeDelta = new Vector2(0f, 400f);

            ScrollRect sr = viewportObj.GetComponent<ScrollRect>();
            sr.content = s_listContentRt;
            sr.viewport = vpRt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 28f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            // Warning note above buttons
            GameObject warnGo = GUIManager.Instance.CreateText(
                "",
                panel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 92f),
                averia,
                15,
                new Color(1.0f, 0.68f, 0.42f, 1f),
                true,
                Color.black,
                760f,
                28f,
                false);
            s_warningText = warnGo.GetComponent<Text>();
            s_warningText.alignment = TextAnchor.MiddleCenter;

            // Extract & Cancel buttons
            GameObject extBtnObj = GUIManager.Instance.CreateButton(
                "Extract",
                panel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-115f, 38f),
                260f,
                46f);
            s_extractBtn = extBtnObj.GetComponent<Button>();
            s_extractBtnText = extBtnObj.GetComponentInChildren<Text>();
            s_extractBtn.onClick.AddListener(OnExtractConfirmed);

            GameObject canBtnObj = GUIManager.Instance.CreateButton(
                "Cancel",
                panel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(135f, 38f),
                190f,
                46f);
            s_cancelBtn = canBtnObj.GetComponent<Button>();
            s_cancelBtnText = canBtnObj.GetComponentInChildren<Text>();
            s_cancelBtn.onClick.AddListener(Close);

            s_overlayObj.SetActive(false);
            s_canvasRootObj.SetActive(false);
        }

        private static void RebuildWeaponRows(List<ItemDrop.ItemData> weapons)
        {
            s_titleText.text = WeaponAttributesLocalization.Loc("$wa_extract_ui_title");
            s_subtitleText.text = WeaponAttributesLocalization.Loc("$wa_extract_ui_subtitle");
            s_warningText.text = WeaponAttributesLocalization.Loc("$wa_extract_ui_warning");
            s_extractBtnText.text = WeaponAttributesLocalization.Loc("$wa_extract_ui_confirm");
            s_cancelBtnText.text = WeaponAttributesLocalization.Loc("$wa_extract_ui_cancel");

            if (s_listContentRt != null)
            {
                for (int i = s_listContentRt.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.Destroy(s_listContentRt.GetChild(i).gameObject);
                }
            }
            s_rows.Clear();

            Font averia = GUIManager.Instance.AveriaSerif != null ? GUIManager.Instance.AveriaSerif : Resources.GetBuiltinResource<Font>("Arial.ttf");
            Font averiaBold = GUIManager.Instance.AveriaSerifBold != null ? GUIManager.Instance.AveriaSerifBold : averia;

            float rowH = 74f;
            float gap = 8f;
            float padTop = 8f;

            for (int i = 0; i < weapons.Count; i++)
            {
                ItemDrop.ItemData w = weapons[i];
                string bossKey = SoulExtractionSystem.InferBossKeyFromWeapon(w);
                int lv = Mathf.Clamp(WeaponAttributesPlugin.GetEnchantLevel(w), 1, 5);
                string hex = SoulExtractionSystem.GetReadableBossColorHex(bossKey);

                int capturedIdx = i;
                GameObject borderObj = new GameObject("WeaponExtractRow_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                borderObj.transform.SetParent(s_listContentRt, false);
                RectTransform bRt = borderObj.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0.5f, 1f);
                bRt.anchorMax = new Vector2(0.5f, 1f);
                bRt.pivot = new Vector2(0.5f, 1f);
                bRt.anchoredPosition = new Vector2(0f, -(padTop + i * (rowH + gap)));
                bRt.sizeDelta = new Vector2(736f, rowH);
                Image borderImg = borderObj.GetComponent<Image>();

                GameObject bgObj = new GameObject("RowBg", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(borderObj.transform, false);
                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = new Vector2(2f, 2f);
                bgRt.offsetMax = new Vector2(-2f, -2f);
                Image bgImg = bgObj.GetComponent<Image>();

                // Weapon Icon
                GameObject iconObj = new GameObject("WeaponIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(bgObj.transform, false);
                RectTransform icRt = iconObj.GetComponent<RectTransform>();
                icRt.anchorMin = new Vector2(0f, 0.5f);
                icRt.anchorMax = new Vector2(0f, 0.5f);
                icRt.pivot = new Vector2(0f, 0.5f);
                icRt.anchoredPosition = new Vector2(12f, 0f);
                icRt.sizeDelta = new Vector2(54f, 54f);
                Image icImg = iconObj.GetComponent<Image>();
                icImg.sprite = w.GetIcon();
                icImg.preserveAspect = true;

                // Quality + Enchant badge on icon
                GameObject qGo = GUIManager.Instance.CreateText(
                    string.Format("{0}<color={1}>+{2}</color>", w.m_quality, hex, lv),
                    iconObj.transform,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-2f, -2f),
                    averiaBold,
                    14,
                    new Color(1f, 0.85f, 0.35f, 1f),
                    true,
                    Color.black,
                    54f,
                    20f,
                    false);
                Text qTxt = qGo.GetComponent<Text>();
                qTxt.alignment = TextAnchor.UpperRight;
                qTxt.supportRichText = true;

                // Weapon Name & current enchantment line
                string wName = Localization.instance.Localize(w.m_shared.m_name);
                string enchantStr = WeaponAttributesPlugin.GetAttributeEnchantString(w) ?? "";
                string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                float perAttr = WeaponAttributesPlugin.GetEnchantDamagePerAttribute(w, attrs, lv, w.m_quality);
                StringBuilder attrDesc = new StringBuilder();
                for (int a = 0; a < attrs.Length; a++)
                {
                    if (a > 0) attrDesc.Append(" • ");
                    attrDesc.Append(WeaponAttributesPlugin.GetAttributeNameLocalized(attrs[a]));
                    attrDesc.Append(" +");
                    attrDesc.Append(WeaponAttributesPlugin.FormatEnchantValue(perAttr));
                }

                GameObject nameGo = GUIManager.Instance.CreateText(
                    string.Format("<b>{0}</b>\n<size=14><color=#B8B0C8>{1}</color></size>", wName, attrDesc.ToString()),
                    bgObj.transform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(80f, 0f),
                    averia,
                    18,
                    Color.white,
                    true,
                    Color.black,
                    380f,
                    60f,
                    false);
                RectTransform nmRt = nameGo.GetComponent<RectTransform>();
                nmRt.pivot = new Vector2(0f, 0.5f);
                nmRt.anchoredPosition = new Vector2(80f, 0f);
                Text nmTxt = nameGo.GetComponent<Text>();
                nmTxt.alignment = TextAnchor.MiddleLeft;
                nmTxt.supportRichText = true;

                // Resulting Soul Crystal preview on the right ("-> Душа Эйктюра III")
                string crystalTitle = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
                GameObject outGo = GUIManager.Instance.CreateText(
                    "→ " + crystalTitle,
                    bgObj.transform,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-16f, 0f),
                    averiaBold,
                    18,
                    Color.white,
                    true,
                    Color.black,
                    250f,
                    50f,
                    false);
                RectTransform outRt = outGo.GetComponent<RectTransform>();
                outRt.pivot = new Vector2(1f, 0.5f);
                outRt.anchoredPosition = new Vector2(-16f, 0f);
                Text outTxt = outGo.GetComponent<Text>();
                outTxt.alignment = TextAnchor.MiddleRight;
                outTxt.supportRichText = true;

                Button rowBtn = borderObj.GetComponent<Button>();
                rowBtn.transition = Selectable.Transition.None;
                rowBtn.onClick.AddListener(delegate { SelectIndex(capturedIdx); });

                WeaponRowEntry entry = new WeaponRowEntry
                {
                    Weapon = w,
                    BossKey = bossKey,
                    EnchantLevel = lv,
                    BorderImg = borderImg,
                    BgImg = bgImg
                };
                s_rows.Add(entry);
            }

            float totalH = Mathf.Max(400f, padTop * 2f + weapons.Count * (rowH + gap));
            s_listContentRt.sizeDelta = new Vector2(0f, totalH);
            s_listContentRt.anchoredPosition = Vector2.zero;
        }

        private static void SelectIndex(int idx)
        {
            if (idx < 0 || idx >= s_rows.Count)
            {
                s_selectedIndex = -1;
                if (s_extractBtn != null) s_extractBtn.interactable = false;
                return;
            }
            s_selectedIndex = idx;
            if (s_extractBtn != null) s_extractBtn.interactable = true;

            for (int i = 0; i < s_rows.Count; i++)
            {
                bool sel = (i == s_selectedIndex);
                WeaponRowEntry r = s_rows[i];
                if (r.BorderImg != null)
                {
                    r.BorderImg.color = sel
                        ? new Color(1.0f, 0.78f, 0.28f, 1.0f)
                        : new Color(0.32f, 0.26f, 0.40f, 0.85f);
                }
                if (r.BgImg != null)
                {
                    r.BgImg.color = sel
                        ? new Color(0.22f, 0.14f, 0.30f, 0.95f)
                        : new Color(0.11f, 0.09f, 0.14f, 0.90f);
                }
            }
        }

        private static void OnExtractConfirmed()
        {
            Player player = Player.m_localPlayer;
            if (player == null || s_selectedIndex < 0 || s_selectedIndex >= s_rows.Count)
            {
                Close();
                return;
            }

            Inventory inv = player.GetInventory();
            if (inv == null)
            {
                Close();
                return;
            }

            // Locate 1 Empty Soul Crystal in player's inventory
            ItemDrop.ItemData emptyCrystal = null;
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                if (SoulExtractionSystem.IsEmptySoulCrystal(all[i]))
                {
                    emptyCrystal = all[i];
                    break;
                }
            }
            if (emptyCrystal == null)
            {
                player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_missing_empty_crystal"));
                Close();
                return;
            }

            WeaponRowEntry chosen = s_rows[s_selectedIndex];
            ItemDrop.ItemData weapon = chosen.Weapon;
            if (weapon == null || !all.Contains(weapon) || WeaponAttributesPlugin.GetEnchantLevel(weapon) <= 0)
            {
                Close();
                return;
            }

            string bossKey = SoulExtractionSystem.InferBossKeyFromWeapon(weapon);
            int level = Mathf.Clamp(WeaponAttributesPlugin.GetEnchantLevel(weapon), 1, 5);

            // Unequip weapon first if currently equipped so broken 0-durability state updates cleanly
            if (weapon.m_equipped)
            {
                player.UnequipItem(weapon, false);
            }

            // Strip enchantment custom data from weapon and break weapon durability to 0 (repairable at forge/workbench!)
            if (weapon.m_customData != null)
            {
                weapon.m_customData.Remove(WeaponAttributesPlugin.CUSTOM_DATA_KEY);
                weapon.m_customData.Remove(WeaponAttributesPlugin.CUSTOM_DATA_BOSS_KEY);
                weapon.m_customData.Remove(WeaponAttributesPlugin.CUSTOM_DATA_LEVEL_KEY);
            }
            weapon.m_durability = 0f;

            // Consume 1 Empty Soul Crystal, grant 1 Filled Soul Crystal ("Душа Эйктюра I..V"), and apply 3s elemental soul backlash!
            inv.RemoveItem(emptyCrystal, 1);
            SoulExtractionSystem.GiveFilledSoulCrystal(player, bossKey, level);
            SoulExtractionSystem.ApplySoulExtractionDebuff(player, bossKey, 3.0f);

            // Play exact Forge of Potential weapon-break effect ("как на кузнице возможностей")!
            SoulExtractionSystem.PlayForgeOfPotentialBreakEffect(player, weapon);

            string weaponName = Localization.instance.Localize(weapon.m_shared.m_name);
            string crystalTitle = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, level, true);
            player.Message(
                MessageHud.MessageType.Center,
                string.Format(WeaponAttributesLocalization.Loc("$wa_msg_soul_extracted_ok"), crystalTitle, weaponName));

            Close();
        }
    }

    // ====================================================================================================
    // HARMONY PATCHES FOR TRADER SHOP, RIGHT-CLICK CONSUMPTION, DYNAMIC CRYSTAL NAMES & SOUL CAULDRON UI
    // ====================================================================================================
    [HarmonyPatch(typeof(Trader), "GetAvailableItems", new Type[0])]
    public static class Patch_Trader_GetAvailableItems_SoulCrystal
    {
        public static void Postfix(Trader __instance, ref List<Trader.TradeItem> __result)
        {
            if (!WeaponAttributesPlugin.IsActive || __instance == null || __result == null) return;

            string tName = __instance.m_name ?? "";
            string goName = __instance.gameObject != null ? __instance.gameObject.name : "";
            bool isHildir =
                tName.IndexOf("hildir", StringComparison.OrdinalIgnoreCase) >= 0 ||
                goName.IndexOf("hildir", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isHildir) return;

            GameObject crystalPrefab = PrefabManager.Instance.GetPrefab(SoulExtractionSystem.EmptyCrystalPrefab);
            if (crystalPrefab == null && ObjectDB.instance != null)
            {
                crystalPrefab = ObjectDB.instance.GetItemPrefab(SoulExtractionSystem.EmptyCrystalPrefab);
            }
            if (crystalPrefab == null) return;

            ItemDrop crystalDrop = crystalPrefab.GetComponent<ItemDrop>();
            if (crystalDrop == null) return;

            int price = SoulExtractionSystem.SoulCrystalTraderPrice != null
                ? Mathf.Max(1, SoulExtractionSystem.SoulCrystalTraderPrice.Value)
                : 999;

            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] != null && __result[i].m_prefab == crystalDrop)
                {
                    __result[i].m_price = price;
                    return;
                }
            }

            Trader.TradeItem item = new Trader.TradeItem
            {
                m_prefab = crystalDrop,
                m_stack = 1,
                m_price = price,
                m_requiredGlobalKey = "",
                m_name = "$wa_item_soul_crystal_empty",
                m_tooltip = "",
                m_buyKey = "",
                m_incrementKey = "",
                m_incrementAmount = 1,
                m_buyPlayerEffects = new EffectList()
            };
            __result.Add(item);
        }
    }

    [HarmonyPatch(typeof(Player), "ConsumeItem", new Type[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool) })]
    public static class Patch_Player_ConsumeItem_SoulCrystal
    {
        public static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, ref bool __result)
        {
            if (!WeaponAttributesPlugin.IsActive || __instance == null || item == null) return true;
            if (SoulExtractionSystem.IsEmptySoulCrystal(item))
            {
                if (__instance == Player.m_localPlayer)
                {
                    SoulExtractionUI.OpenForPlayer(__instance);
                }
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "CreateItemTooltip", new Type[] { typeof(ItemDrop.ItemData), typeof(UITooltip) })]
    public static class Patch_InventoryGrid_CreateItemTooltip_SoulCrystal
    {
        public static void Postfix(ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (!WeaponAttributesPlugin.IsActive || item == null || tooltip == null) return;
            if (SoulExtractionSystem.IsFilledSoulCrystal(item))
            {
                string bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(item);
                int lv = SoulExtractionSystem.GetFilledCrystalLevel(item);
                tooltip.m_topic = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrop), "GetHoverName", new Type[0])]
    public static class Patch_ItemDrop_GetHoverName_SoulCrystal
    {
        public static void Postfix(ItemDrop __instance, ref string __result)
        {
            if (!WeaponAttributesPlugin.IsActive || __instance == null || __instance.m_itemData == null) return;
            if (SoulExtractionSystem.IsFilledSoulCrystal(__instance.m_itemData))
            {
                string bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(__instance.m_itemData);
                int lv = SoulExtractionSystem.GetFilledCrystalLevel(__instance.m_itemData);
                __result = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrop), "GetHoverText", new Type[0])]
    public static class Patch_ItemDrop_GetHoverText_SoulCrystal
    {
        public static void Postfix(ItemDrop __instance, ref string __result)
        {
            if (!WeaponAttributesPlugin.IsActive || __instance == null || __instance.m_itemData == null) return;
            if (SoulExtractionSystem.IsFilledSoulCrystal(__instance.m_itemData))
            {
                string defaultName = Localization.instance.Localize("$wa_item_soul_crystal_filled");
                string bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(__instance.m_itemData);
                int lv = SoulExtractionSystem.GetFilledCrystalLevel(__instance.m_itemData);
                string richName = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
                if (!string.IsNullOrEmpty(__result) && !string.IsNullOrEmpty(defaultName))
                {
                    __result = __result.Replace(defaultName, richName);
                }
            }
        }
    }

    // ====================================================================================================
    // SOUL EXTRACTION CAULDRON CRAFTING PANEL INTEGRATION (Break Filled Crystal -> 50% Mob + 25% Boss)
    // ====================================================================================================
    public static class SoulCauldronGuiHelper
    {
        private static readonly Dictionary<string, Recipe> s_cauldronRecipes = new Dictionary<string, Recipe>();
        private static MethodInfo s_addRecipeToListMethod;
        private static FieldInfo s_availableRecipesField = typeof(InventoryGui).GetField("m_availableRecipes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo s_recipeListBaseSizeField = typeof(InventoryGui).GetField("m_recipeListBaseSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static bool IsCauldronRecipe(Recipe recipe)
        {
            return recipe != null && recipe.name != null && recipe.name.StartsWith(SoulExtractionSystem.CAULDRON_RECIPE_PREFIX);
        }

        public static Recipe GetOrCreateCauldronRecipe(string bossKey, int level, CraftingStation station)
        {
            string key = string.Format("{0}__lvl{1}", bossKey, Mathf.Clamp(level, 1, 5));
            Recipe rec;
            if (s_cauldronRecipes.TryGetValue(key, out rec) && rec != null)
            {
                rec.m_craftingStation = station;
                return rec;
            }

            GameObject crystalPrefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(SoulExtractionSystem.FilledCrystalPrefab) : null;
            if (crystalPrefab == null) crystalPrefab = PrefabManager.Instance.GetPrefab(SoulExtractionSystem.FilledCrystalPrefab);
            if (crystalPrefab == null) return null;

            ItemDrop crystalDrop = crystalPrefab.GetComponent<ItemDrop>();
            if (crystalDrop == null) return null;

            rec = ScriptableObject.CreateInstance<Recipe>();
            rec.name = SoulExtractionSystem.CAULDRON_RECIPE_PREFIX + key;
            rec.m_item = crystalDrop;
            rec.m_amount = 1;
            rec.m_enabled = true;
            rec.m_craftingStation = station;
            rec.m_minStationLevel = 1;
            rec.m_resources = new Piece.Requirement[0];

            s_cauldronRecipes[key] = rec;
            return rec;
        }

        public static void ParseCauldronRecipe(Recipe recipe, out string bossKey, out int level)
        {
            bossKey = "eikthyr";
            level = 1;
            if (!IsCauldronRecipe(recipe)) return;
            string payload = recipe.name.Substring(SoulExtractionSystem.CAULDRON_RECIPE_PREFIX.Length);
            int idx = payload.LastIndexOf("__lvl");
            if (idx > 0)
            {
                bossKey = payload.Substring(0, idx).ToLower();
                int lv;
                if (int.TryParse(payload.Substring(idx + 5), out lv))
                {
                    level = Mathf.Clamp(lv, 1, 5);
                }
            }
        }

        public static void PopulateCauldronRecipeList(InventoryGui gui, Player player, CraftingStation station)
        {
            if (SoulExtractionSystem.s_soulCauldronIcon != null)
            {
                station.m_icon = SoulExtractionSystem.s_soulCauldronIcon;
                if (gui.m_craftingStationIcon != null)
                {
                    gui.m_craftingStationIcon.sprite = SoulExtractionSystem.s_soulCauldronIcon;
                }
            }

            if (gui.m_tabCraft != null && !gui.InCraftTab())
            {
                gui.m_tabCraft.interactable = false;
                if (gui.m_tabUpgrade != null) gui.m_tabUpgrade.interactable = true;
            }
            if (gui.m_tabUpgrade != null)
            {
                gui.m_tabUpgrade.gameObject.SetActive(false);
            }

            if (gui.m_craftButton != null)
            {
                TMP_Text defaultBtnTxt = gui.m_craftButton.GetComponentInChildren<TMP_Text>();
                if (defaultBtnTxt != null)
                {
                    defaultBtnTxt.text = WeaponAttributesLocalization.Loc("$wa_cauldron_btn_extract");
                }
            }

            if (s_addRecipeToListMethod == null)
            {
                s_addRecipeToListMethod = typeof(InventoryGui).GetMethod("AddRecipeToList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (s_addRecipeToListMethod == null) return;

            Inventory inv = player.GetInventory();
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            HashSet<string> ownedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool addedAny = false;

            // Pass 0: Owned Filled Soul Crystals in player's inventory (full color, craftable at top!)
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData it = all[i];
                if (!SoulExtractionSystem.IsFilledSoulCrystal(it)) continue;

                string bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(it);
                int lv = SoulExtractionSystem.GetFilledCrystalLevel(it);
                Recipe rec = GetOrCreateCauldronRecipe(bossKey, lv, station);
                if (rec == null) continue;

                ownedKeys.Add(string.Format("{0}__{1}", bossKey, lv));
                IList availBefore = s_availableRecipesField != null ? s_availableRecipesField.GetValue(gui) as IList : null;
                int prevCount = availBefore != null ? availBefore.Count : -1;

                s_addRecipeToListMethod.Invoke(gui, new object[] { player, rec, it, true });
                DecorateCauldronRow(gui, prevCount, bossKey, lv, true);
                addedAny = true;
            }

            // Pass 1: Unowned Soul Crystals (Levels I..V for all world-unlocked bosses) shown in muted gray, matching the Enchanting Table!
            for (int b = 0; b < WeaponAttributesPlugin.OrderedBossKeys.Length; b++)
            {
                string bKey = WeaponAttributesPlugin.OrderedBossKeys[b];
                string bPrefab;
                if (!WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bKey, out bPrefab))
                {
                    continue;
                }
                if (!WeaponAttributesPlugin.IsBossUnlockedForEnchanting(player, inv, bKey, bPrefab))
                {
                    continue;
                }

                for (int lv = 1; lv <= 5; lv++)
                {
                    string pairKey = string.Format("{0}__{1}", bKey, lv);
                    if (ownedKeys.Contains(pairKey)) continue;

                    Recipe rec = GetOrCreateCauldronRecipe(bKey, lv, station);
                    if (rec == null) continue;

                    IList availBeforeGray = s_availableRecipesField != null ? s_availableRecipesField.GetValue(gui) as IList : null;
                    int prevCountGray = availBeforeGray != null ? availBeforeGray.Count : -1;

                    s_addRecipeToListMethod.Invoke(gui, new object[] { player, rec, null, false });
                    DecorateCauldronRow(gui, prevCountGray, bKey, lv, false);
                    addedAny = true;
                }
            }

            if (addedAny && gui.m_recipeListRoot != null && s_availableRecipesField != null)
            {
                IList avail = s_availableRecipesField.GetValue(gui) as IList;
                if (avail != null)
                {
                    float baseSize = s_recipeListBaseSizeField != null ? (float)s_recipeListBaseSizeField.GetValue(gui) : 0f;
                    float targetH = Mathf.Max(baseSize, (float)avail.Count * gui.m_recipeListSpace);
                    gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetH);
                }
            }
        }

        private static void DecorateCauldronRow(InventoryGui gui, int prevAvailCount, string bossKey, int lv, bool canCraft)
        {
            Transform row = Patch_InventoryGui_UpdateRecipeList.GetNewlyAddedRowTransform(gui, prevAvailCount);
            if (row == null) return;

            Transform iconTr = row.Find("icon");
            if (iconTr != null && !canCraft)
            {
                Image iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null)
                {
                    // Valheim's Custom/GuiScroll UI shader desaturates to muted grayscale at Color(1, 0, 1, 0)
                    iconImg.color = new Color(1f, 0f, 1f, 0f);
                }
            }

            Transform nameTr = row.Find("name");
            if (nameTr != null)
            {
                TMP_Text nt = nameTr.GetComponent<TMP_Text>();
                if (nt != null)
                {
                    if (canCraft)
                    {
                        nt.color = Color.white;
                        nt.text = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
                    }
                    else
                    {
                        nt.color = new Color(0.50f, 0.50f, 0.50f, 0.90f);
                        string plainTitle = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, false);
                        nt.text = string.Format("<color=#808080>{0}</color>", plainTitle);
                    }
                }
            }

            Transform qualTr = row.Find("QualityLevel");
            if (qualTr != null)
            {
                TMP_Text qt = qualTr.GetComponent<TMP_Text>();
                if (qt != null)
                {
                    qt.gameObject.SetActive(true);
                    qt.textWrappingMode = TextWrappingModes.NoWrap;
                    qt.overflowMode = TextOverflowModes.Overflow;
                    string badgeHex = canCraft ? SoulExtractionSystem.GetReadableBossColorHex(bossKey) : "#787878";
                    qt.color = canCraft ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.88f);
                    qt.text = string.Format("<color={0}>{1}</color>", badgeHex, SoulExtractionSystem.ToRoman(lv));
                }
            }
        }

        public static void RenderCauldronSelectedRecipe(InventoryGui gui, Player player, Recipe recipe, ItemDrop.ItemData crystalItem)
        {
            string bossKey;
            int lv;
            if (crystalItem != null && SoulExtractionSystem.IsFilledSoulCrystal(crystalItem))
            {
                bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(crystalItem);
                lv = SoulExtractionSystem.GetFilledCrystalLevel(crystalItem);
            }
            else
            {
                ParseCauldronRecipe(recipe, out bossKey, out lv);
            }

            string crystalTitle = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, true);
            if (gui.m_recipeName != null)
            {
                gui.m_recipeName.text = string.Format(WeaponAttributesLocalization.Loc("$wa_cauldron_recipe_title"), crystalTitle);
            }
            if (gui.m_minStationLevelIcon != null)
            {
                gui.m_minStationLevelIcon.gameObject.SetActive(false);
            }
            if (gui.m_itemCraftType != null)
            {
                gui.m_itemCraftType.gameObject.SetActive(true);
                gui.m_itemCraftType.text = WeaponAttributesLocalization.Loc("$wa_cauldron_craft_type");
            }

            int mobReturn = SoulExtractionSystem.GetReturnedMobTrophyCount(lv);
            float bossChance = SoulExtractionSystem.GetBossReturnChancePercent();
            string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);
            string bossPrefab;
            WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bossKey, out bossPrefab);

            string mobName = SoulExtractionSystem.GetLocalizedItemPrefabName(mobPrefab, mobPrefab ?? "");
            string bossName = SoulExtractionSystem.GetLocalizedItemPrefabName(bossPrefab, Patch_InventoryGui_UpdateRecipeList.GetShortBossLabel(bossKey));

            if (gui.m_recipeDecription != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(WeaponAttributesLocalization.Loc("$wa_cauldron_desc_intro"));
                sb.Append("\n\n<color=#FFD700>");
                sb.Append(WeaponAttributesLocalization.Loc("$wa_cauldron_yield_header"));
                sb.Append("</color>\n• ");
                sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_crystal_tooltip_mob_return"), mobReturn, mobName, Mathf.RoundToInt(SoulExtractionSystem.SoulCauldronMobReturnPercent != null ? SoulExtractionSystem.SoulCauldronMobReturnPercent.Value : 50f)));
                sb.Append("\n• ");
                sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_crystal_tooltip_boss_chance"), bossName, Mathf.RoundToInt(bossChance)));
                gui.m_recipeDecription.text = sb.ToString();
            }

            bool hasCrystal = (crystalItem != null && player.GetInventory().ContainsItem(crystalItem));
            if (gui.m_recipeRequirementList != null && gui.m_recipeRequirementList.Length > 0)
            {
                GameObject slot0 = gui.m_recipeRequirementList[0];
                slot0.SetActive(true);
                Piece.Requirement req = new Piece.Requirement();
                req.m_amount = 1;
                req.m_resItem = recipe.m_item;
                InventoryGui.SetupRequirement(slot0.transform, req, player, false, 1, 1);

                Transform reqIconTr = slot0.transform.Find("res_icon");
                if (reqIconTr != null)
                {
                    Image reqIconImg = reqIconTr.GetComponent<Image>();
                    if (reqIconImg != null)
                    {
                        reqIconImg.color = hasCrystal ? Color.white : new Color(1f, 0f, 1f, 0f);
                    }
                }

                Transform amtTr = slot0.transform.Find("res_amount");
                if (amtTr != null)
                {
                    TMP_Text amtTxt = amtTr.GetComponent<TMP_Text>();
                    if (amtTxt != null)
                    {
                        amtTxt.text = "1";
                        amtTxt.color = hasCrystal ? Color.white : ((Mathf.Sin(Time.time * 10f) > 0f) ? Color.red : Color.white);
                    }
                }

                Transform nameReqTr = slot0.transform.Find("res_name");
                if (nameReqTr != null)
                {
                    TMP_Text nameReqTxt = nameReqTr.GetComponent<TMP_Text>();
                    if (nameReqTxt != null)
                    {
                        nameReqTxt.text = SoulExtractionSystem.GetFilledCrystalDisplayName(bossKey, lv, false);
                    }
                }

                for (int r = 1; r < gui.m_recipeRequirementList.Length; r++)
                {
                    InventoryGui.HideRequirement(gui.m_recipeRequirementList[r].transform);
                }
            }

            if (gui.m_craftButton != null)
            {
                gui.m_craftButton.interactable = hasCrystal;
                TMP_Text btnText = gui.m_craftButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = WeaponAttributesLocalization.Loc("$wa_cauldron_btn_extract");
                }
                UITooltip tooltip = gui.m_craftButton.GetComponent<UITooltip>();
                if (tooltip != null)
                {
                    if (hasCrystal)
                    {
                        tooltip.m_text = "";
                        UITooltip.HideTooltip();
                    }
                    else
                    {
                        tooltip.m_text = Localization.instance.Localize("$msg_missingrequirement");
                    }
                }
            }
        }

        public static void ExecuteCauldronExtraction(Player player, Recipe recipe, ItemDrop.ItemData selectedCrystal)
        {
            if (player == null) return;
            Inventory inv = player.GetInventory();
            if (inv == null) return;

            ItemDrop.ItemData targetCrystal = selectedCrystal;
            string bossKey;
            int lv;
            ParseCauldronRecipe(recipe, out bossKey, out lv);

            if (targetCrystal == null || !inv.ContainsItem(targetCrystal) || !SoulExtractionSystem.IsFilledSoulCrystal(targetCrystal))
            {
                targetCrystal = null;
                List<ItemDrop.ItemData> all = inv.GetAllItems();
                for (int i = 0; i < all.Count; i++)
                {
                    ItemDrop.ItemData it = all[i];
                    if (SoulExtractionSystem.IsFilledSoulCrystal(it) &&
                        string.Equals(SoulExtractionSystem.GetFilledCrystalBossKey(it), bossKey, StringComparison.OrdinalIgnoreCase) &&
                        SoulExtractionSystem.GetFilledCrystalLevel(it) == lv)
                    {
                        targetCrystal = it;
                        break;
                    }
                }
            }

            if (targetCrystal == null)
            {
                return;
            }

            bossKey = SoulExtractionSystem.GetFilledCrystalBossKey(targetCrystal);
            lv = SoulExtractionSystem.GetFilledCrystalLevel(targetCrystal);

            // Deduct the filled crystal and briefly speed up the cauldron's ladle stir (no cauldron smoke/flash VFX or player debuff)
            inv.RemoveOneItem(targetCrystal);
            SoulCauldronLadleAnimator.BoostNearestCauldronStir(player.transform.position);

            // 1) Guaranteed 50% Mob Trophies (1 / 3 / 8 / 16 / 26)
            int mobReturn = SoulExtractionSystem.GetReturnedMobTrophyCount(lv);
            string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);
            if (!string.IsNullOrEmpty(mobPrefab) && mobReturn > 0)
            {
                SoulExtractionSystem.GiveOrDropItems(player, mobPrefab, mobReturn);
            }

            // 2) 25% Roll for Boss Trophy
            float bossChance = SoulExtractionSystem.GetBossReturnChancePercent();
            string bossPrefab = null;
            WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bossKey, out bossPrefab);
            bool wonBoss = !string.IsNullOrEmpty(bossPrefab) && (UnityEngine.Random.Range(0f, 100f) < bossChance);
            if (wonBoss)
            {
                SoulExtractionSystem.GiveOrDropItems(player, bossPrefab, 1);
            }

            string mobName = SoulExtractionSystem.GetLocalizedItemPrefabName(mobPrefab, mobPrefab ?? "");
            string bossName = SoulExtractionSystem.GetLocalizedItemPrefabName(bossPrefab, Patch_InventoryGui_UpdateRecipeList.GetShortBossLabel(bossKey));

            if (wonBoss)
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    string.Format(WeaponAttributesLocalization.Loc("$wa_msg_cauldron_won_boss"), bossName, mobReturn, mobName));
            }
            else
            {
                player.Message(
                    MessageHud.MessageType.Center,
                    string.Format(WeaponAttributesLocalization.Loc("$wa_msg_cauldron_mob_only"), mobReturn, mobName));
            }
        }
    }
}
