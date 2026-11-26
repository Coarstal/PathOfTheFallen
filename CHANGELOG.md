# Changelog — Valheim: Path of the Fallen

All notable changes to **Path of the Fallen** (`com.coarstal.pathofthefallen`) from `v1.0.0` onward are documented in this file (in both **English** and **Russian**).

---

## [1.0.7] — 2026-11-26

### English
- **Soul Extraction Crystal (`SoulCrystalEmpty` — *Empty Soul Crystal*):**
  - Sold exclusively by **Hildir** for **`999` Coins** (`SoulCrystalTraderPrice = 999`), giving late-game and Champion-farmed Coins a permanent progression sink.
  - Custom 3D world-drop mesh and `128x128` inventory icon.
  - **Right-Click Soul Extraction Window (`SoulExtractionUI`):** Right-clicking an *Empty Soul Crystal* in your inventory opens a dedicated Valheim Woodpanel UI listing all enchanted weapons currently in your inventory with their quality/enchant badge, enchantment stats, and output preview (`-> Soul of Eikthyr I..V`).
  - **Weapon Durability Breakage & `3s` Elemental Backlash:** Extracting a soul strips the Boss Soul enchantment from the weapon, **breaks the weapon down to `0` durability** (repairable for free at a Forge/Workbench), inflicts a **`3s` debuff matching the Boss Soul's damage type** (*Lightning*, *Bleeding*, *Poison*, *Frost*, *Burning*, *Poison + Lightning*, or *Burning + Spirit*), consumes `1x` *Empty Soul Crystal*, and seals the soul inside a *Filled Soul Crystal*.
- **Filled Soul Crystals (`SoulCrystalFilled` — *Soul of <Boss> I..V*):**
  - Custom glowing 3D crystal mesh, `128x128` icon, and colored Roman numeral badge (`I..V`) in the inventory slot corner.
  - Dynamically titled and colored per Boss Soul and Tier (`I–V`), with exact trophy yield preview in its tooltip.
- **New Crafting Station — Soul Extraction Cauldron (`piece_soul_cauldron`):**
  - Built with the Hammer (*Crafting* tab, near Workbench): **`Tin x10`**, **`Copper x5`**, **`SurtlingCore x3`**, **`BoneFragments x10`**.
  - Custom 3D runic iron cauldron over a stone hearth with horned Norse skulls, swirling purple-cyan soul brew, balanced HDR glow, and a **self-stirring enchanted ladle** that continuously orbits & tilts in the brew.
  - **Progression-Gated Gray Soul Preview & Shattering:** Owned Soul Crystals appear at the top in full boss color, while unowned Soul Crystals (`I..V`) for defeated/unlocked bosses appear below in **muted grayscale** so you can inspect yields at any time:
    - **Guaranteed `50%` return of total invested mob trophies** (`SoulCauldronMobReturnPercent = 50%`: **`1 / 3 / 8 / 16 / 26`** mob trophies returned across Tiers `I..V`).
    - **`25%` chance to recover `1x` Boss Trophy** (`SoulCauldronBossReturnChance = 25%`).
- **Fixes & Polish:**
  - Fixed *Staff of Whispering Wind* (`StaffMeadows`) workbench upgrade filter intercepting Enchanting Table rows and mislabeling adjacent weapons.
  - Aligned *Moder's Crystal Altar* (`piece_enchant_ext_moder`) and *Yagluth's Eternal Flame* (`piece_enchant_ext_yagluth`) 3D meshes to exact `90°` axes and disabled `m_randomInitBuildRotation` on custom pieces.
  - Fixed all 5 custom Magic Staves rendering a duplicate overlapping mesh (`attach`) when dropped on the ground.
  - Exempted **Hildir's quest map table** from `NoVegvisir` so interacting with Hildir's map pins her 3 quest dungeons normally.

