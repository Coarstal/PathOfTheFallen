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
                if (veg == null) continue;

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

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        public static class ZNetScene_CreateObject_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref GameObject __result)
            {
                if (__result != null && IsActive)
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
            public static bool Prefix()
            {
                if (IsActive)
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
            public static bool Prefix(ref string __result)
            {
                if (IsActive)
                {
                    __result = string.Empty;
                    return false;
                }
                return true;
            }
        }
    }
}
