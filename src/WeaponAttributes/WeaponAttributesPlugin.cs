using System;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Configs;
using Jotunn.Utils;
using FinalMod;

namespace WeaponAttributesMod
{
    public class WeaponAttributesPlugin
    {
        public const string PluginGUID = "com.valheim.weaponattributes";
        public const string PluginName = "WeaponAttributes";
        public const string PluginVersion = "1.0.0";

        public const string CUSTOM_DATA_KEY = "WeaponAttributes_Enchant";
        public const string CUSTOM_DATA_BOSS_KEY = "WeaponAttributes_Boss";
        public const string CUSTOM_DATA_LEVEL_KEY = "WeaponAttributes_Level";

        internal const string ZDO_MINION_ATTRS = "wa_minion_attrs";
        internal const string ZDO_MINION_DMG = "wa_minion_dmg";
        internal const string ZDO_MINION_NOPVP = "wa_minion_nopvp";

        public static ConfigEntry<float> BaseAttributeDamage;
        public static ConfigEntry<bool> DualAttributeSplit;
        public static ConfigEntry<float> BaseDamagePercentPerLevelV2;
        public static ConfigEntry<float> DualAttributeFactorPerElementV2;

        internal static ItemDrop.ItemData s_activeProjectileSpawnWeapon;

        public static readonly Dictionary<string, string[]> BossEnchantMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // 1. Eikthyr -> Lightning
            { "TrophyEikthyr", new string[] { "lightning" } },

            // 2. The Elder -> Additional Physical Damage
            { "TrophyTheElder", new string[] { "physical" } },

            // 3. Bonemass -> Poison
            { "TrophyBonemass", new string[] { "poison" } },

            // 4. Moder -> Frost
            { "TrophyDragonQueen", new string[] { "frost" } },

            // 5. Yagluth -> Fire
            { "TrophyGoblinKing", new string[] { "fire" } },

            // 6. The Queen -> Poison + Lightning
            { "TrophySeekerQueen", new string[] { "poison", "lightning" } },

            // 7. Fader (Emerald Flame) -> Fire + Spirit
            { "TrophyFader", new string[] { "fire", "spirit" } }
        };

