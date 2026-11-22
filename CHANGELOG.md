# Changelog — Valheim: Path of the Fallen

All notable changes to **Path of the Fallen** (`com.coarstal.pathofthefallen`) from `v1.0.0` onward are documented in this file (in both **English** and **Russian**).

---

## [1.0.5] — 2026-11-22

### English
- **Warrior («Brave Heart» — Bloody Onslaught) Buff:** Added **`+2.5%` melee lifesteal per stack** (up to **`12.5%` lifesteal** and `+20%` attack speed at 5 stacks).
- **Sorcerer («Archmage») Forsaken Power Fix & Party Sharing:**
  - Fixed a vanilla Valheim 1.0 quirk where `Player.ActivateGuardianPower()` always returned `false` in IL, preventing the **`15 min` (`-25%`) cooldown** from applying! Cooldown now reliably sets to **`15 min`** in both singleplayer and Dedicated Server multiplayer.
  - Activating Eikthyr or any other Forsaken Power as a **Sorcerer** now grants the full **`7.5 min` (`+50%`) duration to ALL nearby allies** in range (synced via `ZNetView` RPC), not just to the Sorcerer.
- **3rd Staff — Censer of Restless Spirit (`StaffSwamp`):**
  - Rebalanced damage per tick to **`5` Poison / `5` Spirit** at Level 1 with **`+5` Poison / `+5` Spirit per upgrade level** (`5/5 -> 10/10 -> 15/15 -> 20/20`), ticking cleanly once every **`2.0 seconds`**.
  - Enemies inside the Spirit Miasma cloud now suffer **`-20%` movement speed**.
- **2nd Staff — Staff of Purification (`StaffBlackForest`):**
  - Increased healing per quality level from `25 / 35 / 45 / 55 HP` to **`30 / 40 / 50 / 60 HP`**.
- **5th Staff — Staff of Blood Harvest (`StaffPlains`) Rework & Synergy:**
  - **Infinite Totem Field:** The deployed Blood Ritual Totem now lasts **indefinitely** in its zone until you redeploy it elsewhere (max `1` active totem per caster).
  - **`5s` Linger Buff:** Leaving the crimson rain field keeps the *Blood Totem Aura* active on players for an additional **`5 seconds`**.
  - **Synergy with 2nd Staff (`x1.5` Healing):** Casting *Staff of Purification* while empowered by the Blood Totem field increases its healing by **`1.5x`** (`45 / 60 / 75 / 90 HP`).
  - **Fixed Cast Cost:** Changed resource cost from `% HP` to a stable **`75` Eitr** and **`50` flat HP** (regardless of Blood Magic skill level).

### Русский
- **Воин («Храброе сердце» — Кровавый натиск):** К каждому стаку *«Кровавого натиска»* добавлено **`+2,5%` вампиризма** от урона ближнего боя (до **`12,5%` вампиризма** и `+20%` скорости атаки на 5 стаках).
- **Колдун («Архимаг») — исправление отката `15 мин.` и бафф `7,5 мин.` на всю группу:**
  - Исправлен баг движка Valheim 1.0 (метод `Player.ActivateGuardianPower()` в коде игры всегда возвращает `false`), из-за которого у Колдуна оставался откат `20 мин.` вместо **`15 мин.`** — теперь откат стабильно становится **`15 минут`** и на сервере, и в одиночной игре.
  - Когда **Колдун** прожимает Эйктюра или любую другую Силу Павших, продлённый бафф на **`7,5 минут`** теперь накладывается **на всех союзников в радиусе раздачи**, а не только на самого Колдуна (с прямой синхронизацией по RPC на сервере).
- **3-й посох — Кадило Беспокойного Духа (`StaffSwamp`):**
  - Урон сбалансирован до **`5` ядом / `5` духом** на 1-м уровне и **`+5 / +5` за каждый уровень** (`5/5 -> 10/10 -> 15/15 -> 20/20`), а интервал срабатывания выставлен на **каждые `2 секунды`** единым чистым тиком.
  - Враги внутри облака духовной миазмы теперь получают **`-20%` к скорости бега**.
- **2-й посох — Посох Очищения (`StaffBlackForest`):**
  - Базовое исцеление усилено с `25 / 35 / 45 / 55 HP` до **`30 / 40 / 50 / 60 HP`** по уровню посоха.
