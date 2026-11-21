using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using CoarstalsRPClasses;
using WeaponAttributesMod;
using NewDualAxesMod;
using CoarstalsEitrArmorMod;
using CoarstalsEnemyOverhaul;
using NoVegvisir;

namespace FinalMod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class PathOfTheFallenPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.coarstal.pathofthefallen";
        public const string PluginName = "Valheim - Path of the Fallen";
        public const string PluginVersion = "1.0.4";

        internal static ManualLogSource ModLogger;
        private Harmony m_harmony;

        private void Awake()
        {
            ModLogger = Logger;

            // 00. Master Module Toggles
            FinalModulesConfig.Bind(Config);

            // 01..06. Bind all module configurations in unified com.coarstal.pathofthefallen.cfg order
            RPClassesModule.BindConfigs(Config);
            WeaponAttributesPlugin.BindConfigs(Config);
            NewDualAxesPlugin.BindConfigs(Config);
            CoarstalsEitrArmorPlugin.BindConfigs(Config);
            EnemyOverhaulModule.BindConfigs(Config);
            NoVegvisirPlugin.BindConfigs(Config);

            m_harmony = new Harmony(PluginGUID);

            // Initialize & patch only enabled modules
            if (FinalModulesConfig.EnableRPClasses.Value)
            {
                RPClassesModule.Init(Logger);
                PatchModuleNamespace(m_harmony, "CoarstalsRPClasses");
            }

            if (FinalModulesConfig.EnableWeaponAttributes.Value)
            {
                WeaponAttributesPlugin.Init(Logger);
                PatchModuleNamespace(m_harmony, "WeaponAttributesMod");
            }

            if (FinalModulesConfig.EnableWeaponPack.Value)
            {
                NewDualAxesPlugin.Init(Logger);
                PatchModuleNamespace(m_harmony, "NewDualAxesMod");
            }

            if (FinalModulesConfig.EnableEitrArmor.Value)
            {
                CoarstalsEitrArmorPlugin.Init(Logger);
                PatchModuleNamespace(m_harmony, "CoarstalsEitrArmorMod");
            }

            if (FinalModulesConfig.EnableEnemyOverhaul.Value)
            {
                EnemyOverhaulModule.Init(Logger);
                PatchModuleNamespace(m_harmony, "CoarstalsEnemyOverhaul");
            }

            if (FinalModulesConfig.EnableNoVegvisir.Value)
            {
                NoVegvisirPlugin.Init(Logger);
                PatchModuleNamespace(m_harmony, "NoVegvisir");
            }

            Logger.LogInfo(string.Format("{0} v{1} loaded successfully!", PluginName, PluginVersion));
        }

        private void OnDestroy()
        {
            RPClassesModule.Shutdown();
            WeaponAttributesPlugin.Shutdown();
            NewDualAxesPlugin.Shutdown();
            CoarstalsEitrArmorPlugin.Shutdown();
            EnemyOverhaulModule.Shutdown();

            if (m_harmony != null)
            {
                m_harmony.UnpatchSelf();
            }
        }

        private static void PatchModuleNamespace(Harmony harmony, string targetNamespace)
        {
            if (harmony == null || string.IsNullOrEmpty(targetNamespace)) return;

            Type[] allTypes = AccessTools.GetTypesFromAssembly(typeof(PathOfTheFallenPlugin).Assembly);
            int patchedClasses = 0;

            for (int i = 0; i < allTypes.Length; i++)
            {
                Type t = allTypes[i];
                if (t == null || t.Namespace != targetNamespace) continue;

                try
                {
                    List<HarmonyMethod> harmonyAttrs = HarmonyMethodExtensions.GetFromType(t);
                    if (harmonyAttrs != null && harmonyAttrs.Count > 0)
                    {
                        new PatchClassProcessor(harmony, t).Patch();
                        patchedClasses++;
                    }
                }
                catch (Exception ex)
                {
                    if (ModLogger != null)
                    {
                        ModLogger.LogError(string.Format("[PathOfTheFallen] Failed patching {0}: {1}", t.FullName, ex));
                    }
                }
            }

            if (ModLogger != null)
            {
                ModLogger.LogInfo(string.Format("[PathOfTheFallen] Patched {0} Harmony class(es) in namespace '{1}'.", patchedClasses, targetNamespace));
            }
        }
    }
}