        public static readonly Dictionary<string, string> BossNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "TrophyEikthyr", "eikthyr" },
            { "TrophyTheElder", "elder" },
            { "TrophyBonemass", "bonemass" },
            { "TrophyDragonQueen", "moder" },
            { "TrophyGoblinKing", "yagluth" },
            { "TrophySeekerQueen", "queen" },
            { "TrophyFader", "fader" }
        };

        // Key to boss trophy prefab name (for level 1 enchant)
        public static readonly Dictionary<string, string> BossKeyToTrophyPrefab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "eikthyr", "TrophyEikthyr" },
            { "elder", "TrophyTheElder" },
            { "bonemass", "TrophyBonemass" },
            { "moder", "TrophyDragonQueen" },
            { "yagluth", "TrophyGoblinKing" },
            { "queen", "TrophySeekerQueen" },
            { "fader", "TrophyFader" }
        };

        // Mob trophies for each boss attribute
        public static readonly Dictionary<string, string> BossMobTrophyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "eikthyr", "TrophyDeer" },
            { "elder", "TrophyForestTroll" },
            { "bonemass", "TrophyBlob" },
            { "moder", "TrophyHatchling" },
            { "yagluth", "TrophyGoblinShaman" },
            { "queen", "TrophySeekerBrute" },
            { "fader", "TrophyAsksvin" }
        };

        // Standard boss progression global keys (ZoneSystem)
        public static readonly Dictionary<string, string> BossDefeatedGlobalKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "eikthyr", "defeated_eikthyr" },
            { "elder", "defeated_gdking" },
            { "bonemass", "defeated_bonemass" },
            { "moder", "defeated_dragon" },
            { "yagluth", "defeated_goblinking" },
            { "queen", "defeated_queen" },
            { "fader", "defeated_fader" }
        };

        private static readonly FieldInfo s_playerTrophiesField = typeof(Player).GetField("m_trophies", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>
        /// Returns true if the boss has been defeated in the current world OR the player has ever held/picked up its trophy (or has it in inventory).
        /// </summary>
        public static bool IsBossUnlockedForEnchanting(Player player, Inventory inv, string bossKey, string bossPrefab)
        {
            if (string.IsNullOrEmpty(bossKey)) return false;

            // 1. Check if boss was defeated in this world (ZoneSystem global key)
            if (ZoneSystem.instance != null)
            {
                string globalKey;
                if (BossDefeatedGlobalKeyMap.TryGetValue(bossKey, out globalKey) && !string.IsNullOrEmpty(globalKey))
                {
                    if (ZoneSystem.instance.GetGlobalKey(globalKey))
                    {
                        return true;
                    }
                }
            }

            if (player == null) return false;

            // 2. Check if player currently holds the Boss Trophy in inventory
            if (inv != null && !string.IsNullOrEmpty(bossPrefab) && CountInventoryItem(inv, bossPrefab) >= 1)
            {
                return true;
            }

            // 3. Check if player has EVER picked up / held this Boss Trophy (saved in Player.m_trophies & Player.m_knownMaterial)
            if (!string.IsNullOrEmpty(bossPrefab))
            {
                if (s_playerTrophiesField != null)
                {
                    HashSet<string> trophySet = s_playerTrophiesField.GetValue(player) as HashSet<string>;
                    if (trophySet != null && trophySet.Contains(bossPrefab))
                    {
                        return true;
                    }
                }
                else
                {
                    List<string> trophies = player.GetTrophies();
                    if (trophies != null && trophies.Contains(bossPrefab))
                    {
                        return true;
                    }
                }

                if (ObjectDB.instance != null)
                {
                    GameObject bObj = ObjectDB.instance.GetItemPrefab(bossPrefab);
                    ItemDrop bDrop = bObj != null ? bObj.GetComponent<ItemDrop>() : null;
                    if (bDrop != null && bDrop.m_itemData != null && bDrop.m_itemData.m_shared != null &&
                        !string.IsNullOrEmpty(bDrop.m_itemData.m_shared.m_name))
                    {
                        if (player.IsKnownMaterial(bDrop.m_itemData.m_shared.m_name))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasEnchantMaterialsInInventory(Inventory inv, string bossKey, string bossPrefab, int targetLevel)
        {
            if (inv == null || string.IsNullOrEmpty(bossKey)) return false;
            string mobPrefab = GetMobTrophyPrefab(bossKey);
            int mobCount = !string.IsNullOrEmpty(mobPrefab) ? CountInventoryItem(inv, mobPrefab) : 0;
            if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
            {
                mobCount += CountInventoryItem(inv, "TrophyFrostTroll");
            }

            if (targetLevel <= 1)
            {
                if (string.IsNullOrEmpty(bossPrefab) || CountInventoryItem(inv, bossPrefab) < 1)
                {
                    return false;
                }
                return mobCount >= 1;
            }

            int reqMob = GetMobTrophyCountForLevel(targetLevel);
            return mobCount >= reqMob;
        }

        // Standard boss attributes map by bossKey
        public static readonly string[] OrderedBossKeys = new string[]
        {
            "eikthyr",
            "elder",
            "bonemass",
            "moder",
            "yagluth",
            "queen",
            "fader"
        };

        public static readonly Dictionary<string, string[]> BossKeyAttributesMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "eikthyr", new string[] { "lightning" } },
            { "elder", new string[] { "physical" } },
            { "bonemass", new string[] { "poison" } },
            { "moder", new string[] { "frost" } },
            { "yagluth", new string[] { "fire" } },
            { "queen", new string[] { "poison", "lightning" } },
            { "fader", new string[] { "fire", "spirit" } }
        };

        public static readonly Dictionary<string, Sprite> BossSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static void RegisterIcon(string key, string base64Data)
        {
            try
            {
                byte[] rawBytes = Convert.FromBase64String(base64Data);
                Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                if (ImageConversion.LoadImage(tex, rawBytes))
                {
                    Sprite spr = Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f));
                    BossSprites[key] = spr;
                }
            }
            catch (Exception) { }
        }

        private static void LoadEmbeddedIcons()
        {
            RegisterIcon("bonemass", "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAXcSURBVHhe7Zp9bBN1GMf339K7a7t1L23XDrrt2t6tdO0Y3eYGkwFDJMibvChjC4HxMsjG26KA21BRDEh4CYYXgwQcIyGgImhETIguIMgiKjpjcKWDCmXU4NQNlq3tY35nj8CvTFy79drSb/JJmt7T+93vea7P73l+dzExUUUVVVRRRRWVEIrVxqZTNJWEf//EiGCIYwRDNOLfPxEiGbKEYIhWhEgvWowfj3iJ9KKTmumJdlOVykEwRAuRSZhxm4gVH/1Znxg7l7flu1NK4tsIhvgqho6JxW0jT3RMLJqsZmqiffXtAkCUnzPfQw4hGXIzbh5xIvTEPDTZ2Z8Zu1Y6ngKe4q0Zd7z54Hn8N5EjFH2WOG+sTHEsv5EPOLq58hsoH4hokRr/aUSIj37ZWXN31fU8wFl4OccVb6GuouUx4vIBKnhEjOiSYbHSscyWC30x9SjbyS2PLFGLnyOshSYkNlHW0iZTT6U1F/6LvPqhTi4fsKI8/DxhKRR99N/OrR3iXHzFAo+j4qccj2qC7BrKFxFRKvPRL//G7Fr4ywj4P8w+Y+xBvxExov34+cJKlIFiUfQt61KdFS050B9K9tJ/cflAT8zDzxs2Ihhid0KhxFZ2weyef3k49BdmgSJ8S2Vv9FtHbUnvmPd9NvhD6dcmd/JoaRvBEqfDbmnkol8gsZVeMHnKv80Gf3nuAzb8SmW+4SncpOmY22yGQLGsTw2vUhm1uykT4+1zLphgoEibkXQDFVMhXyrz0R+zh+584VwWDBTTThlccTlhUCqj6CufibPPajLCQDN6VwZXKpMsWY2PGxLiG57id+muGV8Og8HAuCIlREtlb7ubUZromH7GAIPFlM8zPfKxcaFXKvPRH9eg6576hQEGk/GHtT3iLNKKllr8OoSRN/ppcxIdk09lQjDI3TiEK5VDYmlEDQ+VRVrHHKR7Jn3KQrAIiYTIt7vMUoVz4kkGgklIOOB+9A/RrgnH9RBMBHcAH339Erlz/Ic6CDaCOwBl4bhcylbcQLvHHdXCo6g+Ox72tC7hWHNxss/xQBDUAXy7a6hRdow5QsOjqGoqgV/vNsH17ktw8vd6jleap/nY+YugDuCjX/R+umf04Qx4FO/ZquBOrx263H/cd8CxW2t87PxFMAegHRo0OLtS0VHUkA590XB9FZxw1j3ER+3rfOz8RTAHoOIDDZ63S+MaeTAd+qK+eaaPAzb9UO5j5y+COYBiqSI0+PDNqd0F+9OgL4oO0PDmpVI4cbuOY3vLAihu0PnY+UPebo0bXYMgm6Z8AhxWr+rK36eBQHn6oBZevTgbdv68CN76rgzGNrA+Njjmt9U9gpXC3vW/VbssuSNv71AIlA3Nc+Dj9rr77GxZ5GODk7lWye0NCNYWo40P+UTpTcuuIRAojddq4Pit2ofAbXDUM2XtqAgTbHcIJR9qGGnN3qp257yTCoGw48eFD03+gHWFjw2OZARlE/TJEb8Uplcm/5m9IxUCoWS/AfZdqYbjjlo4ZFsNM47k+9g8iK5G8e/WGEO+iF9XUIU2KMU5lC1ri8pj2qaGYBE/SmxH+w+C3f68+NVAXZZwx7hFBcFAszSJ2wgRPPq80BMb0kBatS8l3zNsSoHBRF+n6KXM5FWUgPHrEEwyWiZBb35Jcqk2XZ3CxW5UwmDAbFB44grFdpT50Wu2+HUIKv4xuLRQbNfVK9z6NxQwkOhek3tk4yQ3vbd+CT5+SIhgiEnICRIL1aapSerWvi6HgSDt5aReaQGFIi9M3d8fcUsjS5xHOUFZKuvIWC+HQFAtkP1Nmgj0OKwFORgfLyTlLZMbUcTEFqpNVSHrTKtNhv6gqky4Kx0p/o1rdljiNPqL4eOEvLiOkSVOo0lQ2aQt4Vlpu3J+fKd6eULv0LVJ8CCpqxJdKRWyrsQpUqd4BIXeHW5FT4IF6fQGWt69g+3e25h7Rf4xNKKJo9UFP1fYC3VuaHIomaH6ATmG+8yS1ehYRE46qqiiiiqqENU/xU+x5PqCVsoAAAAASUVORK5CYII=");
            RegisterIcon("eikthyr", "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");
            RegisterIcon("elder", "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAU5SURBVHhe7Zt7bFNVHMdP27W962tbu3Z9rutj6+1aRqeMGcSpgYHgQCOJQjDOKUaJc/hM8PkPOMwijj26EKYsBCdEiSgTVAIhi8+FIMQwY8zYhhenaGJQL0pct5/53fQachOUsfbevr7JJ9k5TXbO/X3v99xzT1pCcsopp5xykkZBQur8hDiF/VmjACEDNCH9wv6sEE1IPU3ISJw1ws8zXuj+XRoN81BBwQRNyHBWRYF3/6DPx46Ew7FahWI0q6KA7t+p0TA/RyKA7HG7f8+aKPDuH/B62Z8iEeB5MFuigO7fodEwE3PnwuUMh0KxmkyPAu/+ux4Py1RVgZDdpaWZHQV0fyWlYc7OqYIr0WQwZGYUePffcZexY+E5cCVO0sHMjAK6v4KimJFQGP6PPqcrs6LAu7/X7Wa/C4XhamjU6zMnCrz731aG4WoZqqAzIwq8+/0uN/tNMAQzYYfDmf5RQPcbKIo5TYfgWrhPl8ZR4N3f5Sxlvw5UwrXwia8iNk+eplFA929XU8zJiiDMhqjNkX5R4N3f6XCxJyqCMFvuTbcooPvL1RRzvJyGRHDU60+fKPDu99qd7Jf+ACSKDqstPaKA7i9Tq5nPfQFINGu0utSOAu/+dquD/dRbAYnmQ7c3dr1cnrpRQPeXqtTMoKccksVWizU1o8C73221s8c85ZBM7snXpl4U0P0lKjVztMwPyeY9lye1osC732G2sYfdPhCDLcUlXBQChKwSzkd08e5/VOoDMcECBAlpEc5HVPHuv1ZsZQ+5vCAmKVEAdL9eqWIGnB4QG8kLwLvfZrKyA24/HAqE4X2nRzQkLwC6v1ipYvY7ymDw1nr4MdoDp5tbANtiIGkBePdbjWZ2n90NQ6vXcgU40fgAYFsMJC0Aur9IqWLetrkBGX7iaZjo7oFji5ZybTGQrAC8+5uLzOxeaykgo5tb4YfuHjgQqubaYiBZAXDzgYPvMNtj/SUuQIaf2cgV4OD8G7m2GEhWAPx+D/fsN1kv7S5xAXLqscfhHBZgwc1cO9m8bnFMxXeC9wvnl3SFCAni4JsKzRd3WVyAHF/3CDBdPXBk2UqunWzaTda/4wUQfyvsJ8SMg7foii70mZ2AfLx4OTBdUTi14SmunWxeNJjYeARqhfMTRdyZv4qaeKPYCUif1Q3j7Z3wfVcU3iwLcH3JZDWlOx9/JVYL5yaKcPEJy2Rnuo22qV6TA5Chh5u5AnzWuI5rJ5MauWKMJmSncF6iqZKQCBcDbcFv2412QN6qrIbR9g442xmF/Qtu4fqSwbM6I3f7S34qRBOyb55cPtZZZJvuMdoB+WBJA4x3RrlC9Nq9XF+iqVMomSAhX0h2+/PinwZN+fpfu4tswHPk7rWwp/qGf9uJZIO2MLXOBGlC2nAteEFv+quj0ArJ5GWDeTIik4/iAiych2TyE2KgCRmslSvGWw2WWHuBFZLBqwUl0zcplAyu/OWEeIXzkFTxKAwvVOQxW/SWqa2GEkgkbQbL9G15KjwJxlu/Xjh+SogmpAGLMF+uGH9Oa7zUprdAInhJa5pcKM9D56XZ989E+GjE1TlEZGea1LoLr+gtMBvW5xv+iMhkePw9jAUWjpeSim+T+9ExvBsepQxsq84MM+HJ/MI/6xTKc3HXD2PEhOOkvPCNESePF3GdTD62Io86v54ysBs1xslN2mK4nOc1xlgzZbi4Spn/S41MMR7P+leSvOklWvGzg23x25j/kcR/0Y8Xjk8X4f9Ke+GbG14cLma4f8DC4N9xajPyonPKKaecckpR/QPTVzoMS1L/iwAAAABJRU5ErkJggg==");
            RegisterIcon("fader", "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");
            RegisterIcon("moder", "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");
            RegisterIcon("queen", "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");
            RegisterIcon("yagluth", "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");
        }

        private static bool s_initialized;

        public static bool IsActive
        {
            get { return FinalModulesConfig.EnableWeaponAttributes == null || FinalModulesConfig.EnableWeaponAttributes.Value; }
        }

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            BaseDamagePercentPerLevelV2 = config.Bind(
                "02. Weapon Attributes",
                "BaseDamagePercentPerLevelV2",
                7.0f,
                new ConfigDescription("Base enchantment bonus per level (Levels 1..5) as % of weapon combat damage before weapon-archetype scaling (default 7% per level => 7%..35% on standard 1H weapons).", null, adminAttr));
            DualAttributeFactorPerElementV2 = config.Bind(
                "02. Weapon Attributes",
                "DualAttributeFactorPerElementV2",
                0.65f,
                new ConfigDescription("Multiplier applied to each element on dual-attribute late-game Boss Souls (The Queen, Fader). 0.65 = 65% per element (130% combined total vs 100% single-element Boss Souls).", null, adminAttr));
            BaseAttributeDamage = config.Bind(
                "02. Weapon Attributes",
                "BaseAttributeDamage",
                10f,
                new ConfigDescription("[Legacy] Unused in v1.0.6+ dynamic weapon-archetype balance.", null, adminAttr));
            DualAttributeSplit = config.Bind(
                "02. Weapon Attributes",
                "DualAttributeSplit",
                false,
                new ConfigDescription("[Legacy] Replaced by DualAttributeFactorPerElementV2 in v1.0.6+.", null, adminAttr));
        }

        public static void Init(ManualLogSource logger)
        {
            if (s_initialized) return;
            WeaponAttributesLocalization.RegisterAll();
            LoadEmbeddedIcons();
            PrefabManager.OnPrefabsRegistered += CustomEnchantingTable.RegisterCustomTable;
            s_initialized = true;
            if (logger != null)
            {
                logger.LogInfo("[PathOfTheFallen] WeaponAttributes module initialized.");
            }
        }

        public static void Shutdown()
        {
            PrefabManager.OnPrefabsRegistered -= CustomEnchantingTable.RegisterCustomTable;
            s_initialized = false;
        }

        public static string m_upgradingEnchant = null;
        public static string m_upgradingBoss = null;
        public static string m_upgradingLevel = null;

        public static string GetWeaponPrefabName(ItemDrop.ItemData item)
        {
            if (item == null) return "";
            if (item.m_dropPrefab != null && !string.IsNullOrEmpty(item.m_dropPrefab.name))
            {
                string raw = item.m_dropPrefab.name;
                int cloneIdx = raw.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
                return cloneIdx >= 0 ? raw.Substring(0, cloneIdx).Trim() : raw;
            }
            if (item.m_shared != null && !string.IsNullOrEmpty(item.m_shared.m_name))
            {
                string tok = item.m_shared.m_name;
                switch (tok)
                {
                    case "$item_staff_shield": return "StaffShield";
                    case "$item_staff_blackforest": return "StaffBlackForest";
                    case "$item_staff_plains": return "StaffPlains";
                    case "$item_staff_meadows": return "StaffMeadows";
                    case "$item_staff_swamp": return "StaffSwamp";
                    case "$item_staff_mountain": return "StaffMountain";
                    case "$item_staff_skeleton": return "StaffSkeleton";
                    case "$item_staff_redtroll": return "StaffRedTroll";
                    case "$item_staff_greenroots": return "StaffGreenRoots";
                    case "$item_staff_iceshards": return "StaffIceShards";
                    case "$item_staff_lightning": return "StaffLightning";
                    case "$item_staff_clusterbomb": return "StaffClusterbomb";
                    case "$item_staff_fireball": return "StaffFireball";
                    case "$item_axedual_swamp": return "AxeDualSwamp";
                    case "$item_axedual_plains": return "AxeDualPlains";
                    case "$item_knife_butcher": return "KnifeButcher";
                    case "$item_torch": return "Torch";
                    case "$item_torch_mist": return "TorchMist";
                    case "$item_scythe": return "Scythe";
                }
                if (tok.StartsWith("$item_axe_berzerkr", StringComparison.OrdinalIgnoreCase))
                {
                    return "AxeBerzerkr";
                }
            }
            return "";
        }

        public static bool IsPureSummonStaff(ItemDrop.ItemData item)
        {
            string pName = GetWeaponPrefabName(item);
            return string.Equals(pName, "StaffSkeleton", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pName, "StaffRedTroll", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHybridSummonStaff(ItemDrop.ItemData item)
        {
            string pName = GetWeaponPrefabName(item);
            return string.Equals(pName, "StaffGreenRoots", StringComparison.OrdinalIgnoreCase);
        }

        public static float GetRawCombatDamage(ItemDrop.ItemData item, int qualityLevel)
        {
            if (item == null || item.m_shared == null) return 0f;
            int q = qualityLevel > 0 ? qualityLevel : Mathf.Max(1, item.m_quality);
            HitData.DamageTypes d = item.m_shared.m_damages;
            if (q > 1)
            {
                d.Add(item.m_shared.m_damagesPerLevel, q - 1);
            }
            // Sum strictly combat damage types (exclude tree-chop m_chop and rock-mining m_pickaxe!)
            return Mathf.Max(0f, d.m_damage + d.m_blunt + d.m_slash + d.m_pierce + d.m_fire + d.m_frost + d.m_lightning + d.m_poison + d.m_spirit);
        }

        public static bool IsWeapon(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            bool weaponType = type == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
                              type == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                              type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft ||
                              type == ItemDrop.ItemData.ItemType.Bow;
            if (!weaponType) return false;

            // Exclude stackable throwable bombs/consumables, tamed-animal butcher tools, and mining pickaxes
            if (item.m_shared.m_maxStackSize > 1) return false;
            if (item.m_shared.m_tamedOnly) return false;
            if (item.m_shared.m_skillType == Skills.SkillType.Pickaxes) return false;

            string pName = GetWeaponPrefabName(item);
            // Exclude pure non-damaging defensive/healing/totem staves & utility tools
            if (string.Equals(pName, "StaffShield", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "StaffBlackForest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "StaffPlains", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "Torch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "TorchMist", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "Lantern", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "Scythe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "KnifeButcher", StringComparison.OrdinalIgnoreCase) ||
                pName.StartsWith("Tankard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "Sparkler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pName, "DvergrKey", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Allow summoning staves (Dead Raiser, Trollstav, Staff of the Wild) — summons inherit Boss Soul attributes!
            if (IsPureSummonStaff(item) || IsHybridSummonStaff(item))
            {
                return true;
            }

            // Exclude any remaining 0-damage non-combat items
            return GetRawCombatDamage(item, 1) > 0.1f;
        }

        public static string GetBossKeyFromItem(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            if (item.m_dropPrefab != null)
            {
                string key;
                if (BossNameMap.TryGetValue(item.m_dropPrefab.name, out key))
                {
                    return key;
                }
            }
            if (item.m_shared != null && !string.IsNullOrEmpty(item.m_shared.m_name))
            {
                foreach (KeyValuePair<string, string> kvp in BossNameMap)
                {
                    string trophyPrefab = kvp.Key;
                    if (IsItemMatch(item, trophyPrefab))
                    {
                        return kvp.Value;
                    }
                }
            }
            return null;
        }

        public static string[] GetBossAttributesFromItem(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            if (item.m_dropPrefab != null)
            {
                string[] attrs;
                if (BossEnchantMap.TryGetValue(item.m_dropPrefab.name, out attrs))
                {
                    return attrs;
                }
            }
            if (item.m_shared != null && !string.IsNullOrEmpty(item.m_shared.m_name))
            {
                foreach (KeyValuePair<string, string[]> kvp in BossEnchantMap)
                {
                    string trophyPrefab = kvp.Key;
                    if (IsItemMatch(item, trophyPrefab))
                    {
                        return kvp.Value;
                    }
                }
            }
            return null;
        }

        public static bool IsItemMatch(ItemDrop.ItemData item, string prefabName)
        {
            if (item == null || string.IsNullOrEmpty(prefabName)) return false;
            if (item.m_dropPrefab != null && string.Equals(item.m_dropPrefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (ObjectDB.instance != null)
            {
                GameObject prefabObj = ObjectDB.instance.GetItemPrefab(prefabName);
                if (prefabObj != null)
                {
                    ItemDrop id = prefabObj.GetComponent<ItemDrop>();
                    if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null && item.m_shared != null)
                    {
                        if (string.Equals(item.m_shared.m_name, id.m_itemData.m_shared.m_name, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static int CountInventoryItem(Inventory inv, string prefabName)
        {
            if (inv == null || string.IsNullOrEmpty(prefabName)) return 0;
            int total = 0;
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData it = all[i];
                if (it != null && IsItemMatch(it, prefabName))
                {
                    total += it.m_stack;
                }
            }
            return total;
        }

        public static ItemDrop.ItemData FindInventoryItem(Inventory inv, string prefabName)
        {
            if (inv == null || string.IsNullOrEmpty(prefabName)) return null;
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData it = all[i];
                if (it != null && IsItemMatch(it, prefabName))
                {
                    return it;
                }
            }
            return null;
        }

        public static int RemoveInventoryItem(Inventory inv, string prefabName, int amount)
        {
            if (inv == null || string.IsNullOrEmpty(prefabName) || amount <= 0) return 0;
            int left = amount;
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = all.Count - 1; i >= 0 && left > 0; i--)
            {
                if (i >= all.Count) continue;
                ItemDrop.ItemData it = all[i];
                if (it != null && IsItemMatch(it, prefabName))
                {
                    int take = Mathf.Min(it.m_stack, left);
                    inv.RemoveItem(it, take);
                    left -= take;
                }
            }
            return amount - left;
        }

        public static string GetAttributeEnchantString(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return null;
            string val;
            if (item.m_customData.TryGetValue(CUSTOM_DATA_KEY, out val))
            {
                return val;
            }
            return null;
        }

        public static string GetEnchantBossKey(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return null;
            string val;
            if (item.m_customData.TryGetValue(CUSTOM_DATA_BOSS_KEY, out val))
            {
                return val;
            }
            return null;
        }

        public static int GetEnchantLevel(ItemDrop.ItemData item)
        {
            if (item == null || item.m_customData == null) return 0;
            string lvlStr;
            if (item.m_customData.TryGetValue(CUSTOM_DATA_LEVEL_KEY, out lvlStr))
            {
                int lvl;
                if (int.TryParse(lvlStr, out lvl))
                {
                    return Mathf.Clamp(lvl, 1, 5);
                }
            }
            if (!string.IsNullOrEmpty(GetAttributeEnchantString(item)))
            {
                return 1;
            }
            return 0;
        }

        public static int GetMobTrophyCountForLevel(int targetLevel)
        {
            switch (targetLevel)
            {
                case 1: return 1;
                case 2: return 5;
                case 3: return 10;
                case 4: return 15;
                case 5: return 20;
                default: return 1;
            }
        }

        public static string GetMobTrophyPrefab(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return null;
            string mobPrefab;
            if (BossMobTrophyMap.TryGetValue(bossKey, out mobPrefab))
            {
                return mobPrefab;
            }
            return null;
        }

        public static void SetAttributeEnchant(ItemDrop.ItemData item, string bossKey, string[] attrs, int level)
        {
            if (item == null || attrs == null || attrs.Length == 0) return;
            if (item.m_customData == null)
            {
                item.m_customData = new Dictionary<string, string>();
            }
            item.m_customData[CUSTOM_DATA_KEY] = string.Join(",", attrs);
            if (!string.IsNullOrEmpty(bossKey))
            {
                item.m_customData[CUSTOM_DATA_BOSS_KEY] = bossKey;
            }
            item.m_customData[CUSTOM_DATA_LEVEL_KEY] = Mathf.Clamp(level, 1, 5).ToString();
        }

        public static float GetWeaponArchetypeFactor(ItemDrop.ItemData item, out float fixedPerLevelBase, out float minEffectiveBaseDamage)
        {
            fixedPerLevelBase = 0f;
            minEffectiveBaseDamage = 0f;
            if (item == null || item.m_shared == null) return 1.0f;

            string pName = GetWeaponPrefabName(item);

            // 1. Summoning staves (fixed balanced bonus per minion hit)
            if (string.Equals(pName, "StaffSkeleton", StringComparison.OrdinalIgnoreCase))
            {
                fixedPerLevelBase = 3.0f; // +3 / +6 / +9 / +12 / +15 per skeleton attack (up to 4 skeletons)
                return 1.0f;
            }
            if (string.Equals(pName, "StaffRedTroll", StringComparison.OrdinalIgnoreCase))
            {
                fixedPerLevelBase = 9.0f; // +9 / +18 / +27 / +36 / +45 on meteor & Summoned Troll slams
                return 1.0f;
            }
            if (string.Equals(pName, "StaffGreenRoots", StringComparison.OrdinalIgnoreCase))
            {
                fixedPerLevelBase = 4.0f; // +4 / +8 / +12 / +16 / +20 on seed impact & Root Tentacle strikes
                return 1.0f;
            }

            // 2. Custom Magic Staves (`MagicStavesModule`)
            if (string.Equals(pName, "StaffMountain", StringComparison.OrdinalIgnoreCase))
            {
                // 3.0s-4.5s immobile hold-charge sniper railgun (150/300/450/600 Lightning)
                minEffectiveBaseDamage = 220f;
                return 1.05f; // -> +16..+81 (Q1), +22..+110 (Q2), +33..+165 (Q3), +44..+220 (Q4)
            }
            if (string.Equals(pName, "StaffSwamp", StringComparison.OrdinalIgnoreCase))
            {
                // 8s resistance-stripping AoE cloud ticking 5 times (every 2.0s; 10/20/30/40 base per tick)
                return 1.00f; // -> +1..+3.5 (Q1), +1.5..+7 (Q2), +2..+10.5 (Q3), +3..+14 (Q4) per tick
            }
            if (string.Equals(pName, "StaffMeadows", StringComparison.OrdinalIgnoreCase))
            {
                // Piercing Wind Blade (25/40/55/70 Slash)
                return 1.00f; // -> +2..+9 (Q1) up to +5..+25 (Q4)
            }

            // 3. Vanilla Multi-Pellet / Rapid-Burst / AoE Staves
            if (string.Equals(pName, "StaffLightning", StringComparison.OrdinalIgnoreCase))
            {
                // Dundr: 12 shotgun pellets per shot, each pellet calls GetDamage()!
                return 0.80f; // -> +1..+5.5 (Q1) / +1.5..+7.5 (Q4) per pellet = +18..+90 full shotgun blast
            }
            if (string.Equals(pName, "StaffIceShards", StringComparison.OrdinalIgnoreCase))
            {
                // Staff of Frost: rapid machine-gun shards (~4.5 shots/sec)
                return 0.80f; // -> +1.5..+8.5 (Q1) / +2.5..+13.5 (Q4) per shard
            }
            if (string.Equals(pName, "StaffClusterbomb", StringComparison.OrdinalIgnoreCase))
            {
                // Staff of Fracturing: splits into 12 cluster bomblets
                return 0.75f; // -> +1.5..+8 (Q1) / +2..+9.5 (Q4) per bomblet
            }
            if (string.Equals(pName, "StaffFireball", StringComparison.OrdinalIgnoreCase))
            {
                // Staff of Embers: wide AoE explosion (120 -> 138 base)
                return 0.85f; // -> +7..+36 (Q1) / +8..+41 (Q4)
            }

            // 4. Dual-Wield Axes (vanilla Berzerkr + gem variants + modded Swamp/Plains Twin Axes)
            if (pName.StartsWith("AxeBerzerkr", StringComparison.OrdinalIgnoreCase) ||
                pName.StartsWith("AxeDual", StringComparison.OrdinalIgnoreCase) ||
                pName.StartsWith("AxeEarly", StringComparison.OrdinalIgnoreCase))
            {
                return 0.80f;
            }

            // 5. Standard & Heavy Weapon Skill Archetypes
            Skills.SkillType skill = item.m_shared.m_skillType;
            bool isTwoHanded = item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                               item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;

            if (skill == Skills.SkillType.Knives || skill == Skills.SkillType.Unarmed)
            {
                // Very fast combos + 3x-6x backstab / 3x leap attacks (includes Skoll & Hati, Flesh Rippers)
                return 0.75f;
            }
            if (skill == Skills.SkillType.Crossbows)
            {
                // 3.5s slow reload single heavy bolt (Arbalest, Ripper)
                return 1.35f;
            }
            if (skill == Skills.SkillType.Bows)
            {
                // Compensates for ~45% of bow total base damage residing on Arrows rather than the Bow itself
                return 1.35f;
            }
            if (skill == Skills.SkillType.Polearms)
            {
                // Atgeirs (reach + 360° AoE spin secondary)
                return 0.95f;
            }
            if (isTwoHanded && (skill == Skills.SkillType.Swords || skill == Skills.SkillType.Axes))
            {
                // Slow, high-commitment Two-Handed Greatswords (Krom, Slayer) & Two-Handed Battleaxes
                return 1.25f;
            }
            if (isTwoHanded && skill == Skills.SkillType.Clubs)
            {
                // Slow ground-slam Two-Handed Sledgehammers (Stagbreaker, Iron Sledge, Demolisher)
                return 1.20f;
            }
            if (skill == Skills.SkillType.ElementalMagic || skill == Skills.SkillType.BloodMagic)
            {
                return 0.90f;
            }

            // Standard 1H Swords, 1H Maces/Clubs, 1H Axes, Spears
            return 1.00f;
        }

        public static float GetEnchantDamagePerAttribute(ItemDrop.ItemData item, string[] attrs, int level, int qualityLevel)
        {
            if (item == null || item.m_shared == null || attrs == null || attrs.Length == 0) return 0f;

            int clampedLevel = Mathf.Clamp(level, 1, 5);
            float cfgPct = (BaseDamagePercentPerLevelV2 != null ? Mathf.Max(0.5f, BaseDamagePercentPerLevelV2.Value) : 7.0f) * 0.01f;
            float configScale = cfgPct / 0.07f;
            float dualFactor = attrs.Length > 1
                ? (DualAttributeFactorPerElementV2 != null ? Mathf.Clamp(DualAttributeFactorPerElementV2.Value, 0.25f, 1.5f) : 0.65f)
                : 1.0f;

            float fixedPerLevel;
            float minEffectiveBase;
            float archetypeFactor = GetWeaponArchetypeFactor(item, out fixedPerLevel, out minEffectiveBase);

            float rawSingleAttr;
            if (fixedPerLevel > 0f)
            {
                rawSingleAttr = fixedPerLevel * (float)clampedLevel * configScale;
            }
            else
            {
                float combatBase = Mathf.Max(minEffectiveBase, GetRawCombatDamage(item, qualityLevel));
                if (combatBase <= 0.1f) return 0f;
                rawSingleAttr = combatBase * cfgPct * archetypeFactor * (float)clampedLevel;
            }

            float rawPerAttr = rawSingleAttr * dualFactor;

            // Round >= 10 to clean whole integers, and < 10 to crisp 0.5 steps so every upgrade level (1..5) visibly advances
            float rounded;
            if (rawPerAttr >= 10f)
            {
                rounded = Mathf.Round(rawPerAttr);
            }
            else
            {
                rounded = Mathf.Round(rawPerAttr * 2f) * 0.5f;
                float minFloor = 0.5f * (float)clampedLevel;
                if (rounded < minFloor)
                {
                    rounded = minFloor;
                }
            }
            return rounded;
        }

        public static string FormatEnchantValue(float val)
        {
            if (Mathf.Abs(val - Mathf.Round(val)) < 0.05f)
            {
                return Mathf.RoundToInt(val).ToString();
            }
            return val.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static void AddEnchantAttributesToDamageTypes(string[] attrs, float perAttr, ref HitData.DamageTypes dmg)
        {
            if (attrs == null || perAttr <= 0f) return;
            for (int i = 0; i < attrs.Length; i++)
            {
                string attr = attrs[i].Trim().ToLower();
                switch (attr)
                {
                    case "lightning":
                        dmg.m_lightning += perAttr;
                        break;
                    case "poison":
                        dmg.m_poison += perAttr;
                        break;
                    case "frost":
                        dmg.m_frost += perAttr;
                        break;
                    case "fire":
                        dmg.m_fire += perAttr;
                        break;
                    case "spirit":
                        dmg.m_spirit += perAttr;
                        break;
                    case "physical":
                        dmg.m_damage += perAttr;
                        break;
                }
            }
        }

        public static void ApplyEnchantDamage(ItemDrop.ItemData item, ref HitData.DamageTypes dmg)
        {
            ApplyEnchantDamage(item, -1, ref dmg);
        }

        public static void ApplyEnchantDamage(ItemDrop.ItemData item, int qualityLevel, ref HitData.DamageTypes dmg)
        {
            if (!IsActive || item == null) return;
            // Pure 0-damage summon staves (Dead Raiser, Trollstav) imbue their spawned minions/meteor rather than direct staff swings
            if (IsPureSummonStaff(item)) return;

            string enchantStr = GetAttributeEnchantString(item);
            if (string.IsNullOrEmpty(enchantStr)) return;

            string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (attrs.Length == 0) return;

            int level = Mathf.Clamp(GetEnchantLevel(item), 1, 5);
            float perAttr = GetEnchantDamagePerAttribute(item, attrs, level, qualityLevel);
            AddEnchantAttributesToDamageTypes(attrs, perAttr, ref dmg);
        }

        private static readonly FieldInfo s_charNViewField = typeof(Character).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        internal static ZNetView GetCharacterNetView(Character ch)
        {
            if (ch == null) return null;
            if (s_charNViewField != null)
            {
                ZNetView nv = s_charNViewField.GetValue(ch) as ZNetView;
                if (nv != null) return nv;
            }
            return ch.GetComponent<ZNetView>();
        }

        public static void TryImbueSummonedCharacter(Character spawnedChar, ItemDrop.ItemData sourceWeapon)
        {
            if (!IsActive || spawnedChar == null || sourceWeapon == null) return;
            string enchantStr = GetAttributeEnchantString(sourceWeapon);
            if (string.IsNullOrEmpty(enchantStr)) return;

            string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (attrs.Length == 0) return;

            int level = Mathf.Clamp(GetEnchantLevel(sourceWeapon), 1, 5);
            float perAttr = GetEnchantDamagePerAttribute(sourceWeapon, attrs, level, sourceWeapon.m_quality);
            if (perAttr <= 0f) return;

            ZNetView nv = GetCharacterNetView(spawnedChar);
            if (nv == null || !nv.IsValid()) return;
            ZDO zdo = nv.GetZDO();
            if (zdo == null) return;

            zdo.Set(ZDO_MINION_ATTRS, enchantStr);
            zdo.Set(ZDO_MINION_DMG, perAttr);
            string pName = GetWeaponPrefabName(sourceWeapon);
            if (string.Equals(pName, "StaffRedTroll", StringComparison.OrdinalIgnoreCase))
            {
                zdo.Set(ZDO_MINION_NOPVP, true);
            }
        }

        public static string GetAttributeNameLocalized(string attr)
        {
            switch (attr.Trim().ToLower())
            {
                case "lightning":
                    return Localization.instance.Localize("$inventory_lightning");
                case "poison":
                    return Localization.instance.Localize("$inventory_poison");
                case "frost":
                    return Localization.instance.Localize("$inventory_frost");
                case "fire":
                    return Localization.instance.Localize("$inventory_fire");
                case "spirit":
                    return Localization.instance.Localize("$inventory_spirit");
                case "physical":
                    return Localization.instance.Localize("$inventory_damage");
                default:
                    return attr;
            }
        }

        public static string GetBossEnchantColor(string bossKey, string enchantStr)
        {
            string key = bossKey != null ? bossKey.Trim().ToLower() : "";

            if (string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(enchantStr))
            {
                string lower = enchantStr.ToLower();
                if (lower.Contains("fire") && lower.Contains("spirit")) key = "fader";
                else if (lower.Contains("poison") && lower.Contains("lightning")) key = "queen";
                else if (lower.Contains("lightning")) key = "eikthyr";
                else if (lower.Contains("physical")) key = "elder";
                else if (lower.Contains("poison")) key = "bonemass";
                else if (lower.Contains("frost")) key = "moder";
                else if (lower.Contains("fire")) key = "yagluth";
            }

            switch (key)
            {
                case "eikthyr":
                    // Eikthyr - Yellow (#FFD700)
                    return "#FFD700";

                case "elder":
                    // Elder - Darker Gray (#707070)
                    return "#707070";

                case "bonemass":
                    // Bonemass - Bright Green, but soft (#48D858)
                    return "#48D858";

                case "moder":
                    // Moder - Deep Blue (#1E90FF)
                    return "#1E90FF";

                case "yagluth":
                    // Yagluth - Red (#FF453A)
                    return "#FF453A";

                case "queen":
                    // Queen - Purple (#BA55D3)
                    return "#BA55D3";

                case "fader":
                    // Emerald flame - Brown / SaddleBrown (#8B4513)
                    return "#8B4513";

                default:
                    return null;
            }
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetDamage", new Type[] { typeof(int), typeof(float) })]
    public static class Patch_ItemData_GetDamage
    {
        public static void Postfix(ItemDrop.ItemData __instance, int quality, ref HitData.DamageTypes __result)
        {
            WeaponAttributesPlugin.ApplyEnchantDamage(__instance, quality, ref __result);
        }
    }

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    public static class Patch_ItemData_GetTooltip
    {
        public static void Postfix(ItemDrop.ItemData item, int qualityLevel, ref string __result)
        {
            if (item == null || !WeaponAttributesPlugin.IsActive) return;
            string enchantStr = WeaponAttributesPlugin.GetAttributeEnchantString(item);
            if (string.IsNullOrEmpty(enchantStr)) return;

            string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (attrs.Length == 0) return;

            int enchantLevel = Mathf.Clamp(WeaponAttributesPlugin.GetEnchantLevel(item), 1, 5);
            float perAttr = WeaponAttributesPlugin.GetEnchantDamagePerAttribute(item, attrs, enchantLevel, qualityLevel);
            if (perAttr <= 0f) return;

            string suffix = "";
            if (WeaponAttributesPlugin.IsPureSummonStaff(item))
            {
                suffix = WeaponAttributesLocalization.Loc("$wa_summon_suffix");
            }
            else if (WeaponAttributesPlugin.IsHybridSummonStaff(item))
            {
                suffix = WeaponAttributesLocalization.Loc("$wa_roots_suffix");
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("\n<color=#A020F0>");
            sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_tooltip_header"), enchantLevel));
            sb.Append("</color>");
            string formattedVal = WeaponAttributesPlugin.FormatEnchantValue(perAttr);
            for (int i = 0; i < attrs.Length; i++)
            {
                string attrName = WeaponAttributesPlugin.GetAttributeNameLocalized(attrs[i]);
                sb.Append(string.Format("\n  {0}{1}: <color=#00FFFF>+{2}</color>", attrName, suffix, formattedVal));
            }

            // Append at the very bottom (below movement speed modifier, etc.)
            __result += sb.ToString();
        }
    }

    [HarmonyPatch(typeof(SpawnAbility), "SetupAoe", new Type[] { typeof(Character), typeof(Vector3) })]
    public static class Patch_SpawnAbility_SetupAoe
    {
        private static readonly FieldInfo s_weaponField = typeof(SpawnAbility).GetField("m_weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static void Postfix(SpawnAbility __instance, Character owner)
        {
            if (__instance == null || owner == null || s_weaponField == null || !WeaponAttributesPlugin.IsActive) return;
            ItemDrop.ItemData weapon = s_weaponField.GetValue(__instance) as ItemDrop.ItemData;
            if (weapon != null)
            {
                WeaponAttributesPlugin.TryImbueSummonedCharacter(owner, weapon);
            }
        }
    }

    [HarmonyPatch(typeof(SpawnAbility), "SetupProjectile", new Type[] { typeof(Projectile), typeof(Vector3) })]
    public static class Patch_SpawnAbility_SetupProjectile
    {
        private static readonly FieldInfo s_spawnWeaponField = typeof(SpawnAbility).GetField("m_weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_projWeaponField = typeof(Projectile).GetField("m_weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static void Postfix(SpawnAbility __instance, Projectile projectile)
        {
            if (__instance == null || projectile == null || s_spawnWeaponField == null || !WeaponAttributesPlugin.IsActive) return;
            ItemDrop.ItemData weapon = s_spawnWeaponField.GetValue(__instance) as ItemDrop.ItemData;
            if (weapon == null) return;

            if (s_projWeaponField != null)
            {
                s_projWeaponField.SetValue(projectile, weapon);
            }

            string enchantStr = WeaponAttributesPlugin.GetAttributeEnchantString(weapon);
            if (!string.IsNullOrEmpty(enchantStr))
            {
                string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int level = Mathf.Clamp(WeaponAttributesPlugin.GetEnchantLevel(weapon), 1, 5);
                float perAttr = WeaponAttributesPlugin.GetEnchantDamagePerAttribute(weapon, attrs, level, weapon.m_quality);
                WeaponAttributesPlugin.AddEnchantAttributesToDamageTypes(attrs, perAttr, ref projectile.m_damage);
            }
        }
    }

    [HarmonyPatch(typeof(Aoe), "Setup", new Type[] { typeof(Character), typeof(Vector3), typeof(float), typeof(HitData), typeof(ItemDrop.ItemData), typeof(ItemDrop.ItemData) })]
    public static class Patch_Aoe_Setup_SummonMeteor
    {
        public static void Postfix(Aoe __instance, HitData hitData, ItemDrop.ItemData item)
        {
            if (__instance == null || hitData != null || item == null || !WeaponAttributesPlugin.IsActive) return;
            if (!WeaponAttributesPlugin.IsPureSummonStaff(item)) return;

            string enchantStr = WeaponAttributesPlugin.GetAttributeEnchantString(item);
            if (string.IsNullOrEmpty(enchantStr)) return;

            string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int level = Mathf.Clamp(WeaponAttributesPlugin.GetEnchantLevel(item), 1, 5);
            float perAttr = WeaponAttributesPlugin.GetEnchantDamagePerAttribute(item, attrs, level, item.m_quality);
            WeaponAttributesPlugin.AddEnchantAttributesToDamageTypes(attrs, perAttr, ref __instance.m_damage);
        }
    }

    [HarmonyPatch(typeof(Projectile), "SpawnOnHit", new Type[] { typeof(GameObject), typeof(Collider), typeof(Vector3) })]
    public static class Patch_Projectile_SpawnOnHit_SummonContext
    {
        private static readonly FieldInfo s_projWeaponField = typeof(Projectile).GetField("m_weapon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static void Prefix(Projectile __instance)
        {
            if (__instance != null && s_projWeaponField != null && WeaponAttributesPlugin.IsActive)
            {
                WeaponAttributesPlugin.s_activeProjectileSpawnWeapon = s_projWeaponField.GetValue(__instance) as ItemDrop.ItemData;
            }
        }

        public static void Postfix()
        {
            WeaponAttributesPlugin.s_activeProjectileSpawnWeapon = null;
        }
    }

    [HarmonyPatch(typeof(Character), "Awake", new Type[0])]
    public static class Patch_Character_Awake_SummonImbue
    {
        public static void Postfix(Character __instance)
        {
            if (__instance != null && WeaponAttributesPlugin.s_activeProjectileSpawnWeapon != null && WeaponAttributesPlugin.IsActive)
            {
                WeaponAttributesPlugin.TryImbueSummonedCharacter(__instance, WeaponAttributesPlugin.s_activeProjectileSpawnWeapon);
            }
        }
    }

    [HarmonyPatch(typeof(Character), "Damage", new Type[] { typeof(HitData) })]
    public static class Patch_Character_Damage_SummonMinionEnchant
    {
        public static void Prefix(Character __instance, HitData hit)
        {
            if (!WeaponAttributesPlugin.IsActive || __instance == null || hit == null) return;
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer()) return;

            ZNetView nv = WeaponAttributesPlugin.GetCharacterNetView(attacker);
            if (nv == null || !nv.IsValid()) return;
            ZDO zdo = nv.GetZDO();
            if (zdo == null) return;

            float perAttr = zdo.GetFloat(WeaponAttributesPlugin.ZDO_MINION_DMG, 0f);
            if (perAttr <= 0f) return;

            if (zdo.GetBool(WeaponAttributesPlugin.ZDO_MINION_NOPVP, false) && __instance.IsPlayer())
            {
                return;
            }

            string enchantStr = zdo.GetString(WeaponAttributesPlugin.ZDO_MINION_ATTRS, "");
            if (string.IsNullOrEmpty(enchantStr)) return;

            string[] attrs = enchantStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            WeaponAttributesPlugin.AddEnchantAttributesToDamageTypes(attrs, perAttr, ref hit.m_damage);
        }
    }

    // Visually colors the weapon quality number according to enchanted boss attribute
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    public static class Patch_InventoryGrid_UpdateGui
    {
        private static FieldInfo m_elementsField = typeof(InventoryGrid).GetField("m_elements", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_qualityField = typeof(InventoryElement).GetField("m_quality", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo m_iconField = typeof(InventoryElement).GetField("m_icon", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private const string OLD_BADGE_NAME = "WeaponAttributes_BossBadge";

        public static void Postfix(InventoryGrid __instance, Player player, ItemDrop.ItemData dragItem)
        {
            if (__instance == null || m_elementsField == null || m_qualityField == null) return;

            Inventory inv = __instance.GetInventory();
            if (inv == null) return;

            IList elements = m_elementsField.GetValue(__instance) as IList;
            if (elements == null) return;

            int width = inv.GetWidth();
            if (width <= 0) return;

            for (int i = 0; i < elements.Count; i++)
            {
                object elem = elements[i];
                if (elem == null) continue;

                TMP_Text qualityText = m_qualityField.GetValue(elem) as TMP_Text;

                // Remove legacy badge object if it exists on the icon
                if (m_iconField != null)
                {
                    Image iconImg = m_iconField.GetValue(elem) as Image;
                    if (iconImg != null)
                    {
                        Transform oldBadge = iconImg.transform.Find(OLD_BADGE_NAME);
                        if (oldBadge != null)
                        {
                            GameObject.Destroy(oldBadge.gameObject);
                        }
                    }
                }

                int x = i % width;
                int y = i / width;
                ItemDrop.ItemData itemAt = inv.GetItemAt(x, y);

                if (itemAt != null && WeaponAttributesPlugin.IsWeapon(itemAt))
                {
                    string enchant = WeaponAttributesPlugin.GetAttributeEnchantString(itemAt);
                    if (!string.IsNullOrEmpty(enchant) && qualityText != null)
                    {
                        string bossKey = WeaponAttributesPlugin.GetEnchantBossKey(itemAt);
                        string colorHex = WeaponAttributesPlugin.GetBossEnchantColor(bossKey, enchant);

                        if (!string.IsNullOrEmpty(colorHex))
                        {
                            int quality = itemAt.m_quality;
                            int enchantLevel = WeaponAttributesPlugin.GetEnchantLevel(itemAt);
                            qualityText.gameObject.SetActive(true);
                            qualityText.enabled = true;
                            qualityText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                            qualityText.overflowMode = TMPro.TextOverflowModes.Overflow;
                            qualityText.text = string.Format("{0}<color={1}>+{2}</color>", quality, colorHex, enchantLevel);
                        }
                    }
                }
            }
        }
    }

    public static class CustomEnchantingTable
    {
        public static GameObject m_enchantingTablePrefab = null;
        public static Sprite m_tableIcon = null;

        public static void RegisterCustomTable()
        {
            try
            {
                if (!WeaponAttributesPlugin.IsActive) return;
                if (m_enchantingTablePrefab != null) return;

                // Clone piece_artisanstation or piece_workbench safely using Jotunn
                GameObject custom = PrefabManager.Instance.CreateClonedPrefab("piece_enchanting_table", "piece_artisanstation");
                if (custom == null)
                {
                    custom = PrefabManager.Instance.CreateClonedPrefab("piece_enchanting_table", "piece_workbench");
                }
                if (custom == null)
                {
                    Debug.LogWarning("[WeaponAttributes] Could not clone base table for piece_enchanting_table");
                    return;
                }

                // Capture authentic Valheim shader for reference, and also get Unity's built-in Standard shader
                // IMPORTANT: Valheim's 'Custom/Piece' shader has a vertex-shader wind/sway/damage displacement that reads UV2 and Vertex Color!
                // If a custom mesh does not have Valheim's proprietary vertex color & UV2 encoding, 'Custom/Piece' distorts vertices into huge spikes!
                // Using Unity's Standard shader (or zeroing vertex colors + UV2) prevents any vertex displacement spikes!
                Shader standardShader = Shader.Find("Standard");
                if (standardShader == null)
                {
                    Renderer anyBaseRenderer = custom.GetComponentInChildren<Renderer>(true);
                    standardShader = (anyBaseRenderer != null && anyBaseRenderer.sharedMaterial != null)
                        ? anyBaseRenderer.sharedMaterial.shader
                        : null;
                }

                // Adjust solid physical colliders WITHOUT destroying Trigger colliders (which EffectArea needs in Awake!)
                int pieceLayer = LayerMask.NameToLayer("piece");
                if (pieceLayer < 0) pieceLayer = custom.layer;

                foreach (Collider col in custom.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;
                    // Keep trigger colliders (like EffectArea / warm area / station range) intact!
                    if (col.isTrigger || col.GetComponent<EffectArea>() != null)
                    {
                        continue;
                    }
                    pieceLayer = col.gameObject.layer;
                    // Disable old Artisan Table physical colliders so the player doesn't bump into invisible walls
                    col.enabled = false;
                }

                // Remove _GhostOnly from cloned Artisan Table so placement ghost never activates Artisan Table visuals
                Transform ghostOnly = custom.transform.Find("_GhostOnly");
                if (ghostOnly != null)
                {
                    UnityEngine.Object.DestroyImmediate(ghostOnly.gameObject);
                }

                // Hide original Artisan Table visual meshes & clear MeshFilters without destroying components that Piece/WearNTear might reference
                foreach (Renderer rnd in custom.GetComponentsInChildren<Renderer>(true))
                {
                    if (rnd != null && rnd.gameObject != custom)
                    {
                        rnd.enabled = false;
                        MeshFilter mf = rnd.GetComponent<MeshFilter>();
                        if (mf != null)
                        {
                            mf.sharedMesh = null;
                        }
                    }
                }

                // Load custom 128x128 icon from embedded resource first so BOTH CraftingStation.m_icon AND Piece.m_icon get it!
                Sprite customIcon = LoadEmbeddedSprite("BossSoulTableIcon.png");
                if (customIcon != null)
                {
                    m_tableIcon = customIcon;
                }

                // Setup CraftingStation & WearNTear references (including m_icon used inside the Station UI & Hammer required-station slot!)
                CraftingStation station = custom.GetComponent<CraftingStation>();
                if (station != null)
                {
                    station.m_name = "$wa_piece_table";
                    station.m_rangeBuild = 10f;
                    station.m_useDistance = 2.8f;
                    if (customIcon != null)
                    {
                        station.m_icon = customIcon;
                    }
                }

                Piece basePiece = custom.GetComponent<Piece>();
                if (basePiece != null && customIcon != null)
                {
                    basePiece.m_icon = customIcon;
                }

                // Load our custom Blender 3D Model ("Boss Soul Enchanting Table"), colliders, and purple glow effects
                BuildCustomBlenderModel(custom, standardShader, pieceLayer);

                // Configure piece in Jotunn
                PieceConfig config = new PieceConfig();
                config.PieceTable = "Hammer";
                config.Category = "Crafting";
                config.Name = "$wa_piece_table";
                config.Description = "$wa_piece_table_desc";
                if (customIcon != null)
                {
                    config.Icon = customIcon;
                }
                config.AddRequirement(new RequirementConfig("RoundLog", 8, 0, true));
                config.AddRequirement(new RequirementConfig("Tin", 10, 0, true));
                config.AddRequirement(new RequirementConfig("SurtlingCore", 4, 0, true));
                config.AddRequirement(new RequirementConfig("Stone", 4, 0, true));

                CustomPiece cp = new CustomPiece(custom, false, config);
                PieceManager.Instance.AddPiece(cp);

                m_enchantingTablePrefab = custom;
                Debug.Log("[WeaponAttributes] Successfully registered piece_enchanting_table with custom 3D model via Jotunn!");

                // Register 4 Boss-Themed Station Extensions (Levels 2, 3, 4, 5)
                if (station != null)
                {
                    RegisterStationExtensions(station, standardShader, pieceLayer);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[WeaponAttributes] Exception in RegisterCustomTable: " + ex);
            }
        }

        private static void RegisterStationExtensions(CraftingStation targetStation, Shader baseShader, int pieceLayer)
        {
            // 1. Extension 1 (Level 2 — The Elder, Bronze Tier)
            RegisterSingleExtension(
                "piece_enchant_ext_elder",
                "$wa_ext_elder",
                "$wa_ext_elder_desc",
                "Ext1_ElderMesh.bin.gz",
                "Ext1_ElderIcon.png",
                targetStation,
                baseShader,
                pieceLayer,
                new Color(0.18f, 0.95f, 0.52f, 1f),
                new Vector3(0f, 1.05f, 0f),
                new Vector3(0.95f, 1.35f, 0.95f),
                new RequirementConfig[] {
                    new RequirementConfig("Bronze", 6, 0, true),
                    new RequirementConfig("AncientSeed", 3, 0, true),
                    new RequirementConfig("GreydwarfEye", 10, 0, true),
                    new RequirementConfig("Wood", 10, 0, true)
                }
            );

            // 2. Extension 2 (Level 3 — Bonemass, Iron Tier)
            RegisterSingleExtension(
                "piece_enchant_ext_bonemass",
                "$wa_ext_bonemass",
                "$wa_ext_bonemass_desc",
                "Ext2_BonemassMesh.bin.gz",
                "Ext2_BonemassIcon.png",
                targetStation,
                baseShader,
                pieceLayer,
                new Color(0.42f, 1.0f, 0.12f, 1f),
                new Vector3(0f, 0.95f, 0f),
                new Vector3(1.05f, 1.15f, 1.05f),
                new RequirementConfig[] {
                    new RequirementConfig("Iron", 10, 0, true),
                    new RequirementConfig("ElderBark", 10, 0, true),
                    new RequirementConfig("Ooze", 10, 0, true),
                    new RequirementConfig("BoneFragments", 10, 0, true)
                }
            );

            // 3. Extension 3 (Level 4 — Moder, Silver Tier)
            RegisterSingleExtension(
                "piece_enchant_ext_moder",
                "$wa_ext_moder",
                "$wa_ext_moder_desc",
                "Ext3_ModerMesh.bin.gz",
                "Ext3_ModerIcon.png",
                targetStation,
                baseShader,
                pieceLayer,
                new Color(0.22f, 0.85f, 1.0f, 1f),
                new Vector3(0f, 0.95f, 0f),
                new Vector3(1.05f, 1.25f, 1.05f),
                new RequirementConfig[] {
                    new RequirementConfig("Silver", 10, 0, true),
                    new RequirementConfig("Crystal", 8, 0, true),
                    new RequirementConfig("FreezeGland", 4, 0, true),
                    new RequirementConfig("Obsidian", 10, 0, true)
                }
            );

            // 4. Extension 4 (Level 5 — Yagluth, Black Metal Tier)
            RegisterSingleExtension(
                "piece_enchant_ext_yagluth",
                "$wa_ext_yagluth",
                "$wa_ext_yagluth_desc",
                "Ext4_YagluthMesh.bin.gz",
                "Ext4_YagluthIcon.png",
                targetStation,
                baseShader,
                pieceLayer,
                new Color(0.95f, 0.18f, 0.62f, 1f),
                new Vector3(0f, 0.95f, 0f),
                new Vector3(1.15f, 1.15f, 1.15f),
                new RequirementConfig[] {
                    new RequirementConfig("BlackMetal", 10, 0, true),
                    new RequirementConfig("GoblinTotem", 2, 0, true),
                    new RequirementConfig("Tar", 10, 0, true),
                    new RequirementConfig("SurtlingCore", 5, 0, true)
                }
            );
        }

        private static void RegisterSingleExtension(
            string prefabName,
            string displayName,
            string description,
            string meshResName,
            string iconResName,
            CraftingStation targetStation,
            Shader baseShader,
            int pieceLayer,
            Color lightColor,
            Vector3 lightLocalPos,
            Vector3 colliderSize,
            RequirementConfig[] reqs)
        {
            try
            {
                GameObject extObj = PrefabManager.Instance.CreateClonedPrefab(prefabName, "piece_workbench_ext1");
                if (extObj == null)
                {
                    extObj = PrefabManager.Instance.CreateClonedPrefab(prefabName, "forge_ext1");
                }
                if (extObj == null)
                {
                    Debug.LogWarning("[WeaponAttributes] Could not clone base extension prefab for " + prefabName);
                    return;
                }

                // Disable old solid colliders while preserving triggers/EffectArea
                foreach (Collider col in extObj.GetComponentsInChildren<Collider>(true))
                {
                    if (col == null) continue;
                    if (col.isTrigger || col.GetComponent<EffectArea>() != null) continue;
                    col.enabled = false;
                }

                // Hide original chopping block / bellows visual renderers
                foreach (Renderer rnd in extObj.GetComponentsInChildren<Renderer>(true))
                {
                    if (rnd != null && rnd.gameObject != extObj)
                    {
                        rnd.enabled = false;
                    }
                }

                // Configure StationExtension component to link to our Enchanting Table
                StationExtension stationExt = extObj.GetComponent<StationExtension>();
                if (stationExt == null)
                {
                    stationExt = extObj.AddComponent<StationExtension>();
                }
                stationExt.m_craftingStation = targetStation;
                stationExt.m_maxStationDistance = 6f;

                // Create custom model root
                GameObject modelRoot = new GameObject(prefabName + "_Model");
                modelRoot.layer = pieceLayer;
                modelRoot.transform.SetParent(extObj.transform, false);
                modelRoot.transform.localPosition = Vector3.zero;
                modelRoot.transform.localRotation = Quaternion.identity;
                modelRoot.transform.localScale = Vector3.one;

                // Physical BoxCollider on modelRoot & root
                BoxCollider modelCol = modelRoot.AddComponent<BoxCollider>();
                modelCol.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                modelCol.size = colliderSize;

                BoxCollider rootCol = extObj.GetComponent<BoxCollider>();
                if (rootCol == null)
                {
                    rootCol = extObj.AddComponent<BoxCollider>();
                }
                rootCol.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
                rootCol.size = colliderSize;
                rootCol.enabled = true;

                // Update WearNTear visual roots
                WearNTear wnt = extObj.GetComponent<WearNTear>();
                if (wnt != null)
                {
                    wnt.m_new = modelRoot;
                    wnt.m_worn = modelRoot;
                    wnt.m_broken = modelRoot;
                }

                // Add boss-themed magical point light
                GameObject lightObj = new GameObject("ExtMagicLight");
                lightObj.transform.SetParent(modelRoot.transform, false);
                lightObj.transform.localPosition = lightLocalPos;
                Light magicLight = lightObj.AddComponent<Light>();
                magicLight.type = LightType.Point;
                magicLight.color = lightColor;
                magicLight.range = 4.2f;
                magicLight.intensity = 1.45f;

                // Load embedded 3D mesh from Blender
                LoadEmbeddedMeshIntoRoot(modelRoot, meshResName, baseShader, pieceLayer);

                // For Yagluth (Extension 4): Clone real animated Valheim fire FX (bonfire / hearth / surtling fire particles)
                // and tint them into Yagluth's purple-crimson-orange Eternal Flame so it burns with genuine animated fire in-game!
                if (prefabName == "piece_enchant_ext_yagluth")
                {
                    AttachAnimatedValheimFireToYagluth(modelRoot, magicLight);
                }

                // Load embedded 128x128 icon
                Sprite extIcon = LoadEmbeddedSprite(iconResName);

                PieceConfig extConfig = new PieceConfig();
                extConfig.PieceTable = "Hammer";
                extConfig.Category = "Crafting";
                extConfig.CraftingStation = "piece_enchanting_table";
                extConfig.Name = displayName;
                extConfig.Description = description;
                if (extIcon != null)
                {
                    extConfig.Icon = extIcon;
                }
                if (reqs != null)
                {
                    for (int i = 0; i < reqs.Length; i++)
                    {
                        extConfig.AddRequirement(reqs[i]);
                    }
                }

                CustomPiece cp = new CustomPiece(extObj, false, extConfig);
                PieceManager.Instance.AddPiece(cp);
                Debug.Log("[WeaponAttributes] Registered station extension: " + prefabName);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WeaponAttributes] Failed to register extension " + prefabName + ": " + ex);
            }
        }

        private static void AttachAnimatedValheimFireToYagluth(GameObject modelRoot, Light magicLight)
        {
            try
            {
                // Attach live flickering fire light animator
                if (magicLight != null)
                {
                    YagluthFireLightFlicker flicker = magicLight.gameObject.AddComponent<YagluthFireLightFlicker>();
                    flicker.m_light = magicLight;
                }

                // Clone genuine animated Valheim fire FX from vanilla fire prefabs (piece_groundtorch_wood / fire_pit / hearth)
                string[] fireSources = new string[] { "piece_groundtorch_wood", "piece_groundtorch", "fire_pit", "hearth", "bonfire" };
                GameObject donorPrefab = null;
                for (int i = 0; i < fireSources.Length; i++)
                {
                    donorPrefab = PrefabManager.Instance.GetPrefab(fireSources[i]);
                    if (donorPrefab != null) break;
                }

                if (donorPrefab != null)
                {
                    // Find the enabled fire FX root inside the vanilla fireplace/torch (usually "_enabled", "_enabled_high", or "Fire")
                    Transform fxSource = null;
                    foreach (Transform tr in donorPrefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (tr == null) continue;
                        string n = tr.name.ToLowerInvariant();
                        if (n == "_enabled" || n == "_enabled_high" || n == "fire" || n == "flames")
                        {
                            fxSource = tr;
                            break;
                        }
                    }
                    if (fxSource == null)
                    {
                        fxSource = donorPrefab.transform;
                    }

                    GameObject clonedFX = UnityEngine.Object.Instantiate(fxSource.gameObject);
                    clonedFX.name = "YagluthRealAnimatedFireFX";
                    clonedFX.transform.SetParent(modelRoot.transform, false);
                    clonedFX.transform.localPosition = new Vector3(0f, 0.50f, 0f);
                    clonedFX.transform.localRotation = Quaternion.identity;
                    clonedFX.transform.localScale = new Vector3(0.75f, 0.78f, 0.75f);
                    clonedFX.SetActive(true);

                    // Ensure all child FX objects are active and tint their fire shaders/materials into Yagluth's purple-crimson-orange palette!
                    foreach (Transform child in clonedFX.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != null)
                        {
                            child.gameObject.SetActive(true);
                        }
                    }

                    Color yagPurple = new Color(0.72f, 0.14f, 0.88f, 1.0f);
                    Color yagCrimson = new Color(0.88f, 0.12f, 0.48f, 1.0f);
                    foreach (Renderer r in clonedFX.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        // Hide solid wood logs / standing torch meshes if we had to clone the root
                        if (r is MeshRenderer && !r.name.ToLowerInvariant().Contains("flame") && !r.name.ToLowerInvariant().Contains("fire") && !r.name.ToLowerInvariant().Contains("glow"))
                        {
                            r.enabled = false;
                            continue;
                        }
                        Material[] mats = r.materials;
                        if (mats != null)
                        {
                            for (int m = 0; m < mats.Length; m++)
                            {
                                Material mat = mats[m];
                                if (mat == null) continue;
                                if (mat.HasProperty("_Color")) mat.SetColor("_Color", yagPurple);
                                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", yagCrimson);
                                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", yagPurple * 2.2f);
                            }
                            r.materials = mats;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponAttributes] AttachAnimatedValheimFireToYagluth warning: " + ex.Message);
            }
        }

        private static Sprite LoadEmbeddedSprite(string resName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null) return null;
                    byte[] data = new byte[s.Length];
                    s.Read(data, 0, data.Length);
                    Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, data))
                    {
                        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WeaponAttributes] Failed to load icon " + resName + ": " + ex.Message);
            }
            return null;
        }

        private static Material CreatePBRMaterial(Shader shader, string matName)
        {
            Material m = new Material(shader);
            m.name = matName;

            Color baseCol = new Color(0.25f, 0.2f, 0.16f, 1f);
            float metallic = 0.0f;
            float gloss = 0.25f;
            Color emission = Color.black;
            bool hasEmission = false;

            switch (matName)
            {
                case "DarkYggdrasilWood":
                    baseCol = new Color(0.22f, 0.15f, 0.11f, 1f);
                    metallic = 0.05f;
                    gloss = 0.22f;
                    break;
                case "CarvedDarkStone":
                    baseCol = new Color(0.28f, 0.29f, 0.34f, 1f);
                    metallic = 0.10f;
                    gloss = 0.30f;
                    break;
                case "ForgedBlackIron":
                    baseCol = new Color(0.24f, 0.25f, 0.29f, 1f);
                    metallic = 0.85f;
                    gloss = 0.62f;
                    break;
                case "PolishedSteelEdge":
                    baseCol = new Color(0.68f, 0.72f, 0.78f, 1f);
                    metallic = 0.92f;
                    gloss = 0.80f;
                    break;
                case "AgedBoneSkull":
                    baseCol = new Color(0.76f, 0.68f, 0.54f, 1f);
                    metallic = 0.02f;
                    gloss = 0.35f;
                    break;
                case "DarkHorn":
                    baseCol = new Color(0.18f, 0.14f, 0.11f, 1f);
                    metallic = 0.08f;
                    gloss = 0.45f;
                    break;
                case "GlowingSoulCrystal":
                    baseCol = new Color(0.72f, 0.22f, 0.98f, 1f);
                    metallic = 0.20f;
                    gloss = 0.88f;
                    emission = new Color(0.75f, 0.18f, 1.0f, 1f) * 2.2f;
                    hasEmission = true;
                    break;
                case "GlowingArcaneRune":
                    baseCol = new Color(0.90f, 0.35f, 1.0f, 1f);
                    metallic = 0.10f;
                    gloss = 0.80f;
                    emission = new Color(0.88f, 0.25f, 1.0f, 1f) * 3.8f;
                    hasEmission = true;
                    break;
                case "ArcaneEmberCoal":
                    baseCol = new Color(0.35f, 0.10f, 0.48f, 1f);
                    metallic = 0.05f;
                    gloss = 0.20f;
                    emission = new Color(0.62f, 0.12f, 0.92f, 1f) * 1.6f;
                    hasEmission = true;
                    break;
                case "PurpleLeatherWrap":
                    baseCol = new Color(0.32f, 0.14f, 0.38f, 1f);
                    metallic = 0.05f;
                    gloss = 0.30f;
                    break;
                case "AncientBronze":
                    baseCol = new Color(0.55f, 0.36f, 0.16f, 1f);
                    metallic = 0.88f;
                    gloss = 0.68f;
                    break;
                case "GlowingEmeraldLeaf":
                    baseCol = new Color(0.15f, 0.88f, 0.48f, 1f);
                    metallic = 0.10f;
                    gloss = 0.75f;
                    emission = new Color(0.12f, 0.92f, 0.50f, 1f) * 2.2f;
                    hasEmission = true;
                    break;
                case "GlowingToxicAcid":
                    baseCol = new Color(0.38f, 0.95f, 0.08f, 1f);
                    metallic = 0.05f;
                    gloss = 0.85f;
                    emission = new Color(0.40f, 1.0f, 0.08f, 1f) * 2.6f;
                    hasEmission = true;
                    break;
                case "ShiningSilver":
                    baseCol = new Color(0.76f, 0.82f, 0.90f, 1f);
                    metallic = 0.92f;
                    gloss = 0.80f;
                    break;
                case "GlowingFrostCrystal":
                    baseCol = new Color(0.18f, 0.82f, 1.0f, 1f);
                    metallic = 0.20f;
                    gloss = 0.88f;
                    emission = new Color(0.18f, 0.85f, 1.0f, 1f) * 2.2f;
                    hasEmission = true;
                    break;
                case "GreenBlackMetal":
                    baseCol = new Color(0.12f, 0.20f, 0.15f, 1f);
                    metallic = 0.88f;
                    gloss = 0.72f;
                    break;
                case "YagluthGold":
                    baseCol = new Color(0.82f, 0.62f, 0.20f, 1f);
                    metallic = 0.90f;
                    gloss = 0.75f;
                    break;
                case "GlowingEternalFlame":
                    baseCol = new Color(0.92f, 0.15f, 0.58f, 1f);
                    metallic = 0.05f;
                    gloss = 0.80f;
                    emission = new Color(0.95f, 0.18f, 0.62f, 1f) * 2.8f;
                    hasEmission = true;
                    break;
                case "MossyForestPatch":
                    baseCol = new Color(0.20f, 0.34f, 0.14f, 1f);
                    metallic = 0.02f;
                    gloss = 0.18f;
                    break;
                case "GlowingElderRune":
                    baseCol = new Color(0.15f, 0.96f, 0.58f, 1f);
                    metallic = 0.05f;
                    gloss = 0.85f;
                    emission = new Color(0.12f, 1.0f, 0.58f, 1f) * 2.8f;
                    hasEmission = true;
                    break;
                case "DeepIndigoLeaf":
                    baseCol = new Color(0.24f, 0.18f, 0.46f, 1f);
                    metallic = 0.12f;
                    gloss = 0.62f;
                    emission = new Color(0.28f, 0.18f, 0.56f, 1f) * 0.7f;
                    hasEmission = true;
                    break;
                case "ElderOrbCoreInner":
                    baseCol = new Color(0.22f, 0.96f, 0.64f, 1f);
                    metallic = 0.05f;
                    gloss = 0.92f;
                    emission = new Color(0.18f, 0.98f, 0.62f, 1f) * 2.1f;
                    hasEmission = true;
                    break;
                case "ElderOrbVortexGreen":
                    baseCol = new Color(0.10f, 0.96f, 0.55f, 1f);
                    metallic = 0.08f;
                    gloss = 0.90f;
                    emission = new Color(0.08f, 1.0f, 0.56f, 1f) * 2.8f;
                    hasEmission = true;
                    break;
                case "ElderOrbVortexPurple":
                    baseCol = new Color(0.52f, 0.16f, 0.92f, 1f);
                    metallic = 0.08f;
                    gloss = 0.88f;
                    emission = new Color(0.56f, 0.18f, 0.95f, 1f) * 2.2f;
                    hasEmission = true;
                    break;
                case "SkullEyeCavity":
                    baseCol = new Color(0.03f, 0.03f, 0.03f, 1f);
                    metallic = 0.0f;
                    gloss = 0.05f;
                    break;
                case "GlowingAcidRune":
                    baseCol = new Color(0.42f, 0.88f, 0.14f, 1f);
                    metallic = 0.08f;
                    gloss = 0.80f;
                    emission = new Color(0.38f, 0.92f, 0.12f, 1f) * 2.0f;
                    hasEmission = true;
                    break;
                case "BonemassAcidSurface":
                    baseCol = new Color(0.28f, 0.88f, 0.04f, 1f);
                    metallic = 0.05f;
                    gloss = 0.90f;
                    emission = new Color(0.30f, 0.92f, 0.05f, 1f) * 2.1f;
                    hasEmission = true;
                    break;
                case "BonemassBubblesA":
                    baseCol = new Color(0.42f, 0.96f, 0.10f, 1f);
                    metallic = 0.05f;
                    gloss = 0.92f;
                    emission = new Color(0.40f, 0.98f, 0.12f, 1f) * 2.5f;
                    hasEmission = true;
                    break;
                case "BonemassBubblesB":
                    baseCol = new Color(0.50f, 0.98f, 0.16f, 1f);
                    metallic = 0.05f;
                    gloss = 0.92f;
                    emission = new Color(0.48f, 1.0f, 0.18f, 1f) * 2.7f;
                    hasEmission = true;
                    break;
                case "BonemassRisingDroplets":
                    baseCol = new Color(0.62f, 1.0f, 0.25f, 1f);
                    metallic = 0.0f;
                    gloss = 0.95f;
                    emission = new Color(0.58f, 1.0f, 0.24f, 1f) * 3.4f;
                    hasEmission = true;
                    break;
                case "GlowingIceRune":
                    baseCol = new Color(0.18f, 0.88f, 1.0f, 1f);
                    metallic = 0.08f;
                    gloss = 0.85f;
                    emission = new Color(0.15f, 0.90f, 1.0f, 1f) * 2.5f;
                    hasEmission = true;
                    break;
                case "ModerCrystalCyan":
                    baseCol = new Color(0.12f, 0.82f, 1.0f, 1f);
                    metallic = 0.22f;
                    gloss = 0.94f;
                    emission = new Color(0.10f, 0.85f, 1.0f, 1f) * 2.1f;
                    hasEmission = true;
                    break;
                case "ModerCrystalAzure":
                    baseCol = new Color(0.12f, 0.45f, 0.96f, 1f);
                    metallic = 0.22f;
                    gloss = 0.94f;
                    emission = new Color(0.12f, 0.48f, 1.0f, 1f) * 1.9f;
                    hasEmission = true;
                    break;
                case "ModerCrystalViolet":
                    baseCol = new Color(0.54f, 0.18f, 0.96f, 1f);
                    metallic = 0.22f;
                    gloss = 0.94f;
                    emission = new Color(0.56f, 0.20f, 1.0f, 1f) * 1.9f;
                    hasEmission = true;
                    break;
                case "YagluthPinkStone":
                    baseCol = new Color(0.35f, 0.25f, 0.27f, 1f);
                    metallic = 0.06f;
                    gloss = 0.22f;
                    break;
                case "DarkCarvedRune":
                    baseCol = new Color(0.04f, 0.04f, 0.05f, 1f);
                    metallic = 0.10f;
                    gloss = 0.12f;
                    break;
                case "YagluthCharcoal":
                    baseCol = new Color(0.08f, 0.05f, 0.07f, 1f);
                    metallic = 0.04f;
                    gloss = 0.08f;
                    break;
                case "YagluthLavaBed":
                    baseCol = new Color(1.0f, 0.42f, 0.08f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(1.0f, 0.36f, 0.05f, 1f) * 2.2f;
                    hasEmission = true;
                    break;
                case "GlowingFireRune":
                    baseCol = new Color(1.0f, 0.48f, 0.26f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(1.0f, 0.40f, 0.22f, 1f) * 2.1f;
                    hasEmission = true;
                    break;
                case "YagluthFlameCore":
                    // Zero gloss & zero metallic so the fire has NO plastic specular shine in Valheim!
                    baseCol = new Color(0.95f, 0.28f, 0.46f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(1.0f, 0.35f, 0.48f, 1f) * 2.3f;
                    hasEmission = true;
                    break;
                case "YagluthFlameMid":
                    baseCol = new Color(0.58f, 0.12f, 0.72f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(0.62f, 0.14f, 0.76f, 1f) * 1.85f;
                    hasEmission = true;
                    break;
                case "YagluthFlameOuter":
                    baseCol = new Color(0.46f, 0.06f, 0.24f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(0.52f, 0.08f, 0.28f, 1f) * 1.45f;
                    hasEmission = true;
                    break;
                case "YagluthFireSparks":
                    baseCol = new Color(1.0f, 0.55f, 0.28f, 1f);
                    metallic = 0.0f;
                    gloss = 0.0f;
                    emission = new Color(1.0f, 0.45f, 0.62f, 1f) * 3.4f;
                    hasEmission = true;
                    break;
            }

            // Generate a subtle 64x64 procedural noise/grain texture so Valheim shaders render rich surface detail
            Texture2D grainTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            grainTex.wrapMode = TextureWrapMode.Repeat;
            int seed = matName.GetHashCode();
            bool isPlasmaCore = (matName == "ElderOrbCoreInner");
            bool isAcidPool = (matName == "BonemassAcidSurface" || matName == "BonemassBubblesA" || matName == "BonemassBubblesB");
            bool isModerCrystal = (matName == "ModerCrystalCyan" || matName == "ModerCrystalAzure" || matName == "ModerCrystalViolet");
            bool isYagluthFire = (matName == "YagluthFlameCore" || matName == "YagluthFlameMid" || matName == "YagluthFlameOuter");
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (isPlasmaCore)
                    {
                        // Swirling emerald-violet plasma nebula pattern for the inner orb!
                        float sw = Mathf.Sin(x * 0.18f + Mathf.Cos(y * 0.18f) * 2.5f) * 0.5f + 0.5f;
                        Color c1 = new Color(0.28f, 0.08f, 0.58f, 1f); // deep cosmic violet
                        Color c2 = new Color(0.10f, 0.96f, 0.58f, 1f); // vibrant emerald
                        grainTex.SetPixel(x, y, Color.Lerp(c1, c2, sw));
                    }
                    else if (isAcidPool)
                    {
                        // Cellular boiling acid slime pattern (dark swampy green veins + bright neon lime foam)
                        float cell = Mathf.Sin(x * 0.28f + Mathf.Sin(y * 0.22f) * 2.2f) * Mathf.Cos(y * 0.28f - Mathf.Cos(x * 0.22f) * 2.2f);
                        float t = Mathf.Clamp01(cell * 0.5f + 0.5f);
                        Color darkSlime = new Color(0.14f, 0.58f, 0.02f, 1f);
                        Color brightFoam = new Color(0.65f, 1.0f, 0.22f, 1f);
                        grainTex.SetPixel(x, y, Color.Lerp(darkSlime, brightFoam, t));
                    }
                    else if (isModerCrystal)
                    {
                        // Internal crystal fracture veins + deep gem facets!
                        float v1 = Mathf.Abs(Mathf.Sin((x + y * 0.7f) * 0.32f + (seed & 7)));
                        float v2 = Mathf.Abs(Mathf.Cos((x * 0.6f - y) * 0.28f + (seed & 13)));
                        float crack = (v1 < 0.14f || v2 < 0.12f) ? 1.35f : Mathf.Lerp(0.75f, 1.12f, v1 * v2);
                        grainTex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(baseCol.r * crack),
                            Mathf.Clamp01(baseCol.g * crack),
                            Mathf.Clamp01(baseCol.b * crack),
                            1f));
                    }
                    else if (isYagluthFire)
                    {
                        // 2D Fire Heat LUT (u = groove valley depth [0=ridge, 1=deep groove], v = Z height [0=base, 1=apex]):
                        float uValley = x / 63f;
                        float vHeight = y / 63f;
                        float heat = Mathf.Clamp01((1f - vHeight) * 0.48f + uValley * 0.62f);

                        Color cBurgundy = new Color(0.24f, 0.015f, 0.06f, 1f); // deep dark burgundy-crimson (ridges & upper tip)
                        Color cViolet   = new Color(0.42f, 0.06f,  0.68f, 1f); // rich royal purple-violet (swirling mid-folds)
                        Color cMagenta  = new Color(0.86f, 0.14f,  0.48f, 1f); // vibrant fiery magenta (inner valley walls)
                        Color cOrange   = new Color(1.00f, 0.44f,  0.10f, 1f); // incandescent orange-gold (deep lower grooves)

                        Color fireCol;
                        if (heat < 0.38f)
                            fireCol = Color.Lerp(cBurgundy, cViolet, heat / 0.38f);
                        else if (heat < 0.72f)
                            fireCol = Color.Lerp(cViolet, cMagenta, (heat - 0.38f) / 0.34f);
                        else
                            fireCol = Color.Lerp(cMagenta, cOrange, (heat - 0.72f) / 0.28f);

                        grainTex.SetPixel(x, y, fireCol);
                    }
                    else
                    {
                        float wave = Mathf.Sin((x * 0.35f) + Mathf.Sin(y * 0.2f) * 2.0f + (seed & 15)) * 0.06f;
                        float n = (((x * 37 + y * 131 + seed) & 255) / 255f - 0.5f) * 0.08f;
                        float f = Mathf.Clamp(1f + wave + n, 0.82f, 1.18f);
                        grainTex.SetPixel(x, y, new Color(
                            Mathf.Clamp01(baseCol.r * f),
                            Mathf.Clamp01(baseCol.g * f),
                            Mathf.Clamp01(baseCol.b * f),
                            1f));
                    }
                }
            }
            grainTex.Apply();

            m.mainTexture = grainTex;
            m.color = Color.white;
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", grainTex);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);

            if (hasEmission)
            {
                if (m.HasProperty("_EmissionColor"))
                {
                    m.SetColor("_EmissionColor", emission);
                }
                if (m.HasProperty("_EmissiveColor"))
                {
                    m.SetColor("_EmissiveColor", emission);
                }
                m.EnableKeyword("_EMISSION");
            }

            return m;
        }

        private static void BuildCustomBlenderModel(GameObject root, Shader baseShader, int pieceLayer)
        {
            GameObject modelRoot = new GameObject("BossSoulForgeModel");
            modelRoot.layer = pieceLayer;
            modelRoot.transform.SetParent(root.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;
            modelRoot.transform.localScale = Vector3.one;

            // 1. Add accurate physical BoxColliders matching the new Boss Soul Enchanting Table geometry
            // Main workbench body & tabletop slab (width 2.2m, height 0.96m, depth 1.15m)
            BoxCollider tableCol = modelRoot.AddComponent<BoxCollider>();
            tableCol.center = new Vector3(0f, 0.48f, 0f);
            tableCol.size = new Vector3(2.22f, 0.96f, 1.16f);

            // Upper back tool rack & dragon-head posts (width 2.35m, height 0.72m, depth 0.28m at back z = +0.42m)
            BoxCollider rackCol = modelRoot.AddComponent<BoxCollider>();
            rackCol.center = new Vector3(0f, 1.32f, 0.42f);
            rackCol.size = new Vector3(2.35f, 0.72f, 0.28f);

            // Brazier & Anvil collision volume on top of the table
            BoxCollider topPropsCol = modelRoot.AddComponent<BoxCollider>();
            topPropsCol.center = new Vector3(-0.05f, 1.12f, -0.02f);
            topPropsCol.size = new Vector3(1.65f, 0.34f, 0.68f);

            // Ensure root also has an enabled BoxCollider if Piece placement / raycast expects one on root
            BoxCollider rootCol = root.GetComponent<BoxCollider>();
            if (rootCol == null)
            {
                rootCol = root.AddComponent<BoxCollider>();
            }
            rootCol.center = new Vector3(0f, 0.55f, 0f);
            rootCol.size = new Vector3(2.22f, 1.10f, 1.16f);
            rootCol.enabled = true;

            // Update WearNTear highlight & bound references so health/repair/deconstruct works seamlessly
            WearNTear wnt = root.GetComponent<WearNTear>();
            if (wnt != null)
            {
                wnt.m_new = modelRoot;
                wnt.m_worn = modelRoot;
                wnt.m_broken = modelRoot;
            }

            // 2. Purple Arcane Light over the Soul Crystal Brazier (-0.50, 1.30, -0.04)
            GameObject lightObj = new GameObject("SoulBrazierLight");
            lightObj.transform.SetParent(modelRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(-0.50f, 1.32f, -0.04f);
            Light magicLight = lightObj.AddComponent<Light>();
            magicLight.type = LightType.Point;
            magicLight.color = new Color(0.78f, 0.22f, 1f, 1f);
            magicLight.range = 5.5f;
            magicLight.intensity = 1.65f;

            // 2. Load embedded binary 3D mesh exported directly from Blender 5.0
            LoadEmbeddedMeshIntoRoot(modelRoot, "BossSoulTableMesh.bin.gz", baseShader, pieceLayer);
        }

        private static void LoadEmbeddedMeshIntoRoot(GameObject modelRoot, string resourceName, Shader baseShader, int pieceLayer)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream gzStream = asm.GetManifestResourceStream(resourceName))
                {
                    if (gzStream != null)
                    {
                        using (GZipStream decomp = new GZipStream(gzStream, CompressionMode.Decompress))
                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buf = new byte[16384];
                            int read;
                            while ((read = decomp.Read(buf, 0, buf.Length)) > 0)
                            {
                                ms.Write(buf, 0, read);
                            }
                            ms.Position = 0;

                            using (BinaryReader br = new BinaryReader(ms))
                            {
                                uint subCount = br.ReadUInt32();

                                for (int s = 0; s < subCount; s++)
                                {
                                    byte nameLen = br.ReadByte();
                                    string matName = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                                    uint vCount = br.ReadUInt32();
                                    uint tCount = br.ReadUInt32();

                                    Vector3[] verts = new Vector3[vCount];
                                    Vector3[] norms = new Vector3[vCount];
                                    Vector2[] uvs = new Vector2[vCount];
                                    int[] tris = new int[tCount];

                                    bool isVortexOrb = (matName == "ElderOrbCoreInner" || matName == "ElderOrbVortexGreen" || matName == "ElderOrbVortexPurple");
                                    bool isAcidAnim = (matName == "BonemassAcidSurface" || matName == "BonemassBubblesA" || matName == "BonemassBubblesB" || matName == "BonemassRisingDroplets");
                                    bool isModerCrystalAnim = (matName == "ModerCrystalCyan" || matName == "ModerCrystalAzure" || matName == "ModerCrystalViolet");
                                    bool isYagluthFireAnim = (matName == "YagluthFlameCore" || matName == "YagluthFlameMid" || matName == "YagluthFlameOuter" || matName == "YagluthFireSparks");
                                    Vector3 orbPivot = new Vector3(0f, 0.88f, 0f);
                                    Vector3 acidPivot = new Vector3(0f, 0.885f, 0f);
                                    Vector3 crystalPivot = new Vector3(0f, 0.50f, 0f);
                                    Vector3 firePivot = new Vector3(0f, 0.46f, 0f);
                                    Vector3 activePivot = isVortexOrb ? orbPivot : (isAcidAnim ? acidPivot : (isModerCrystalAnim ? crystalPivot : (isYagluthFireAnim ? firePivot : Vector3.zero)));

                                    for (int v = 0; v < vCount; v++)
                                    {
                                        Vector3 rawV = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                        verts[v] = (isVortexOrb || isAcidAnim || isModerCrystalAnim || isYagluthFireAnim) ? (rawV - activePivot) : rawV;
                                    }
                                    for (int v = 0; v < vCount; v++)
                                    {
                                        norms[v] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                    }
                                    for (int v = 0; v < vCount; v++)
                                    {
                                        uvs[v] = new Vector2(br.ReadSingle(), br.ReadSingle());
                                    }
                                    for (int t = 0; t < tCount; t++)
                                    {
                                        // Read local 0..vCount-1 index directly for this submesh!
                                        tris[t] = (int)br.ReadUInt16();
                                    }

                                    // Create a dedicated child MeshFilter + MeshRenderer per material (100% immune to 65k vertex indexing limits and static batching glitches)
                                    GameObject partObj = new GameObject("Part_" + matName);
                                    partObj.layer = pieceLayer;
                                    partObj.transform.SetParent(modelRoot.transform, false);
                                    partObj.transform.localPosition = activePivot;
                                    partObj.transform.localRotation = Quaternion.identity;
                                    partObj.transform.localScale = Vector3.one;

                                    if (isVortexOrb)
                                    {
                                        ElderOrbVortexAnimator anim = partObj.AddComponent<ElderOrbVortexAnimator>();
                                        if (matName == "ElderOrbCoreInner")
                                        {
                                            anim.m_rotSpeed = new Vector3(18f, 38f, 12f);
                                            anim.m_pulseScale = 0.06f;
                                            anim.m_pulseSpeed = 2.6f;
                                            anim.m_scrollUV = true;
                                            anim.m_uvSpeed = new Vector2(0.18f, 0.12f);
                                        }
                                        else if (matName == "ElderOrbVortexGreen")
                                        {
                                            anim.m_rotSpeed = new Vector3(45f, -85f, 32f);
                                            anim.m_pulseScale = 0.045f;
                                            anim.m_pulseSpeed = 3.4f;
                                        }
                                        else if (matName == "ElderOrbVortexPurple")
                                        {
                                            anim.m_rotSpeed = new Vector3(-55f, 72f, -42f);
                                            anim.m_pulseScale = 0.05f;
                                            anim.m_pulseSpeed = 2.9f;
                                        }
                                    }
                                    else if (isAcidAnim)
                                    {
                                        BonemassAcidBubbler bubbler = partObj.AddComponent<BonemassAcidBubbler>();
                                        bubbler.m_baseLocalPos = activePivot;
                                        if (matName == "BonemassAcidSurface")
                                        {
                                            bubbler.m_mode = 0; // Slowly churning surface + scrolling cellular slime UV
                                        }
                                        else if (matName == "BonemassBubblesA")
                                        {
                                            bubbler.m_mode = 1; // Boiling blister domes group A
                                            bubbler.m_phaseOffset = 0.0f;
                                        }
                                        else if (matName == "BonemassBubblesB")
                                        {
                                            bubbler.m_mode = 1; // Boiling blister domes group B (counter-phase)
                                            bubbler.m_phaseOffset = 3.14159f;
                                        }
                                        else if (matName == "BonemassRisingDroplets")
                                        {
                                            bubbler.m_mode = 2; // Effervescent rising & popping acid droplets
                                        }
                                    }
                                    else if (isModerCrystalAnim)
                                    {
                                        ModerCrystalPulseAnimator cAnim = partObj.AddComponent<ModerCrystalPulseAnimator>();
                                        cAnim.m_baseLocalPos = activePivot;
                                        if (matName == "ModerCrystalCyan")
                                        {
                                            cAnim.m_phaseOffset = 0.0f;
                                            cAnim.m_baseEmission = new Color(0.10f, 0.85f, 1.0f, 1f);
                                        }
                                        else if (matName == "ModerCrystalAzure")
                                        {
                                            cAnim.m_phaseOffset = 2.09f;
                                            cAnim.m_baseEmission = new Color(0.12f, 0.48f, 1.0f, 1f);
                                        }
                                        else if (matName == "ModerCrystalViolet")
                                        {
                                            cAnim.m_phaseOffset = 4.18f;
                                            cAnim.m_baseEmission = new Color(0.56f, 0.20f, 1.0f, 1f);
                                        }
                                    }
                                    else if (isYagluthFireAnim)
                                    {
                                        YagluthEternalFlameAnimator fAnim = partObj.AddComponent<YagluthEternalFlameAnimator>();
                                        fAnim.m_baseLocalPos = activePivot;
                                        if (matName == "YagluthFlameCore")
                                        {
                                            fAnim.m_layerMode = 0; // Inner incandescent orange-magenta heart
                                            fAnim.m_rotSpeedY = 36f;
                                            fAnim.m_phaseOffset = 0.0f;
                                            fAnim.m_baseEmission = new Color(1.0f, 0.35f, 0.48f, 1f);
                                        }
                                        else if (matName == "YagluthFlameMid")
                                        {
                                            fAnim.m_layerMode = 1; // Swirling royal purple-violet mid-flame
                                            fAnim.m_rotSpeedY = -28f;
                                            fAnim.m_phaseOffset = 2.1f;
                                            fAnim.m_baseEmission = new Color(0.62f, 0.14f, 0.76f, 1f);
                                        }
                                        else if (matName == "YagluthFlameOuter")
                                        {
                                            fAnim.m_layerMode = 2; // Outer deep crimson-burgundy licking flame folds
                                            fAnim.m_rotSpeedY = 20f;
                                            fAnim.m_phaseOffset = 4.2f;
                                            fAnim.m_baseEmission = new Color(0.52f, 0.08f, 0.28f, 1f);
                                        }
                                        else if (matName == "YagluthFireSparks")
                                        {
                                            fAnim.m_layerMode = 3; // Rising & swirling magical embers/sparks!
                                            fAnim.m_rotSpeedY = 52f;
                                            fAnim.m_phaseOffset = 1.05f;
                                            fAnim.m_baseEmission = new Color(1.0f, 0.48f, 0.62f, 1f);
                                        }
                                    }

                                    Mesh partMesh = new Mesh();
                                    partMesh.name = "Mesh_" + matName;
                                    partMesh.vertices = verts;
                                    partMesh.normals = norms;
                                    partMesh.uv = uvs;

                                    // Initialize zeroed UV2 and black Vertex Colors so even if Valheim's Custom/Piece shader is used,
                                    // vertex displacement (wind sway / wear distortion) is 0.0 and never creates spikes!
                                    Vector2[] zeroUV2 = new Vector2[vCount];
                                    Color32[] zeroCols = new Color32[vCount];
                                    Color32 blackCol = new Color32(0, 0, 0, 255);
                                    for (int v = 0; v < vCount; v++)
                                    {
                                        zeroCols[v] = blackCol;
                                    }
                                    partMesh.uv2 = zeroUV2;
                                    partMesh.colors32 = zeroCols;

                                    partMesh.triangles = tris;
                                    partMesh.RecalculateBounds();
                                    partMesh.RecalculateTangents();

                                    MeshFilter mf = partObj.AddComponent<MeshFilter>();
                                    mf.sharedMesh = partMesh;

                                    MeshRenderer mr = partObj.AddComponent<MeshRenderer>();
                                    mr.sharedMaterial = CreatePBRMaterial(baseShader, matName);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[WeaponAttributes] Resource " + resourceName + " not found in assembly!");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[WeaponAttributes] Failed to load mesh " + resourceName + ": " + ex);
            }
        }
    }

    public class ElderOrbVortexAnimator : MonoBehaviour
    {
        public Vector3 m_rotSpeed = new Vector3(30f, 60f, 20f);
        public float m_pulseScale = 0.05f;
        public float m_pulseSpeed = 3.0f;
        public bool m_scrollUV = false;
        public Vector2 m_uvSpeed = new Vector2(0.15f, 0.10f);

        private Material m_instMat;
        private float m_phaseOffset;

        void Start()
        {
            m_phaseOffset = UnityEngine.Random.Range(0f, 6.28f);
            if (m_scrollUV)
            {
                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr != null && mr.material != null)
                {
                    m_instMat = mr.material;
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time + m_phaseOffset;

            transform.Rotate(m_rotSpeed * dt, Space.Self);

            float s = 1f + Mathf.Sin(t * m_pulseSpeed) * m_pulseScale;
            transform.localScale = new Vector3(s, s, s);

            if (m_scrollUV && m_instMat != null)
            {
                Vector2 off = new Vector2((t * m_uvSpeed.x) % 1f, (t * m_uvSpeed.y) % 1f);
                m_instMat.mainTextureOffset = off;
            }
        }
    }

    public class BonemassAcidBubbler : MonoBehaviour
    {
        public int m_mode = 0; // 0 = Acid Pool Surface, 1 = Boiling Blister Domes, 2 = Rising Droplets
        public Vector3 m_baseLocalPos = new Vector3(0f, 0.885f, 0f);
        public float m_phaseOffset = 0f;

        private Material m_instMat;

        void Start()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                m_instMat = mr.material;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time + m_phaseOffset;

            if (m_mode == 0)
            {
                // Slowly churning toxic pool surface + scrolling cellular slime texture
                transform.Rotate(0f, 14f * dt, 0f, Space.Self);
                float bob = Mathf.Sin(t * 2.2f) * 0.006f;
                transform.localPosition = new Vector3(m_baseLocalPos.x, m_baseLocalPos.y + bob, m_baseLocalPos.z);
                if (m_instMat != null)
                {
                    m_instMat.mainTextureOffset = new Vector2((t * 0.09f) % 1f, (Mathf.Sin(t * 0.6f) * 0.12f) % 1f);
                }
            }
            else if (m_mode == 1)
            {
                // Boiling blister domes swelling up and popping/subsiding in alternating phases!
                float wave1 = Mathf.Sin(t * 3.6f);
                float wave2 = Mathf.Cos(t * 2.3f);
                float sy = 1.0f + wave1 * 0.24f + wave2 * 0.08f;
                float sxz = 1.0f - wave1 * 0.06f;
                transform.localScale = new Vector3(sxz, Mathf.Max(0.55f, sy), sxz);
                transform.Rotate(0f, (m_phaseOffset > 1f ? -12f : 12f) * dt, 0f, Space.Self);
                transform.localPosition = new Vector3(m_baseLocalPos.x, m_baseLocalPos.y + wave1 * 0.010f, m_baseLocalPos.z);
            }
            else if (m_mode == 2)
            {
                // Effervescent rising & swirling acid droplets above the cauldron mouth
                float cycle = (Time.time * 0.75f) % 1.0f;
                float riseY = cycle * 0.11f;
                float fadeScale = Mathf.Sin(cycle * Mathf.PI); // Smoothly grows from 0, peaks in middle, shrinks at top pop!
                float s = Mathf.Lerp(0.65f, 1.18f, fadeScale);
                transform.localPosition = new Vector3(m_baseLocalPos.x, m_baseLocalPos.y + riseY, m_baseLocalPos.z);
                transform.localScale = new Vector3(1.0f, s, 1.0f);
                transform.Rotate(0f, 38f * dt, 0f, Space.Self);
            }
        }
    }

    public class ModerCrystalPulseAnimator : MonoBehaviour
    {
        public Vector3 m_baseLocalPos = new Vector3(0f, 0.50f, 0f);
        public float m_phaseOffset = 0f;
        public Color m_baseEmission = new Color(0.15f, 0.85f, 1.0f, 1f);

        private Material m_instMat;

        void Start()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                m_instMat = mr.material;
            }
        }

        void Update()
        {
            float t = Time.time * 2.2f + m_phaseOffset;
            float pulse = Mathf.Sin(t) * 0.5f + 0.5f;

            // Gentle magical crystal breathing & levitation shimmer
            float sy = 1.0f + Mathf.Sin(t) * 0.025f;
            float bob = Mathf.Sin(t * 0.8f) * 0.008f;
            transform.localPosition = new Vector3(m_baseLocalPos.x, m_baseLocalPos.y + bob, m_baseLocalPos.z);
            transform.localScale = new Vector3(1.0f, sy, 1.0f);

            if (m_instMat != null)
            {
                float emitMult = Mathf.Lerp(1.35f, 3.10f, pulse);
                Color em = m_baseEmission * emitMult;
                if (m_instMat.HasProperty("_EmissionColor"))
                {
                    m_instMat.SetColor("_EmissionColor", em);
                }
                m_instMat.mainTextureOffset = new Vector2((Time.time * 0.04f) % 1f, (Time.time * 0.06f) % 1f);
            }
        }
    }

    public class YagluthEternalFlameAnimator : MonoBehaviour
    {
        public int m_layerMode = 0; // 0 = Core, 1 = Mid, 2 = Outer, 3 = Sparks
        public Vector3 m_baseLocalPos = new Vector3(0f, 0.46f, 0f);
        public float m_rotSpeedY = 25f;
        public float m_phaseOffset = 0f;
        public Color m_baseEmission = new Color(0.85f, 0.20f, 0.65f, 1f);

        private Material m_instMat;
        private Mesh m_instMesh;
        private Vector3[] m_baseVerts;
        private Vector3[] m_workVerts;
        private float m_accumYaw = 0f;

        void Start()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                m_instMat = mr.material;
                if (m_instMat.HasProperty("_Glossiness"))
                {
                    m_instMat.SetFloat("_Glossiness", 0.0f);
                }
                if (m_instMat.HasProperty("_Metallic"))
                {
                    m_instMat.SetFloat("_Metallic", 0.0f);
                }
            }

            // Cache base vertices for real-time organic vertex wave animation so each flame tongue licks and dances independently!
            if (m_layerMode >= 0 && m_layerMode <= 2)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.mesh != null)
                {
                    m_instMesh = mf.mesh;
                    m_baseVerts = m_instMesh.vertices;
                    m_workVerts = new Vector3[m_baseVerts.Length];
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time * 4.2f + m_phaseOffset;

            if (m_layerMode == 3)
            {
                // Mode 3: Rising & swirling magical embers/sparks!
                float cycle = (Time.time * 0.85f) % 1.0f;
                float riseY = cycle * 0.16f;
                float pulseScale = Mathf.Lerp(0.70f, 1.25f, Mathf.Sin(cycle * Mathf.PI));
                m_accumYaw = (m_accumYaw + m_rotSpeedY * dt) % 360f;
                transform.localPosition = new Vector3(m_baseLocalPos.x, m_baseLocalPos.y + riseY, m_baseLocalPos.z);
                transform.localRotation = Quaternion.Euler(0f, m_accumYaw, 0f);
                transform.localScale = new Vector3(1.0f, pulseScale, 1.0f);
                return;
            }

            // Counter-rotating swirling flame layers + organic wind-flicker tilt
            m_accumYaw = (m_accumYaw + m_rotSpeedY * dt) % 360f;
            float tiltX = Mathf.Sin(t * 1.15f) * 3.5f;
            float tiltZ = Mathf.Cos(t * 0.92f) * 3.5f;
            transform.localRotation = Quaternion.Euler(tiltX, m_accumYaw, tiltZ);

            // Real-time procedural vertex wave deformation on the flame tongues!
            if (m_instMesh != null && m_baseVerts != null)
            {
                int count = m_baseVerts.Length;
                for (int i = 0; i < count; i++)
                {
                    Vector3 bv = m_baseVerts[i];
                    // Height above brazier coals (0 at base, ~0.58 at apex)
                    float h = Mathf.Clamp01(bv.y / 0.56f);
                    float h2 = h * h;
                    // Rising travelling wave along Y + radial phase from X/Z so tongues whip and lick organically like real fire!
                    float wavePhase = bv.y * 15.0f - t * 2.2f + (bv.x - bv.z) * 9.0f;
                    float swayX = Mathf.Sin(wavePhase) * 0.022f * h2;
                    float swayZ = Mathf.Cos(wavePhase * 0.88f + 1.3f) * 0.022f * h2;
                    float stretchY = Mathf.Sin(t * 1.7f + (bv.x + bv.z) * 12.0f) * 0.028f * h;
                    m_workVerts[i] = new Vector3(bv.x + swayX, bv.y + stretchY, bv.z + swayZ);
                }
                m_instMesh.vertices = m_workVerts;
            }

            if (m_instMat != null)
            {
                // Scroll fire gradient & turbulence upward continuously
                float scrollX = (Time.time * (m_layerMode == 1 ? -0.16f : 0.18f)) % 1f;
                float scrollY = (-Time.time * (0.48f + m_layerMode * 0.10f)) % 1f;
                m_instMat.mainTextureOffset = new Vector2(scrollX, scrollY);

                // Flickering fire emission intensity
                float flicker = 0.5f + 0.5f * Mathf.Sin(t * 2.1f + Mathf.Cos(t * 3.3f));
                float emitMult = Mathf.Lerp(1.45f, 2.75f, flicker);
                Color em = m_baseEmission * emitMult;
                if (m_instMat.HasProperty("_EmissionColor"))
                {
                    m_instMat.SetColor("_EmissionColor", em);
                }
            }
        }
    }

    public class YagluthFireLightFlicker : MonoBehaviour
    {
        public Light m_light;

        void Update()
        {
            if (m_light == null) return;
            float t = Time.time * 6.5f;
            float n = Mathf.Sin(t) * 0.5f + Mathf.Cos(t * 1.73f) * 0.35f + Mathf.Sin(t * 2.91f) * 0.15f;
            m_light.intensity = Mathf.Lerp(1.35f, 2.35f, n * 0.5f + 0.5f);
            m_light.color = Color.Lerp(
                new Color(0.76f, 0.14f, 0.88f, 1f),
                new Color(1.00f, 0.32f, 0.52f, 1f),
                Mathf.Sin(Time.time * 3.2f) * 0.5f + 0.5f
            );
        }
    }

    [HarmonyPatch(typeof(Player), "HaveRequirements", new Type[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) })]
    public static class Patch_Player_HaveRequirements
    {
        public static bool Prefix(Recipe recipe, bool discover, int qualityLevel, int amount, ref bool __result)
        {
            if (Patch_InventoryGui_UpdateRecipeList.IsOurRecipe(recipe))
            {
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer == null)
                {
                    __result = false;
                    return false;
                }

                int targetLevel = Patch_InventoryGui_UpdateRecipeList.GetTargetLevelFromRecipe(recipe);
                string bossKey = Patch_InventoryGui_UpdateRecipeList.GetBossKeyFromRecipe(recipe);
                if (string.IsNullOrEmpty(bossKey))
                {
                    __result = false;
                    return false;
                }

                // Check CraftingStation level requirement: Enchantment Level N requires Enchanting Table Level N (1..5)
                CraftingStation currentStation = localPlayer.GetCurrentCraftingStation();
                int stationLevel = currentStation != null ? currentStation.GetLevel(true) : 0;
                if (stationLevel < targetLevel)
                {
                    __result = false;
                    return false;
                }

                Inventory inv = localPlayer.GetInventory();

                // If level 1: requires 1 boss trophy AND 1 mob trophy
                if (targetLevel <= 1)
                {
                    string bossPrefab = Patch_InventoryGui_UpdateRecipeList.GetBossPrefabFromRecipe(recipe);
                    if (string.IsNullOrEmpty(bossPrefab) || WeaponAttributesPlugin.CountInventoryItem(inv, bossPrefab) < 1)
                    {
                        __result = false;
                        return false;
                    }

                    string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);
                    int mobCount = WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab);
                    if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        mobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
                    }

                    __result = (mobCount >= 1);
                    return false;
                }
                else
                {
                    // Level 2..5: requires mob trophies: Lv2=5, Lv3=10, Lv4=15, Lv5=20
                    int requiredMobCount = WeaponAttributesPlugin.GetMobTrophyCountForLevel(targetLevel);
                    string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);
                    int mobCount = WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab);
                    if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        mobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
                    }

                    __result = (mobCount >= requiredMobCount);
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
    public static class Patch_InventoryGui_UpdateRecipeList
    {
        public static Dictionary<string, Recipe> m_weaponEnchantRecipes = new Dictionary<string, Recipe>();
        private static MethodInfo m_addRecipeToListMethod;
        private static FieldInfo m_availableRecipesField = typeof(InventoryGui).GetField("m_availableRecipes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo m_recipeListBaseSizeField = typeof(InventoryGui).GetField("m_recipeListBaseSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        public const string RECIPE_PREFIX = "Recipe_Enchant_";

        public static string GetShortBossLabel(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return "";
            switch (bossKey.Trim().ToLower())
            {
                case "eikthyr": return WeaponAttributesLocalization.Loc("$wa_boss_short_eikthyr");
                case "elder": return WeaponAttributesLocalization.Loc("$wa_boss_short_elder");
                case "bonemass": return WeaponAttributesLocalization.Loc("$wa_boss_short_bonemass");
                case "moder": return WeaponAttributesLocalization.Loc("$wa_boss_short_moder");
                case "yagluth": return WeaponAttributesLocalization.Loc("$wa_boss_short_yagluth");
                case "queen": return WeaponAttributesLocalization.Loc("$wa_boss_short_queen");
                case "fader": return WeaponAttributesLocalization.Loc("$wa_boss_short_fader");
                default: return bossKey;
            }
        }

        private static void DecorateLastAddedRecipeRow(InventoryGui gui, ItemDrop.ItemData weapon, string bossKey, int currentEnchantLevel, int targetLevel, bool canCraft)
        {
            if (gui == null || gui.m_recipeListRoot == null || weapon == null || weapon.m_shared == null) return;
            int childCount = gui.m_recipeListRoot.childCount;
            if (childCount <= 0) return;

            Transform row = gui.m_recipeListRoot.GetChild(childCount - 1);
            if (row == null) return;

            string weaponName = Localization.instance.Localize(weapon.m_shared.m_name);
            string baseHex = WeaponAttributesPlugin.GetBossEnchantColor(bossKey, null);
            if (string.IsNullOrEmpty(baseHex)) baseHex = "#FFD700";

            string listHex = baseHex;
            if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase)) listHex = "#A8B2A8";
            else if (string.Equals(bossKey, "fader", StringComparison.OrdinalIgnoreCase)) listHex = "#D87A38";

            // When missing items/station level, render the ENTIRE row (icon, weapon name, boss tag, quality badge) in unmistakable muted gray!
            string tagHex = canCraft ? listHex : "#787878";
            Color disabledTextCol = new Color(0.50f, 0.50f, 0.50f, 0.90f);

            Transform iconTr = row.Find("icon");
            if (iconTr != null && !canCraft)
            {
                Image iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null)
                {
                    // Valheim's Custom/GuiScroll UI shader uses Color(1, 0, 1, 0) as its desaturate-to-grayscale flag!
                    iconImg.color = new Color(1f, 0f, 1f, 0f);
                }
            }

            Transform nameTr = row.Find("name");
            if (nameTr != null)
            {
                TMP_Text nameText = nameTr.GetComponent<TMP_Text>();
                if (nameText != null)
                {
                    nameText.color = canCraft ? Color.white : disabledTextCol;
                    string displayedWeaponName = canCraft ? weaponName : string.Format("<color=#808080>{0}</color>", weaponName);
                    if (targetLevel <= 1)
                    {
                        string shortBoss = GetShortBossLabel(bossKey);
                        nameText.text = string.Format("{0} <color={1}>[{2}]</color>", displayedWeaponName, tagHex, shortBoss);
                    }
                    else
                    {
                        nameText.text = string.Format("{0} <color={1}>(+{2})</color>", displayedWeaponName, tagHex, targetLevel);
                    }
                }
            }

            Transform qualTr = row.Find("QualityLevel");
            if (qualTr != null)
            {
                TMP_Text qualText = qualTr.GetComponent<TMP_Text>();
                if (qualText != null)
                {
                    qualText.gameObject.SetActive(true);
                    qualText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    qualText.overflowMode = TMPro.TextOverflowModes.Overflow;
                    if (!canCraft)
                    {
                        qualText.color = new Color(0.55f, 0.55f, 0.55f, 0.88f);
                    }

                    if (currentEnchantLevel > 0)
                    {
                        // Right-align "4+1" inside the icon's right edge and slightly scale font down so "+1" grows LEFT over the icon and NEVER overlaps the weapon name!
                        qualText.enableAutoSizing = false;
                        qualText.fontSize = Mathf.Max(11f, Mathf.Round(qualText.fontSize * 0.78f));
                        qualText.alignment = TextAlignmentOptions.TopRight;

                        RectTransform qualRect = qualTr as RectTransform;
                        RectTransform iconRect = iconTr as RectTransform;
                        if (qualRect != null && iconRect != null)
                        {
                            float iconRightX = iconTr.localPosition.x + iconRect.rect.xMax;
                            float iconTopY = iconTr.localPosition.y + iconRect.rect.yMax;
                            float boxW = 34f;
                            float boxH = 18f;
                            qualRect.anchorMin = new Vector2(0.5f, 0.5f);
                            qualRect.anchorMax = new Vector2(0.5f, 0.5f);
                            qualRect.pivot = new Vector2(1f, 1f);
                            qualRect.sizeDelta = new Vector2(boxW, boxH);
                            qualRect.localPosition = new Vector3(iconRightX + 1f, iconTopY + 2f, qualTr.localPosition.z);
                        }

                        RectTransform nameRect = nameTr as RectTransform;
                        if (nameRect != null)
                        {
                            nameRect.anchoredPosition = nameRect.anchoredPosition + new Vector2(4f, 0f);
                            nameRect.sizeDelta = nameRect.sizeDelta - new Vector2(4f, 0f);
                        }

                        string plusHex = canCraft ? baseHex : "#787878";
                        qualText.text = string.Format("{0}<color={1}>+{2}</color>", weapon.m_quality, plusHex, currentEnchantLevel);
                    }
                    else
                    {
                        qualText.text = weapon.m_quality.ToString();
                    }
                }
            }
        }

        public static bool IsOurRecipe(Recipe recipe)
        {
            if (recipe == null) return false;
            return recipe.name != null && recipe.name.StartsWith(RECIPE_PREFIX);
        }

        public static int GetTargetLevelFromRecipe(Recipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.name) || !recipe.name.StartsWith(RECIPE_PREFIX)) return 1;
            int idx = recipe.name.LastIndexOf("__lvl");
            if (idx >= 0 && idx + 5 < recipe.name.Length)
            {
                int lvl;
                if (int.TryParse(recipe.name.Substring(idx + 5), out lvl))
                {
                    return lvl;
                }
            }
            return 1;
        }

        public static string GetBossKeyFromRecipe(Recipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.name) || !recipe.name.StartsWith(RECIPE_PREFIX)) return null;
            string payload = recipe.name.Substring(RECIPE_PREFIX.Length);
            string[] parts = payload.Split(new string[] { "__" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                string candidate = parts[1];
                string mappedBoss;
                if (WeaponAttributesPlugin.BossNameMap.TryGetValue(candidate, out mappedBoss))
                {
                    return mappedBoss;
                }
                return candidate.ToLower();
            }
            return null;
        }

        public static string GetBossPrefabFromRecipe(Recipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.name) || !recipe.name.StartsWith(RECIPE_PREFIX)) return null;
            string payload = recipe.name.Substring(RECIPE_PREFIX.Length);
            string[] parts = payload.Split(new string[] { "__" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                string candidate = parts[1];
                if (WeaponAttributesPlugin.BossEnchantMap.ContainsKey(candidate))
                {
                    return candidate;
                }
                string bossTrophyPrefab;
                if (WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(candidate, out bossTrophyPrefab))
                {
                    return bossTrophyPrefab;
                }
            }
            return null;
        }

        private static Recipe GetOrCreateEnchantRecipe(ItemDrop.ItemData weapon, string bossKey, string bossPrefab, int targetLevel, CraftingStation station)
        {
            if (weapon == null || string.IsNullOrEmpty(bossKey)) return null;
            GameObject dropPrefabObj = weapon.m_dropPrefab;
            if (dropPrefabObj == null && ObjectDB.instance != null)
            {
                string resolvedName = WeaponAttributesPlugin.GetWeaponPrefabName(weapon);
                if (!string.IsNullOrEmpty(resolvedName))
                {
                    dropPrefabObj = ObjectDB.instance.GetItemPrefab(resolvedName);
                }
            }
            if (dropPrefabObj == null) return null;

            string weaponPrefab = dropPrefabObj.name;
            string identifier = (targetLevel <= 1 && !string.IsNullOrEmpty(bossPrefab)) ? bossPrefab : bossKey;
            string recipeKey = string.Format("{0}__{1}__lvl{2}", weaponPrefab, identifier, targetLevel);

            Recipe rec;
            if (m_weaponEnchantRecipes.TryGetValue(recipeKey, out rec) && rec != null)
            {
                rec.m_craftingStation = station;
                rec.m_minStationLevel = Mathf.Clamp(targetLevel, 1, 5);
                return rec;
            }

            ItemDrop weaponDrop = dropPrefabObj.GetComponent<ItemDrop>();
            if (weaponDrop == null) return null;

            rec = ScriptableObject.CreateInstance<Recipe>();
            rec.name = RECIPE_PREFIX + recipeKey;
            rec.m_item = weaponDrop;
            rec.m_amount = 1;
            rec.m_enabled = true;
            rec.m_craftingStation = station;
            rec.m_minStationLevel = Mathf.Clamp(targetLevel, 1, 5);
            rec.m_resources = new Piece.Requirement[0];

            m_weaponEnchantRecipes[recipeKey] = rec;
            return rec;
        }

        public static void Postfix(InventoryGui __instance, List<Recipe> recipes)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;

            CraftingStation currentStation = player.GetCurrentCraftingStation();
            if (currentStation == null) return;

            string sName = currentStation.name != null ? currentStation.name.ToLower() : "";
            string rawStationToken = currentStation.m_name ?? "";
            string locName = !string.IsNullOrEmpty(rawStationToken) ? Localization.instance.Localize(rawStationToken).ToLower() : "";
            if (!sName.Contains("piece_enchanting_table") && !sName.Contains("enchant") &&
                rawStationToken != "$wa_piece_table" && !locName.Contains("enchant") && !locName.Contains("зачарован"))
            {
                return;
            }

            // Ensure custom Enchanting Table icon is always shown in the station UI header (never the cloned Artisan Table icon)
            if (CustomEnchantingTable.m_tableIcon != null)
            {
                currentStation.m_icon = CustomEnchantingTable.m_tableIcon;
                if (__instance.m_craftingStationIcon != null)
                {
                    __instance.m_craftingStationIcon.sprite = CustomEnchantingTable.m_tableIcon;
                }
            }

            if (m_addRecipeToListMethod == null)
            {
                m_addRecipeToListMethod = typeof(InventoryGui).GetMethod("AddRecipeToList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (m_addRecipeToListMethod == null) return;

            bool inCraftTab = __instance.InCraftTab();
            bool inUpgradeTab = __instance.InUpradeTab();
            int currentStationLevel = currentStation.GetLevel(true);

            Inventory inv = player.GetInventory();
            List<ItemDrop.ItemData> allItems = inv.GetAllItems();
            List<ItemDrop.ItemData> unenchantedWeapons = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> upgradableEnchantedWeapons = new List<ItemDrop.ItemData>();

            for (int i = 0; i < allItems.Count; i++)
            {
                ItemDrop.ItemData it = allItems[i];
                if (WeaponAttributesPlugin.IsWeapon(it))
                {
                    int currentLevel = WeaponAttributesPlugin.GetEnchantLevel(it);
                    if (currentLevel == 0)
                    {
                        unenchantedWeapons.Add(it);
                    }
                    else if (currentLevel < 5)
                    {
                        upgradableEnchantedWeapons.Add(it);
                    }
                }
            }

            bool addedAny = false;

            // 1. Initial enchanting for unenchanted weapons in inventory (Level 1) -> Craft Tab (InCraftTab)
            // Only show bosses that were defeated in this world OR whose head/trophy the player has held at least once!
            if (inCraftTab && unenchantedWeapons.Count > 0)
            {
                List<string> unlockedBossKeys = new List<string>();
                for (int b = 0; b < WeaponAttributesPlugin.OrderedBossKeys.Length; b++)
                {
                    string bKey = WeaponAttributesPlugin.OrderedBossKeys[b];
                    string bPrefab;
                    if (WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bKey, out bPrefab))
                    {
                        if (WeaponAttributesPlugin.IsBossUnlockedForEnchanting(player, inv, bKey, bPrefab))
                        {
                            unlockedBossKeys.Add(bKey);
                        }
                    }
                }

                for (int pass = 0; pass < 2; pass++)
                {
                    bool wantCraftable = (pass == 0);
                    for (int i = 0; i < unenchantedWeapons.Count; i++)
                    {
                        ItemDrop.ItemData weapon = unenchantedWeapons[i];
                        for (int b = 0; b < unlockedBossKeys.Count; b++)
                        {
                            string bossKey = unlockedBossKeys[b];
                            string bossPrefab;
                            if (!WeaponAttributesPlugin.BossKeyToTrophyPrefab.TryGetValue(bossKey, out bossPrefab))
                            {
                                continue;
                            }

                            Recipe rec = GetOrCreateEnchantRecipe(weapon, bossKey, bossPrefab, 1, currentStation);
                            if (rec == null) continue;

                            bool hasItems = WeaponAttributesPlugin.HasEnchantMaterialsInInventory(inv, bossKey, bossPrefab, 1);
                            bool canCraft = hasItems && (currentStationLevel >= 1);
                            if (canCraft != wantCraftable) continue;

                            m_addRecipeToListMethod.Invoke(__instance, new object[] { player, rec, weapon, canCraft });
                            DecorateLastAddedRecipeRow(__instance, weapon, bossKey, 0, 1, canCraft);
                            addedAny = true;
                        }
                    }
                }
            }

            // 2. Enchantment upgrades for already enchanted weapons (Level 2..5) -> Upgrade Tab (InUpradeTab)
            if (inUpgradeTab && upgradableEnchantedWeapons.Count > 0)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool wantCraftable = (pass == 0);
                    for (int i = 0; i < upgradableEnchantedWeapons.Count; i++)
                    {
                        ItemDrop.ItemData weapon = upgradableEnchantedWeapons[i];
                        string bossKey = WeaponAttributesPlugin.GetEnchantBossKey(weapon);
                        if (string.IsNullOrEmpty(bossKey)) continue;

                        int currentLevel = WeaponAttributesPlugin.GetEnchantLevel(weapon);
                        int targetLevel = currentLevel + 1;
                        if (targetLevel <= 5)
                        {
                            Recipe rec = GetOrCreateEnchantRecipe(weapon, bossKey, null, targetLevel, currentStation);
                            if (rec == null) continue;

                            bool hasItems = WeaponAttributesPlugin.HasEnchantMaterialsInInventory(inv, bossKey, null, targetLevel);
                            bool canCraft = hasItems && (currentStationLevel >= targetLevel);
                            if (canCraft != wantCraftable) continue;

                            m_addRecipeToListMethod.Invoke(__instance, new object[] { player, rec, weapon, canCraft });
                            DecorateLastAddedRecipeRow(__instance, weapon, bossKey, currentLevel, targetLevel, canCraft);
                            addedAny = true;
                        }
                    }
                }
            }

            // Update scroll container vertical height after appending custom recipes in Postfix so all rows scroll smoothly!
            if (addedAny && __instance.m_recipeListRoot != null && m_availableRecipesField != null)
            {
                IList avail = m_availableRecipesField.GetValue(__instance) as IList;
                if (avail != null)
                {
                    float baseSize = m_recipeListBaseSizeField != null ? (float)m_recipeListBaseSizeField.GetValue(__instance) : 0f;
                    float targetHeight = Mathf.Max(baseSize, (float)avail.Count * __instance.m_recipeListSpace);
                    __instance.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
                }
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe", new Type[] { typeof(Player), typeof(float) })]
    public static class Patch_InventoryGui_UpdateRecipe
    {
        private static FieldInfo m_selectedRecipeField = typeof(InventoryGui).GetField("m_selectedRecipe", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_minStationLevelBasecolorField = typeof(InventoryGui).GetField("m_minStationLevelBasecolor", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(InventoryGui __instance, Player player, float dt)
        {
            if (m_selectedRecipeField == null || player == null) return;

            object rdpObj = m_selectedRecipeField.GetValue(__instance);
            if (rdpObj == null) return;

            PropertyInfo recipeProp = rdpObj.GetType().GetProperty("Recipe");
            Recipe recipe = recipeProp != null ? (Recipe)recipeProp.GetValue(rdpObj, null) : null;

            if (recipe != null && Patch_InventoryGui_UpdateRecipeList.IsOurRecipe(recipe))
            {
                PropertyInfo itemDataProp = rdpObj.GetType().GetProperty("ItemData");
                ItemDrop.ItemData weapon = itemDataProp != null ? (ItemDrop.ItemData)itemDataProp.GetValue(rdpObj, null) : null;

                int targetLevel = Patch_InventoryGui_UpdateRecipeList.GetTargetLevelFromRecipe(recipe);
                string bossKey = Patch_InventoryGui_UpdateRecipeList.GetBossKeyFromRecipe(recipe);
                string bossPrefab = Patch_InventoryGui_UpdateRecipeList.GetBossPrefabFromRecipe(recipe);
                string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);

                Inventory inv = player.GetInventory();
                ItemDrop.ItemData foundBossTrophy = null;
                if (!string.IsNullOrEmpty(bossPrefab))
                {
                    foundBossTrophy = WeaponAttributesPlugin.FindInventoryItem(inv, bossPrefab);
                }

                GameObject mobTrophyPrefabObj = !string.IsNullOrEmpty(mobPrefab) ? ObjectDB.instance.GetItemPrefab(mobPrefab) : null;
                ItemDrop mobItemDrop = mobTrophyPrefabObj != null ? mobTrophyPrefabObj.GetComponent<ItemDrop>() : null;

                int mobCount = !string.IsNullOrEmpty(mobPrefab) ? WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab) : 0;
                if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                {
                    mobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
                }

                int requiredMobCount = WeaponAttributesPlugin.GetMobTrophyCountForLevel(targetLevel);
                bool hasMaterials = false;

                if (targetLevel <= 1)
                {
                    hasMaterials = (foundBossTrophy != null) && (mobCount >= 1);
                }
                else
                {
                    hasMaterials = (mobCount >= requiredMobCount);
                }

                int requiredStationLevel = Mathf.Clamp(targetLevel, 1, 5);
                CraftingStation currentStation = player.GetCurrentCraftingStation();
                int currentStationLevel = currentStation != null ? currentStation.GetLevel(true) : 0;
                bool hasStationLevel = (currentStationLevel >= requiredStationLevel);

                // Attributes and boss title
                string[] attrs = null;
                if (foundBossTrophy != null)
                {
                    attrs = WeaponAttributesPlugin.GetBossAttributesFromItem(foundBossTrophy);
                }
                if (attrs == null && !string.IsNullOrEmpty(bossKey))
                {
                    WeaponAttributesPlugin.BossKeyAttributesMap.TryGetValue(bossKey, out attrs);
                }

                if (weapon != null)
                {
                    string weaponName = Localization.instance.Localize(weapon.m_shared.m_name);
                    string bossDisplayName = Patch_InventoryGui_UpdateRecipeList.GetShortBossLabel(bossKey);
                    if (foundBossTrophy != null && foundBossTrophy.m_shared != null)
                    {
                        bossDisplayName = Localization.instance.Localize(foundBossTrophy.m_shared.m_name);
                    }
                    else if (!string.IsNullOrEmpty(bossPrefab) && ObjectDB.instance != null)
                    {
                        GameObject bObj = ObjectDB.instance.GetItemPrefab(bossPrefab);
                        ItemDrop bDrop = bObj != null ? bObj.GetComponent<ItemDrop>() : null;
                        if (bDrop != null && bDrop.m_itemData != null && bDrop.m_itemData.m_shared != null)
                        {
                            bossDisplayName = Localization.instance.Localize(bDrop.m_itemData.m_shared.m_name);
                        }
                    }

                    if (targetLevel <= 1)
                    {
                        __instance.m_recipeName.text = string.Format(WeaponAttributesLocalization.Loc("$wa_recipe_enchant"), weaponName, bossDisplayName);
                    }
                    else
                    {
                        __instance.m_recipeName.text = string.Format(WeaponAttributesLocalization.Loc("$wa_recipe_upgrade"), weaponName, targetLevel);
                    }

                    // Set crafting station level requirement UI (1..5) and highlight red/magenta if station level is too low
                    if (__instance.m_minStationLevelText != null)
                    {
                        __instance.m_minStationLevelText.text = requiredStationLevel.ToString();
                        if (!hasStationLevel)
                        {
                            __instance.m_minStationLevelText.color = ((Mathf.Sin(Time.time * 10f) > 0f) ? Color.red : Color.magenta);
                        }
                        else if (m_minStationLevelBasecolorField != null)
                        {
                            __instance.m_minStationLevelText.color = (Color)m_minStationLevelBasecolorField.GetValue(__instance);
                        }
                        else
                        {
                            __instance.m_minStationLevelText.color = Color.white;
                        }
                    }
                    if (__instance.m_minStationLevelIcon != null)
                    {
                        __instance.m_minStationLevelIcon.gameObject.SetActive(true);
                    }
                    if (__instance.m_itemCraftType != null)
                    {
                        __instance.m_itemCraftType.gameObject.SetActive(true);
                        __instance.m_itemCraftType.text = (targetLevel <= 1)
                            ? WeaponAttributesLocalization.Loc("$wa_craft_type_base")
                            : string.Format(WeaponAttributesLocalization.Loc("$wa_craft_type_lvl"), targetLevel);
                    }

                    // Render weapon tooltip with preview of enchanting stats
                    int quality = weapon.m_quality;
                    string baseTooltip = ItemDrop.ItemData.GetTooltip(weapon, quality, true, (float)Game.m_worldLevel, 0, false);
                    string localizedTooltip = Localization.instance.Localize(baseTooltip);

                    if (attrs != null && attrs.Length > 0)
                    {
                        int previewLevel = Mathf.Clamp(targetLevel, 1, 5);
                        float perAttr = WeaponAttributesPlugin.GetEnchantDamagePerAttribute(weapon, attrs, previewLevel, quality);
                        string formattedVal = WeaponAttributesPlugin.FormatEnchantValue(perAttr);
                        string suffix = "";
                        if (WeaponAttributesPlugin.IsPureSummonStaff(weapon))
                        {
                            suffix = WeaponAttributesLocalization.Loc("$wa_summon_suffix");
                        }
                        else if (WeaponAttributesPlugin.IsHybridSummonStaff(weapon))
                        {
                            suffix = WeaponAttributesLocalization.Loc("$wa_roots_suffix");
                        }

                        StringBuilder sb = new StringBuilder();
                        sb.Append("\n<color=yellow>");
                        sb.Append(string.Format(WeaponAttributesLocalization.Loc("$wa_preview_header"), previewLevel));
                        sb.Append("</color>");
                        for (int a = 0; a < attrs.Length; a++)
                        {
                            string attrName = WeaponAttributesPlugin.GetAttributeNameLocalized(attrs[a]);
                            sb.Append(string.Format("\n  {0}{1}: <color=#00FFFF>+{2}</color>", attrName, suffix, formattedVal));
                        }
                        __instance.m_recipeDecription.text = localizedTooltip + "\n" + sb.ToString();
                    }
                    else
                    {
                        __instance.m_recipeDecription.text = localizedTooltip;
                    }
                }

                // Requirements display (Slot 0: Boss Trophy if lvl 1, Slot 1: Mob Trophy)
                if (__instance.m_recipeRequirementList != null && __instance.m_recipeRequirementList.Length > 0)
                {
                    int reqSlot = 0;

                    // Slot for Boss Trophy (only level 1)
                    if (targetLevel <= 1)
                    {
                        GameObject slotBoss = __instance.m_recipeRequirementList[reqSlot++];
                        slotBoss.SetActive(true);
                        Piece.Requirement reqBoss = new Piece.Requirement();
                        reqBoss.m_amount = 1;
                        if (foundBossTrophy != null && foundBossTrophy.m_dropPrefab != null)
                        {
                            reqBoss.m_resItem = foundBossTrophy.m_dropPrefab.GetComponent<ItemDrop>();
                        }
                        else if (!string.IsNullOrEmpty(bossPrefab))
                        {
                            GameObject bObj = ObjectDB.instance.GetItemPrefab(bossPrefab);
                            if (bObj != null) reqBoss.m_resItem = bObj.GetComponent<ItemDrop>();
                        }
                        InventoryGui.SetupRequirement(slotBoss.transform, reqBoss, player, false, 1, 1);
                    }

                    // Slot for Mob Trophy
                    if (mobItemDrop != null && reqSlot < __instance.m_recipeRequirementList.Length)
                    {
                        GameObject slotMob = __instance.m_recipeRequirementList[reqSlot++];
                        slotMob.SetActive(true);
                        Piece.Requirement reqMob = new Piece.Requirement();
                        reqMob.m_amount = requiredMobCount;
                        reqMob.m_amountPerLevel = 0;
                        reqMob.m_extraAmountOnlyOneIngredient = 0;
                        reqMob.m_resItem = mobItemDrop;
                        InventoryGui.SetupRequirement(slotMob.transform, reqMob, player, false, 1, 1);

                        // Ensure exact required count text is displayed on UI
                        Transform amountTransform = slotMob.transform.Find("res_amount");
                        if (amountTransform != null)
                        {
                            TMP_Text amountText = amountTransform.GetComponent<TMP_Text>();
                            if (amountText != null)
                            {
                                amountText.text = requiredMobCount.ToString();
                            }
                        }
                    }

                    // Hide remaining slots
                    for (int r = reqSlot; r < __instance.m_recipeRequirementList.Length; r++)
                    {
                        InventoryGui.HideRequirement(__instance.m_recipeRequirementList[r].transform);
                    }
                }

                if (__instance.m_craftButton != null)
                {
                    bool canCraftOrUpgrade = hasMaterials && hasStationLevel;
                    __instance.m_craftButton.interactable = canCraftOrUpgrade;
                    TMP_Text btnText = __instance.m_craftButton.GetComponentInChildren<TMP_Text>();
                    if (btnText != null)
                    {
                        if (targetLevel <= 1)
                        {
                            btnText.text = Localization.instance.Localize("$inventory_craftbutton");
                        }
                        else
                        {
                            btnText.text = Localization.instance.Localize("$inventory_upgradebutton");
                        }
                    }

                    UITooltip tooltip = __instance.m_craftButton.GetComponent<UITooltip>();
                    if (tooltip != null)
                    {
                        if (canCraftOrUpgrade)
                        {
                            tooltip.m_text = "";
                            UITooltip.HideTooltip();
                        }
                        else if (!hasStationLevel)
                        {
                            tooltip.m_text = Localization.instance.Localize("$msg_toofewstationlevel");
                        }
                        else
                        {
                            tooltip.m_text = Localization.instance.Localize("$msg_missingrequirement");
                        }
                    }
                }
            }
        }
    }

    // Preserve custom enchant data when Valheim destroys the weapon and creates an upgraded version in Inventory
    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] {
        typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool)
    })]
    public static class Patch_Inventory_AddItem_10
    {
        public static void Postfix(ItemDrop.ItemData __result)
        {
            if (__result != null && !string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingEnchant))
            {
                if (__result.m_customData == null)
                {
                    __result.m_customData = new Dictionary<string, string>();
                }
                __result.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_KEY] = WeaponAttributesPlugin.m_upgradingEnchant;
                if (!string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingBoss))
                {
                    __result.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_BOSS_KEY] = WeaponAttributesPlugin.m_upgradingBoss;
                }
                if (!string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingLevel))
                {
                    __result.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_LEVEL_KEY] = WeaponAttributesPlugin.m_upgradingLevel;
                }

                WeaponAttributesPlugin.m_upgradingEnchant = null;
                WeaponAttributesPlugin.m_upgradingBoss = null;
                WeaponAttributesPlugin.m_upgradingLevel = null;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
    public static class Patch_InventoryGui_OnCraftPressed
    {
        private static FieldInfo m_selectedRecipeField = typeof(InventoryGui).GetField("m_selectedRecipe", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_craftRecipeField = typeof(InventoryGui).GetField("m_craftRecipe", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_craftUpgradeItemField = typeof(InventoryGui).GetField("m_craftUpgradeItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_craftVariantField = typeof(InventoryGui).GetField("m_craftVariant", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_multiCraftingField = typeof(InventoryGui).GetField("m_multiCrafting", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_touchMultiCraftingField = typeof(InventoryGui).GetField("m_touchMultiCrafting", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_craftTimerField = typeof(InventoryGui).GetField("m_craftTimer", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool Prefix(InventoryGui __instance)
        {
            if (m_selectedRecipeField == null) return true;
            object rdpObj = m_selectedRecipeField.GetValue(__instance);
            if (rdpObj == null) return true;

            PropertyInfo recipeProp = rdpObj.GetType().GetProperty("Recipe");
            Recipe recipe = recipeProp != null ? (Recipe)recipeProp.GetValue(rdpObj, null) : null;
            if (recipe == null || !Patch_InventoryGui_UpdateRecipeList.IsOurRecipe(recipe)) return true;

            PropertyInfo itemDataProp = rdpObj.GetType().GetProperty("ItemData");
            ItemDrop.ItemData weapon = itemDataProp != null ? (ItemDrop.ItemData)itemDataProp.GetValue(rdpObj, null) : null;
            if (weapon == null) return true;

            Player player = Player.m_localPlayer;
            if (player == null) return true;

            int targetLevel = Patch_InventoryGui_UpdateRecipeList.GetTargetLevelFromRecipe(recipe);
            string bossKey = Patch_InventoryGui_UpdateRecipeList.GetBossKeyFromRecipe(recipe);
            string bossPrefab = Patch_InventoryGui_UpdateRecipeList.GetBossPrefabFromRecipe(recipe);
            string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);

            Inventory inv = player.GetInventory();
            int mobCount = !string.IsNullOrEmpty(mobPrefab) ? WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab) : 0;
            if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
            {
                mobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
            }

            bool hasMaterials = false;
            if (targetLevel <= 1)
            {
                bool hasBoss = !string.IsNullOrEmpty(bossPrefab) && WeaponAttributesPlugin.CountInventoryItem(inv, bossPrefab) >= 1;
                hasMaterials = hasBoss && (mobCount >= 1);
            }
            else
            {
                int reqMobCount = WeaponAttributesPlugin.GetMobTrophyCountForLevel(targetLevel);
                hasMaterials = (mobCount >= reqMobCount);
            }

            int requiredStationLevel = Mathf.Clamp(targetLevel, 1, 5);
            CraftingStation currentStation = player.GetCurrentCraftingStation();
            int currentStationLevel = currentStation != null ? currentStation.GetLevel(true) : 0;
            if (currentStationLevel < requiredStationLevel)
            {
                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_toofewstationlevel"));
                return false;
            }

            if (!hasMaterials)
            {
                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_missingrequirement"));
                return false;
            }

            // Set crafting state directly, bypassing vanilla maxQuality / canAddItem checks
            if (m_craftRecipeField != null) m_craftRecipeField.SetValue(__instance, recipe);
            if (m_craftUpgradeItemField != null) m_craftUpgradeItemField.SetValue(__instance, weapon);
            if (m_craftVariantField != null) m_craftVariantField.SetValue(__instance, 0);
            if (m_multiCraftingField != null) m_multiCraftingField.SetValue(__instance, false);
            if (m_touchMultiCraftingField != null) m_touchMultiCraftingField.SetValue(__instance, false);
            if (m_craftTimerField != null) m_craftTimerField.SetValue(__instance, 0f);

            if (__instance.CraftingVibration != null)
            {
                UnityEngine.Object.Instantiate<GameObject>(__instance.CraftingVibration);
            }

            CraftingStation station = player.GetCurrentCraftingStation();
            if (station != null && station.m_craftItemEffects != null)
            {
                station.m_craftItemEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1);
            }
            else if (__instance.m_craftItemEffects != null)
            {
                __instance.m_craftItemEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
    public static class Patch_InventoryGui_DoCrafting
    {
        private static FieldInfo m_craftRecipeField = typeof(InventoryGui).GetField("m_craftRecipe", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo m_craftUpgradeItemField = typeof(InventoryGui).GetField("m_craftUpgradeItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo m_updateCraftingPanelMethod = typeof(InventoryGui).GetMethod("UpdateCraftingPanel", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool Prefix(InventoryGui __instance, Player player)
        {
            WeaponAttributesPlugin.m_upgradingEnchant = null;
            WeaponAttributesPlugin.m_upgradingBoss = null;
            WeaponAttributesPlugin.m_upgradingLevel = null;

            Recipe currentRecipe = m_craftRecipeField != null ? (Recipe)m_craftRecipeField.GetValue(__instance) : null;

            if (currentRecipe != null && Patch_InventoryGui_UpdateRecipeList.IsOurRecipe(currentRecipe))
            {
                ItemDrop.ItemData weapon = m_craftUpgradeItemField != null ? (ItemDrop.ItemData)m_craftUpgradeItemField.GetValue(__instance) : null;
                if (weapon == null) return false;

                int targetLevel = Patch_InventoryGui_UpdateRecipeList.GetTargetLevelFromRecipe(currentRecipe);
                string bossKey = Patch_InventoryGui_UpdateRecipeList.GetBossKeyFromRecipe(currentRecipe);
                string bossPrefab = Patch_InventoryGui_UpdateRecipeList.GetBossPrefabFromRecipe(currentRecipe);
                string mobPrefab = WeaponAttributesPlugin.GetMobTrophyPrefab(bossKey);

                int currentLevel = WeaponAttributesPlugin.GetEnchantLevel(weapon);
                if (targetLevel <= 1 && currentLevel > 0)
                {
                    player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_already_enchanted"));
                    return false;
                }
                if (targetLevel > 1 && currentLevel != targetLevel - 1)
                {
                    player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_not_ready_level"));
                    return false;
                }

                Inventory inv = player.GetInventory();
                int requiredMobCount = WeaponAttributesPlugin.GetMobTrophyCountForLevel(targetLevel);

                // Check materials
                if (targetLevel <= 1)
                {
                    ItemDrop.ItemData bossItem = null;
                    if (!string.IsNullOrEmpty(bossPrefab))
                    {
                        bossItem = WeaponAttributesPlugin.FindInventoryItem(inv, bossPrefab);
                    }

                    int totalMobCount = !string.IsNullOrEmpty(mobPrefab) ? WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab) : 0;
                    if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        totalMobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
                    }

                    if (bossItem == null || totalMobCount < 1)
                    {
                        player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_missing_enchant_trophies"));
                        return false;
                    }

                    // Deduct 1 boss trophy
                    inv.RemoveItem(bossItem, 1);

                    // Deduct 1 mob trophy
                    if (!string.IsNullOrEmpty(mobPrefab) && WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab) >= 1)
                    {
                        WeaponAttributesPlugin.RemoveInventoryItem(inv, mobPrefab, 1);
                    }
                    else if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        WeaponAttributesPlugin.RemoveInventoryItem(inv, "TrophyFrostTroll", 1);
                    }

                    string[] attrs = WeaponAttributesPlugin.GetBossAttributesFromItem(bossItem);
                    if (attrs == null && !string.IsNullOrEmpty(bossKey))
                    {
                        WeaponAttributesPlugin.BossKeyAttributesMap.TryGetValue(bossKey, out attrs);
                    }

                    WeaponAttributesPlugin.SetAttributeEnchant(weapon, bossKey, attrs, 1);
                }
                else
                {
                    // Upgrades Lv 2..5
                    int totalMobCount = !string.IsNullOrEmpty(mobPrefab) ? WeaponAttributesPlugin.CountInventoryItem(inv, mobPrefab) : 0;
                    if (string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        totalMobCount += WeaponAttributesPlugin.CountInventoryItem(inv, "TrophyFrostTroll");
                    }

                    if (totalMobCount < requiredMobCount)
                    {
                        player.Message(MessageHud.MessageType.Center, WeaponAttributesLocalization.Loc("$wa_msg_missing_upgrade_trophies"));
                        return false;
                    }

                    int leftToRemove = requiredMobCount;
                    if (!string.IsNullOrEmpty(mobPrefab))
                    {
                        int removed = WeaponAttributesPlugin.RemoveInventoryItem(inv, mobPrefab, leftToRemove);
                        leftToRemove -= removed;
                    }
                    if (leftToRemove > 0 && string.Equals(bossKey, "elder", StringComparison.OrdinalIgnoreCase))
                    {
                        WeaponAttributesPlugin.RemoveInventoryItem(inv, "TrophyFrostTroll", leftToRemove);
                    }

                    string[] attrs = null;
                    WeaponAttributesPlugin.BossKeyAttributesMap.TryGetValue(bossKey, out attrs);
                    WeaponAttributesPlugin.SetAttributeEnchant(weapon, bossKey, attrs, targetLevel);
                }

                CraftingStation station = player.GetCurrentCraftingStation();
                if (station != null && station.m_craftItemDoneEffects != null)
                {
                    station.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity, null, 1f, -1);
                }

                string weaponName = Localization.instance.Localize(weapon.m_shared.m_name);
                if (targetLevel <= 1)
                {
                    player.Message(MessageHud.MessageType.Center, string.Format(WeaponAttributesLocalization.Loc("$wa_msg_enchanted_ok"), weaponName));
                }
                else
                {
                    player.Message(MessageHud.MessageType.Center, string.Format(WeaponAttributesLocalization.Loc("$wa_msg_upgraded_ok"), weaponName, targetLevel));
                }

                if (m_updateCraftingPanelMethod != null)
                {
                    m_updateCraftingPanelMethod.Invoke(__instance, new object[] { false });
                }
                return false;
            }

            // Normal crafting / upgrading: save enchantment and level of weapon being upgraded
            ItemDrop.ItemData upgradeItem = m_craftUpgradeItemField != null ? (ItemDrop.ItemData)m_craftUpgradeItemField.GetValue(__instance) : null;
            if (upgradeItem != null && WeaponAttributesPlugin.IsWeapon(upgradeItem))
            {
                WeaponAttributesPlugin.m_upgradingEnchant = WeaponAttributesPlugin.GetAttributeEnchantString(upgradeItem);
                WeaponAttributesPlugin.m_upgradingBoss = WeaponAttributesPlugin.GetEnchantBossKey(upgradeItem);
                int lvl = WeaponAttributesPlugin.GetEnchantLevel(upgradeItem);
                if (lvl > 0)
                {
                    WeaponAttributesPlugin.m_upgradingLevel = lvl.ToString();
                }
            }

            return true;
        }

        public static void Postfix(InventoryGui __instance, Player player)
        {
            if (!string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingEnchant) && player != null)
            {
                Recipe currentRecipe = m_craftRecipeField != null ? (Recipe)m_craftRecipeField.GetValue(__instance) : null;
                string prefabName = (currentRecipe != null && currentRecipe.m_item != null && currentRecipe.m_item.m_itemData != null && currentRecipe.m_item.m_itemData.m_dropPrefab != null)
                    ? currentRecipe.m_item.m_itemData.m_dropPrefab.name
                    : null;

                List<ItemDrop.ItemData> allItems = player.GetInventory().GetAllItems();
                for (int i = 0; i < allItems.Count; i++)
                {
                    ItemDrop.ItemData item = allItems[i];
                    if (item == null || item.m_dropPrefab == null) continue;

                    if (prefabName == null || string.Equals(item.m_dropPrefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(WeaponAttributesPlugin.GetAttributeEnchantString(item)))
                        {
                            if (item.m_customData == null)
                            {
                                item.m_customData = new Dictionary<string, string>();
                            }
                            item.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_KEY] = WeaponAttributesPlugin.m_upgradingEnchant;
                            if (!string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingBoss))
                            {
                                item.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_BOSS_KEY] = WeaponAttributesPlugin.m_upgradingBoss;
                            }
                            if (!string.IsNullOrEmpty(WeaponAttributesPlugin.m_upgradingLevel))
                            {
                                item.m_customData[WeaponAttributesPlugin.CUSTOM_DATA_LEVEL_KEY] = WeaponAttributesPlugin.m_upgradingLevel;
                            }
                            break;
                        }
                    }
                }
            }

            WeaponAttributesPlugin.m_upgradingEnchant = null;
            WeaponAttributesPlugin.m_upgradingBoss = null;
            WeaponAttributesPlugin.m_upgradingLevel = null;
        }
    }
}