- **5-й посох — Посох Кровавой Жатвы (`StaffPlains`) и синергия со 2-м посохом:**
  - **Бесконечное поле тотема:** установленный Кровавый Тотем теперь стоит **бесконечно** в области действия, пока вы не переставите его в новое место (лимит: `1` активный тотем).
  - **Сохранение баффа на `5 сек.`:** при выходе из поля кровавого дождя аура тотема продолжает действовать на игрока ещё **`5 секунд`**.
  - **Синергия со 2-м посохом (`x1,5` хила):** в поле действия Кровавого Тотема мощность исцеления *Посоха Очищения* увеличивается в **`1,5 раза`** (`45 / 60 / 75 / 90 HP`).
  - **Стабильный расход:** стоимость установки тотема зафиксирована на **`75` Эйтра** и **`50` ед. здоровья** (вместо процентной траты HP).

---

## [1.0.4] — 2026-11-21

### English
- **Dedicated Server Hunt Champion Fix:** Fixed an issue where Hunt Champions failed to spawn on headless Dedicated Servers (which do not load terrain colliders around remote players). Terrain height checks and Champion instantiation are now delegated to the killer's client peer via `ZRoutedRpc`, and the kill counter no longer resets if the player is over water or inside a `PlayerBase` area.
- **Sorcerer («Archmage») Class Rework:** Removed direct magic weapon (`+15%`) and status effect (`+10%`) damage buffs and replaced them with **Forsaken Power Mastery**:
  - **Forsaken Power Duration:** `+50%` (`7.5 min` instead of `5 min`).
  - **Forsaken Power Cooldown:** `-25%` (`15 min` instead of `20 min`).
  - *Arcane Resonance* (every 5th cast costs `0` Eitr) remains unchanged.
- **New Minimap HUD Indicators:**
  - **Bottom-Right:** Live server-synced counter showing remaining kills until the next Hunt Champion (`Champion: X`, highlighted when `≤ 3`; toggleable via `ShowMinimapChampionCounter`).
  - **Top-Left:** Smoothed real-time round-trip server ping indicator (`ping: X`, measured directly via `ZRpc` ping/pong without vanilla ZDO queue spikes; toggleable via `ShowMinimapPing`).
- **HUD Alignment Fix:** Lifted the Forsaken Power (`Eikthyr` / Boss Power) HUD icon (`+32 px`) so its *"Ready"* / cooldown label never overlaps the Armor Shield (`AC`) indicator.

### Русский
- **Исправление спавна Чемпионов на выделенном сервере (Dedicated Server):** Исправлена проблема, из-за которой Чемпионы Охоты не появлялись на headless-серверах (поскольку сервер без графики не подгружает физический рельеф вокруг удалённых игроков). Проверка поверхности и спавн Чемпиона теперь выполняются через клиент игрока, убившего последнего моба, а если игрок находится в воде или на базе — счётчик не сбрасывается впустую и ждёт выхода на открытую местность.
- **Переработка класса Колдун («Архимаг»):** Убраны все прямые бонусы урона (`+15%` магическим оружием и `+10%` статусом). Вместо них пассивный навык **«Архимаг»** усиливает духовную связь с камнями боссов:
  - **Длительность Силы Павших:** `+50%` (`7,5 мин.` вместо `5 мин.`).
  - **Перезарядка Силы Павших:** `-25%` (`15 мин.` вместо `20 мин.`).
  - *Поток магии* (каждый 5-й каст бесплатный по Эйтру) сохранён.
- **Новые индикаторы на мини-карте:**
  - **Снизу справа:** синхронизируемый с сервером счётчик оставшихся мобов до появления Чемпиона (`До чемпиона: X`, подсвечивается при `≤ 3`; переключатель `ShowMinimapChampionCounter`).
  - **Сверху слева:** сглаженный индикатор реального сетевого отклика до сервера (`ping: X`, замеряется напрямую по пакетам `ZRpc` без фальшивых скачков очереди Valheim; переключатель `ShowMinimapPing`).
- **Исправление интерфейса (HUD):** Иконка Силы Павших (Эйктюр и др.) приподнята выше (`+32 px`), чтобы надпись *«Готово»* и таймер отката не перекрывали щит брони.

---

## [1.0.3] — 2026-11-21

