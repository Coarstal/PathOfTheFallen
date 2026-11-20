using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsEnemyOverhaul
{
    public static class EnemyLocalization
    {
        private static bool s_registered;

        public static bool IsRussian()
        {
            return Localization.instance != null &&
                   string.Equals(Localization.instance.GetSelectedLanguage(), "Russian", StringComparison.OrdinalIgnoreCase);
        }

        public static string Loc(string token)
        {
            if (Localization.instance != null)
            {
                return Localization.instance.Localize(token);
            }
            return token;
        }

        public static void RegisterAll()
        {
            if (s_registered) return;
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string eng = "English";
            loc.AddTranslation(ref eng, new Dictionary<string, string>
            {
                // 6 Combat Injuries
                { "ceo_se_bleeding", "Laceration" },
                { "ceo_se_bleeding_tip", "Deep bleeding wound.\n• Periodic blood loss.\n• <color=#FF6E6E>Sprinting or dodge-rolling doubles bleed damage!</color>\n• Standing or walking lets the wound clot faster." },
                { "ceo_se_bleeding_start", "Lacerated! Don't sprint!" },

                { "ceo_se_hamstrung", "Pierced Tendon" },
                { "ceo_se_hamstrung_tip", "Crippled leg tendon.\n• Movement speed: <color=#FF6E6E>-{0}%</color>\n• Jump & dodge stamina cost: <color=#FF6E6E>+{1}%</color>" },
                { "ceo_se_hamstrung_start", "Pierced Tendon!" },

                { "ceo_se_armorsunder", "Sundered Armor" },
                { "ceo_se_armorsunder_tip", "Crushing impact cracked your defenses.\n• Total Armor: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_armorsunder_start", "Armor Sundered!" },

                { "ceo_se_winded", "Winded" },
                { "ceo_se_winded_tip", "Heavy blow knocked the wind out of you.\n• Stamina regeneration: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_winded_start", "Winded!" },

                { "ceo_se_eitrburn", "Eitr Burn" },
                { "ceo_se_eitrburn_tip", "Unstable arcane backlash.\n• Eitr regeneration: <color=#FF6E6E>-{0}%</color>\n• <color=#FF6E6E>Spending Eitr burns {1}% of spent Eitr from your Health!</color>" },
                { "ceo_se_eitrburn_start", "Eitr Burn!" },

                { "ceo_se_rot", "Putrid Rot" },
                { "ceo_se_rot_tip", "Festering necrosis in your veins.\n• All incoming healing & regen: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_rot_start", "Inflicted with Putrid Rot!" },

                // Champion tags & Blood Dominion immunity
                { "ceo_champ_dominion_immune", "A Champion's will cannot be dominated!" },
                { "ceo_champ_tag", "CHAMPION" },
                { "ceo_dungeon_warden_tag", "CURSED WARDEN" },
                { "ceo_cursed_mob_tag", "Cursed" },

                // Cursed Dungeons
                { "ceo_cursed_enter_banner", "<color=#D44CFF>CURSED DUNGEON!</color>\n<size=78%>The exit is sealed by dark mist until the Dungeon Champion falls!</size>" },
                { "ceo_cursed_exit_locked", "<color=#FF5268>The exit is sealed by a curse! Slay the Dungeon Champion to escape!</color>" },
                { "ceo_cursed_exit_hover", "[<color=#FF5268>SEALED BY CURSE</color>]\nSlay the Cursed Champion inside to break the seal" },
                { "ceo_cursed_cleansed_banner", "<color=#7CFF9B>THE CURSE IS SHATTERED!</color>\n<size=80%>The dark mist fades and the exit is unsealed!</size>" },

                // Active Effects Raven panel section
                { "ceo_raven_injuries_header", "Active Combat Injuries" }
            });

            string rus = "Russian";
            loc.AddTranslation(ref rus, new Dictionary<string, string>
            {
                // 6 Боевых Травм
                { "ceo_se_bleeding", "Рваная рана" },
                { "ceo_se_bleeding_tip", "Глубокое кровотечение.\n• Периодическая потеря крови.\n• <color=#FF6E6E>Бег на спринте и перекаты усиливают кровотечение в 2 раза!</color>\n• Шаг или остановка ускоряют заживление раны." },
                { "ceo_se_bleeding_start", "Рваная рана! Не бегите!" },

                { "ceo_se_hamstrung", "Простреленное сухожилие" },
                { "ceo_se_hamstrung_tip", "Повреждено сухожилие ноги.\n• Скорость передвижения: <color=#FF6E6E>-{0}%</color>\n• Расход выносливости на прыжок и перекат: <color=#FF6E6E>+{1}%</color>" },
                { "ceo_se_hamstrung_start", "Прострелено сухожилие!" },

                { "ceo_se_armorsunder", "Раскол брони" },
                { "ceo_se_armorsunder_tip", "Сокрушительный удар проломил защиту.\n• Общая броня: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_armorsunder_start", "Раскол брони!" },

                { "ceo_se_winded", "Выбитое дыхание" },
                { "ceo_se_winded_tip", "Тяжёлая контузия сбила дыхание.\n• Восстановление выносливости: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_winded_start", "Сбито дыхание!" },

                { "ceo_se_eitrburn", "Ожог Эйтра" },
                { "ceo_se_eitrburn_tip", "Нестабильное магическое выгорание.\n• Восстановление Эйтра: <color=#FF6E6E>-{0}%</color>\n• <color=#FF6E6E>Трата Эйтра сжигает здоровье ({1}% от потраченного Эйтра)!</color>" },
                { "ceo_se_eitrburn_start", "Ожог Эйтра!" },

                { "ceo_se_rot", "Заражение" },
                { "ceo_se_rot_tip", "Трупное гниение отравляет кровь.\n• Эффективность любого лечения и регенерации: <color=#FF6E6E>-{0}%</color>" },
                { "ceo_se_rot_start", "Трупное заражение!" },

                // Чемпионы и иммунитет к Посоху Крови
                { "ceo_champ_dominion_immune", "Волю Чемпиона невозможно подчинить!" },
                { "ceo_champ_tag", "ЧЕМПИОН" },
                { "ceo_dungeon_warden_tag", "СТРАЖ ПРОКЛЯТИЯ" },
                { "ceo_cursed_mob_tag", "Проклятый" },

                // Проклятые Данжи
                { "ceo_cursed_enter_banner", "<color=#D44CFF>ПРОКЛЯТОЕ ПОДЗЕМЕЛЬЕ!</color>\n<size=78%>Выход запечатан мглой, пока жив Чемпион этого данжа!</size>" },
                { "ceo_cursed_exit_locked", "<color=#FF5268>Выход скован проклятием! Убейте Чемпиона данжа, чтобы выбраться!</color>" },
                { "ceo_cursed_exit_hover", "[<color=#FF5268>ЗАПЕЧАТАНО ПРОКЛЯТИЕМ</color>]\nУбейте Чемпиона подземелья, чтобы разрушить печать" },
                { "ceo_cursed_cleansed_banner", "<color=#7CFF9B>ПРОКЛЯТИЕ РАЗРУШЕНО!</color>\n<size=80%>Туман рассеивается, путь наружу открыт!</size>" },

                // Панель активных эффектов
                { "ceo_raven_injuries_header", "Боевые ранения" }
            });

            s_registered = true;
        }

        // ====================================================================
        // Procedural Lore-Friendly Champion Name Generator (RU + EN)
        // ====================================================================
        private static readonly string[] s_rusPrefixesMeadows = { "Старый Клык", "Кровавый Рог", "Дикий Коготь", "Лесной Вожак", "Шрам Чащи" };
        private static readonly string[] s_rusPrefixesForest = { "Мшистый Череп", "Корнелом", "Гнилой Корень", "Теневой Охотник", "Страж Чащобы", "Каменный Хребет" };
        private static readonly string[] s_rusPrefixesSwamp = { "Трупный Жнец", "Болотный Палач", "Топяной Ужас", "Ржавый Клинок", "Чумной Страж", "Костяной Гнев" };
        private static readonly string[] s_rusPrefixesMountain = { "Ледяной Клык", "Лавинный Крушитель", "Северный Потрошитель", "Морозный Вожак", "Коготь Бури" };
        private static readonly string[] s_rusPrefixesPlains = { "Кровавый Тотем", "Костолом Равнин", "Безумный Палач", "Чёрный Клык", "Ярость Яглута" };
        private static readonly string[] s_rusPrefixesMist = { "Панцирный Жнец", "Пожиратель Эйтра", "Туманный Потрошитель", "Вестник Улья" };
        private static readonly string[] s_rusPrefixesAsh = { "Пепельный Палач", "Обугленный Череп", "Пламенный Разрушитель", "Жнец Изумрудного Огня" };

        private static readonly string[] s_engPrefixesMeadows = { "Oldfang", "Gorehorn", "Wildclaw", "Stalker", "Scarhide" };
        private static readonly string[] s_engPrefixesForest = { "Mossskull", "Rootbreaker", "Blightbark", "Shadehunter", "Thicket Warden", "Stonespike" };
        private static readonly string[] s_engPrefixesSwamp = { "Grave-Reaper", "Bog Executioner", "Mire Horror", "Rustblade", "Plaguebound", "Bonewrath" };
        private static readonly string[] s_engPrefixesMountain = { "Frostfang", "Avalanche Crusher", "Rime Ripper", "Blizzard Alpha", "Stormclaw" };
        private static readonly string[] s_engPrefixesPlains = { "Bloodtotem", "Skullcrusher", "Frenzied Butcher", "Blackfang", "Wrath-Sworn" };
        private static readonly string[] s_engPrefixesMist = { "Carapace Reaper", "Eitr-Gorged", "Mist Ripper", "Hive Executioner" };
        private static readonly string[] s_engPrefixesAsh = { "Cinder Executioner", "Charred Skull", "Flame Ravager", "Emerald Reaper" };

        public static void GenerateChampionIdentity(Heightmap.Biome biome, int seed, out string rusTitle, out string engTitle)
        {
            System.Random rng = new System.Random(seed);
            string[] rusPool = GetPool(biome, true);
            string[] engPool = GetPool(biome, false);

            rusTitle = rusPool[rng.Next(rusPool.Length)];
            engTitle = engPool[rng.Next(engPool.Length)];
        }

        private static string[] GetPool(Heightmap.Biome biome, bool russian)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows:
                    return russian ? s_rusPrefixesMeadows : s_engPrefixesMeadows;
                case Heightmap.Biome.BlackForest:
                    return russian ? s_rusPrefixesForest : s_engPrefixesForest;
                case Heightmap.Biome.Swamp:
                    return russian ? s_rusPrefixesSwamp : s_engPrefixesSwamp;
                case Heightmap.Biome.Mountain:
                    return russian ? s_rusPrefixesMountain : s_engPrefixesMountain;
                case Heightmap.Biome.Plains:
                    return russian ? s_rusPrefixesPlains : s_engPrefixesPlains;
                case Heightmap.Biome.Mistlands:
                    return russian ? s_rusPrefixesMist : s_engPrefixesMist;
                case Heightmap.Biome.AshLands:
                case Heightmap.Biome.DeepNorth:
                    return russian ? s_rusPrefixesAsh : s_engPrefixesAsh;
                default:
                    return russian ? s_rusPrefixesForest : s_engPrefixesForest;
            }
        }
    }
}
