using BepInEx.Configuration;
using BepInEx.Logging;
using FinalMod;

namespace CoarstalsEnemyOverhaul
{
    public static class EnemyOverhaulModule
    {
        private static bool s_initialized;

        public static bool IsActive
        {
            get { return FinalModulesConfig.EnableEnemyOverhaul == null || FinalModulesConfig.EnableEnemyOverhaul.Value; }
        }

        public static void BindConfigs(ConfigFile config)
        {
            EnemyOverhaulConfig.Bind(config);
        }

        public static void Init(ManualLogSource logger)
        {
            if (s_initialized) return;

            CoarstalsEnemyOverhaulPlugin.ModLogger = logger;

            EnemyLocalization.RegisterAll();
            EnemyInjuryManager.RegisterAll();
            EnemyHarmonyPatches.RegisterConsoleCommands();

            s_initialized = true;
            if (logger != null)
            {
                logger.LogInfo("[PathOfTheFallen] EnemyOverhaul module initialized.");
            }
        }

        public static void Shutdown()
        {
            CursedDungeonSystem.ResetSessionFlag();
            ChampionSystem.ResetSessionFlag();
            s_initialized = false;
        }
    }

    public static class CoarstalsEnemyOverhaulPlugin
    {
        public const string PluginGUID = "com.coarstal.enemyoverhaul";
        public const string PluginName = "Coarstal's Enemy Overhaul";
        public const string PluginVersion = "1.1.1";

        internal static ManualLogSource ModLogger;
    }
}