### Русский
- **Пустой кристалл душ (`SoulCrystalEmpty`):**
  - Продаётся эксклюзивно у **Хильдир** за **`999` монет** (`SoulCrystalTraderPrice = 999`), превращая монеты с Чемпионов в ценную валюту на всю игру.
  - Собственная 3D-модель предмета в мире и иконка `128x128`.
  - **Меню извлечения души по ПКМ (`SoulExtractionUI`):** нажатие ПКМ по *Пустому кристаллу душ* открывает окно со списком всех зачарованных пушек в инвентаре, их бонусами и предпросмотром результата (`-> Душа Эйктюра I..V`).
  - **Поломка оружия в `0` прочности и `3-секундный` дебафф стихии:** при извлечении чары снимаются с оружия, **само оружие ломается в `0` прочности** (остаётся в инвентаре и бесплатно чинится на кузнице/верстаке), на игрока на **`3 секунды` накладывается дебафф соответствующего типа урона** (*Молния*, *Рваная рана*, *Яд*, *Мороз*, *Горение*, *Яд + Молния* или *Горение + Дух*), тратится `1` пустой кристалл и выдаётся кристалл с душой.
- **Запечатанные кристаллы душ (`SoulCrystalFilled` — *Душа <Босса> I..V*):**
  - Собственная светящаяся 3D-модель, иконка `128x128`, цветная римская цифра уровня (`I..V`) в слоте инвентаря и динамическое название в цвет босса (*Душа Эйктюра I..V* .. *Душа Прородителя I..V*).
- **Новая станция — «Котёл для извлечения Душ» (`piece_soul_cauldron`):**
  - Строится молотком (вкладка *«Ремесло»*, рядом с верстаком): **`Олово x10`**, **`Медь x5`**, **`Ядро суртлинга x3`**, **`Обломки костей x10`**.
  - Кастомная 3D-модель рунического котла со смягчённым свечением и **зачарованной поварёшкой, которая сама помешивает варево по кругу**.
  - **Предпросмотр душ серым цветом и разбивание кристаллов:** имеющиеся в кармане кристаллы отображаются сверху в цвете босса, а отсутствующие души (`I..V`) открытых в мире боссов подсвечиваются **серым цветом**, как на Столе зачарования:
    - **Гарантированный возврат `50%` вложенных трофеев мобов** (`1 / 3 / 8 / 16 / 26` шт. за уровни `I..V`).
    - **`25%` шанс вернуть `1x` трофей самого босса**.
- **Исправления:**
  - Исправлен баг на Столе зачарования, из-за которого фильтр верстака для *Посоха Шепчущего Ветра* (`StaffMeadows`) сдвигал подписи соседнего оружия (например, Бронзовой алебарды).
  - Выровнены строго под **`90°`** 3D-модели *Кристального алтаря Моудер* (`piece_enchant_ext_moder`) и *Вечного пламени Яглута* (`piece_enchant_ext_yagluth`), а также отключён случайный стартовый поворот при постройке молотком.
  - Исправлено раздвоение 3D-модели у всех 5 новых посохов при выбрасывании на землю.
  - Исправлена неработающая **карта квестов на столе у Хильдир** (добавлено исключение в модуль `NoVegvisir`).

---

## [1.0.6] — 2026-11-25

### English
- **5th Staff — Staff of Blood Harvest (`StaffPlains`):**
  - Restored upgradable Totem lifetime by quality level: **`15 / 30 / 45 / 60s`** (`+15s` per upgrade level, Levels 1–4).
  - **Timerless Field Aura -> `5s` Exit Countdown:** While standing inside the Blood Totem's crimson rain circle, the *Blood Totem Aura* HUD icon displays **no countdown timer** at all; the moment you step outside the circle (or the totem expires), a **`5s` countdown timer** appears on the buff.
- **Class Block Power Balance:**
  - **Warrior:** Block Power penalty increased from `-10%` to **`-35%`**.
  - **Sorcerer:** Added **`-55%` Block Power** penalty.
- **Champion Crushing Blows (Knight-Exclusive Guard):**
  - **Only the Knight class** can block or parry attacks from Champions (and Curse Wardens). Any other class attempting to block or parry a Champion has **100% of their stamina drained**, suffers guard-break stagger, and takes the unblocked hit (`ChampionOnlyKnightCanBlock = true`).