### English
- **Cursed Dungeon Exclusions:** Excluded Troll Caves and Bear Caves (`TrollCave`, `BearCave`, `Bjorn`, `Unbjorn`) from Cursed Dungeons and Warden selection, with automatic cleanup of previously generated curse ZDOs.
- **Eitr Food Recipe Balance:** Changed the recipe of *Story of the Sad Aufer* (`FoodSadAufer`) from `DragonTear` to `FreezeGland` (`2 CookedWolfMeat + 2 FreezeGland`).
- **Combat Injury Damage Threshold:** Added a minimum unblocked health loss threshold of `7.5 HP` (`MinHealthDamageForInjury = 7.5`) required before any Combat Injury debuff can trigger.
- **Global Hunt Champion Counter (`25–35`):** Reworked the Hunt Champion counter to track all hostile creature kills globally (`25–35` kills), always spawning the exact creature type slain last.
- **Staff of Whispering Wind (`StaffMeadows`) Progression:** Restricted upgrades of the 1st staff (`StaffMeadows`) exclusively to the **Forge of Potential** (Workbench crafting & repair remain available, Workbench upgrades disabled) and rebalanced damage to `25` Slash (`+15` per upgrade level).

### Русский
- **Исключения Проклятых подземелий:** Пещеры троллей и медведей (`TrollCave`, `BearCave`, `Bjorn`, `Unbjorn`) полностью исключены из системы Проклятых данжей и пула Стражей проклятия (с автоматическим снятием печати со старых пещер).
- **Рецепт еды на Эйтр:** В блюде *«История грустного ауфера»* (`FoodSadAufer`) драконья слеза заменена на морозную железу (`2 жареного мяса волка + 2 морозные железы`).
- **Минимальный порог урона для травм:** Добавлен минимальный порог пропущенного урона по здоровью `7,5 ед.` (`MinHealthDamageForInjury = 7.5`), ниже которого боевые ранения не накладываются.
- **Общий счётчик Чемпионов (`25–35`):** Счётчик Охоты сделан единым на всех враждебных мобов (`25–35` убийств), а Чемпионом всегда приходит именно тот вид существа, который был убит последним.
- **Прогрессия Посоха Шепчущего Ветра (`StaffMeadows`):** Улучшение первого посоха запрещено на обычном Верстаке и перенесено эксклюзивно в **Кузницу возможностей** (крафт 1-го уровня и починка на Верстаке сохранены); урон сбалансирован до `25` рубящего (`+15` за каждый уровень).

---

## [1.0.2] — 2026-11-20

### English
- **Staff of Purification (`StaffBlackForest`) Fix:** Fixed cast animation trigger (`staff_shield`) so cleansing/healing casts play smoothly every time, and suppressed `0` heal popups at full health.
- **Champion Stat Normalization (`2★` + Elemental):** Normalized vanilla `Attack.GetLevelDamageFactor()` on Champions so both melee/ranged attacks and AoE abilities scale cleanly to exact `2-star` stats (`3.0x` HP, `2.0x` base damage) plus guaranteed `+15%` biome elemental damage.
- **Frost Cave Warden Fix:** Fixed Mountain Frost Caves (`MountainCave02`) spawning Forest Trolls instead of `Fenring_Cultist` Wardens.
- **Exact `10%` Cursed Dungeon Quota & Performance:** Changed Cursed Dungeons to deterministically curse exactly `10%` of dungeons per dungeon type across the world, and eliminated dungeon microfreezes via `O(1)` spatial, `DungeonAnchor`, `EnemyHud`, and `NoVegvisir` caches.

### Русский
- **Исправление Посоха Очищения (`StaffBlackForest`):** Починен запуск анимации каста (`staff_shield`) при каждом применении и убраны всплывающие нули лечения при полном здоровье.
- **Нормализация характеристик Чемпионов (`2★` + стихия):** Устранён скрытый ванильный множитель `2.5x` от звёздности на обычных атаках — теперь все Чемпионы имеют ровные характеристики `2★` существа (`3.0x` здоровья, `2.0x` базового урона) и гарантированные `+15%` стихийного урона своего биома.
- **Исправление Морозных пещер:** В горных пещерах (`MountainCave02`) Стражем проклятия вместо тролля корректно спавнится Культист Фенринга (`Fenring_Cultist`).
- **Квота `10%` Проклятых данжей и оптимизация:** Проклятыми теперь становятся ровно `10%` подземелий каждого отдельного типа в мире; полностью устранены микрофризы в данжах благодаря `O(1)`-кэшированию якорей данжей, `EnemyHud` и `NoVegvisir`.

