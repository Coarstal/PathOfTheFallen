using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CoarstalsEnemyOverhaul
{
    public class ChampionBehaviour : MonoBehaviour
    {
        private Character m_char;
        private ZNetView m_nview;
        private MonsterAI m_ai;
        private bool m_visualsApplied;
        private bool m_packCalled;
        private GameObject m_auraObj;
        private Light m_auraLight;
        private ParticleSystem m_auraParticles;
        private Vector3 m_originalScale = Vector3.one;

        public bool IsEnraged { get; private set; }
        public ChampionPerk PrimaryPerk { get; private set; }
        public ChampionPerk SecondaryPerk { get; private set; }
        public Heightmap.Biome ChampionBiome { get; private set; }
        public bool IsDungeonWarden { get; private set; }
        public Vector3 AnchorDungeonPos { get; private set; }
        private float m_fallCheckTimer;

        private void Awake()
        {
            m_char = GetComponent<Character>();
            m_nview = GetComponent<ZNetView>();
            m_ai = GetComponent<MonsterAI>();
            m_originalScale = transform.localScale;
        }

        private void Start()
        {
            RefreshFromZDO();
        }

        private void Update()
        {
            if (m_char == null || m_nview == null || !m_nview.IsValid()) return;
            if (!m_visualsApplied)
            {
                RefreshFromZDO();
            }

            if (m_char.IsDead())
            {
                if (m_auraObj != null && m_auraObj.activeSelf)
                {
                    m_auraObj.SetActive(false);
                }
                return;
            }

            // Anti-Void Safety Net: if a Dungeon Warden or Dungeon Champion EVER clips/falls out of the sky-layer dungeon (y < 4500m),
            // immediately teleport him right back onto his solid dungeon floor anchor with zero velocity!
            if (IsDungeonWarden && m_nview.IsOwner() && AnchorDungeonPos.y > 3000f)
            {
                m_fallCheckTimer -= Time.deltaTime;
                if (m_fallCheckTimer <= 0f)
                {
                    m_fallCheckTimer = 0.5f;
                    if (transform.position.y < AnchorDungeonPos.y - 18f || transform.position.y > AnchorDungeonPos.y + 18f)
                    {
                        Rigidbody rb = GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.linearVelocity = Vector3.zero;
                        }
                        transform.position = AnchorDungeonPos;
                        ZDO zdo = m_nview.GetZDO();
                        if (zdo != null)
                        {
                            zdo.SetPosition(AnchorDungeonPos);
                        }
                    }
                }
            }

            float hpPct = m_char.GetHealthPercentage();

            // Perk: Berserk Fury (< 40% HP -> +25% move & attack speed + crimson pulse)
            if ((PrimaryPerk == ChampionPerk.BerserkFury || SecondaryPerk == ChampionPerk.BerserkFury) && !IsEnraged && hpPct <= 0.40f)
            {
                IsEnraged = true;
                if (m_auraLight != null)
                {
                    m_auraLight.intensity = 3.2f;
                    m_auraLight.range = 7.5f;
                    m_auraLight.color = new Color(1.0f, 0.22f, 0.18f, 1f);
                }
            }

            // Perk: Pack Caller (< 65% HP in combat -> summons 2 bodyguards of same species once!)
            if ((PrimaryPerk == ChampionPerk.PackCaller || SecondaryPerk == ChampionPerk.PackCaller) &&
                !m_packCalled && m_nview.IsOwner() && hpPct <= 0.68f)
            {
                ZDO zdo = m_nview.GetZDO();
                if (zdo != null && !zdo.GetBool(ChampionSystem.ZdoChampPackUsed, false))
                {
                    if (m_ai == null || m_ai.IsAlerted() || m_ai.GetTargetCreature() != null)
                    {
                        m_packCalled = true;
                        zdo.Set(ChampionSystem.ZdoChampPackUsed, true);
                        ChampionSystem.SpawnPackBodyguards(m_char);
                    }
                }
                else
                {
                    m_packCalled = true;
                }
            }
        }

        public void RefreshFromZDO()
        {
            if (m_char == null || m_nview == null || !m_nview.IsValid()) return;
            ZDO zdo = m_nview.GetZDO();
            if (zdo == null || !zdo.GetBool(ChampionSystem.ZdoIsChampion, false)) return;

            PrimaryPerk = (ChampionPerk)zdo.GetInt(ChampionSystem.ZdoChampPerk1, 1);
            SecondaryPerk = (ChampionPerk)zdo.GetInt(ChampionSystem.ZdoChampPerk2, 0);
            ChampionBiome = (Heightmap.Biome)zdo.GetInt(ChampionSystem.ZdoChampBiome, (int)Heightmap.Biome.BlackForest);
            IsDungeonWarden = zdo.GetBool(ChampionSystem.ZdoIsDungeonWarden, false);
            if (IsDungeonWarden)
            {
                Vector3 savedAnchor = zdo.GetVec3(ChampionSystem.ZdoWardenAnchorPos, Vector3.zero);
                if (savedAnchor.y > 3000f)
                {
                    AnchorDungeonPos = savedAnchor;
                }
                else if (transform.position.y > 3000f)
                {
                    AnchorDungeonPos = transform.position;
                }
            }

            // Visual scale (+25% default, slightly gentler inside tight dungeon corridors so Champion never gets stuck in doorways!)
            float cfgScale = Mathf.Clamp(EnemyOverhaulConfig.ChampionScaleMultiplier.Value, 1.0f, 2.0f);
            if (IsDungeonWarden || transform.position.y > 3000f)
            {
                cfgScale = Mathf.Min(cfgScale, 1.15f);
            }
            transform.localScale = m_originalScale * cfgScale;

            // Ensure max HP is synced to our Champion multiplier
            if (m_nview.IsOwner())
            {
                float targetHp = zdo.GetFloat(ChampionSystem.ZdoChampMaxHP, 0f);
                if (targetHp > 1f && Mathf.Abs(m_char.GetMaxHealth() - targetHp) > 1f)
                {
                    bool wasFull = m_char.GetHealthPercentage() >= 0.98f;
                    m_char.SetMaxHealth(targetHp);
                    if (wasFull)
                    {
                        m_char.SetHealth(targetHp);
                    }
                }
            }

            EnsureChampionAura();
            m_visualsApplied = true;
        }

        private void EnsureChampionAura()
        {
            if (m_auraObj != null) return;

            Color auraCol = ChampionSystem.GetBiomeAccentColor(ChampionBiome, IsDungeonWarden);

            m_auraObj = new GameObject("CEO_ChampionAura");
            m_auraObj.transform.SetParent(transform, false);
            m_auraObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            m_auraLight = m_auraObj.AddComponent<Light>();
            m_auraLight.type = LightType.Point;
            m_auraLight.color = auraCol;
            m_auraLight.range = IsDungeonWarden ? 6.5f : 5.2f;
            m_auraLight.intensity = IsDungeonWarden ? 2.2f : 1.75f;
            m_auraLight.shadows = LightShadows.None;

            m_auraParticles = m_auraObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = m_auraParticles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
            main.startColor = new Color(auraCol.r, auraCol.g, auraCol.b, 0.72f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.gravityModifier = -0.15f;

            ParticleSystem.EmissionModule emission = m_auraParticles.emission;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = m_auraParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;

            ParticleSystem.ColorOverLifetimeModule colOverLife = m_auraParticles.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.Lerp(auraCol, Color.white, 0.35f), 0f),
                    new GradientColorKey(auraCol, 0.6f),
                    new GradientColorKey(auraCol, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = m_auraObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = CursedDungeonSystem.GetSharedSoftParticleMaterial();
            }
        }
    }

    public static class ChampionSystem
    {
        public const string ZdoIsChampion = "CEO_IsChamp";
        public const string ZdoIsChampionMinion = "CEO_ChampMinion";
        public const string ZdoIsDungeonWarden = "CEO_IsWarden";
        public const string ZdoWardenDungeonId = "CEO_WardenDungId";
        public const string ZdoWardenAnchorPos = "CEO_WardenAnchor";
        public const string ZdoChampRusTitle = "CEO_TitleRu";
        public const string ZdoChampEngTitle = "CEO_TitleEn";
        public const string ZdoChampPerk1 = "CEO_Perk1";
        public const string ZdoChampPerk2 = "CEO_Perk2";
        public const string ZdoChampBiome = "CEO_Biome";
        public const string ZdoChampMaxHP = "CEO_MaxHP";
        public const string ZdoChampPackUsed = "CEO_PackUsed";

        public const string RPC_ReportKillToServer = "CEO_RPC_ReportKill";
        private const string GlobalKeyPrefix = "ceo_kills_";

        private static readonly HashSet<string> s_largeSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Troll",
            "Abomination",
            "StoneGolem",
            "Fenring",
            "Fenring_Cultist",
            "GoblinBrute",
            "Lox",
            "Gjall",
            "SeekerBrute",
            "DvergerMage",
            "Morgen",
            "Asksvin",
            "FallenValkyrie",
            "Charred_Melee_Dyrnwyn",
            "BonemawSerpent",
            "Serpent"
        };

        private static readonly HashSet<string> s_excludedSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Deer",
            "Hare",
            "Hen",
            "Chicken",
            "Boar_piggy",
            "Wolf_cub",
            "Lox_Calf",
            "Asksvin_hatchling",
            "TrainingDummy",
            "Haldor",
            "Hildir",
            "BogWitch",
            "Dverger",
            "WildHuntDeerBoss"
        };

        // Explicit fallback map from creature prefab -> trophy prefab (for guaranteed WeaponAttributes trophy drops even if vanilla rate is tiny!)
        private static readonly Dictionary<string, string> s_fallbackTrophyBySpecies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Greydwarf", "TrophyGreydwarf" },
            { "Greydwarf_Elite", "TrophyGreydwarfBrute" },
            { "Greydwarf_Shaman", "TrophyGreydwarfShaman" },
            { "Troll", "TrophyForestTroll" },
            { "Skeleton", "TrophySkeleton" },
            { "Skeleton_Poison", "TrophySkeletonPoison" },
            { "Ghost", "TrophySkeleton" },
            { "Draugr", "TrophyDraugr" },
            { "Draugr_Ranged", "TrophyDraugr" },
            { "Draugr_Elite", "TrophyDraugrElite" },
            { "Blob", "TrophyBlob" },
            { "BlobElite", "TrophyBlob" },
            { "Wraith", "TrophyWraith" },
            { "Abomination", "TrophyAbomination" },
            { "Surtling", "TrophySurtling" },
            { "Leech", "TrophyLeech" },
            { "Wolf", "TrophyWolf" },
            { "Hatchling", "TrophyHatchling" },
            { "Fenring", "TrophyFenring" },
            { "Fenring_Cultist", "TrophyCultist" },
            { "Ulv", "TrophyUlv" },
            { "StoneGolem", "TrophySGolem" },
            { "Goblin", "TrophyGoblin" },
            { "GoblinArcher", "TrophyGoblin" },
            { "GoblinBrute", "TrophyGoblinBrute" },
            { "GoblinShaman", "TrophyGoblinShaman" },
            { "Deathsquito", "Needle" },
            { "Lox", "TrophyLox" },
            { "BlobTar", "TrophyGrowth" },
            { "Seeker", "TrophySeeker" },
            { "SeekerBrute", "TrophySeekerBrute" },
            { "Gjall", "TrophyGjall" },
            { "Tick", "TrophyTick" },
            { "Asksvin", "TrophyAsksvin" },
            { "Morgen", "TrophyMorgen" },
            { "FallenValkyrie", "TrophyFallenValkyrie" },
            { "Charred_Melee", "TrophyCharredMelee" },
            { "Charred_Archer", "TrophyCharredArcher" },
            { "Charred_Mage", "TrophyCharredMage" }
        };

        private static bool s_rpcRegistered;

        public static void RegisterRPCs(ZRoutedRpc routedRpc)
        {
            if (routedRpc == null || s_rpcRegistered) return;
            routedRpc.Register<string, Vector3, long>(RPC_ReportKillToServer, OnServerReceivedSpeciesKill);
            s_rpcRegistered = true;
        }

        public static void ResetSessionFlag()
        {
            s_rpcRegistered = false;
        }

        public static bool IsChampion(Character ch)
        {
            if (ch == null) return false;
            ZNetView nv = ch.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null) return false;
            return nv.GetZDO().GetBool(ZdoIsChampion, false);
        }

        public static bool IsLargeSpecies(string prefabName)
        {
            return !string.IsNullOrEmpty(prefabName) && s_largeSpecies.Contains(prefabName);
        }

        public static bool IsEligibleHuntSpecies(Character victim)
        {
            if (victim == null || victim.IsPlayer() || victim.IsBoss() || victim.IsTamed()) return false;
            if (IsChampion(victim)) return false;

            ZNetView nv = victim.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && nv.GetZDO() != null && nv.GetZDO().GetBool(ZdoIsChampionMinion, false))
            {
                return false;
            }

            string prefabName = Utils.GetPrefabName(victim.gameObject);
            if (string.IsNullOrEmpty(prefabName) || s_excludedSpecies.Contains(prefabName)) return false;

            // Must be an actual monster/animal with AI that is hostile to players
            BaseAI ai = victim.GetBaseAI();
            if (ai == null) return false;
            if (victim.m_faction == Character.Faction.Players || victim.m_faction == Character.Faction.Dverger) return false;

            return true;
        }

        public static void NotifyCreatureKilledByPlayer(Character victim, Player killer)
        {
            if (!EnemyOverhaulConfig.EnableChampions.Value) return;
            if (victim == null || killer == null) return;
            if (!IsEligibleHuntSpecies(victim)) return;

            string prefabName = Utils.GetPrefabName(victim.gameObject);
            if (string.IsNullOrEmpty(prefabName)) return;

            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                ProcessSpeciesKillOnServer(prefabName, killer.transform.position, killer.GetPlayerID());
            }
            else if (ZRoutedRpc.instance != null && ZNet.instance != null)
            {
                ZNetPeer serverPeer = ZNet.instance.GetServerPeer();
                if (serverPeer != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(
                        serverPeer.m_uid,
                        RPC_ReportKillToServer,
                        prefabName,
                        killer.transform.position,
                        killer.GetPlayerID());
                }
            }
        }

        private static void OnServerReceivedSpeciesKill(long senderPeer, string prefabName, Vector3 killerPos, long killerPlayerId)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            ProcessSpeciesKillOnServer(prefabName, killerPos, killerPlayerId);
        }

        private static void ProcessSpeciesKillOnServer(string prefabName, Vector3 killerPos, long killerPlayerId)
        {
            if (!EnemyOverhaulConfig.EnableChampions.Value || ZoneSystem.instance == null) return;

            int currentKills = GetServerSpeciesKillCount(prefabName) + 1;
            bool isLarge = IsLargeSpecies(prefabName);

            int minKills = isLarge
                ? Mathf.Max(2, EnemyOverhaulConfig.LargeMobMinKills.Value)
                : Mathf.Max(3, EnemyOverhaulConfig.SmallMobMinKills.Value);
            int maxKills = isLarge
                ? Mathf.Max(minKills, EnemyOverhaulConfig.LargeMobMaxKills.Value)
                : Mathf.Max(minKills, EnemyOverhaulConfig.SmallMobMaxKills.Value);

            bool triggerAmbush = false;
            if (currentKills >= maxKills)
            {
                triggerAmbush = true;
            }
            else if (currentKills >= minKills)
            {
                float span = Mathf.Max(1f, (float)(maxKills - minKills + 1));
                float stepChance = (float)(currentKills - minKills + 1) / span;
                if (UnityEngine.Random.value <= stepChance)
                {
                    triggerAmbush = true;
                }
            }

            if (triggerAmbush)
            {
                SetServerSpeciesKillCount(prefabName, 0);
                SpawnAmbushChampionNear(prefabName, killerPos);
            }
            else
            {
                SetServerSpeciesKillCount(prefabName, currentKills);
            }
        }

        public static int GetServerSpeciesKillCount(string prefabName)
        {
            if (ZoneSystem.instance == null || string.IsNullOrEmpty(prefabName)) return 0;
            string prefix = GlobalKeyPrefix + prefabName.ToLowerInvariant() + "_";
            List<string> keys = ZoneSystem.instance.GetGlobalKeys();
            if (keys == null) return 0;

            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                if (k != null && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    int val;
                    if (int.TryParse(k.Substring(prefix.Length), out val))
                    {
                        return Mathf.Max(0, val);
                    }
                }
            }
            return 0;
        }

        public static void SetServerSpeciesKillCount(string prefabName, int newCount)
        {
            if (ZoneSystem.instance == null || string.IsNullOrEmpty(prefabName)) return;
            string prefix = GlobalKeyPrefix + prefabName.ToLowerInvariant() + "_";
            List<string> keys = ZoneSystem.instance.GetGlobalKeys();
            if (keys != null)
            {
                List<string> toRemove = null;
                for (int i = 0; i < keys.Count; i++)
                {
                    string k = keys[i];
                    if (k != null && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (toRemove == null) toRemove = new List<string>();
                        toRemove.Add(k);
                    }
                }
                if (toRemove != null)
                {
                    for (int i = 0; i < toRemove.Count; i++)
                    {
                        ZoneSystem.instance.RemoveGlobalKey(toRemove[i]);
                    }
                }
            }

            if (newCount > 0)
            {
                ZoneSystem.instance.SetGlobalKey(prefix + newCount);
            }
        }

        /// <summary>
        /// Spawns a stealth ambush Named Champion of `prefabName` near `targetPos`:
        /// - If inside a dungeon (`y > 3000m`): picks a floor point in an adjacent corridor/room of the same dungeon!
        /// - If outdoors: picks a ground point `30-45m` away outside PlayerBase and above water!
        /// No top Boss HUD and no warning banner — pure surprise ambush!
        /// </summary>
        public static GameObject SpawnAmbushChampionNear(string prefabName, Vector3 targetPos)
        {
            if (ZNetScene.instance == null) return null;
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null) return null;

            bool insideDungeon = targetPos.y > 3000f;
            Vector3 spawnPos;

            if (insideDungeon)
            {
                if (!TryFindDungeonAmbushPoint(targetPos, out spawnPos))
                {
                    return null;
                }
            }
            else
            {
                if (!TryFindOutdoorAmbushPoint(targetPos, 30f, 45f, out spawnPos))
                {
                    return null;
                }
            }

            Vector3 lookDir = targetPos - spawnPos;
            lookDir.y = 0f;
            Quaternion rot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir.normalized, Vector3.up) : Quaternion.identity;

            Heightmap.Biome biome = GuessBiomeForPositionAndPrefab(targetPos, prefabName);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, spawnPos, rot);
            ImbueAsChampion(instance, biome, isDungeonWarden: false, wardenDungeonId: string.Empty);

            // Command AI to actively stalk & hunt the player immediately!
            MonsterAI ai = instance.GetComponent<MonsterAI>();
            if (ai != null)
            {
                ai.SetHuntPlayer(true);
                ai.SetPatrolPoint();
                ai.Alert();
            }

            if (CoarstalsEnemyOverhaulPlugin.ModLogger != null)
            {
                CoarstalsEnemyOverhaulPlugin.ModLogger.LogInfo(string.Format(
                    "[EnemyOverhaul] Ambush Champion ({0}) spawned silently at {1} (dungeon={2})!",
                    prefabName, spawnPos, insideDungeon));
            }

            return instance;
        }

        public static void ImbueAsChampion(GameObject creatureObj, Heightmap.Biome biome, bool isDungeonWarden, string wardenDungeonId)
        {
            if (creatureObj == null) return;
            Character ch = creatureObj.GetComponent<Character>();
            ZNetView nv = creatureObj.GetComponent<ZNetView>();
            if (ch == null || nv == null || !nv.IsValid()) return;

            ZDO zdo = nv.GetZDO();
            if (zdo == null) return;

            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            string rusTitle;
            string engTitle;
            EnemyLocalization.GenerateChampionIdentity(biome, seed, out rusTitle, out engTitle);

            // Roll 1-2 combat perks
            ChampionPerk perk1 = (ChampionPerk)UnityEngine.Random.Range(1, 4); // ElementalInfusion, BerserkFury, or PackCaller
            ChampionPerk perk2 = ChampionPerk.None;
            if (UnityEngine.Random.value <= 0.55f)
            {
                perk2 = (perk1 == ChampionPerk.ElementalInfusion) ? ChampionPerk.BerserkFury : ChampionPerk.ElementalInfusion;
            }
            // Inside tight dungeons, prefer ElementalInfusion + BerserkFury over PackCaller so hallways don't clog
            if (isDungeonWarden && perk1 == ChampionPerk.PackCaller)
            {
                perk1 = ChampionPerk.ElementalInfusion;
                perk2 = ChampionPerk.BerserkFury;
            }

            // Set visual star level to 3 (level 4 in Valheim = 3 stars!) BEFORE overriding our exact custom HP (~x3.2)
            float basePrefabHp = ch.m_health;
            ch.SetLevel(4);

            float hpMult = Mathf.Max(1.2f, EnemyOverhaulConfig.ChampionHPMultiplier.Value);
            if (isDungeonWarden)
            {
                hpMult *= 1.15f; // Dungeon Wardens are slightly beefier guardians of the seal
            }

            // Modest co-op HP scaling (+25% per extra nearby player within 60m)
            int nearbyPlayers = CountNearbyAlivePlayers(creatureObj.transform.position, 65f);
            if (nearbyPlayers > 1)
            {
                hpMult *= (1f + (nearbyPlayers - 1) * 0.25f);
            }

            float finalMaxHp = Mathf.Max(40f, Mathf.Round(basePrefabHp * hpMult));
            ch.SetMaxHealth(finalMaxHp);
            ch.SetHealth(finalMaxHp);

            // Ensure NO top-screen Boss HUD (per user design: surprise encounter with overhead skull/3-star name!)
            ch.m_boss = false;

            zdo.Set(ZdoIsChampion, true);
            zdo.Set(ZdoIsDungeonWarden, isDungeonWarden);
            if (!string.IsNullOrEmpty(wardenDungeonId))
            {
                zdo.Set(ZdoWardenDungeonId, wardenDungeonId);
            }
            if (isDungeonWarden)
            {
                zdo.Set(ZdoWardenAnchorPos, creatureObj.transform.position);
            }
            zdo.Set(ZdoChampRusTitle, rusTitle);
            zdo.Set(ZdoChampEngTitle, engTitle);
            zdo.Set(ZdoChampPerk1, (int)perk1);
            zdo.Set(ZdoChampPerk2, (int)perk2);
            zdo.Set(ZdoChampBiome, (int)biome);
            zdo.Set(ZdoChampMaxHP, finalMaxHp);

            ChampionBehaviour comp = creatureObj.GetComponent<ChampionBehaviour>();
            if (comp == null) comp = creatureObj.AddComponent<ChampionBehaviour>();
            comp.RefreshFromZDO();
        }

        public static void SpawnPackBodyguards(Character champion)
        {
            if (champion == null || ZNetScene.instance == null) return;
            string prefabName = Utils.GetPrefabName(champion.gameObject);
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null) return;

            // Large monsters summon 1 bodyguard; regular monsters summon 2 bodyguards
            int count = IsLargeSpecies(prefabName) ? 1 : 2;
            Vector3 center = champion.transform.position;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, (i == 0 ? -55f : 55f), 0f) * champion.transform.forward * 2.8f;
                Vector3 pos = center + offset + Vector3.up * 0.35f;

                RaycastHit hit;
                if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out hit, 6f, LayerMask.GetMask("Default", "static_solid", "terrain", "piece")))
                {
                    pos = hit.point + Vector3.up * 0.2f;
                }

                GameObject guard = UnityEngine.Object.Instantiate(prefab, pos, champion.transform.rotation);
                ZNetView nv = guard.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO() != null)
                {
                    nv.GetZDO().Set(ZdoIsChampionMinion, true);
                }

                MonsterAI ai = guard.GetComponent<MonsterAI>();
                if (ai != null)
                {
                    ai.SetHuntPlayer(true);
                    ai.Alert();
                }
            }
        }

        public static void SpawnChampionBonusRewards(Character champion)
        {
            if (champion == null || ZNetScene.instance == null) return;

            string speciesPrefab = Utils.GetPrefabName(champion.gameObject);
            bool isLarge = IsLargeSpecies(speciesPrefab);
            Vector3 dropCenter = champion.GetCenterPoint() + Vector3.up * 0.35f;

            // 1) Guaranteed Mob Trophies (2 for large species, 3 for common species -> fuels WeaponAttributes enchanting!)
            int trophyCount = isLarge
                ? Mathf.Max(1, EnemyOverhaulConfig.LargeChampionTrophyCount.Value)
                : Mathf.Max(1, EnemyOverhaulConfig.SmallChampionTrophyCount.Value);

            GameObject trophyPrefab = FindTrophyPrefabForCreature(champion, speciesPrefab);
            if (trophyPrefab != null)
            {
                DropItemStack(trophyPrefab, trophyCount, dropCenter);
            }

            // 2) Extra regular resource drops so total loot equals ChampionLootMultiplier (e.g. x3)
            int extraMult = Mathf.Max(0, EnemyOverhaulConfig.ChampionLootMultiplier.Value - 1);
            CharacterDrop charDrop = champion.GetComponent<CharacterDrop>();
            if (extraMult > 0 && charDrop != null && charDrop.m_drops != null)
            {
                for (int i = 0; i < charDrop.m_drops.Count; i++)
                {
                    CharacterDrop.Drop d = charDrop.m_drops[i];
                    if (d == null || d.m_prefab == null) continue;
                    if (d.m_prefab.name.IndexOf("Trophy", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    if (UnityEngine.Random.value <= Mathf.Clamp01(d.m_chance * 1.35f))
                    {
                        int baseAmt = UnityEngine.Random.Range(Mathf.Max(1, d.m_amountMin), Mathf.Max(1, d.m_amountMax) + 1);
                        int bonusAmt = baseAmt * extraMult;
                        if (bonusAmt > 0)
                        {
                            DropItemStack(d.m_prefab, bonusAmt, dropCenter);
                        }
                    }
                }
            }

            // 3) Handful of Coins (scales with biome tier -> helps fund Hildir's 1000g Rebirth Scroll in RP Classes!)
            GameObject coinsPrefab = ZNetScene.instance.GetPrefab("Coins");
            if (coinsPrefab != null)
            {
                Heightmap.Biome b = Heightmap.Biome.BlackForest;
                ZNetView nv = champion.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO() != null)
                {
                    b = (Heightmap.Biome)nv.GetZDO().GetInt(ZdoChampBiome, (int)Heightmap.Biome.BlackForest);
                }
                float biomeCoinScale = GetBiomeTierCoinMultiplier(b);
                int minC = Mathf.Max(1, Mathf.RoundToInt(EnemyOverhaulConfig.ChampionMinCoins.Value * biomeCoinScale));
                int maxC = Mathf.Max(minC, Mathf.RoundToInt(EnemyOverhaulConfig.ChampionMaxCoins.Value * biomeCoinScale));
                int coinAmount = UnityEngine.Random.Range(minC, maxC + 1);
                DropItemStack(coinsPrefab, coinAmount, dropCenter);
            }
        }

        private static GameObject FindTrophyPrefabForCreature(Character ch, string speciesPrefab)
        {
            CharacterDrop cd = ch.GetComponent<CharacterDrop>();
            if (cd != null && cd.m_drops != null)
            {
                for (int i = 0; i < cd.m_drops.Count; i++)
                {
                    CharacterDrop.Drop d = cd.m_drops[i];
                    if (d != null && d.m_prefab != null && d.m_prefab.name.IndexOf("Trophy", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return d.m_prefab;
                    }
                }
            }

            string trophyName;
            if (!string.IsNullOrEmpty(speciesPrefab) && s_fallbackTrophyBySpecies.TryGetValue(speciesPrefab, out trophyName))
            {
                return ZNetScene.instance.GetPrefab(trophyName);
            }
            return null;
        }

        private static void DropItemStack(GameObject itemPrefab, int totalAmount, Vector3 center)
        {
            if (itemPrefab == null || totalAmount <= 0) return;

            ItemDrop templateDrop = itemPrefab.GetComponent<ItemDrop>();
            int maxStack = (templateDrop != null && templateDrop.m_itemData != null && templateDrop.m_itemData.m_shared != null)
                ? Mathf.Max(1, templateDrop.m_itemData.m_shared.m_maxStackSize)
                : 50;

            int remaining = totalAmount;
            while (remaining > 0)
            {
                int stack = Mathf.Min(remaining, maxStack);
                remaining -= stack;

                Vector2 rnd = UnityEngine.Random.insideUnitCircle * 0.65f;
                Vector3 pos = center + new Vector3(rnd.x, 0.25f, rnd.y);
                GameObject dropped = UnityEngine.Object.Instantiate(itemPrefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));

                ItemDrop id = dropped.GetComponent<ItemDrop>();
                if (id != null)
                {
                    id.SetStack(stack);
                }

                Rigidbody rb = dropped.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 v = UnityEngine.Random.insideUnitSphere;
                    v.y = Mathf.Abs(v.y) + 1.2f;
                    rb.linearVelocity = v * 2.4f;
                }
            }
        }

        public static void ApplyChampionElementalPerkDamage(Character attacker, ref HitData hit)
        {
            if (attacker == null || hit == null) return;
            ZNetView nv = attacker.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null) return;
            ZDO zdo = nv.GetZDO();
            if (!zdo.GetBool(ZdoIsChampion, false)) return;

            float dmgMult = Mathf.Max(1.0f, EnemyOverhaulConfig.ChampionDamageMultiplier.Value);
            ScaleHitDamage(ref hit.m_damage, dmgMult);

            int p1 = zdo.GetInt(ZdoChampPerk1, 0);
            int p2 = zdo.GetInt(ZdoChampPerk2, 0);
            if (p1 == (int)ChampionPerk.ElementalInfusion || p2 == (int)ChampionPerk.ElementalInfusion)
            {
                Heightmap.Biome biome = (Heightmap.Biome)zdo.GetInt(ZdoChampBiome, (int)Heightmap.Biome.BlackForest);
                float totalPhys = hit.m_damage.m_damage + hit.m_damage.m_blunt + hit.m_damage.m_slash + hit.m_damage.m_pierce;
                float elemBonus = Mathf.Max(4f, totalPhys * 0.22f);

                switch (biome)
                {
                    case Heightmap.Biome.Meadows:
                        hit.m_damage.m_lightning += elemBonus;
                        break;
                    case Heightmap.Biome.BlackForest:
                        hit.m_damage.m_poison += elemBonus * 0.5f;
                        hit.m_damage.m_blunt += elemBonus * 0.5f;
                        break;
                    case Heightmap.Biome.Swamp:
                        hit.m_damage.m_poison += elemBonus;
                        break;
                    case Heightmap.Biome.Mountain:
                    case Heightmap.Biome.DeepNorth:
                        hit.m_damage.m_frost += elemBonus;
                        break;
                    case Heightmap.Biome.Plains:
                        hit.m_damage.m_fire += elemBonus;
                        break;
                    case Heightmap.Biome.Mistlands:
                        hit.m_damage.m_lightning += elemBonus * 0.6f;
                        hit.m_damage.m_poison += elemBonus * 0.6f;
                        break;
                    case Heightmap.Biome.AshLands:
                        hit.m_damage.m_fire += elemBonus;
                        break;
                }
            }
        }

        internal static void ScaleHitDamage(ref HitData.DamageTypes dmg, float mult)
        {
            dmg.m_damage *= mult;
            dmg.m_blunt *= mult;
            dmg.m_slash *= mult;
            dmg.m_pierce *= mult;
            dmg.m_fire *= mult;
            dmg.m_frost *= mult;
            dmg.m_lightning *= mult;
            dmg.m_poison *= mult;
            dmg.m_spirit *= mult;
        }

        private static bool TryFindOutdoorAmbushPoint(Vector3 center, float minRadius, float maxRadius, out Vector3 result)
        {
            for (int attempt = 0; attempt < 28; attempt++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(minRadius, maxRadius);
                Vector3 candidate = center + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);

                // Never spawn inside a player's workbench / campfire base protection radius!
                if (EffectArea.IsPointInsideArea(candidate, EffectArea.Type.PlayerBase, 2f))
                {
                    continue;
                }

                float groundY;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(candidate, out groundY, 1000))
                {
                    float waterLvl = ZoneSystem.instance.m_waterLevel;
                    if (groundY >= waterLvl - 0.35f)
                    {
                        result = new Vector3(candidate.x, groundY + 0.5f, candidate.z);
                        return true;
                    }
                }
            }

            result = Vector3.zero;
            return false;
        }

        private static bool TryFindDungeonAmbushPoint(Vector3 playerPos, out Vector3 result)
        {
            result = Vector3.zero;
            DungeonGenerator nearestDg = CursedDungeonSystem.FindOwningDungeonGeneratorForPoint(playerPos);
            if (nearestDg == null) return false;

            int solidMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");

            // 1) Search child CreatureSpawners of THIS EXACT `DungeonGenerator`!
            CreatureSpawner[] spawners = nearestDg.GetComponentsInChildren<CreatureSpawner>(true);
            Vector3 bestFallback = Vector3.zero;
            float bestScore = float.MaxValue;

            if (spawners != null && spawners.Length > 0)
            {
                for (int i = 0; i < spawners.Length; i++)
                {
                    CreatureSpawner sp = spawners[i];
                    if (sp == null || !sp.gameObject.activeInHierarchy) continue;

                    Vector3 p = sp.transform.position;
                    float horizDist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z));

                    if (horizDist >= 10f && horizDist <= 42f)
                    {
                        RaycastHit hit1;
                        if (Physics.Raycast(p + Vector3.up * 1.1f, Vector3.down, out hit1, 4.5f, solidMask))
                        {
                            result = hit1.point + Vector3.up * 0.18f;
                        }
                        else
                        {
                            result = p + Vector3.up * 0.18f;
                        }
                        return true;
                    }

                    if (horizDist >= 4f && Mathf.Abs(horizDist - 18f) < bestScore)
                    {
                        bestScore = Mathf.Abs(horizDist - 18f);
                        bestFallback = p + Vector3.up * 0.25f;
                    }
                }
            }

            // 2) Fallback: check child Room transforms of THIS EXACT `DungeonGenerator`!
            Room[] rooms = nearestDg.GetComponentsInChildren<Room>(true);
            if (rooms != null && rooms.Length > 0)
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    Room r = rooms[i];
                    if (r == null || !r.gameObject.activeInHierarchy) continue;

                    Vector3 p = r.transform.position;
                    if (r.m_endCap || r.m_divider) continue;
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z));
                    if (d >= 7f && d <= 52f)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out hit, 4.5f, solidMask))
                        {
                            result = hit.point + Vector3.up * 0.18f;
                            return true;
                        }
                    }
                }
            }

            // 3) Fallback for single-room TrollCave02 (`DG_Cave`): use deepest floor of THIS cave!
            if (bestFallback == Vector3.zero)
            {
                string unusedPrefab;
                if (CursedDungeonSystem.TryFindDeepestFloorInThisDungeon(nearestDg, out bestFallback, out unusedPrefab))
                {
                    result = bestFallback;
                    return true;
                }
            }

            if (bestFallback != Vector3.zero)
            {
                result = bestFallback;
                return true;
            }

            return false;
        }

        private static Heightmap.Biome GuessBiomeForPositionAndPrefab(Vector3 pos, string prefabName)
        {
            if (pos.y < 3000f && WorldGenerator.instance != null)
            {
                Heightmap.Biome b = WorldGenerator.instance.GetBiome(pos);
                if (b != Heightmap.Biome.None) return b;
            }

            string low = prefabName != null ? prefabName.ToLowerInvariant() : "";
            if (low.Contains("draugr") || low.Contains("blob") || low.Contains("wraith") || low.Contains("abomination"))
                return Heightmap.Biome.Swamp;
            if (low.Contains("wolf") || low.Contains("fenring") || low.Contains("cultist") || low.Contains("ulv") || low.Contains("golem") || low.Contains("hatchling"))
                return Heightmap.Biome.Mountain;
            if (low.Contains("goblin") || low.Contains("lox") || low.Contains("deathsquito"))
                return Heightmap.Biome.Plains;
            if (low.Contains("seeker") || low.Contains("gjall") || low.Contains("tick"))
                return Heightmap.Biome.Mistlands;
            if (low.Contains("charred") || low.Contains("morgen") || low.Contains("asksvin") || low.Contains("valkyrie"))
                return Heightmap.Biome.AshLands;
            if (low.Contains("skeleton") || low.Contains("greydwarf") || low.Contains("troll") || low.Contains("ghost"))
                return Heightmap.Biome.BlackForest;

            return Heightmap.Biome.Meadows;
        }

        private static int CountNearbyAlivePlayers(Vector3 pos, float radius)
        {
            List<Player> all = Player.GetAllPlayers();
            if (all == null) return 1;
            int count = 0;
            float rSq = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                Player p = all[i];
                if (p != null && !p.IsDead() && (p.transform.position - pos).sqrMagnitude <= rSq)
                {
                    count++;
                }
            }
            return Mathf.Max(1, count);
        }

        private static float GetBiomeTierCoinMultiplier(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows: return 0.75f;
                case Heightmap.Biome.BlackForest: return 1.0f;
                case Heightmap.Biome.Swamp: return 1.45f;
                case Heightmap.Biome.Mountain: return 1.85f;
                case Heightmap.Biome.Plains: return 2.35f;
                case Heightmap.Biome.Mistlands: return 3.0f;
                case Heightmap.Biome.AshLands:
                case Heightmap.Biome.DeepNorth: return 3.8f;
                default: return 1.0f;
            }
        }

        public static Color GetBiomeAccentColor(Heightmap.Biome biome, bool isDungeonWarden)
        {
            if (isDungeonWarden)
            {
                return new Color(0.84f, 0.24f, 1.00f, 1f); // Deep Cursed Violet-Magenta
            }

            switch (biome)
            {
                case Heightmap.Biome.Meadows: return new Color(1.00f, 0.82f, 0.25f, 1f);
                case Heightmap.Biome.BlackForest: return new Color(0.35f, 0.92f, 0.55f, 1f);
                case Heightmap.Biome.Swamp: return new Color(0.45f, 0.95f, 0.22f, 1f);
                case Heightmap.Biome.Mountain: return new Color(0.30f, 0.78f, 1.00f, 1f);
                case Heightmap.Biome.Plains: return new Color(1.00f, 0.32f, 0.22f, 1f);
                case Heightmap.Biome.Mistlands: return new Color(0.76f, 0.35f, 1.00f, 1f);
                case Heightmap.Biome.AshLands: return new Color(1.00f, 0.45f, 0.12f, 1f);
                default: return new Color(1.00f, 0.38f, 0.28f, 1f);
            }
        }

        public static string FormatChampionOverheadName(Character ch)
        {
            ZNetView nv = ch.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null) return ch.GetHoverName();
            ZDO zdo = nv.GetZDO();

            bool isRus = EnemyLocalization.IsRussian();
            string title = isRus ? zdo.GetString(ZdoChampRusTitle, "") : zdo.GetString(ZdoChampEngTitle, "");
            bool isWarden = zdo.GetBool(ZdoIsDungeonWarden, false);
            Heightmap.Biome biome = (Heightmap.Biome)zdo.GetInt(ZdoChampBiome, (int)Heightmap.Biome.BlackForest);

            Color32 col = GetBiomeAccentColor(biome, isWarden);
            string hex = string.Format("#{0:X2}{1:X2}{2:X2}", col.r, col.g, col.b);
            string tag = EnemyLocalization.Loc(isWarden ? "$ceo_dungeon_warden_tag" : "$ceo_champ_tag");
            string baseSpeciesName = Localization.instance != null ? Localization.instance.Localize(ch.m_name) : ch.m_name;

            if (!string.IsNullOrEmpty(title))
            {
                return string.Format(
                    "<size=82%><color={0}>☠ [{1}] ★★★</color></size>\n<color={0}><b>{2}</b></color> <size=88%>({3})</size>",
                    hex, tag, title, baseSpeciesName);
            }

            return string.Format("<size=82%><color={0}>☠ [{1}] ★★★</color></size>\n<color={0}><b>{2}</b></color>", hex, tag, baseSpeciesName);
        }
    }
}