- **Full Boss Soul Weapon Attributes Overhaul (`Weapon Attributes`):**
  - Replaced the flat `+10..+50` (`+20..+100` on dual-boss souls) bonus with a **progression- & archetype-scaled system** (`BaseDamagePercentPerLevelV2 = 7%` per level × weapon archetype factor) across **all vanilla and modded weapons**:
    - **3rd Staff (`StaffSwamp`):** `+1..+3.5` (Q1), `+1.5..+7` (Q2), `+2..+10.5` (Q3), `+3..+14` (Q4) per `2s` cloud tick (e.g., Q2 hits `~30` with Lv.3 Frost instead of `~80`).
    - **4th Staff (`StaffMountain` Railgun):** `+16..+81` (Q1), `+22..+110` (Q2), `+33..+165` (Q3), **`+44..+220`** (Q4) to match its `3.0–4.5s` immobile charge.
    - **1st Staff (`StaffMeadows`):** `+2..+9` (Q1) up to `+5..+25` (Q4).
    - **Summoning Staves Now Imbue Summons (Networked via ZDO):** *Dead Raiser* (`StaffSkeleton`, `+3..+15` per skeleton hit), *Staff of the Wild* (`StaffGreenRoots`, `+4..+20` on seed & root strikes), and *Trollstav* (`StaffRedTroll`, `+9..+45` on meteor & summoned troll hits vs non-players) are no longer useless with Boss Soul enchantments!
    - **Multi-Pellet / Rapid Vanilla Staves Fixed:** *Dundr* (`StaffLightning`, `0.80x` per pellet -> `+1.5..+7.5` × 12 pellets = `+18..+90` total instead of `+600`), *Staff of Frost* (`StaffIceShards`, `0.80x` -> `+1.5..+13.5` per shard), *Staff of Fracturing* (`StaffClusterbomb`, `0.75x` -> `+1.5..+9.5` per bomblet), *Staff of Embers* (`StaffFireball`, `0.85x` -> `+7..+41`).
    - **Melee & Ranged Archetypes:** Fast Knives & Claws (`0.75x`), Dual Axes (`0.80x`), Polearms/Atgeirs (`0.95x`), Standard 1H Swords/Maces/Axes/Spears (`1.00x`), 2H Sledges (`1.20x`), 2H Greatswords & Battleaxes (`1.25x`), Bows & Slow-Reload Crossbows (`1.35x`, Crossbows up to `+20..+99`).
    - **Dual-Attribute Late-Game Boss Souls (*The Queen*, *Fader*):** Grant **`65%` per element (`130%` combined total)** instead of `200%`.
    - **Non-Combat Exclusion:** Blocked Pickaxes, Butcher Knife, Torches, Scythe, stackable Bombs, and pure support staves (`StaffShield`, `StaffBlackForest`, `StaffPlains`) from wasting Boss Trophies.
  - **Enchanting Table UI & Station Icon Polish:**
    - Unenchanted weapons in your inventory are now visible in the Enchanting Table's *Craft* tab even without carrying a Boss Trophy — gated cleanly by **world boss progression** (`ZoneSystem` defeated boss key **or** having picked up that Boss Trophy at least once). Craftable recipes appear at the top in full color, while recipes missing trophies or station level are rendered in muted **grayscale**; fixed `4+X` badge alignment in the *Upgrade* tab so it never overlaps weapon names.
    - Fixed `CraftingStation.m_icon` and placement ghost cleanup on `piece_enchanting_table` so the custom Enchanting Table icon replaces the vanilla Artisan Table icon both in the Hammer required-station slot and inside the Enchanting Table UI header.

### Русский
- **5-й посох — Посох Кровавой Жатвы (`StaffPlains`):**
  - Возвращена прокачка времени жизни Кровавого Тотема по уровням посоха: **`15 / 30 / 45 / 60 сек.`** (`+15 сек.` за уровень, ур. 1–4).
  - **Бафф без таймера в круге -> таймер `5 сек.` при выходе:** пока вы находитесь внутри круга кровавого дождя, иконка ауры висит **вообще без цифр таймера**; как только вы выходите из круга (или тотем исчезает), на иконке запускается обратный отсчёт **`5 секунд`**.
- **Баланс силы блока классов:**
  - **Воин:** штраф к силе блока увеличен с `-10%` до **`-35%`**.
  - **Колдун:** добавлен штраф **`-55%` к силе блока**.