---

## [1.0.1] — 2026-11-20

### English
- **Staff of Purification (`StaffBlackForest`):** Added a new Black Forest support staff that cleanses all Combat Injuries and negative status effects from allies in a `15m` radius while restoring health (`15 / 20 / 25 / 30 HP`).
- **Documentation & Showcases:** Added full bilingual (`EN / RU`) documentation, class & equipment tables, visual showcases, and admin console command references.

### Русский
- **Посох Очищения (`StaffBlackForest`):** Добавлен новый посох поддержки Чёрного леса, снимающий все боевые ранения и негативные эффекты с союзников в радиусе `15 м` и исцеляющий их (`15 / 20 / 25 / 30 HP`).
- **Документация и витрина мода:** Добавлено полное двуязычное описание (`EN / RU`), таблицы классов, оружия, брони, еды и список админ-команд.

---

## [1.0.0] — 2026-11-20

### English
- **Initial Unified Release of *Valheim — Path of the Fallen*:** Merged 6 standalone RPG mods into a single modular, server-synced BepInEx plugin (`com.coarstal.pathofthefallen`) with independent module toggles:
  1. **RP Classes (`01`):** 6 permanent roleplay classes (*Warrior, Knight, Thief, Hunter, Sorcerer, Beggar*) with passive skills, HUD indicators, and Hildir's *Contract of Rebirth*.
  2. **Boss Soul Weapon Enchanting (`02`):** Custom *Boss Soul Enchanting Table* with 4 boss upgrades and 7 socketable Boss Soul weapon attributes.
  3. **Arsenal, Early Magic & Eitr Cuisine (`03`):** Early magic progression from the Meadows (*Staff of Whispering Wind*, *Censer of the Restless Spirit*, *Thor's Thunder Tuning Fork*, *Blood Harvest Staff*), 2 Dual Berserker Axes (*Swamp* & *Plains*), and 6 early/mid-game Eitr foods with placeable table feast pieces.
  4. **Eitr Mage Armor Sets (`04`):** Custom 3D *Forest Shaman* (Black Forest) and *Mountain Embla* (Mountain, frost-resistant) mage armor sets.
  5. **Enemy Overhaul (`05`):** Named Hunt Champions, 6 tactical Combat Injuries, and Sealed Cursed Dungeons.
  6. **Hardcore Exploration — No Vegvisirs (`06`):** Removes boss location runestones (*Vegvisirs*) for true landmark-driven exploration.

### Русский
- **Первый единый релиз *Valheim — Path of the Fallen*:** Объединение 6 отдельных RPG-модов в единый модульный плагин с серверной синхронизацией (`com.coarstal.pathofthefallen`) и возможностью включать/отключать каждый модуль в конфиге:
  1. **Ролевые классы (`01`):** 6 постоянных классов (*Воин, Рыцарь, Вор, Охотник, Колдун, Нищий*) с уникальными пассивными навыками, HUD-индикаторами и *Свитком Перерождения* у Хильдир.
  2. **Зачарование оружия душами боссов (`02`):** *Стол зачарования* с 4 улучшениями боссов и 7 боевыми атрибутами для любого оружия.
  3. **Арсенал, ранняя магия и кухня Эйтра (`03`):** Магическая прогрессия с самых Лугов (*Посох Шепчущего Ветра*, *Кадило Беспокойного Духа*, *Громовой Камертон Тора*, *Посох Кровавой Жатвы*), 2 пары топоров берсерка (*Болота* и *Равнины*) и 6 блюд на Эйтр с сервировкой на стол.
  4. **Магическая броня Эйтра (`04`):** Два 3D-комплекта лёгкой брони мага — *Лесной Шаман* (Чёрный лес) и *Горная Эмбла* (Горы, с защитой от мороза).
  5. **Переработка врагов (`05`):** Именные Чемпионы Охоты, 6 тактических боевых ранений и Запечатанные Проклятые подземелья.
  6. **Хардкорная разведка — Без Вегвизиров (`06`):** Отключение камней-указателей боссов для честного исследования мира.
