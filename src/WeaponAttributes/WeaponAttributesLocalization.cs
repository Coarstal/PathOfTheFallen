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
                { "wa_msg_upgraded_ok", "Weapon {0} enchant upgraded to Lv.{1}!" },

                { "wa_item_soul_crystal_empty", "Empty Soul Crystal" },
                { "wa_item_soul_crystal_empty_desc", "A smoky runic quartz cage sold exclusively by Hildir.\n<color=#FFD700>Right-click in inventory</color> to extract the Boss Soul enchantment from an enchanted weapon into this crystal (breaks the weapon to 0 repairable durability and inflicts a 3s elemental soul backlash)." },
                { "wa_item_soul_crystal_filled", "Sealed Soul Crystal" },
                { "wa_item_soul_crystal_filled_desc", "A runic crystal blazing with a captured Forsaken Boss Soul. Shatter it inside a <color=#DDA0DD>Soul Extraction Cauldron</color> to recover trophies." },

                { "wa_soul_title_eikthyr", "Soul of Eikthyr {0}" },
                { "wa_soul_title_elder", "Soul of The Elder {0}" },
                { "wa_soul_title_bonemass", "Soul of Bonemass {0}" },
                { "wa_soul_title_moder", "Soul of Moder {0}" },
                { "wa_soul_title_yagluth", "Soul of Yagluth {0}" },
                { "wa_soul_title_queen", "Soul of The Queen {0}" },
                { "wa_soul_title_fader", "Soul of Fader {0}" },

                { "wa_crystal_tooltip_sealed", "Sealed soul: {0}" },
                { "wa_crystal_tooltip_mob_return", "Guaranteed return ({2}%): <color=#00FFFF>{0}x {1}</color>" },
                { "wa_crystal_tooltip_boss_chance", "Boss soul return chance ({1}%): <color=#FFD700>1x {0}</color>" },

                { "wa_piece_soul_cauldron", "Soul Extraction Cauldron" },
                { "wa_piece_soul_cauldron_desc", "An enchanted runic cauldron with a self-stirring ladle. Dissolves Sealed Soul Crystals to recover 50% of invested mob trophies and a 25% chance at the Boss Trophy." },

                { "wa_extract_ui_title", "SOUL EXTRACTION" },
                { "wa_extract_ui_subtitle", "Choose an enchanted weapon in your inventory to transfer its soul into the crystal:" },
                { "wa_extract_ui_warning", "⚠ The weapon will break to 0 durability (you can repair it normally at a Forge or Workbench)." },
                { "wa_extract_ui_confirm", "Extract Soul" },
                { "wa_extract_ui_cancel", "Cancel" },

                { "wa_cauldron_recipe_title", "Shatter: {0}" },
                { "wa_cauldron_craft_type", "Soul Extraction" },
                { "wa_cauldron_desc_intro", "Dissolve this crystal in the boiling soul brew to reclaim bound trophies:" },
                { "wa_cauldron_yield_header", "Extraction Yield:" },
                { "wa_cauldron_btn_extract", "Extract" },

                { "wa_msg_no_enchanted_weapons", "You have no enchanted weapons in your inventory!" },
                { "wa_msg_missing_empty_crystal", "Empty Soul Crystal not found in inventory!" },
                { "wa_msg_soul_extracted_ok", "Extracted {0}!\n«{1}» broke (repair at Forge/Workbench)." },
                { "wa_msg_cauldron_won_boss", "Boss Soul recovered! Received: 1x {0} and {1}x {2}!" },
                { "wa_msg_cauldron_mob_only", "Crystal dissolved! Received: {0}x {1} (Boss Soul dissipated)." }
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
                { "wa_msg_upgraded_ok", "Чары оружия «{0}» усилены до Ур.{1}!" },

                { "wa_item_soul_crystal_empty", "Пустой кристалл душ" },
                { "wa_item_soul_crystal_empty_desc", "Дымчатый рунический кристалл в бронзовой оправе (продаётся у Хильдир).\n<color=#FFD700>Нажмите ПКМ в инвентаре</color>, чтобы перенести чары с выбранного оружия в кристалл (оружие сломается до 0 прочности с возможностью починки на кузнице, а душа обожжёт вас своей стихией на 3 сек.)." },
                { "wa_item_soul_crystal_filled", "Кристалл с душой" },
                { "wa_item_soul_crystal_filled_desc", "Рунический кристалл, в котором заточена душа Павшего босса. Разбейте его в <color=#DDA0DD>Котле для извлечения Душ</color>, чтобы вернуть часть вложенных трофеев." },

                { "wa_soul_title_eikthyr", "Душа Эйктюра {0}" },
                { "wa_soul_title_elder", "Душа Древнего {0}" },
                { "wa_soul_title_bonemass", "Душа Массы Костей {0}" },
                { "wa_soul_title_moder", "Душа Матери {0}" },
                { "wa_soul_title_yagluth", "Душа Яглута {0}" },
                { "wa_soul_title_queen", "Душа Королевы {0}" },
                { "wa_soul_title_fader", "Душа Прородителя {0}" },

                { "wa_crystal_tooltip_sealed", "Запечатана: {0}" },
                { "wa_crystal_tooltip_mob_return", "Гарантированный возврат ({2}%): <color=#00FFFF>{0} шт. — {1}</color>" },
                { "wa_crystal_tooltip_boss_chance", "Шанс возврата души босса ({1}%): <color=#FFD700>1 шт. — {0}</color>" },

                { "wa_piece_soul_cauldron", "Котёл для извлечения Душ" },
                { "wa_piece_soul_cauldron_desc", "Зачарованный рунический котёл с самокрутящейся поварёшкой. Расщепляет кристаллы с душами, возвращая 50% вложенных трофеев мобов и с шансом 25% — голову самого босса." },

                { "wa_extract_ui_title", "ИЗВЛЕЧЕНИЕ ДУШИ" },
                { "wa_extract_ui_subtitle", "Выберите зачарованное оружие в инвентаре, чтобы перенести его душу в кристалл:" },
                { "wa_extract_ui_warning", "⚠ Оружие сломается до 0 прочности, но его можно бесплатно починить на кузнице или верстаке." },
                { "wa_extract_ui_confirm", "Извлечь душу" },
                { "wa_extract_ui_cancel", "Отмена" },

                { "wa_cauldron_recipe_title", "Разбить: {0}" },
                { "wa_cauldron_craft_type", "Извлечение душ" },
                { "wa_cauldron_desc_intro", "Расщепите запечатанный кристалл в кипящем вареве душ, чтобы высвободить трофеи:" },
                { "wa_cauldron_yield_header", "Результат извлечения:" },
                { "wa_cauldron_btn_extract", "Извлечь" },

                { "wa_msg_no_enchanted_weapons", "В инвентаре нет зачарованного оружия!" },
                { "wa_msg_missing_empty_crystal", "Пустой кристалл душ не найден в инвентаре!" },
                { "wa_msg_soul_extracted_ok", "Получена {0}!\nОружие «{1}» сломано (почините на кузнице)." },
                { "wa_msg_cauldron_won_boss", "Душа босса вернулась! Получено: {0} и {1}x {2}!" },
                { "wa_msg_cauldron_mob_only", "Кристалл разбит! Получено: {0}x {1} (душа босса рассеялась)." }
            });

            s_registered = true;
        }
    }
}