- **Сокрушительные удары Чемпионов (блок только для Рыцаря):**
  - **Заблокировать или запарировать Чемпиона теперь может только класс Рыцарь.** Если любой другой класс пытается принять удар Чемпиона в блок/парирование, ему **гарантированно выбивает всю выносливость в 0**, пробивает стойку со стаггером и наносит урон сквозь блок (`ChampionOnlyKnightCanBlock = true`).
- **Глобальный ребаланс Зачарования оружия душами боссов (`Weapon Attributes`) для ВСЕХ пушек и посохов:**
  - Плоская прибавка `+10..+50` (`+20..+100` у двойных душ) заменена на **умную формулу от боевого урона оружия, его тира и архетипа скорости/механики** (`BaseDamagePercentPerLevelV2 = 7%` за уровень чар × множитель архетипа):
    - **3-й посох (`StaffSwamp`):** `+1..+3,5` (ур.1), `+1,5..+7` (ур.2), `+2..+10,5` (ур.3), `+3..+14` (ур.4) за тик каждые `2 сек.` (на ур.2 с Морозом +3 бьёт `~30` за тик вместо `~80`).
    - **4-й посох (`StaffMountain` — рейлган):** `+16..+81` (ур.1), `+22..+110` (ур.2), `+33..+165` (ур.3), **`+44..+220`** (ур.4) под долгую зарядку `3,0–4,5 сек.`.
    - **1-й посох (`StaffMeadows`):** `+2..+9` (ур.1) -> `+5..+25` (ур.4).
    - **Посохи призыва теперь передают чары существам (с синхронизацией по ZDO на сервере):** *Пробуждающий мертвецов* (`StaffSkeleton`, `+3..+15` к каждому удару/выстрелу скелета), *Посох дикой природы* (`StaffGreenRoots`, `+4..+20` снаряду и каждому удару корня), *Посох тролля* (`StaffRedTroll`, `+9..+45` метеориту и ударам тролля по мобам).
    - **Ванильные боевые посохи:** убрано 12-кратное умножение плоского урона у *Дундра* (`StaffLightning`, `0.80x` на картечину -> `+1,5..+7,5` × 12 = `+18..+90` за залп вместо `+600`), сбалансированы *Посох мороза* (`StaffIceShards`, `+1,5..+13,5` за осколок), *Посох раскола* (`StaffClusterbomb`, `+1,5..+9,5` за бомбочку) и *Посох угольков* (`StaffFireball`, `+7..+41`).
    - **Ближний и дальний бой:** быстрые ножи и когти (`0.75x`), парные топоры (`0.80x`), алебарды (`0.95x`), 1H мечи/булавы/топоры/копья (`1.00x`), двуручные молоты (`1.20x`), двуручные мечи и секиры (`1.25x`), луки и арбалеты (`1.35x`, арбалеты до `+20..+99`).
    - **Двойные души эндгейм-боссов (*Королева*, *Фадер*):** дают по **`65%` на каждую стихию (`130%` суммарно)** вместо двойных `200%`.
    - **Защита от пустой траты трофеев:** исключены кирки, мясницкий нож, факелы, коса, бомбы и чисто защитные/хилящие посохи (`StaffShield`, `StaffBlackForest`, `StaffPlains`).
  - **Улучшение интерфейса и иконки Стола зачарования:**
    - Все незачарованные пушки в инвентаре отображаются во вкладке *«Создать»* даже без трофеев боссов в кармане — при этом показываются только те боссы, которые **уже убиты в мире** или **чью голову игрок хотя бы раз держал в руках**. При нехватке трофеев или уровня стола строка полностью подсвечивается **серым цветом**; обновлены русские подписи боссов (*Матерь*, *Прородитель*), а плашка заточки `4+1` во вкладке *«Улучшить»* выровнена вправо внутри иконки без наезда на название оружия.
    - Исправлена иконка `CraftingStation.m_icon` и очистка мешей донора у `piece_enchanting_table`: теперь и при постройке молотком, и внутри самого интерфейса Стола зачарования отображается собственная иконка Стола зачарования вместо стола ремесленника.

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
