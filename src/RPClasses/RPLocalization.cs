using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CoarstalsRPClasses
{
    public static class RPLocalization
    {
        public static void RegisterAll()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string langEn = "English";
            loc.AddTranslation(ref langEn, new Dictionary<string, string>
            {
                // Class Names
                { "rp_class_none", "Classless" },
                { "rp_class_warrior", "Warrior" },
                { "rp_class_knight", "Knight" },
                { "rp_class_thief", "Thief" },
                { "rp_class_hunter", "Hunter" },
                { "rp_class_sorcerer", "Sorcerer" },
                { "rp_class_beggar", "Beggar" },

                // Class Subtitles / Roles
                { "rp_role_warrior", "Fierce Melee Vanguard" },
                { "rp_role_knight", "Armored Bulwark Tank" },
                { "rp_role_thief", "Shadow Backstabber" },
                { "rp_role_hunter", "Swift Marksman" },
                { "rp_role_sorcerer", "Arcane Eitr Weaver" },
                { "rp_role_beggar", "Pack-Mule Outcast" },

                // Class Lore
                { "rp_lore_warrior", "Tempered by relentless close-quarters bloodshed. Strikes fiercely with any melee weapon, shunning bows and arcane craft." },
                { "rp_lore_knight", "Sworn to stand unyielding behind steel plate. Gains immense armor when fully clad, though heavy plates slow every step and roll." },
                { "rp_lore_thief", "Strikes unseen from the dark. Excels with daggers and deadly blows to the enemy's back, trading away armor and shield mastery." },
                { "rp_lore_hunter", "Master of bowstring, crossbow bolt, and thrown spear. Deadly from afar, yet vulnerable when forced into heavy melee." },
                { "rp_lore_sorcerer", "Born with an innate wellspring of Eitr in a fragile vessel. Channels the ancient blessing of the Forsaken stones and commands arcane staves at the cost of physical strength." },
                { "rp_lore_beggar", "Possesses nothing save a broad back and scraps of every trade. Carries vast burdens with minor universal talents, yet learns slowly." },

                // Passive Skills (All 6 Classes)
                { "rp_se_braveheart", "Brave Heart" },
                { "rp_se_braveheart_lore", "Near death, battle fury ignites — every strike lands with desperate might." },
                { "rp_se_unwavering", "Unwavering" },
                { "rp_se_unwavering_lore", "An ironclad stance forged to hold the front line with shield and full plate." },
                { "rp_se_shadow_ascension", "Shadow Ascension" },
                { "rp_se_shadow_ascension_lore", "Melds into darkness, striking unsuspecting prey with lethal precision." },
                { "rp_se_falcon_eye", "Falcon's Swiftness" },
                { "rp_se_falcon_eye_lore", "Trained hands draw bowstrings and crank crossbows in the blink of an eye." },
                { "rp_se_archmage", "Archmage" },
                { "rp_se_archmage_lore", "Deep attunement to the Forsaken stones prolongs their blessing and hastens the awakening of ancient power." },
                { "rp_se_ineptitude", "Ineptitude" },
                { "rp_se_ineptitude_lore", "Lack of formal training slows mastery of every discipline." },

                // Rebirth Scroll Item
                { "item_rp_rebirth_scroll", "Contract of Rebirth" },
                { "item_rp_rebirth_scroll_desc", "An enchanted parchment bearing Hildir's golden seal. Read it from your inventory to forsake your old path and choose a new RP class." },
                { "msg_rp_class_chosen", "Destiny sealed: {0}" },
                { "msg_rp_rebirth_used", "Hildir's seal burns away — your fate is rewritten!" },

                // UI Section Headers & Buttons
                { "rp_ui_title_initial", "CHOOSE YOUR DESTINY" },
                { "rp_ui_subtitle_initial", "Select your permanent Roleplay Class. Later it can only be changed via Hildir's Contract for 1000 Gold." },
                { "rp_ui_title_respec", "CONTRACT OF REBIRTH" },
                { "rp_ui_subtitle_respec", "Choose a new Roleplay Class. Your Contract of Rebirth will be consumed only upon confirmation." },
                { "rp_ui_bonuses", "Advantages" },
                { "rp_ui_passive", "Passive Skill" },
                { "rp_ui_debuffs", "Weaknesses" },
                { "rp_ui_confirm", "Confirm Path" },
                { "rp_ui_cancel", "Cancel" },
                { "rp_ui_current", "Current" },
                { "rp_hud_passive_section", "Class Overview" },

                // Unified Standardized Stat Terminology (100% consistent across all 6 classes!)
                { "rp_stat_base_hp", "Base Health" },
                { "rp_stat_base_stamina", "Base Stamina" },
                { "rp_stat_base_eitr", "Base Eitr" },
                { "rp_stat_eitr_regen", "Eitr Regeneration" },
                { "rp_stat_heavy_armor_eitr", "Eitr Regen (in Heavy Armor)" },
                { "rp_stat_carry_weight", "Carry Weight" },
                { "rp_stat_move_speed", "Movement Speed" },
                { "rp_stat_shield_move_speed", "Movement Speed (with Shield)" },
                { "rp_stat_dodge_stam", "Dodge Stamina Cost" },
                { "rp_stat_sneak_stam", "Sneak Stamina Cost" },
                { "rp_stat_sneak", "Sneak Skill" },
                { "rp_stat_base_armor", "Base Armor" },
                { "rp_stat_total_armor", "Total Armor" },
                { "rp_stat_full_set_armor", "Total Armor (with Raised Shield)" },
                { "rp_stat_block_power", "Block Power" },
                { "rp_stat_shield_block", "Block Power (with Shield)" },
                { "rp_stat_parried_dmg", "Damage to Parried Enemies" },
                { "rp_stat_melee_dmg", "Melee Weapon Damage" },
                { "rp_stat_full_hp_enemy_dmg", "Damage to Full-Health Enemies" },
                { "rp_stat_heavy_melee_dmg", "Melee Damage (except Knives & Spears)" },
                { "rp_stat_knife_dmg", "Knife Damage" },
                { "rp_stat_back_dmg", "Back Attack Damage" },
                { "rp_stat_stealth_mult", "Sneak Attack Multiplier" },
                { "rp_stat_night_dmg", "All Damage (at Night)" },
                { "rp_stat_ranged_dmg", "Bow & Crossbow Damage" },
                { "rp_stat_ranged_spear_dmg", "Bow, Crossbow & Spear Throw Damage" },
                { "rp_stat_ranged_speed", "Bow Draw & Crossbow Reload Speed" },
                { "rp_stat_bow_draw_stam", "Bow Draw Stamina Cost" },
                { "rp_stat_hunter_combo", "{0} Consecutive Hits on Target" },
                { "rp_stat_hunter_combo_val", "next shot x{0} damage" },
                { "rp_stat_magic_dmg", "Magic Weapon Damage" },
                { "rp_stat_status_dmg", "Status Effect Damage" },
                { "rp_stat_gp_duration", "Forsaken Power Duration" },
                { "rp_stat_gp_duration_val", "+{0}% ({1} min)" },
                { "rp_stat_gp_cooldown", "Forsaken Power Cooldown" },
                { "rp_stat_gp_cooldown_val", "-{0}% ({1} min)" },
                { "rp_stat_phys_dmg", "Physical Weapon Damage" },
                { "rp_stat_low_hp_dmg", "All Damage (below {0}% Player Health)" },
                { "rp_stat_warrior_onslaught", "Bloody Onslaught" },
                { "rp_stat_warrior_onslaught_val", "+{0}% attack speed per hit for {1}s (up to +{2}%)" },
                { "rp_stat_knight_bulwark", "Steel Wall" },
                { "rp_stat_knight_bulwark_val", "+{0}% total armor while raising shield" },
                { "rp_stat_thief_night_veil", "Night Stalker" },
                { "rp_stat_thief_night_veil_val", "+{0}% all damage, -{1}% stamina cost & +{2}% move speed at night" },
                { "rp_stat_sorcerer_echo", "Arcane Resonance" },
                { "rp_stat_sorcerer_echo_val", "every {0}th cast costs 0 Eitr" },
                { "rp_stat_beggar_stomach", "Iron Stomach" },
                { "rp_stat_beggar_stomach_val", "+{0}% Health, Stamina & Eitr from food, +{1}% duration" },
                { "rp_stat_all_dmg", "All Damage Dealt" },
                { "rp_stat_skill_xp", "All Skill Experience Gain" }
            });

            string langRu = "Russian";
            loc.AddTranslation(ref langRu, new Dictionary<string, string>
            {
                // Названия классов
                { "rp_class_none", "Без класса" },
                { "rp_class_warrior", "Воин" },
                { "rp_class_knight", "Рыцарь" },
                { "rp_class_thief", "Вор" },
                { "rp_class_hunter", "Охотник" },
                { "rp_class_sorcerer", "Колдун" },
                { "rp_class_beggar", "Нищий" },

                // Подзаголовки ролей
                { "rp_role_warrior", "Яростный боец ближнего боя" },
                { "rp_role_knight", "Несокрушимый латный танк" },
                { "rp_role_thief", "Мастер теней и ударов в спину" },
                { "rp_role_hunter", "Стремительный стрелок" },
                { "rp_role_sorcerer", "Адепт тайного Эйтра" },
                { "rp_role_beggar", "Выносливый бродяга-универсал" },

                // Лор классов
                { "rp_lore_warrior", "Закалён в бесчисленных схватках лицом к лицу. Сокрушает врагов любым оружием ближнего боя, презирая луки и колдовство." },
                { "rp_lore_knight", "Непоколебимый защитник, закованный в броню с головы до пят. Полный доспех делает его стеной стали ценой скорости и ловкости." },
                { "rp_lore_thief", "Разит беззвучно из тени и заходит за спину врага. Смертоносен с клинками, но плохо держит прямой удар и блок щитом." },
                { "rp_lore_hunter", "Виртуоз тетивы, арбалетного болта и метательного копья. Смертельно опасен на дистанции, но уязвим в тяжёлой рубке." },
                { "rp_lore_sorcerer", "От рождения наделён источником Эйтра в хрупком теле. Черпает древнюю мощь жертвенных камней Павших и повелевает посохами в обмен на физическую силу." },
                { "rp_lore_beggar", "За душой ни гроша, зато широкая спина и крупицы чужих ремёсел. Уносит огромные грузы, но медленнее постигает науки." },

                // Пассивные навыки всех 6 классов
                { "rp_se_braveheart", "Храброе сердце" },
                { "rp_se_braveheart_lore", "На грани гибели кровь закипает яростью — каждый удар становится сокрушительнее." },
                { "rp_se_unwavering", "Непоколебимость" },
                { "rp_se_unwavering_lore", "Несокрушимая стойка латника: броня крепчает, а щит становится продолжением руки." },
                { "rp_se_shadow_ascension", "Восхождение в тени" },
                { "rp_se_shadow_ascension_lore", "Растворяясь во мраке, наносит смертоносный удар ничего не подозревающей жертве." },
                { "rp_se_falcon_eye", "Соколиная сноровка" },
                { "rp_se_falcon_eye_lore", "Отточенные движения рук молниеносно натягивают тетиву и взводят арбалет." },
                { "rp_se_archmage", "Архимаг" },
                { "rp_se_archmage_lore", "Духовная связь с камнями Павших продлевает их благословение и ускоряет пробуждение древней силы." },
                { "rp_se_ineptitude", "Бездарность" },
                { "rp_se_ineptitude_lore", "Отсутствие школы и дисциплины замедляет освоение любых навыков." },

                // Свиток Хильдир
                { "item_rp_rebirth_scroll", "Свиток Перерождения" },
                { "item_rp_rebirth_scroll_desc", "Древний пергамент с золотой печатью Хильдир. Используйте из инвентаря (ПКМ), чтобы отречься от прежнего пути и выбрать новый класс." },
                { "msg_rp_class_chosen", "Путь избран: {0}" },
                { "msg_rp_rebirth_used", "Печать Хильдир сгорает в пламени — ваша судьба переписана!" },

                // Заголовки разделов и кнопки UI
                { "rp_ui_title_initial", "ВЫБЕРИТЕ СВОЮ СУДЬБУ" },
                { "rp_ui_subtitle_initial", "Выберите постоянный ролевой класс персонажа. В будущем сменить его можно только у Хильдир за 1000 золота." },
                { "rp_ui_title_respec", "СВИТОК ПЕРЕРОЖДЕНИЯ" },
                { "rp_ui_subtitle_respec", "Выберите новый ролевой класс. Свиток Перерождения исчезнет только после подтверждения выбора." },
                { "rp_ui_bonuses", "Преимущества" },
                { "rp_ui_passive", "Пассивный навык" },
                { "rp_ui_debuffs", "Недостатки" },
                { "rp_ui_confirm", "Принять путь" },
                { "rp_ui_cancel", "Отмена" },
                { "rp_ui_current", "Текущий" },
                { "rp_hud_passive_section", "Характеристики класса" },

                // Единый строгий стандарт терминов (одинаково во всех карточках и подсказках!)
                { "rp_stat_base_hp", "Базовое здоровье" },
                { "rp_stat_base_stamina", "Базовая выносливость" },
                { "rp_stat_base_eitr", "Базовый Эйтр" },
                { "rp_stat_eitr_regen", "Восстановление Эйтра" },
                { "rp_stat_heavy_armor_eitr", "Восстановление Эйтра (в тяжёлой броне)" },
                { "rp_stat_carry_weight", "Переносимый вес" },
                { "rp_stat_move_speed", "Скорость передвижения" },
                { "rp_stat_shield_move_speed", "Скорость передвижения (со щитом)" },
                { "rp_stat_dodge_stam", "Расход выносливости на перекат" },
                { "rp_stat_sneak_stam", "Расход выносливости в скрытности" },
                { "rp_stat_sneak", "Навык скрытности" },
                { "rp_stat_base_armor", "Базовая броня" },
                { "rp_stat_total_armor", "Общая броня" },
                { "rp_stat_full_set_armor", "Общая броня (при поднятом щите)" },
                { "rp_stat_block_power", "Сила блока" },
                { "rp_stat_shield_block", "Сила блока (со щитом)" },
                { "rp_stat_parried_dmg", "Урон по запарированным целям" },
                { "rp_stat_melee_dmg", "Урон оружием ближнего боя" },
                { "rp_stat_full_hp_enemy_dmg", "Урон по врагам с полным здоровьем" },
                { "rp_stat_heavy_melee_dmg", "Урон ближнего боя (кроме ножей и копий)" },
                { "rp_stat_knife_dmg", "Урон ножами" },
                { "rp_stat_back_dmg", "Урон в спину в бою" },
                { "rp_stat_stealth_mult", "Множитель внезапной атаки" },
                { "rp_stat_night_dmg", "Весь урон (ночью)" },
                { "rp_stat_ranged_dmg", "Урон луками и арбалетами" },
                { "rp_stat_ranged_spear_dmg", "Урон луками, арбалетами и копьями" },
                { "rp_stat_ranged_speed", "Скорость лука и арбалета" },
                { "rp_stat_bow_draw_stam", "Расход выносливости на натяжение тетивы" },
                { "rp_stat_hunter_combo", "Серия из {0} попаданий подряд" },
                { "rp_stat_hunter_combo_val", "след. выстрел x{0} урона" },
                { "rp_stat_magic_dmg", "Урон магическим оружием" },
                { "rp_stat_status_dmg", "Урон статусом" },
                { "rp_stat_gp_duration", "Длительность Силы Павших" },
                { "rp_stat_gp_duration_val", "+{0}% ({1} мин.)" },
                { "rp_stat_gp_cooldown", "Перезарядка Силы Павших" },
                { "rp_stat_gp_cooldown_val", "-{0}% ({1} мин.)" },
                { "rp_stat_phys_dmg", "Урон физическим оружием" },
                { "rp_stat_low_hp_dmg", "Весь урон (при здоровье игрока ниже {0}%)" },
                { "rp_stat_warrior_onslaught", "Кровавый натиск" },
                { "rp_stat_warrior_onslaught_val", "+{0}% скорости атаки за удар на {1} сек. (до +{2}%)" },
                { "rp_stat_knight_bulwark", "Стальной заслон" },
                { "rp_stat_knight_bulwark_val", "+{0}% к общей броне при поднятом щите" },
                { "rp_stat_thief_night_veil", "Покров ночи" },
                { "rp_stat_thief_night_veil_val", "+{0}% весь урон, -{1}% расход выносливости и +{2}% скорость бега ночью" },
                { "rp_stat_sorcerer_echo", "Поток магии" },
                { "rp_stat_sorcerer_echo_val", "каждый {0}-й каст бесплатный по Эйтру" },
                { "rp_stat_beggar_stomach", "Лужёный желудок" },
                { "rp_stat_beggar_stomach_val", "+{0}% здоровья, выносливости и Эйтра от еды, +{1}% длительности" },
                { "rp_stat_all_dmg", "Весь наносимый урон" },
                { "rp_stat_skill_xp", "Получаемый опыт навыков" }
            });
        }
    }
}
