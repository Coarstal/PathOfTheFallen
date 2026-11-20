using BepInEx.Configuration;
using Jotunn.Utils;

namespace FinalMod
{
    public static class FinalModulesConfig
    {
        public const string SectionName = "00. Modules";

        public static ConfigEntry<bool> EnableRPClasses;
        public static ConfigEntry<bool> EnableWeaponAttributes;
        public static ConfigEntry<bool> EnableWeaponPack;
        public static ConfigEntry<bool> EnableEitrArmor;
        public static ConfigEntry<bool> EnableEnemyOverhaul;
        public static ConfigEntry<bool> EnableNoVegvisir;

        public static void Bind(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true, Order = 100 };

            EnableRPClasses = config.Bind(
                SectionName,
                "EnableRPClasses",
                true,
                new ConfigDescription("Включить модуль ролевой системы классов (RP Classes: 6 классов, пассивки, Свиток Перерождения у Хильдир).", null, adminAttr));

            EnableWeaponAttributes = config.Bind(
                SectionName,
                "EnableWeaponAttributes",
                true,
                new ConfigDescription("Включить модуль зачарования оружия душами боссов (WeaponAttributes: Стол Зачарования, 4 улучшения станции и 5 тиров чар).", null, adminAttr));

            EnableWeaponPack = config.Bind(
                SectionName,
                "EnableWeaponPack",
                true,
                new ConfigDescription("Включить модуль нового снаряжения и кулинарии (WeaponPack: 2 вида парных топоров, 5 магических посохов включая Посох Очищения и 6 блюд на Эйтр).", null, adminAttr));

            EnableEitrArmor = config.Bind(
                SectionName,
                "EnableEitrArmor",
                true,
                new ConfigDescription("Включить модуль ранней и средней магической брони на Эйтр (EitrArmor: сеты Чёрного Леса и Гор).", null, adminAttr));

            EnableEnemyOverhaul = config.Bind(
                SectionName,
                "EnableEnemyOverhaul",
                true,
                new ConfigDescription("Включить модуль усиления врагов и данжей (EnemyOverhaul: Именные Чемпионы Охоты, 6 Боевых Травм и Проклятые Запечатанные Подземелья).", null, adminAttr));

            EnableNoVegvisir = config.Bind(
                SectionName,
                "EnableNoVegvisir",
                true,
                new ConfigDescription("Включить модуль удаления рунных камней-вегвизиров боссов в мире и подземельях (NoVegvisir).", null, adminAttr));
        }
    }
}
