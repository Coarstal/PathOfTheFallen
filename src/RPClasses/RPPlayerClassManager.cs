using System;
using System.Reflection;
using UnityEngine;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsRPClasses
{
    public static class RPPlayerClassManager
    {
        public const string CustomDataClassKey = "Coarstal_RPClass";
        public const string ZDOClassKey = "Coarstal_RPClass";
        public const string RebirthScrollPrefabName = "RebirthScroll";

        private static readonly FieldInfo s_nviewField = typeof(Character).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo s_updateFoodMethod = typeof(Player).GetMethod("UpdateFood", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static bool s_scrollRegistered;

        public static RPClassType GetPlayerClass(Player player)
        {
            if (player == null) return RPClassType.None;

            // If this is the local player (or owner), read authoritative Player.m_customData first
            if (player == Player.m_localPlayer && player.m_customData != null)
            {
                string raw;
                if (player.m_customData.TryGetValue(CustomDataClassKey, out raw) && !string.IsNullOrEmpty(raw))
                {
                    int parsed;
                    if (int.TryParse(raw, out parsed) && Enum.IsDefined(typeof(RPClassType), parsed))
                    {
                        return (RPClassType)parsed;
                    }
                    try
                    {
                        return (RPClassType)Enum.Parse(typeof(RPClassType), raw, true);
                    }
                    catch
                    {
                    }
                }
            }

            // For remote players in multiplayer (or fallback), read synchronized ZDO int
            ZNetView nview = GetNetView(player);
            if (nview != null && nview.IsValid() && nview.GetZDO() != null)
            {
                int zdoVal = nview.GetZDO().GetInt(ZDOClassKey, 0);
                if (Enum.IsDefined(typeof(RPClassType), zdoVal))
                {
                    return (RPClassType)zdoVal;
                }
            }

            // Secondary fallback to customData if available
            if (player.m_customData != null)
            {
                string raw2;
                if (player.m_customData.TryGetValue(CustomDataClassKey, out raw2))
                {
                    int parsed2;
                    if (int.TryParse(raw2, out parsed2) && Enum.IsDefined(typeof(RPClassType), parsed2))
                    {
                        return (RPClassType)parsed2;
                    }
                }
            }

            return RPClassType.None;
        }

        public static void SetPlayerClass(Player player, RPClassType newClass, bool refillVitals, bool showBanner)
        {
            if (player == null) return;

            if (player.m_customData != null)
            {
                if (newClass == RPClassType.None)
                {
                    player.m_customData.Remove(CustomDataClassKey);
                }
                else
                {
                    player.m_customData[CustomDataClassKey] = ((int)newClass).ToString();
                }
            }

            SyncClassToZDO(player, newClass);
            EnsureStatusEffects(player, newClass);
            RefreshMaxVitals(player, refillVitals);

            if (showBanner && newClass != RPClassType.None && Localization.instance != null)
            {
                string className = Localization.instance.Localize(RPClassConfig.GetClassToken(newClass));
                string msg = string.Format(Localization.instance.Localize("$msg_rp_class_chosen"), className);
                player.Message(MessageHud.MessageType.Center, msg, 0, RPIcons.GetClassIcon(newClass));
            }
        }

        public static void SyncClassToZDO(Player player, RPClassType cls)
        {
            ZNetView nview = GetNetView(player);
            if (nview != null && nview.IsValid() && nview.IsOwner() && nview.GetZDO() != null)
            {
                int cur = nview.GetZDO().GetInt(ZDOClassKey, -1);
                if (cur != (int)cls)
                {
                    nview.GetZDO().Set(ZDOClassKey, (int)cls);
                }
            }
        }

        public static void EnsureStatusEffects(Player player, RPClassType activeClass)
        {
            if (player == null) return;
            SEMan seman = player.GetSEMan();
            if (seman == null) return;

            RPClassType[] allClasses = new RPClassType[]
            {
                RPClassType.Warrior,
                RPClassType.Knight,
                RPClassType.Thief,
                RPClassType.Hunter,
                RPClassType.Sorcerer,
                RPClassType.Beggar
            };

            // Remove any stale class or passive status effects
            for (int i = 0; i < allClasses.Length; i++)
            {
                RPClassType c = allClasses[i];
                if (c != activeClass)
                {
                    int clsHash = RPStatusEffectRegistry.GetEffectName(c).GetStableHashCode();
                    if (seman.HaveStatusEffect(clsHash))
                    {
                        seman.RemoveStatusEffect(clsHash, true);
                    }

                    int pasHash = RPStatusEffectRegistry.GetPassiveEffectName(c).GetStableHashCode();
                    if (seman.HaveStatusEffect(pasHash))
                    {
                        seman.RemoveStatusEffect(pasHash, true);
                    }
                }
            }

            // Apply hidden class base effect + visible Passive Skill effect
            if (activeClass != RPClassType.None)
            {
                int activeClsHash = RPStatusEffectRegistry.GetEffectName(activeClass).GetStableHashCode();
                if (!seman.HaveStatusEffect(activeClsHash))
                {
                    SE_RPClass se = RPStatusEffectRegistry.GetClassEffect(activeClass);
                    if (se != null)
                    {
                        seman.AddStatusEffect(se, false, 0, 0f);
                    }
                }

                string passiveName = RPStatusEffectRegistry.GetPassiveEffectName(activeClass);
                if (!string.IsNullOrEmpty(passiveName))
                {
                    int activePasHash = passiveName.GetStableHashCode();
                    if (!seman.HaveStatusEffect(activePasHash))
                    {
                        StatusEffect pas = RPStatusEffectRegistry.GetPassiveEffect(activeClass);
                        if (pas != null)
                        {
                            seman.AddStatusEffect(pas, false, 0, 0f);
                        }
                    }
                }
            }
        }

        public static void RefreshMaxVitals(Player player, bool refill)
        {
            if (player == null) return;

            if (s_updateFoodMethod != null)
            {
                try
                {
                    s_updateFoodMethod.Invoke(player, new object[] { 0f, true });
                }
                catch
                {
                }
            }

            if (refill)
            {
                player.SetHealth(player.GetMaxHealth());
                player.AddStamina(player.GetMaxStamina());
                if (player.GetMaxEitr() > 0f)
                {
                    player.AddEitr(player.GetMaxEitr());
                }
            }
        }

        public static void GetClassBaseStatBonuses(RPClassType cls, out float bonusHP, out float bonusStamina, out float bonusEitr)
        {
            bonusHP = 0f;
            bonusStamina = 0f;
            bonusEitr = 0f;

            switch (cls)
            {
                case RPClassType.Warrior:
                    bonusHP = RPClassConfig.WarriorBaseHP.Value;
                    break;
                case RPClassType.Knight:
                    bonusHP = RPClassConfig.KnightBaseHP.Value;
                    bonusStamina = RPClassConfig.KnightBaseStamina.Value;
                    break;
                case RPClassType.Thief:
                    bonusStamina = RPClassConfig.ThiefBaseStamina.Value;
                    break;
                case RPClassType.Hunter:
                    bonusStamina = RPClassConfig.HunterBaseStamina.Value;
                    break;
                case RPClassType.Sorcerer:
                    bonusEitr = RPClassConfig.SorcererBaseEitr.Value;
                    break;
                case RPClassType.Beggar:
                    float b = RPClassConfig.BeggarBaseStatsBonus.Value;
                    bonusHP = b;
                    bonusStamina = b;
                    bonusEitr = b;
                    break;
            }
        }

        public static ZNetView GetNetView(Character ch)
        {
            if (ch == null || s_nviewField == null) return null;
            return s_nviewField.GetValue(ch) as ZNetView;
        }

        public static void RegisterRebirthScrollPrefab()
        {
            if (s_scrollRegistered) return;

            // Clone a small lightweight vanilla item (Ruby / AmberPearl / MeadHealthMinor) as 3D drop mesh donor
            GameObject donor = PrefabManager.Instance.GetPrefab("Ruby");
            if (donor == null) donor = PrefabManager.Instance.GetPrefab("AmberPearl");
            if (donor == null) donor = PrefabManager.Instance.GetPrefab("Coins");
            if (donor == null)
            {
                CoarstalsRPClassesPlugin.ModLogger.LogError("Could not find vanilla donor prefab for Rebirth Scroll!");
                return;
            }

            if (ItemManager.Instance.GetItem(RebirthScrollPrefabName) != null ||
                PrefabManager.Instance.GetPrefab(RebirthScrollPrefabName) != null)
            {
                s_scrollRegistered = true;
                return;
            }

            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(RebirthScrollPrefabName, donor);
            if (cloned == null)
            {
                if (CoarstalsRPClassesPlugin.ModLogger != null)
                {
                    CoarstalsRPClassesPlugin.ModLogger.LogWarning("CreateClonedPrefab returned null for " + RebirthScrollPrefabName);
                }
                return;
            }
            Sprite scrollIcon = RPIcons.GetRebirthScrollIcon();

            // Tint world drop slightly golden so it stands out if dropped on ground
            Renderer[] renderers = cloned.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r != null && r.sharedMaterial != null)
                {
                    Material mat = new Material(r.sharedMaterial);
                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", new Color(1.0f, 0.84f, 0.32f, 1.0f));
                    }
                    r.sharedMaterial = mat;
                }
            }

            ItemDrop drop = cloned.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
            {
                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                shared.m_name = "$item_rp_rebirth_scroll";
                shared.m_description = "$item_rp_rebirth_scroll_desc";
                shared.m_icons = new Sprite[] { scrollIcon };
                // Consumable allows right-click in inventory -> triggers Player.ConsumeItem!
                shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
                shared.m_maxStackSize = 5;
                shared.m_weight = 0.5f;
                shared.m_value = 0;
                shared.m_food = 0f;
                shared.m_foodStamina = 0f;
                shared.m_foodEitr = 0f;
                shared.m_foodBurnTime = 0f;
                shared.m_foodRegen = 0f;
                shared.m_consumeStatusEffect = null;
                shared.m_teleportable = true;
            }

            ItemConfig cfg = new ItemConfig();
            cfg.Name = "$item_rp_rebirth_scroll";
            cfg.Description = "$item_rp_rebirth_scroll_desc";
            cfg.Icons = new Sprite[] { scrollIcon };
            cfg.Enabled = false; // Purchased from Hildir, not crafted

            ItemManager.Instance.AddItem(new CustomItem(cloned, false, cfg));
            s_scrollRegistered = true;
        }

        public static void RegisterConsoleCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new RPClassConsoleCommand());
        }
    }

    public class RPClassConsoleCommand : ConsoleCommand
    {
        public override string Name
        {
            get { return "rpclass"; }
        }

        public override string Help
        {
            get { return "[ui | reset | set <warrior|knight|thief|hunter|sorcerer|beggar>] - Admin command to manage RP Class."; }
        }

        public override bool IsCheat
        {
            get { return true; }
        }

        public override void Run(string[] args)
        {
            Player p = Player.m_localPlayer;
            if (p == null)
            {
                Console.instance.Print("No local player in world.");
                return;
            }

            bool isAdmin = SynchronizationManager.Instance.PlayerIsAdmin || (ZNet.instance != null && ZNet.instance.IsServer());
            if (!isAdmin)
            {
                Console.instance.Print("Only server administrators or singleplayer hosts with devcommands can run rpclass.");
                return;
            }

            if (args == null || args.Length == 0 || string.Equals(args[0], "ui", StringComparison.OrdinalIgnoreCase))
            {
                RPClassSelectionUI.Open(isRespecFromScroll: false, forceAllowCancel: true);
                Console.instance.Print("Opened RP Class selection UI.");
                return;
            }

            if (string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
            {
                RPPlayerClassManager.SetPlayerClass(p, RPClassType.None, refillVitals: false, showBanner: false);
                RPClassSelectionUI.Open(isRespecFromScroll: false, forceAllowCancel: false);
                Console.instance.Print("Player RP class reset. Selection UI opened.");
                return;
            }

            if (string.Equals(args[0], "set", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
            {
                RPClassType target = ParseClassArg(args[1]);
                if (target == RPClassType.None)
                {
                    Console.instance.Print("Unknown class: " + args[1] + ". Valid: warrior, knight, thief, hunter, sorcerer, beggar.");
                    return;
                }
                RPPlayerClassManager.SetPlayerClass(p, target, refillVitals: true, showBanner: true);
                Console.instance.Print("RP class set to: " + target);
                return;
            }

            Console.instance.Print("Usage: rpclass [ui | reset | set <warrior|knight|thief|hunter|sorcerer|beggar>]");
        }

        private static RPClassType ParseClassArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return RPClassType.None;
            string s = arg.Trim().ToLowerInvariant();
            switch (s)
            {
                case "1":
                case "warrior":
                case "воин":
                    return RPClassType.Warrior;
                case "2":
                case "knight":
                case "рыцарь":
                    return RPClassType.Knight;
                case "3":
                case "thief":
                case "rogue":
                case "вор":
                    return RPClassType.Thief;
                case "4":
                case "hunter":
                case "ranger":
                case "охотник":
                    return RPClassType.Hunter;
                case "5":
                case "sorcerer":
                case "mage":
                case "колдун":
                    return RPClassType.Sorcerer;
                case "6":
                case "beggar":
                case "deprived":
                case "нищий":
                    return RPClassType.Beggar;
                default:
                    return RPClassType.None;
            }
        }
    }
}
