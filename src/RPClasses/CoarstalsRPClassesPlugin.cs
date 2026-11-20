using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using FinalMod;
using Jotunn.Managers;
using Jotunn.Utils;

namespace CoarstalsRPClasses
{
    public static class RPClassesModule
    {
        private static bool s_initialized;

        public static bool IsActive
        {
            get { return FinalModulesConfig.EnableRPClasses == null || FinalModulesConfig.EnableRPClasses.Value; }
        }

        public static void BindConfigs(ConfigFile config)
        {
            RPClassConfig.Bind(config);
        }

        public static void Init(ManualLogSource logger)
        {
            if (s_initialized) return;

            CoarstalsRPClassesPlugin.ModLogger = logger;

            RPLocalization.RegisterAll();
            RPStatusEffectRegistry.RegisterAll();
            RPPlayerClassManager.RegisterConsoleCommands();

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            SynchronizationManager.OnConfigurationSynchronized += OnConfigSynced;

            s_initialized = true;
            if (logger != null)
            {
                logger.LogInfo("[PathOfTheFallen] RPClasses module initialized.");
            }
        }

        public static void Shutdown()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            SynchronizationManager.OnConfigurationSynchronized -= OnConfigSynced;
            RPClassSelectionUI.DestroyUI();
            s_initialized = false;
        }

        private static void OnVanillaPrefabsAvailable()
        {
            if (!IsActive) return;
            try
            {
                RPPlayerClassManager.RegisterRebirthScrollPrefab();
            }
            catch (Exception ex)
            {
                if (CoarstalsRPClassesPlugin.ModLogger != null)
                {
                    CoarstalsRPClassesPlugin.ModLogger.LogError("Failed registering Rebirth Scroll prefab: " + ex);
                }
            }
        }

        private static void OnConfigSynced(object sender, ConfigurationSynchronizationEventArgs e)
        {
            if (!IsActive) return;
            Player p = Player.m_localPlayer;
            if (p != null)
            {
                RPPlayerClassManager.RefreshMaxVitals(p, refill: false);
            }
        }
    }

    public static class CoarstalsRPClassesPlugin
    {
        public const string PluginGUID = "com.coarstal.rpclasses";
        public const string PluginName = "Coarstal's RP Classes";
        public const string PluginVersion = "1.2.5";

        internal static ManualLogSource ModLogger;
    }
}
