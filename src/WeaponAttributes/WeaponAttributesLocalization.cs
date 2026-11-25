using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace WeaponAttributesMod
{
    public static class WeaponAttributesLocalization
    {
        private static bool s_registered;

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
                { "wa_piece_table", "Enchanting Table" },
                { "wa_piece_table_desc", "An ancient boss soul forge with a runic anvil and crystal brazier for enchanting weapons." },

                { "wa_ext_elder", "Elder's Root Totem" },
                { "wa_ext_elder_desc", "An ancient magical root bound in bronze runic rings, infused with the energy of the Black Forest. Increases Enchanting Table level (+1)." },

                { "wa_ext_bonemass", "Bonemass Poison Urn" },
                { "wa_ext_bonemass_desc", "A dark forged iron cauldron adorned with skulls and boiling toxic swamp ooze. Increases Enchanting Table level (+1)." },

                { "wa_ext_moder", "Moder's Crystal Altar" },
                { "wa_ext_moder_desc", "A silver pedestal with dragon wings and a cluster of glowing frost crystals. Increases Enchanting Table level (+1)." },

                { "wa_ext_yagluth", "Yagluth's Eternal Flame" },
                { "wa_ext_yagluth_desc", "A ritual platform of black metal and gold burning with the undying arcane flame of the Goblin King. Increases Enchanting Table level (+1)." },

                { "wa_tooltip_header", "◆ Enchantment (Lv.{0}/5):" },
                { "wa_summon_suffix", " (summons)" },
                { "wa_roots_suffix", " (hit & roots)" },
                { "wa_boss_short_eikthyr", "Eikthyr" },
                { "wa_boss_short_elder", "Elder" },
                { "wa_boss_short_bonemass", "Bonemass" },
                { "wa_boss_short_moder", "Moder" },
                { "wa_boss_short_yagluth", "Yagluth" },
                { "wa_boss_short_queen", "Queen" },
                { "wa_boss_short_fader", "Fader" },
                { "wa_recipe_enchant", "Enchant: {0} ({1})" },
                { "wa_recipe_upgrade", "Upgrade Enchant: {0} (+{1})" },
                { "wa_craft_type_base", "Enchantment" },
                { "wa_craft_type_lvl", "Enchantment Lv.{0}" },
                { "wa_preview_header", "Enchant stats (Level {0}/5):" },

                { "wa_msg_already_enchanted", "Weapon is already enchanted!" },
                { "wa_msg_not_ready_level", "Weapon is not ready for this upgrade level!" },
                { "wa_msg_missing_enchant_trophies", "Missing required trophies to enchant weapon!" },
                { "wa_msg_missing_upgrade_trophies", "Missing required trophies to upgrade enchant!" },
                { "wa_msg_enchanted_ok", "Weapon {0} enchanted!" },
                { "wa_msg_upgraded_ok", "Weapon {0} enchant upgraded to Lv.{1}!" }
            });

            string rus = "Russian";
            loc.AddTranslation(ref rus, new Dictionary<string, string>
            {
                { "wa_piece_table", "Стол зачарования" },
                { "wa_piece_table_desc", "Древняя кузня душ боссов с рунической наковальней и кристальной жаровней для зачарования оружия." },

                { "wa_ext_elder", "Корневой тотем Древнего" },
                { "wa_ext_elder_desc", "Древний магический корень в бронзовых рунических кольцах, напитанный силой Чёрного леса. Повышает уровень Стола зачарования (+1)." },

                { "wa_ext_bonemass", "Ядовитая урна Массы Костей" },
                { "wa_ext_bonemass_desc", "Тёмный кованый котёл с черепами и бурлящей ядовитой болотной жижей. Повышает уровень Стола зачарования (+1)." },

                { "wa_ext_moder", "Кристальный алтарь Моудер" },
                { "wa_ext_moder_desc", "Серебряный пьедестал с крыльями дракона и гроздью сияющих морозных кристаллов. Повышает уровень Стола зачарования (+1)." },

                { "wa_ext_yagluth", "Вечное пламя Яглута" },
                { "wa_ext_yagluth_desc", "Ритуальная жаровня из чёрного металла и золота, пылающая негасимым пламенем короля гоблинов. Повышает уровень Стола зачарования (+1)." },

                { "wa_tooltip_header", "◆ Зачарование (Ур.{0}/5):" },
                { "wa_summon_suffix", " (призыв)" },
                { "wa_roots_suffix", " (удар и корни)" },
                { "wa_boss_short_eikthyr", "Эйктюр" },
                { "wa_boss_short_elder", "Древний" },
                { "wa_boss_short_bonemass", "Масса Костей" },
                { "wa_boss_short_moder", "Матерь" },
                { "wa_boss_short_yagluth", "Яглут" },
                { "wa_boss_short_queen", "Королева" },
                { "wa_boss_short_fader", "Прородитель" },
                { "wa_recipe_enchant", "Зачаровать: {0} ({1})" },
                { "wa_recipe_upgrade", "Усилить чары: {0} (+{1})" },
                { "wa_craft_type_base", "Зачарование" },
                { "wa_craft_type_lvl", "Зачарование Ур.{0}" },
                { "wa_preview_header", "Свойства чар (Уровень {0}/5):" },

                { "wa_msg_already_enchanted", "Оружие уже зачаровано!" },
                { "wa_msg_not_ready_level", "Оружие ещё не готово к этому уровню чар!" },
                { "wa_msg_missing_enchant_trophies", "Не хватает трофеев для зачарования оружия!" },
                { "wa_msg_missing_upgrade_trophies", "Не хватает трофеев для усиления чар!" },
                { "wa_msg_enchanted_ok", "Оружие «{0}» зачаровано!" },
                { "wa_msg_upgraded_ok", "Чары оружия «{0}» усилены до Ур.{1}!" }
            });

            s_registered = true;
        }
    }
}
