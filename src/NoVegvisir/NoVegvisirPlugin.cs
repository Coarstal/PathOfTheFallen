using BepInEx.Configuration;
using BepInEx.Logging;
using FinalMod;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace NoVegvisir
{
    public static class NoVegvisirPlugin
    {
        public static ConfigEntry<bool> DestroyEntireStructure;

        public static bool IsActive
        {
            get { return FinalModulesConfig.EnableNoVegvisir == null || FinalModulesConfig.EnableNoVegvisir.Value; }
        }

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            DestroyEntireStructure = config.Bind(
                "06. No Vegvisir",
                "DestroyEntireGameObject",
                true,
                new ConfigDescription(
                    "Если true, полностью удаляет 3D-модель и объект вегвизира из мира. Если false, оставляет камень, но блокирует взаимодействие с ним.",
                    null,
                    adminAttr));
        }

        public static void Init(ManualLogSource logger)
        {
            if (logger != null)
            {
                logger.LogInfo("[PathOfTheFallen] NoVegvisir module initialized.");
            }
        }

        public static bool IsHildirVegvisir(Vegvisir veg)
        {
            if (veg == null) return false;

            if (!string.IsNullOrEmpty(veg.m_name) && veg.m_name.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(veg.m_hoverName) && veg.m_hoverName.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(veg.m_useText) && veg.m_useText.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (veg.m_locations != null)
            {
                for (int i = 0; i < veg.m_locations.Count; i++)
                {
                    Vegvisir.VegvisrLocation loc = veg.m_locations[i];
                    if (loc == null) continue;
                    if (!string.IsNullOrEmpty(loc.m_locationName) && loc.m_locationName.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (!string.IsNullOrEmpty(loc.m_pinName) && loc.m_pinName.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }

            Transform cur = veg.transform;
            while (cur != null)
            {
                if (!string.IsNullOrEmpty(cur.name) && cur.name.IndexOf("hildir", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                cur = cur.parent;
            }

            return false;
        }

        public static void CleanVegvisir(GameObject go)
        {
            if (go == null || !IsActive)
                return;

            Vegvisir[] vegvisirs = go.GetComponentsInChildren<Vegvisir>(true);
            if (vegvisirs == null || vegvisirs.Length == 0)
                return;

            for (int i = 0; i < vegvisirs.Length; i++)
            {
                Vegvisir veg = vegvisirs[i];
                if (veg == null || IsHildirVegvisir(veg)) continue;

                if (DestroyEntireStructure != null && DestroyEntireStructure.Value)
                {
                    if (ZNetScene.instance != null && veg.gameObject.GetComponent<ZNetView>() != null)
                    {
                        ZNetScene.instance.Destroy(veg.gameObject);
                    }
                    else
                    {
                        Object.Destroy(veg.gameObject);
                    }
                }
                else
                {
                    Object.Destroy(veg);
                }
            }
        }

        private static bool MightContainVegvisir(GameObject go)
        {
            if (go == null) return false;
            string n = go.name;
            if (string.IsNullOrEmpty(n)) return false;

            // Fast zero-hierarchy-walk filter for `ZNetScene.CreateObject` (which runs hundreds of times when loading dungeons/zones
            // for torches, bones, creatures, drops, and VFX that can NEVER contain a Vegvisir!)
            if (n.IndexOf("vegvisir", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("runestone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("location", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("crypt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("tomb", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("dvergr", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("altar", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("stonecircle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("ruin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("tower", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("fortress", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return go.GetComponent<Location>() != null || go.GetComponent<Vegvisir>() != null;
        }

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        public static class ZNetScene_CreateObject_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref GameObject __result)
            {
                if (__result != null && IsActive && MightContainVegvisir(__result))
                {
                    CleanVegvisir(__result);
                }
            }
        }

        [HarmonyPatch(typeof(Room), "Awake")]
        public static class Room_Awake_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Room __instance)
            {
                if (__instance != null && IsActive)
                {
                    CleanVegvisir(__instance.gameObject);
                }
            }
        }

        [HarmonyPatch(typeof(Vegvisir), "Interact")]
        public static class Vegvisir_Interact_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Vegvisir __instance)
            {
                if (IsActive && !IsHildirVegvisir(__instance))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Vegvisir), "GetHoverText")]
        public static class Vegvisir_HoverText_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Vegvisir __instance, ref string __result)
            {
                if (IsActive && !IsHildirVegvisir(__instance))
                {
                    __result = string.Empty;
                    return false;
                }
                return true;
            }
        }
    }
}
