using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CoarstalsEnemyOverhaul
{
    /// <summary>
    /// Bulletproof Sealed Cursed Dungeons (10% chance):
    ///
    /// ZERO-SOFTLOCK CONTRACT (impossible to ever get trapped without a boss!):
    /// 1. Every dungeon interior in Valheim is instantiated under a single `Location` root (`Location.Awake` creates `m_interiorPrefab`
    ///    at `y + 5000m` as a child of `Location.transform`, and `DungeonGenerator` creates all `Room`s as children of `dg.transform`).
    ///    Therefore, every wall, floor, chest, spawner, and the interior exit `Teleport` itself share ONE exact `Location` / `DungeonGenerator` root!
    /// 2. THE EXIT PORTAL IS ONLY SEALED WHILE A VERIFIED LIVE WARDEN CHAMPION PHYSICALLY EXISTS INSIDE THIS EXACT DUNGEON!
    ///    - If you enter a Cursed Dungeon and the Warden hasn't spawned yet -> we spawn him immediately on a verified interior floor of THIS dungeon.
    ///    - If the dungeon was already cleared before (all rooms empty) -> the Warden spawns right inside the dungeon (e.g. deepest room or 6m down the hall from the exit) and charges you!
    ///    - If for ANY reason no live Warden exists within 90m on the same dungeon layer (or he somehow bugged/despawned) -> `IsExitTeleportSealed`
    ///      immediately summons him into the room OR unseals the exit! A player WITHOUT admin console can NEVER be softlocked!
    /// </summary>
    public static class CursedDungeonSystem
    {
        public const int StateUnrolled = -1;
        public const int StateNormal = 0;
        public const int StateCursedActive = 1;
        public const int StateCursedCleansed = 2;

        public const string ZdoDungeonState = "CEO_CurseState";
        public const string ZdoDungeonRuleVersion = "CEO_CurseVer";
        public const string ZdoDungeonAdminForced = "CEO_AdminCurse";
        public const int CurrentCurseRuleVersion = 2;
        public const string ZdoDungeonWardenSpawned = "CEO_WardenSpawned";
        public const string ZdoDungeonBiome = "CEO_DungBiome";
        public const string ZdoCursedMobApplied = "CEO_CursedMob";
        public const string ZdoCursedMobMaxHP = "CEO_CursedMobHP";

        public const string RPC_DungeonCleansed = "CEO_RPC_DungeonCleansed";
        private const string GlobalKeyCleansedPrefix = "ceo_cleansed_";

        private static readonly FieldInfo s_dgNviewField = typeof(DungeonGenerator).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo s_dgLoadedRoomsField = typeof(DungeonGenerator).GetField("m_loadedRooms", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static FieldInfo s_rpdRoomDataField;
        private static FieldInfo s_rpdPosField;
        private static FieldInfo s_rpdRotField;
        private static PropertyInfo s_roomDataRoomInPrefabProp;
        private static FieldInfo s_roomEndCapField = typeof(Room).GetField("m_endCap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static FieldInfo s_roomEntranceField = typeof(Room).GetField("m_entrance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static FieldInfo s_roomDividerField = typeof(Room).GetField("m_divider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static Texture2D s_softSmokeTex;
        private static Material s_softParticleMat;

        private static string s_currentAnnouncedDungeonId = string.Empty;
        private static GameObject s_localCurseFogRoot;
        private static ParticleSystem s_localCurseFogPs;
        private static Light s_localCurseAmbientLight;
        private static bool s_lastFogActiveState;
        private static Heightmap.Biome s_lastFogBiome = Heightmap.Biome.None;
        private static float s_checkTimer;
        private static float s_wardenCheckTimer;
        private static bool s_rpcRegistered;

        // O(1) active dungeon caches to eliminate repeated scene-wide `FindObjectsByType` / `GetComponentsInChildren` microfreezes!
        private static DungeonAnchor s_cachedAnchor;
        private static Heightmap.Biome s_cachedAnchorBiome = Heightmap.Biome.None;
        private static bool s_cachedIsSingleLair;
        private static bool s_cachedSingleLairComputed;
        private static Character s_cachedLiveWarden;
        private static readonly HashSet<int> s_processedDungeonMobIds = new HashSet<int>();
        private static readonly Dictionary<int, bool> s_cursedMobFlagCache = new Dictionary<int, bool>();
        private static int s_hoverCachedPortalId;
        private static float s_hoverCachedUntil;
        private static bool s_hoverCachedSealed;

        // Exact per-dungeon-type stratified 10% quota cache across all generated locations in the world
        private static readonly HashSet<string> s_worldQuotaCursedIds = new HashSet<string>();
        private static int s_worldQuotaBuiltLocationCount = -1;
        private static float s_worldQuotaBuiltChance = -1f;
        private static int s_worldQuotaSeed;

        /// <summary>
        /// Represents a single self-contained Dungeon instance (works identically for procedural Crypts/Caves with `DungeonGenerator`
        /// AND static interior caves like `TrollCave02` that only have `Location` + `m_interiorPrefab` at `y + 5000m`!).
        /// </summary>
        public struct DungeonAnchor
        {
            public Location ParentLocation;
            public DungeonGenerator Generator;
            public Transform InteriorRoot;
            public Teleport InteriorExitPortal;
            public Vector3 EntranceInteriorPos;
            public string UniqueId;

            public bool IsValid
            {
                get { return InteriorRoot != null && !string.IsNullOrEmpty(UniqueId); }
            }
        }

        public static void RegisterRPCs(ZRoutedRpc routedRpc)
        {
            if (routedRpc == null || s_rpcRegistered) return;
            routedRpc.Register<string, Vector3>(RPC_DungeonCleansed, OnRPC_DungeonCleansed);
            s_rpcRegistered = true;
        }

        public static void ResetSessionFlag()
        {
            s_rpcRegistered = false;
            s_currentAnnouncedDungeonId = string.Empty;
            s_cachedAnchor = default(DungeonAnchor);
            s_cachedAnchorBiome = Heightmap.Biome.None;
            s_cachedSingleLairComputed = false;
            s_cachedLiveWarden = null;
            s_processedDungeonMobIds.Clear();
            s_cursedMobFlagCache.Clear();
            s_hoverCachedPortalId = 0;
            s_hoverCachedUntil = 0f;
            s_lastFogActiveState = false;
            s_lastFogBiome = Heightmap.Biome.None;
            s_worldQuotaCursedIds.Clear();
            s_worldQuotaBuiltLocationCount = -1;
            s_worldQuotaBuiltChance = -1f;
            DestroyLocalCurseFog();
        }

        // ====================================================================
        // Exact Hierarchy-Bound Dungeon Resolution (O(1) Cached Inside Active Dungeon!)
        // ====================================================================
        public static bool TryResolveDungeonAt(Vector3 worldPos, out DungeonAnchor anchor)
        {
            anchor = default(DungeonAnchor);
            if (worldPos.y < 3000f) return false;

            // FAST O(1) CACHE HIT: While inside the same loaded dungeon (within 120m XZ & 65m Y of entrance),
            // return the already-resolved `s_cachedAnchor` immediately with ZERO `FindObjectsByType` or `OverlapSphere` calls!
            if (s_cachedAnchor.IsValid)
            {
                float dx = worldPos.x - s_cachedAnchor.EntranceInteriorPos.x;
                float dz = worldPos.z - s_cachedAnchor.EntranceInteriorPos.z;
                float dy = Mathf.Abs(worldPos.y - s_cachedAnchor.EntranceInteriorPos.y);
                if (dy <= 65f && (dx * dx + dz * dz) <= 120f * 120f)
                {
                    anchor = s_cachedAnchor;
                    return true;
                }
            }

            // 1) Find the closest interior Exit `Teleport` (`y > 3000m` whose target leads outdoors `y < 3000m` or is the entrance portal).
            //    Every single enterable dungeon in Valheim (Crypts, Sunken Crypts, Frost Caves, Infested Mines, Troll Caves, Charred Tombs)
            //    MUST have an interior exit `Teleport` right where the player enters!
            Teleport[] allPortals = UnityEngine.Object.FindObjectsByType<Teleport>(FindObjectsSortMode.None);
            Teleport nearestExit = null;
            float bestPortalDistSq = 135f * 135f;

            if (allPortals != null)
            {
                for (int i = 0; i < allPortals.Length; i++)
                {
                    Teleport tp = allPortals[i];
                    if (tp == null || tp.transform.position.y < 3000f) continue;
                    if (Mathf.Abs(tp.transform.position.y - worldPos.y) > 65f) continue;

                    // If this portal belongs to a Location/DungeonGenerator whose child collider is right under the player's feet, pick it immediately!
                    float dSq = (tp.transform.position - worldPos).sqrMagnitude;
                    if (dSq < bestPortalDistSq)
                    {
                        bestPortalDistSq = dSq;
                        nearestExit = tp;
                    }
                }
            }

            // Also check colliders directly touching `worldPos` (feet/walls of the room the player is standing in!)
            Location hitLoc = null;
            DungeonGenerator hitDg = null;
            Collider[] cols = Physics.OverlapSphere(worldPos, 4.0f, ~0, QueryTriggerInteraction.Collide);
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    Collider c = cols[i];
                    if (c == null) continue;
                    if (hitDg == null)
                    {
                        DungeonGenerator dgCand = c.GetComponentInParent<DungeonGenerator>();
                        if (dgCand != null && dgCand.transform.position.y >= 3000f) hitDg = dgCand;
                    }
                    if (hitLoc == null)
                    {
                        Location locCand = c.GetComponentInParent<Location>();
                        if (locCand != null && locCand.m_hasInterior) hitLoc = locCand;
                    }
                }
            }

            if (hitLoc == null && nearestExit != null)
            {
                hitLoc = nearestExit.GetComponentInParent<Location>();
            }
            if (hitDg == null && nearestExit != null)
            {
                hitDg = nearestExit.GetComponentInParent<DungeonGenerator>();
            }
            if (hitDg == null && hitLoc != null && hitLoc.m_generator != null && hitLoc.m_generator.transform.position.y >= 3000f)
            {
                hitDg = hitLoc.m_generator;
            }
            if (hitDg == null && hitLoc != null)
            {
                // Crucial: In Valheim prefabs (`Crypt2/3/4`, `SunkenCrypt4`, `MountainCave02`), `Location.m_generator` is often left NULL in the prefab,
                // while `DG_ForestCrypt(Clone)` is instantiated as a child of `Location` at `y + 5000m`!
                DungeonGenerator[] childDgs = hitLoc.GetComponentsInChildren<DungeonGenerator>(true);
                if (childDgs != null)
                {
                    for (int i = 0; i < childDgs.Length; i++)
                    {
                        if (childDgs[i] != null && childDgs[i].transform.position.y >= 3000f)
                        {
                            hitDg = childDgs[i];
                            break;
                        }
                    }
                }
            }
            if (hitLoc == null && hitDg != null)
            {
                hitLoc = hitDg.GetComponentInParent<Location>();
            }

            // Crucial: In Valheim, `DG_ForestCrypt(Clone)` is instantiated as a TOP-LEVEL networked ZNetView in the same 64x64m Zone
            // (`ZoneSystem.GetZone`), NOT as a child of `Location`! So if `hitDg` is still null, find the `DungeonGenerator` at `y >= 3000m`
            // in the SAME 64x64m Zone (or closest within 65m XZ)!
            if (hitDg == null)
            {
                Vector3 refPt = hitLoc != null ? hitLoc.transform.position : worldPos;
                Vector2s targetZone = ZoneSystem.instance != null ? ZoneSystem.GetZone(refPt) : default(Vector2s);

                DungeonGenerator[] allDgs = UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None);
                float bestDgHorizSq = 70f * 70f;
                if (allDgs != null)
                {
                    for (int i = 0; i < allDgs.Length; i++)
                    {
                        DungeonGenerator d = allDgs[i];
                        if (d == null || d.transform.position.y < 3000f) continue;

                        if (ZoneSystem.instance != null && ZoneSystem.GetZone(d.transform.position) == targetZone)
                        {
                            hitDg = d;
                            break;
                        }

                        float dx = d.transform.position.x - refPt.x;
                        float dz = d.transform.position.z - refPt.z;
                        float hSq = dx * dx + dz * dz;
                        if (hSq < bestDgHorizSq)
                        {
                            bestDgHorizSq = hSq;
                            hitDg = d;
                        }
                    }
                }
            }
            if (hitLoc == null && hitDg != null)
            {
                hitLoc = hitDg.GetComponentInParent<Location>();
                if (hitLoc == null)
                {
                    hitLoc = Location.GetZoneLocation(hitDg.transform.position);
                }
            }

            // Determine InteriorRoot & EntranceInteriorPos
            Transform interiorRoot = null;
            if (hitDg != null)
            {
                interiorRoot = hitDg.transform;
            }
            else if (hitLoc != null)
            {
                // For static TrollCave02 (`m_hasInterior = true`, no `DungeonGenerator`), find the interior child at y > 3000m!
                for (int i = 0; i < hitLoc.transform.childCount; i++)
                {
                    Transform child = hitLoc.transform.GetChild(i);
                    if (child != null && child.position.y >= 3000f)
                    {
                        interiorRoot = child;
                        break;
                    }
                }
                if (interiorRoot == null) interiorRoot = hitLoc.transform;
            }
            else if (nearestExit != null)
            {
                interiorRoot = nearestExit.transform.root;
            }

            if (interiorRoot == null) return false;

            // Find the exact exit Teleport belonging to THIS dungeon root!
            Teleport ownExit = nearestExit;
            if (hitLoc != null)
            {
                Teleport[] locPortals = hitLoc.GetComponentsInChildren<Teleport>(true);
                if (locPortals != null)
                {
                    for (int i = 0; i < locPortals.Length; i++)
                    {
                        if (locPortals[i] != null && locPortals[i].transform.position.y >= 3000f)
                        {
                            ownExit = locPortals[i];
                            break;
                        }
                    }
                }
            }
            else if (hitDg != null)
            {
                Teleport[] dgPortals = hitDg.GetComponentsInChildren<Teleport>(true);
                if (dgPortals != null)
                {
                    for (int i = 0; i < dgPortals.Length; i++)
                    {
                        if (dgPortals[i] != null && dgPortals[i].transform.position.y >= 3000f)
                        {
                            ownExit = dgPortals[i];
                            break;
                        }
                    }
                }
            }

            Vector3 idPos;
            if (hitDg != null)
            {
                idPos = hitDg.transform.position;
            }
            else if (ownExit != null)
            {
                idPos = ownExit.transform.position;
            }
            else
            {
                idPos = interiorRoot.position;
            }

            Vector3 entranceFloorPos = (ownExit != null) ? ownExit.transform.position : idPos;

            anchor.ParentLocation = hitLoc;
            anchor.Generator = hitDg;
            anchor.InteriorRoot = interiorRoot;
            anchor.InteriorExitPortal = ownExit;
            anchor.EntranceInteriorPos = entranceFloorPos;

            // Use canonical ZoneID (`dg_z_X_Y`) so ZoneSystem.m_locationInstances across the ENTIRE world map
            // compute the exact same deterministic Curse roll as the interior dungeon itself!
            if (ZoneSystem.instance != null)
            {
                Vector3 zoneRef = hitLoc != null ? hitLoc.transform.position : idPos;
                Vector2s zoneId = ZoneSystem.GetZone(zoneRef);
                anchor.UniqueId = string.Format("dg_z_{0}_{1}", zoneId.x, zoneId.y);
            }
            else
            {
                anchor.UniqueId = string.Format(
                    "dg_{0}_{1}_{2}",
                    Mathf.RoundToInt(idPos.x),
                    Mathf.RoundToInt(idPos.y),
                    Mathf.RoundToInt(idPos.z));
            }

            if (!s_cachedAnchor.IsValid || s_cachedAnchor.UniqueId != anchor.UniqueId)
            {
                s_cachedAnchor = anchor;
                s_cachedAnchorBiome = GuessBiomeFromAnchor(anchor);
                s_cachedSingleLairComputed = false;
                s_cachedLiveWarden = null;
                s_processedDungeonMobIds.Clear();
            }
            else
            {
                s_cachedAnchor = anchor;
            }

            return true;
        }

        /// <summary>
        /// Pure O(1) spatial check (3 float subtractions — ZERO scene scans!) verifying `point` lies inside `anchor`'s sky-layer bounds.
        /// </summary>
        public static bool IsPointInDungeon(Vector3 point, DungeonAnchor anchor)
        {
            if (!anchor.IsValid || point.y < 3000f) return false;
            if (Mathf.Abs(point.y - anchor.EntranceInteriorPos.y) > 65f) return false;

            float dx = point.x - anchor.EntranceInteriorPos.x;
            float dz = point.z - anchor.EntranceInteriorPos.z;
            return (dx * dx + dz * dz) <= 115f * 115f;
        }

        // Legacy helper used by ChampionSystem for in-dungeon ambushes
        public static DungeonGenerator FindOwningDungeonGeneratorForPoint(Vector3 pos)
        {
            DungeonAnchor anchor;
            if (TryResolveDungeonAt(pos, out anchor))
            {
                return anchor.Generator;
            }
            return null;
        }

        public static DungeonGenerator FindNearestDungeonGenerator(Vector3 pos, float maxDist = 95f)
        {
            return FindOwningDungeonGeneratorForPoint(pos);
        }

        public static ZNetView GetAnchorNetView(DungeonAnchor anchor)
        {
            if (anchor.Generator != null)
            {
                ZNetView nv = anchor.Generator.GetComponent<ZNetView>();
                if (nv == null && s_dgNviewField != null)
                {
                    nv = s_dgNviewField.GetValue(anchor.Generator) as ZNetView;
                }
                if (nv != null && nv.IsValid()) return nv;
            }
            if (anchor.ParentLocation != null)
            {
                ZNetView locNv = anchor.ParentLocation.GetComponentInParent<ZNetView>();
                if (locNv != null && locNv.IsValid()) return locNv;
            }
            return null;
        }

        public static bool IsDungeonCleansedGlobally(string dungeonId)
        {
            if (string.IsNullOrEmpty(dungeonId) || ZoneSystem.instance == null) return false;
            return ZoneSystem.instance.GetGlobalKey(GlobalKeyCleansedPrefix + dungeonId);
        }

        public static int GetOrInitDungeonCurseState(DungeonAnchor anchor)
        {
            if (!anchor.IsValid || !EnemyOverhaulConfig.EnableCursedDungeons.Value) return StateNormal;

            if (IsDungeonCleansedGlobally(anchor.UniqueId))
            {
                ZNetView cNv = GetAnchorNetView(anchor);
                if (cNv != null && cNv.IsValid() && cNv.IsOwner() && cNv.GetZDO() != null)
                {
                    if (cNv.GetZDO().GetInt(ZdoDungeonState, StateUnrolled) != StateCursedCleansed)
                    {
                        cNv.GetZDO().Set(ZdoDungeonState, StateCursedCleansed);
                        cNv.GetZDO().Set(ZdoDungeonRuleVersion, CurrentCurseRuleVersion);
                    }
                }
                return StateCursedCleansed;
            }

            ZNetView nv = GetAnchorNetView(anchor);
            if (nv != null && nv.IsValid() && nv.GetZDO() != null)
            {
                ZDO zdo = nv.GetZDO();
                if (zdo.GetBool(ZdoDungeonAdminForced, false))
                {
                    int forcedState = zdo.GetInt(ZdoDungeonState, StateCursedActive);
                    return forcedState == StateCursedCleansed ? StateCursedCleansed : StateCursedActive;
                }

                int savedVer = zdo.GetInt(ZdoDungeonRuleVersion, 0);
                int state = zdo.GetInt(ZdoDungeonState, StateUnrolled);

                // If host/singleplayer has full world location table, always enforce exact 10% per-type quota
                // (also migrating any stale ZDOs written under older rule versions < CurrentCurseRuleVersion!)
                bool hasWorldTable = ZoneSystem.instance != null &&
                                     ZoneSystem.instance.m_locationInstances != null &&
                                     ZoneSystem.instance.m_locationInstances.Count > 0;

                if (state != StateUnrolled && savedVer == CurrentCurseRuleVersion && !hasWorldTable)
                {
                    return state;
                }

                bool selectedCursed = IsDungeonInExactTypeQuota(anchor.UniqueId);
                int expectedState = selectedCursed ? StateCursedActive : StateNormal;

                if (state == StateCursedCleansed)
                {
                    return StateCursedCleansed;
                }

                if ((state != expectedState || savedVer != CurrentCurseRuleVersion) && nv.IsOwner())
                {
                    zdo.Set(ZdoDungeonState, expectedState);
                    zdo.Set(ZdoDungeonRuleVersion, CurrentCurseRuleVersion);
                    zdo.Set(ZdoDungeonBiome, (int)GuessBiomeFromAnchor(anchor));

                    // If an old pre-quota save had marked this dungeon Cursed and spawned a Warden, clean up the stale Warden once!
                    if (expectedState == StateNormal && state == StateCursedActive && ZNetScene.instance != null)
                    {
                        Character staleWarden = FindLiveWardenInDungeon(anchor);
                        if (staleWarden != null)
                        {
                            ZNetView wNv = staleWarden.GetComponent<ZNetView>();
                            if (wNv != null && wNv.IsValid() && wNv.IsOwner())
                            {
                                ZNetScene.instance.Destroy(staleWarden.gameObject);
                            }
                            s_cachedLiveWarden = null;
                        }
                    }
                }

                return hasWorldTable ? expectedState : (state != StateUnrolled && savedVer == CurrentCurseRuleVersion ? state : expectedState);
            }

            return IsDungeonInExactTypeQuota(anchor.UniqueId) ? StateCursedActive : StateNormal;
        }

        private struct DungeonSortCandidate
        {
            public string UniqueId;
            public uint SortHash;
            public int ZoneX;
            public int ZoneY;
        }

        private static uint ComputeUnsignedDungeonHash(int worldSeed, string uniqueId)
        {
            unchecked
            {
                uint h = (uint)worldSeed ^ 0x9E3779B9u;
                if (!string.IsNullOrEmpty(uniqueId))
                {
                    for (int i = 0; i < uniqueId.Length; i++)
                    {
                        h ^= uniqueId[i];
                        h *= 0x01000193u; // FNV-1a prime
                    }
                }
                // MurmurHash3 32-bit finalizer (strictly unsigned shifts `>>` on `uint`!)
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        private static string GetCanonicalDungeonTypeGroup(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            string low = prefabName.ToLowerInvariant();

            // Never auto-curse Hildir's 3 unique 1-per-world quest locations
            if (low.StartsWith("hildir_")) return null;

            if (low.StartsWith("trollcave")) return "TrollCave";
            if (low.StartsWith("crypt")) return "BurialChamber";
            if (low.StartsWith("sunkencrypt")) return "SunkenCrypt";
            if (low.StartsWith("mountaincave")) return "FrostCave";
            if (low.StartsWith("mistlands_dvergrtown")) return "InfestedMine";
            if (low.StartsWith("morgencave")) return "MorgenCave";
            if (low.StartsWith("charredtomb")) return "CharredTomb";
            return null;
        }

        private static void EnsureWorldPerTypeCurseQuotaBuilt()
        {
            if (ZoneSystem.instance == null || ZoneSystem.instance.m_locationInstances == null) return;
            Dictionary<Vector2s, ZoneSystem.LocationInstance> instances = ZoneSystem.instance.m_locationInstances;
            if (instances.Count == 0) return;

            float chance = Mathf.Clamp01(EnemyOverhaulConfig.CursedDungeonChance.Value);
            int seed = WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 1337;

            if (s_worldQuotaBuiltLocationCount == instances.Count &&
                Mathf.Abs(s_worldQuotaBuiltChance - chance) < 0.0001f &&
                s_worldQuotaSeed == seed)
            {
                return;
            }

            s_worldQuotaCursedIds.Clear();
            s_worldQuotaBuiltLocationCount = instances.Count;
            s_worldQuotaBuiltChance = chance;
            s_worldQuotaSeed = seed;

            if (chance <= 0f) return;

            Dictionary<string, List<DungeonSortCandidate>> byType = new Dictionary<string, List<DungeonSortCandidate>>(StringComparer.Ordinal);

            foreach (KeyValuePair<Vector2s, ZoneSystem.LocationInstance> kv in instances)
            {
                ZoneSystem.LocationInstance inst = kv.Value;
                if (inst.m_location == null) continue;

                string prefabName = inst.m_location.m_prefabName;
                if (string.IsNullOrEmpty(prefabName) && inst.m_location.m_prefab.IsValid)
                {
                    prefabName = inst.m_location.m_prefab.Name;
                }

                string group = GetCanonicalDungeonTypeGroup(prefabName);
                if (group == null) continue;

                Vector2s z = kv.Key;
                string uid = string.Format("dg_z_{0}_{1}", z.x, z.y);

                List<DungeonSortCandidate> list;
                if (!byType.TryGetValue(group, out list))
                {
                    list = new List<DungeonSortCandidate>(64);
                    byType[group] = list;
                }

                DungeonSortCandidate cand;
                cand.UniqueId = uid;
                cand.SortHash = ComputeUnsignedDungeonHash(seed, uid);
                cand.ZoneX = z.x;
                cand.ZoneY = z.y;
                list.Add(cand);
            }

            // Pick EXACTLY `Round(totalOfType * chance)` (10%) dungeons within EACH dungeon type independently!
            foreach (KeyValuePair<string, List<DungeonSortCandidate>> kv in byType)
            {
                List<DungeonSortCandidate> list = kv.Value;
                int total = list.Count;
                if (total == 0) continue;

                list.Sort(delegate(DungeonSortCandidate a, DungeonSortCandidate b)
                {
                    int c = a.SortHash.CompareTo(b.SortHash);
                    if (c != 0) return c;
                    c = a.ZoneX.CompareTo(b.ZoneX);
                    return c != 0 ? c : a.ZoneY.CompareTo(b.ZoneY);
                });

                int quota = (chance >= 1f) ? total : Mathf.RoundToInt(total * chance);
                if (quota == 0 && chance > 0f && total >= 5)
                {
                    quota = 1;
                }
                quota = Mathf.Clamp(quota, 0, total);

                for (int i = 0; i < quota; i++)
                {
                    s_worldQuotaCursedIds.Add(list[i].UniqueId);
                }
            }
        }

        public static bool IsDungeonInExactTypeQuota(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return false;
            float chance = Mathf.Clamp01(EnemyOverhaulConfig.CursedDungeonChance.Value);
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.m_locationInstances != null &&
                ZoneSystem.instance.m_locationInstances.Count > 0)
            {
                EnsureWorldPerTypeCurseQuotaBuilt();
                return s_worldQuotaCursedIds.Contains(uniqueId);
            }

            return RollDeterministicCurseById(uniqueId);
        }

        public static bool RollDeterministicCurseById(string uniqueId)
        {
            float chance = Mathf.Clamp01(EnemyOverhaulConfig.CursedDungeonChance.Value);
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;

            int worldSeed = WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 1337;
            uint hash = ComputeUnsignedDungeonHash(worldSeed, uniqueId);
            double normalized = hash / (double)uint.MaxValue;
            return normalized < chance;
        }

        public static bool IsSingleBossLairDungeon(DungeonAnchor anchor)
        {
            if (s_cachedAnchor.IsValid && s_cachedAnchor.UniqueId == anchor.UniqueId && s_cachedSingleLairComputed)
            {
                return s_cachedIsSingleLair;
            }

            bool result = ComputeIsSingleBossLairDungeon(anchor);
            if (s_cachedAnchor.IsValid && s_cachedAnchor.UniqueId == anchor.UniqueId)
            {
                s_cachedIsSingleLair = result;
                s_cachedSingleLairComputed = true;
            }
            return result;
        }

        private static bool ComputeIsSingleBossLairDungeon(DungeonAnchor anchor)
        {
            string dgName = anchor.Generator != null ? anchor.Generator.gameObject.name.ToLowerInvariant() : "";
            string locName = anchor.ParentLocation != null ? anchor.ParentLocation.gameObject.name.ToLowerInvariant() : "";
            string rootName = anchor.InteriorRoot != null ? anchor.InteriorRoot.name.ToLowerInvariant() : "";

            // Troll Caves (`TrollCave02`) and single-monster Morgen / Lair caves.
            // NOTE: NEVER match `dg_cave` here — in Valheim, `DG_Cave` is the procedural multi-room Mountain Frost Cave (`MountainCave02`)!
            if (dgName.Contains("trollcave") || locName.Contains("trollcave") || rootName.Contains("trollcave") ||
                locName.Contains("morgencave"))
            {
                return true;
            }

            // Any procedural multi-room dungeon (`Crypt`, `SunkenCrypt`, `MountainCave`, `InfestedMine`, `CharredTomb`) is NEVER a single-boss lair!
            if (anchor.Generator != null)
            {
                Room[] dgRooms = anchor.Generator.GetComponentsInChildren<Room>(true);
                if (dgRooms != null && dgRooms.Length >= 2)
                {
                    return false;
                }
            }

            // Also treat any tiny 1-room static cave with == 1 CreatureSpawner and == 0 DungeonGenerator as a single-monster lair!
            Transform hierarchyRoot = anchor.ParentLocation != null ? anchor.ParentLocation.transform : anchor.InteriorRoot;
            if (hierarchyRoot != null && anchor.Generator == null)
            {
                CreatureSpawner[] spawners = hierarchyRoot.GetComponentsInChildren<CreatureSpawner>(true);
                int sCount = spawners != null ? spawners.Length : 0;
                if (sCount == 1)
                {
                    return true;
                }
            }

            return false;
        }

        // ====================================================================
        // Strict Live Warden Lookup & Guaranteed In-Dungeon Spawn / Leash
        // ====================================================================
        /// <summary>
        /// Finds the live Cursed Warden Champion bound to `anchor.UniqueId` in O(1) via cached reference when already resolved!
        /// </summary>
        public static Character FindLiveWardenInDungeon(DungeonAnchor anchor)
        {
            if (!anchor.IsValid) return null;

            // Fast O(1) path: if our cached Warden is alive inside this dungeon, return immediately with ZERO raycasts or loops!
            if (s_cachedLiveWarden != null && !s_cachedLiveWarden.IsDead() && IsPointInDungeon(s_cachedLiveWarden.transform.position, anchor))
            {
                ZNetView cachedNv = s_cachedLiveWarden.GetComponent<ZNetView>();
                if (cachedNv != null && cachedNv.IsValid() && cachedNv.GetZDO() != null &&
                    cachedNv.GetZDO().GetString(ChampionSystem.ZdoWardenDungeonId, "") == anchor.UniqueId)
                {
                    return s_cachedLiveWarden;
                }
            }

            List<Character> allChars = Character.GetAllCharacters();
            if (allChars == null) return null;

            for (int i = 0; i < allChars.Count; i++)
            {
                Character ch = allChars[i];
                if (ch == null || ch.IsPlayer() || ch.IsDead()) continue;
                if (ch.transform.position.y < 3000f) continue;

                ZNetView nv = ch.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || nv.GetZDO() == null) continue;
                ZDO zdo = nv.GetZDO();

                if (!zdo.GetBool(ChampionSystem.ZdoIsDungeonWarden, false)) continue;
                if (zdo.GetString(ChampionSystem.ZdoWardenDungeonId, "") != anchor.UniqueId) continue;

                if (!IsPointInDungeon(ch.transform.position, anchor))
                {
                    if (nv.IsOwner())
                    {
                        Vector3 safeFloor;
                        string unused;
                        if (TryFindGuaranteedFloorInsideDungeon(anchor, out safeFloor, out unused))
                        {
                            Rigidbody rb = ch.GetComponent<Rigidbody>();
                            if (rb != null) rb.linearVelocity = Vector3.zero;
                            ch.transform.position = safeFloor;
                            zdo.SetPosition(safeFloor);
                            zdo.Set(ChampionSystem.ZdoWardenAnchorPos, safeFloor);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                s_cachedLiveWarden = ch;
                return ch;
            }
            return null;
        }

        private static float s_dungeonEntryTime;

        public static bool IsExitTeleportSealed(Teleport tp, bool allowSpawnFallback = true)
        {
            if (tp == null || !EnemyOverhaulConfig.EnableCursedDungeons.Value) return false;

            // Exterior -> Interior entry portals (`y < 3000m`) are NEVER sealed!
            if (tp.transform.position.y < 3000f) return false;
            if (tp.m_targetPoint != null && tp.m_targetPoint.transform.position.y > 3000f) return false;

            // Fast per-frame hover cache (`GetHoverText` runs 60-144x/sec while looking near the exit!)
            if (!allowSpawnFallback)
            {
                int tpId = tp.GetInstanceID();
                if (tpId == s_hoverCachedPortalId && Time.time < s_hoverCachedUntil)
                {
                    return s_hoverCachedSealed;
                }

                DungeonAnchor hAnchor;
                bool isSealed = TryResolveDungeonAt(tp.transform.position, out hAnchor) &&
                                GetOrInitDungeonCurseState(hAnchor) == StateCursedActive;
                s_hoverCachedPortalId = tpId;
                s_hoverCachedUntil = Time.time + 0.5f;
                s_hoverCachedSealed = isSealed;
                return isSealed;
            }

            DungeonAnchor anchor;
            if (!TryResolveDungeonAt(tp.transform.position, out anchor)) return false;

            int state = GetOrInitDungeonCurseState(anchor);
            if (state != StateCursedActive) return false;

            // Verify there is ACTUALLY a live Warden Champion inside this dungeon!
            Character liveWarden = FindLiveWardenInDungeon(anchor);
            if (liveWarden != null)
            {
                return true;
            }

            // NEVER spawn the Warden from `Teleport.OnTriggerEnter` right when the player materializes on the entrance pad!
            // During the first 4 seconds after teleporting into a dungeon, keep the exit sealed while `UpdateLocalPlayerDungeonTick`
            // waits for all rooms to finish spawning and places the Warden in the deepest chamber!
            if (s_currentAnnouncedDungeonId != anchor.UniqueId || Time.time - s_dungeonEntryTime < 4.0f)
            {
                return true;
            }

            if (anchor.Generator != null)
            {
                Room[] ownRooms = anchor.Generator.GetComponentsInChildren<Room>(true);
                if (ownRooms == null || ownRooms.Length == 0)
                {
                    return true;
                }
            }

            // Rooms & physics ARE settled, yet no live Warden exists yet -> spawn him in the deepest room right now!
            EnsureWardenAndBuffedMobsInDungeon(anchor, forceImmediateNearPlayerIfEmpty: true);
            liveWarden = FindLiveWardenInDungeon(anchor);

            // Seal ONLY if the live Warden now physically exists in the scene! Never trap a player in an empty dungeon!
            return liveWarden != null;
        }

        public static void UpdateLocalPlayerDungeonTick(Player localPlayer, float dt)
        {
            if (localPlayer == null || localPlayer.IsDead())
            {
                if (localPlayer != null && localPlayer.IsDead())
                {
                    s_currentAnnouncedDungeonId = string.Empty;
                }
                SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                return;
            }

            if (s_localCurseFogRoot != null && s_localCurseFogRoot.activeSelf)
            {
                s_localCurseFogRoot.transform.position = localPlayer.transform.position + Vector3.up * 1.1f;
            }

            if (localPlayer.transform.position.y < 3000f)
            {
                if (!string.IsNullOrEmpty(s_currentAnnouncedDungeonId) || s_cachedAnchor.IsValid)
                {
                    s_currentAnnouncedDungeonId = string.Empty;
                    s_cachedAnchor = default(DungeonAnchor);
                    s_cachedSingleLairComputed = false;
                    s_cachedLiveWarden = null;
                    s_processedDungeonMobIds.Clear();
                    s_hoverCachedUntil = 0f;
                }
                SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                return;
            }

            s_checkTimer -= dt;
            if (s_checkTimer > 0f) return;
            s_checkTimer = 0.65f;

            if (!EnemyOverhaulConfig.EnableCursedDungeons.Value)
            {
                SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                return;
            }

            DungeonAnchor anchor;
            if (!TryResolveDungeonAt(localPlayer.transform.position, out anchor))
            {
                SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                return;
            }

            int state = GetOrInitDungeonCurseState(anchor);
            if (state != StateCursedActive)
            {
                SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                return;
            }

            Heightmap.Biome biome = (s_cachedAnchor.IsValid && s_cachedAnchor.UniqueId == anchor.UniqueId && s_cachedAnchorBiome != Heightmap.Biome.None)
                ? s_cachedAnchorBiome
                : GuessBiomeFromAnchor(anchor);

            if (s_currentAnnouncedDungeonId != anchor.UniqueId)
            {
                s_currentAnnouncedDungeonId = anchor.UniqueId;
                s_dungeonEntryTime = Time.time;
                if (MessageHud.instance != null)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, EnemyLocalization.Loc("$ceo_cursed_enter_banner"), 0, null);
                }
                PlayOminousCurseAudio(localPlayer.transform.position, cleansed: false);
                // Wait 1.3s for Unity physics to bake async Room MeshColliders before picking the deepest room!
                s_wardenCheckTimer = 1.3f;
            }

            SetLocalCurseFogActive(EnemyOverhaulConfig.CursedDungeonEnableFog.Value, biome);

            s_wardenCheckTimer -= 0.65f;
            if (s_wardenCheckTimer <= 0f)
            {
                s_wardenCheckTimer = 3.0f;
                EnsureWardenAndBuffedMobsInDungeon(anchor, forceImmediateNearPlayerIfEmpty: false);
            }
        }

        private static void EnsureWardenAndBuffedMobsInDungeon(DungeonAnchor anchor, bool forceImmediateNearPlayerIfEmpty)
        {
            if (!anchor.IsValid) return;
            Heightmap.Biome biome = (s_cachedAnchor.IsValid && s_cachedAnchor.UniqueId == anchor.UniqueId && s_cachedAnchorBiome != Heightmap.Biome.None)
                ? s_cachedAnchorBiome
                : GuessBiomeFromAnchor(anchor);
            bool isSingleBossLair = IsSingleBossLairDungeon(anchor);

            // 1) In single-monster lairs (like Troll Caves), ONLY the Cursed Warden Boss should exist inside —
            //    either promote the existing lone monster directly into the Warden, or despawn any extra non-Champion mob!
            //    In multi-room dungeons (Crypts, Caves, Mines), buff regular mobs (+30% HP, +30% damage).
            List<Character> allChars = Character.GetAllCharacters();
            Character existingLiveWarden = FindLiveWardenInDungeon(anchor);

            // Auto-migrate legacy mis-spawned Troll Warden inside Mountain Frost Caves (`DG_Cave` bug in <= v1.0.1)
            if (existingLiveWarden != null && biome == Heightmap.Biome.Mountain)
            {
                string wPrefab = Utils.GetPrefabName(existingLiveWarden.gameObject);
                if (!string.IsNullOrEmpty(wPrefab) && wPrefab.StartsWith("Troll", StringComparison.OrdinalIgnoreCase))
                {
                    ZNetView wNv = existingLiveWarden.GetComponent<ZNetView>();
                    if (wNv != null && wNv.IsValid() && wNv.IsOwner() && ZNetScene.instance != null)
                    {
                        ZNetScene.instance.Destroy(existingLiveWarden.gameObject);
                        existingLiveWarden = null;
                        s_cachedLiveWarden = null;
                    }
                }
            }

            Character candidateToPromote = null;

            if (allChars != null)
            {
                for (int i = allChars.Count - 1; i >= 0; i--)
                {
                    Character ch = allChars[i];
                    if (ch == null || ch.IsPlayer() || ch.IsDead() || ch.IsTamed()) continue;
                    if (ch.transform.position.y < 3000f) continue;

                    int mobId = ch.GetInstanceID();
                    if (!isSingleBossLair && s_processedDungeonMobIds.Contains(mobId)) continue;
                    if (!IsPointInDungeon(ch.transform.position, anchor)) continue;

                    ZNetView cNv = ch.GetComponent<ZNetView>();
                    if (cNv == null || !cNv.IsValid() || cNv.GetZDO() == null) continue;
                    ZDO cZdo = cNv.GetZDO();

                    if (cZdo.GetBool(ChampionSystem.ZdoIsChampion, false))
                    {
                        s_processedDungeonMobIds.Add(mobId);
                        continue;
                    }

                    if (isSingleBossLair)
                    {
                        if (existingLiveWarden == null && candidateToPromote == null && cNv.IsOwner())
                        {
                            candidateToPromote = ch;
                        }
                        else if (cNv.IsOwner() && !cZdo.GetBool(ChampionSystem.ZdoIsChampionMinion, false))
                        {
                            // Remove duplicate vanilla Troll/monster in a single-enemy Cursed Cave so ONLY the Boss stands there!
                            if (ZNetScene.instance != null)
                            {
                                ZNetScene.instance.Destroy(ch.gameObject);
                            }
                            else
                            {
                                UnityEngine.Object.Destroy(ch.gameObject);
                            }
                        }
                        continue;
                    }

                    if (!cZdo.GetBool(ZdoCursedMobApplied, false) && cNv.IsOwner())
                    {
                        float hpBonus = Mathf.Max(0f, EnemyOverhaulConfig.CursedDungeonMobHPBonus.Value);
                        float newMax = Mathf.Round(ch.GetMaxHealth() * (1f + hpBonus));
                        ch.SetMaxHealth(newMax);
                        ch.SetHealth(newMax);
                        cZdo.Set(ZdoCursedMobApplied, true);
                        cZdo.Set(ZdoCursedMobMaxHP, newMax);
                        s_cursedMobFlagCache[mobId] = true;
                        s_processedDungeonMobIds.Add(mobId);
                    }
                    else if (cZdo.GetBool(ZdoCursedMobApplied, false))
                    {
                        if (cNv.IsOwner())
                        {
                            float savedMax = cZdo.GetFloat(ZdoCursedMobMaxHP, 0f);
                            if (savedMax > 1f && Mathf.Abs(ch.GetMaxHealth() - savedMax) > 1f)
                            {
                                ch.SetMaxHealth(savedMax);
                            }
                        }
                        s_cursedMobFlagCache[mobId] = true;
                        s_processedDungeonMobIds.Add(mobId);
                    }
                }
            }

            // Also disable CreatureSpawners in a single-boss lair once the Warden is active so a second vanilla Troll never respawns mid-fight!
            if (isSingleBossLair)
            {
                Transform hRoot = anchor.ParentLocation != null ? anchor.ParentLocation.transform : anchor.InteriorRoot;
                if (hRoot != null)
                {
                    CreatureSpawner[] lairSpawners = hRoot.GetComponentsInChildren<CreatureSpawner>(true);
                    if (lairSpawners != null)
                    {
                        for (int i = 0; i < lairSpawners.Length; i++)
                        {
                            if (lairSpawners[i] != null)
                            {
                                lairSpawners[i].m_respawnTimeMinuts = 0f;
                                lairSpawners[i].enabled = false;
                            }
                        }
                    }
                }
            }

            // 2) Check if our live Warden is already inside this dungeon
            if (existingLiveWarden != null)
            {
                return;
            }

            // If single-boss lair already had a vanilla Troll standing on the cave floor, promote HIM directly into the Cursed Warden Boss!
            if (isSingleBossLair && candidateToPromote != null)
            {
                ChampionSystem.ImbueAsChampion(candidateToPromote.gameObject, biome, isDungeonWarden: true, wardenDungeonId: anchor.UniqueId);
                MonsterAI promAi = candidateToPromote.GetComponent<MonsterAI>();
                if (promAi != null)
                {
                    promAi.SetPatrolPoint();
                }
                ZNetView aNv = GetAnchorNetView(anchor);
                if (aNv != null && aNv.IsValid() && aNv.IsOwner() && aNv.GetZDO() != null)
                {
                    aNv.GetZDO().Set(ZdoDungeonWardenSpawned, true);
                }
                if (CoarstalsEnemyOverhaulPlugin.ModLogger != null)
                {
                    CoarstalsEnemyOverhaulPlugin.ModLogger.LogInfo(string.Format(
                        "[EnemyOverhaul] Promoted lone cave monster ({0}) into sole Cursed Dungeon Warden for {1}!",
                        candidateToPromote.name, anchor.UniqueId));
                }
                return;
            }

            // Wait until rooms have woken up for procedural dungeons
            if (!forceImmediateNearPlayerIfEmpty)
            {
                if (anchor.Generator != null)
                {
                    Room[] ownRooms = anchor.Generator.GetComponentsInChildren<Room>(true);
                    if (ownRooms == null || ownRooms.Length == 0)
                    {
                        return;
                    }
                }
            }

            ZNetView anchorNv = GetAnchorNetView(anchor);
            bool isAuthority = (anchorNv == null || !anchorNv.IsValid() || anchorNv.IsOwner() || (ZNet.instance != null && ZNet.instance.IsServer()));
            if (!isAuthority) return;

            if (SpawnDungeonWardenInside(anchor, biome))
            {
                if (anchorNv != null && anchorNv.IsValid() && anchorNv.IsOwner() && anchorNv.GetZDO() != null)
                {
                    anchorNv.GetZDO().Set(ZdoDungeonWardenSpawned, true);
                }
            }
        }

        private static bool IsColliderPartOfDungeon(Collider col, DungeonAnchor anchor)
        {
            if (col == null) return false;
            Transform t = col.transform;
            if (anchor.Generator != null && t.IsChildOf(anchor.Generator.transform)) return true;
            if (anchor.ParentLocation != null && t.IsChildOf(anchor.ParentLocation.transform)) return true;
            if (anchor.InteriorRoot != null && t.IsChildOf(anchor.InteriorRoot)) return true;
            return false;
        }

        private static bool IsValidInteriorRoomFloor(Vector3 candidate, DungeonAnchor anchor, out Vector3 floorHitPoint)
        {
            floorHitPoint = Vector3.zero;

            // Cast against ALL layers (`~0`) ignoring Triggers so dungeon room MeshColliders ALWAYS register!
            RaycastHit[] downHits = Physics.RaycastAll(candidate + Vector3.up * 1.15f, Vector3.down, 4.5f, ~0, QueryTriggerInteraction.Ignore);
            if (downHits == null || downHits.Length == 0)
            {
                return false;
            }

            bool foundFloor = false;
            RaycastHit bestDown = default(RaycastHit);
            float closestDist = float.MaxValue;

            for (int i = 0; i < downHits.Length; i++)
            {
                RaycastHit h = downHits[i];
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<Character>() != null) continue;
                if (h.normal.y < 0.45f) continue;

                // Must hit a floor collider belonging to EITHER `anchor.Generator` OR `anchor.ParentLocation`!
                if (!IsColliderPartOfDungeon(h.collider, anchor))
                {
                    continue;
                }

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestDown = h;
                    foundFloor = true;
                }
            }

            if (!foundFloor)
            {
                return false;
            }

            // Raycast UP from the floor to verify a solid dungeon ceiling exists above (never on exterior roof!)
            RaycastHit[] upHits = Physics.RaycastAll(bestDown.point + Vector3.up * 0.4f, Vector3.up, 24f, ~0, QueryTriggerInteraction.Ignore);
            bool hasCeiling = false;
            float lowestCeilingDist = float.MaxValue;

            if (upHits != null)
            {
                for (int i = 0; i < upHits.Length; i++)
                {
                    RaycastHit uh = upHits[i];
                    if (uh.collider == null) continue;
                    if (uh.collider.GetComponentInParent<Character>() != null) continue;
                    if (uh.collider.GetComponentInParent<Container>() != null) continue;
                    if (uh.collider.GetComponentInParent<Pickable>() != null) continue;

                    if (!IsColliderPartOfDungeon(uh.collider, anchor))
                    {
                        continue;
                    }

                    if (uh.distance < lowestCeilingDist)
                    {
                        lowestCeilingDist = uh.distance;
                        hasCeiling = true;
                    }
                }
            }

            if (!hasCeiling || lowestCeilingDist < 1.35f)
            {
                return false;
            }

            floorHitPoint = bestDown.point + Vector3.up * 0.15f;
            return true;
        }

        /// <summary>
        /// Reads `DungeonGenerator.m_loadedRooms` (Valheim's exact serialized room coordinates array) + instantiated `dg.transform` rooms!
        /// Because `DG_ForestCrypt(Clone)` is a top-level ZNetView SEPARATE from `Location`, checking `anchor.Generator` directly
        /// finds all 25-40 crypt rooms 100% of the time and places the Warden in the furthest chamber (`25m - 55m` away from the entrance)!
        /// </summary>
        public static bool TryFindGuaranteedFloorInsideDungeon(DungeonAnchor anchor, out Vector3 deepestPos, out string chosenPrefabName)
        {
            deepestPos = Vector3.zero;
            chosenPrefabName = null;
            if (!anchor.IsValid) return false;

            Vector3 entrancePos = anchor.EntranceInteriorPos;
            bool isSingleLair = IsSingleBossLairDungeon(anchor);
            float minEntranceDistSq = isSingleLair ? 2.5f * 2.5f : 12.0f * 12.0f;
            float maxDistSq = -1f;

            // 1) PRIMARY FOR PROCEDURAL DUNGEONS: `anchor.Generator.GetComponentsInChildren<Room>(true)`!
            //    Now that `anchor.Generator` properly resolves the top-level `DG_ForestCrypt(Clone)` in the same Zone,
            //    this directly inspects all 25-40 instantiated Rooms of THIS crypt!
            if (anchor.Generator != null)
            {
                Room[] dgRooms = anchor.Generator.GetComponentsInChildren<Room>(true);
                if (dgRooms != null && dgRooms.Length > 0)
                {
                    for (int i = 0; i < dgRooms.Length; i++)
                    {
                        Room r = dgRooms[i];
                        if (r == null || r.m_entrance || r.m_endCap || r.m_divider || !r.gameObject.activeInHierarchy) continue;

                        float rawDistSq = (r.transform.position - entrancePos).sqrMagnitude;
                        if (rawDistSq < minEntranceDistSq) continue;

                        Vector3[] offsets = new Vector3[]
                        {
                            Vector3.zero,
                            r.transform.forward * 2.0f,
                            -r.transform.forward * 2.0f,
                            r.transform.right * 2.0f,
                            -r.transform.right * 2.0f
                        };
                        for (int k = 0; k < offsets.Length; k++)
                        {
                            Vector3 validFloor;
                            if (IsValidInteriorRoomFloor(r.transform.position + offsets[k], anchor, out validFloor))
                            {
                                float dSq = (validFloor - entrancePos).sqrMagnitude;
                                if (dSq >= minEntranceDistSq && dSq > maxDistSq)
                                {
                                    maxDistSq = dSq;
                                    deepestPos = validFloor;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // 2) World `CreatureSpawner`s standing on a floor collider belonging to `anchor.Generator` or `anchor.ParentLocation`!
            if (deepestPos == Vector3.zero)
            {
                CreatureSpawner[] allSpawners = UnityEngine.Object.FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None);
                if (allSpawners != null)
                {
                    for (int i = 0; i < allSpawners.Length; i++)
                    {
                        CreatureSpawner sp = allSpawners[i];
                        if (sp == null || sp.transform.position.y < 3000f || !sp.gameObject.activeInHierarchy) continue;

                        float dSq = (sp.transform.position - entrancePos).sqrMagnitude;
                        if (dSq < minEntranceDistSq || dSq > 115f * 115f) continue;

                        Vector3 validFloor;
                        if (!IsValidInteriorRoomFloor(sp.transform.position, anchor, out validFloor)) continue;

                        float floorDistSq = (validFloor - entrancePos).sqrMagnitude;
                        if (floorDistSq >= minEntranceDistSq && floorDistSq > maxDistSq)
                        {
                            maxDistSq = floorDistSq;
                            deepestPos = validFloor;
                            if (sp.m_creaturePrefab != null)
                            {
                                chosenPrefabName = sp.m_creaturePrefab.name;
                            }
                        }
                    }
                }
            }

            // 3) Data-driven `DungeonGenerator.m_loadedRooms` array directly from Valheim's serialized ZDO room data!
            //    Even if Unity hasn't finished baking a MeshCollider yet, `m_loadedRooms` already holds the exact world `Vector3 m_position`
            //    of every single Room placed in this dungeon!
            if (deepestPos == Vector3.zero && anchor.Generator != null && s_dgLoadedRoomsField != null)
            {
                Array loadedArr = s_dgLoadedRoomsField.GetValue(anchor.Generator) as Array;
                if (loadedArr != null && loadedArr.Length > 0)
                {
                    for (int i = 0; i < loadedArr.Length; i++)
                    {
                        object rpd = loadedArr.GetValue(i);
                        if (rpd == null) continue;

                        if (s_rpdPosField == null)
                        {
                            Type rpdType = rpd.GetType();
                            s_rpdRoomDataField = rpdType.GetField("m_roomData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            s_rpdPosField = rpdType.GetField("m_position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            s_rpdRotField = rpdType.GetField("m_rotation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        }
                        if (s_rpdPosField == null) break;

                        // Skip endcaps / dividers / entrance if RoomData is loaded
                        if (s_rpdRoomDataField != null)
                        {
                            object rd = s_rpdRoomDataField.GetValue(rpd);
                            if (rd != null)
                            {
                                if (s_roomDataRoomInPrefabProp == null)
                                {
                                    s_roomDataRoomInPrefabProp = rd.GetType().GetProperty("RoomInPrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                }
                                if (s_roomDataRoomInPrefabProp != null)
                                {
                                    Room prefabRoom = s_roomDataRoomInPrefabProp.GetValue(rd, null) as Room;
                                    if (prefabRoom != null && (prefabRoom.m_endCap || prefabRoom.m_divider || prefabRoom.m_entrance))
                                    {
                                        continue;
                                    }
                                }
                            }
                        }

                        Vector3 rPos = (Vector3)s_rpdPosField.GetValue(rpd);
                        float dSq = (rPos - entrancePos).sqrMagnitude;
                        if (dSq >= minEntranceDistSq && dSq > maxDistSq)
                        {
                            Vector3 snapped;
                            if (IsValidInteriorRoomFloor(rPos, anchor, out snapped))
                            {
                                maxDistSq = dSq;
                                deepestPos = snapped;
                            }
                        }
                    }
                }
            }

            // 4) Treasure Chests (`Container`) standing inside THIS dungeon (e.g. static TrollCave02 back chest!)
            if (deepestPos == Vector3.zero)
            {
                Container[] chests = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None);
                if (chests != null)
                {
                    for (int i = 0; i < chests.Length; i++)
                    {
                        Container box = chests[i];
                        if (box == null || box.transform.position.y < 3000f) continue;
                        if ((box.transform.position - entrancePos).sqrMagnitude > 115f * 115f) continue;

                        Vector3 toEntrance = entrancePos - box.transform.position;
                        toEntrance.y = 0f;
                        Vector3 offset = toEntrance.sqrMagnitude > 0.1f ? toEntrance.normalized * 2.2f : Vector3.zero;

                        Vector3 validFloor;
                        if (IsValidInteriorRoomFloor(box.transform.position + offset, anchor, out validFloor) ||
                            IsValidInteriorRoomFloor(box.transform.position, anchor, out validFloor))
                        {
                            float dSq = (validFloor - entrancePos).sqrMagnitude;
                            if (dSq >= minEntranceDistSq && dSq > maxDistSq)
                            {
                                maxDistSq = dSq;
                                deepestPos = validFloor;
                            }
                        }
                    }
                }
            }

            // 5) Fallback ONLY for tiny single-room caves (NEVER for procedural multi-room dungeons with `anchor.Generator != null`!)
            if (deepestPos == Vector3.zero && anchor.Generator == null && Player.m_localPlayer != null && Player.m_localPlayer.transform.position.y > 3000f)
            {
                Vector3 pPos = Player.m_localPlayer.transform.position;
                Vector3 validFloor;
                if (IsValidInteriorRoomFloor(pPos + Player.m_localPlayer.transform.forward * 8f, anchor, out validFloor))
                {
                    deepestPos = validFloor;
                }
                else
                {
                    deepestPos = pPos + Player.m_localPlayer.transform.forward * 5f + Vector3.up * 0.15f;
                }
            }

            return deepestPos != Vector3.zero;
        }

        // Overload kept for ChampionSystem ambush helper
        public static bool TryFindDeepestFloorInThisDungeon(DungeonGenerator dg, out Vector3 deepestPos, out string chosenPrefabName)
        {
            deepestPos = Vector3.zero;
            chosenPrefabName = null;
            if (dg == null) return false;
            DungeonAnchor anchor;
            if (TryResolveDungeonAt(dg.transform.position, out anchor))
            {
                return TryFindGuaranteedFloorInsideDungeon(anchor, out deepestPos, out chosenPrefabName);
            }
            return false;
        }

        private static bool SpawnDungeonWardenInside(DungeonAnchor anchor, Heightmap.Biome biome)
        {
            if (ZNetScene.instance == null) return false;

            Vector3 spawnPos;
            string samplePrefab;
            if (!TryFindGuaranteedFloorInsideDungeon(anchor, out spawnPos, out samplePrefab))
            {
                return false;
            }

            string wardenPrefabName = PickEliteWardenPrefabForAnchor(anchor, biome, samplePrefab);
            GameObject prefab = ZNetScene.instance.GetPrefab(wardenPrefabName);
            if (prefab == null && !string.IsNullOrEmpty(samplePrefab))
            {
                prefab = ZNetScene.instance.GetPrefab(samplePrefab);
            }
            if (prefab == null)
            {
                prefab = ZNetScene.instance.GetPrefab("Skeleton");
            }
            if (prefab == null) return false;

            GameObject wardenObj = UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            ChampionSystem.ImbueAsChampion(wardenObj, biome, isDungeonWarden: true, wardenDungeonId: anchor.UniqueId);
            s_cachedLiveWarden = wardenObj.GetComponent<Character>();

            MonsterAI ai = wardenObj.GetComponent<MonsterAI>();
            if (ai != null)
            {
                // Guard his deepest chamber naturally until players come down the corridor!
                ai.SetPatrolPoint();
            }

            if (CoarstalsEnemyOverhaulPlugin.ModLogger != null)
            {
                float distFromEntrance = Vector3.Distance(spawnPos, anchor.EntranceInteriorPos);
                CoarstalsEnemyOverhaulPlugin.ModLogger.LogInfo(string.Format(
                    "[EnemyOverhaul] Spawned Cursed Dungeon Warden ({0}) strictly inside {1} at {2} ({3:F1}m from entrance, DG={4})!",
                    prefab.name,
                    anchor.UniqueId,
                    spawnPos,
                    distFromEntrance,
                    anchor.Generator != null ? anchor.Generator.name : "none"));
            }

            return true;
        }

        private static string PickEliteWardenPrefabForAnchor(DungeonAnchor anchor, Heightmap.Biome biome, string spawnerSample)
        {
            string dgName = anchor.Generator != null ? anchor.Generator.gameObject.name.ToLowerInvariant() : "";
            string locName = anchor.ParentLocation != null ? anchor.ParentLocation.gameObject.name.ToLowerInvariant() : "";
            string rootName = anchor.InteriorRoot != null ? anchor.InteriorRoot.name.ToLowerInvariant() : "";

            // Only actual Black Forest Troll Caves (`TrollCave02`) get a Troll Warden — NEVER `DG_Cave` (Mountain Frost Caves)!
            if (dgName.Contains("trollcave") || locName.Contains("trollcave") || rootName.Contains("trollcave"))
            {
                return "Troll";
            }

            switch (biome)
            {
                case Heightmap.Biome.BlackForest:
                    return "Skeleton_Poison";
                case Heightmap.Biome.Swamp:
                    return "Draugr_Elite";
                case Heightmap.Biome.Mountain:
                    return (ZNetScene.instance != null && ZNetScene.instance.GetPrefab("Fenring_Cultist") != null)
                        ? "Fenring_Cultist"
                        : "Fenring";
                case Heightmap.Biome.Mistlands:
                    return "SeekerBrute";
                case Heightmap.Biome.AshLands:
                    return "Morgen";
                default:
                    return !string.IsNullOrEmpty(spawnerSample) ? spawnerSample : "Skeleton";
            }
        }

        private static Heightmap.Biome GuessBiomeFromAnchor(DungeonAnchor anchor)
        {
            string n = "";
            if (anchor.Generator != null) n += anchor.Generator.gameObject.name.ToLowerInvariant() + " ";
            if (anchor.ParentLocation != null) n += anchor.ParentLocation.gameObject.name.ToLowerInvariant() + " ";
            if (anchor.InteriorRoot != null) n += anchor.InteriorRoot.name.ToLowerInvariant();

            if (n.Contains("sunkencrypt") || n.Contains("swamp")) return Heightmap.Biome.Swamp;
            if (n.Contains("mountaincave") || n.Contains("hildir_cave") || n.Contains("frost")) return Heightmap.Biome.Mountain;
            if (n.Contains("dvergr") || n.Contains("mist") || n.Contains("infested")) return Heightmap.Biome.Mistlands;
            if (n.Contains("ash") || n.Contains("morgen") || n.Contains("charred") || n.Contains("tomb")) return Heightmap.Biome.AshLands;
            if (n.Contains("forestcrypt") || n.Contains("hildir_crypt") || n.Contains("trollcave")) return Heightmap.Biome.BlackForest;

            // `DG_Cave` in Valheim is the procedural generator used by Mountain Frost Caves (`MountainCave02` / `Hildir_cave`)!
            if (n.Contains("dg_cave"))
            {
                if (anchor.ParentLocation != null && anchor.ParentLocation.m_biome != Heightmap.Biome.None)
                {
                    return anchor.ParentLocation.m_biome;
                }
                if (WorldGenerator.instance != null)
                {
                    Heightmap.Biome wb = WorldGenerator.instance.GetBiome(new Vector3(anchor.EntranceInteriorPos.x, 0f, anchor.EntranceInteriorPos.z));
                    if (wb != Heightmap.Biome.None) return wb;
                }
                return Heightmap.Biome.Mountain;
            }

            if (anchor.ParentLocation != null && anchor.ParentLocation.m_biome != Heightmap.Biome.None)
            {
                return anchor.ParentLocation.m_biome;
            }
            if (WorldGenerator.instance != null)
            {
                Heightmap.Biome b = WorldGenerator.instance.GetBiome(new Vector3(anchor.EntranceInteriorPos.x, 0f, anchor.EntranceInteriorPos.z));
                if (b != Heightmap.Biome.None) return b;
            }
            return Heightmap.Biome.BlackForest;
        }

        public static bool IsCursedDungeonCreature(Character ch)
        {
            if (ch == null || ch.IsPlayer()) return false;
            int id = ch.GetInstanceID();
            bool cached;
            if (s_cursedMobFlagCache.TryGetValue(id, out cached))
            {
                return cached;
            }
            if (s_cursedMobFlagCache.Count > 256) s_cursedMobFlagCache.Clear();

            ZNetView nv = ch.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null) return false;
            bool isCursed = nv.GetZDO().GetBool(ZdoCursedMobApplied, false);
            s_cursedMobFlagCache[id] = isCursed;
            return isCursed;
        }

        public static void NotifyDungeonWardenSlain(Character warden)
        {
            if (warden == null) return;
            ZNetView nv = warden.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || nv.GetZDO() == null) return;
            ZDO zdo = nv.GetZDO();
            if (!zdo.GetBool(ChampionSystem.ZdoIsDungeonWarden, false)) return;

            s_cachedLiveWarden = null;
            s_hoverCachedUntil = 0f;

            string dungeonId = zdo.GetString(ChampionSystem.ZdoWardenDungeonId, "");
            Vector3 deathPos = warden.transform.position;

            if (!string.IsNullOrEmpty(dungeonId) && ZoneSystem.instance != null)
            {
                ZoneSystem.instance.SetGlobalKey(GlobalKeyCleansedPrefix + dungeonId);
            }

            DungeonAnchor anchor;
            if (TryResolveDungeonAt(deathPos, out anchor))
            {
                ZNetView dgNv = GetAnchorNetView(anchor);
                if (dgNv != null && dgNv.IsValid() && dgNv.GetZDO() != null)
                {
                    dgNv.ClaimOwnership();
                    dgNv.GetZDO().Set(ZdoDungeonState, StateCursedCleansed);
                }
            }

            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_DungeonCleansed, dungeonId, deathPos);
            }
            else
            {
                OnRPC_DungeonCleansed(0L, dungeonId, deathPos);
            }
        }

        private static void OnRPC_DungeonCleansed(long sender, string dungeonId, Vector3 deathPos)
        {
            s_hoverCachedUntil = 0f;
            s_cachedLiveWarden = null;
            DungeonAnchor anchor;
            if (TryResolveDungeonAt(deathPos, out anchor))
            {
                ZNetView dgNv = GetAnchorNetView(anchor);
                if (dgNv != null && dgNv.IsValid() && dgNv.IsOwner() && dgNv.GetZDO() != null)
                {
                    dgNv.GetZDO().Set(ZdoDungeonState, StateCursedCleansed);
                }
            }

            Player local = Player.m_localPlayer;
            if (local != null && local.transform.position.y > 3000f)
            {
                DungeonAnchor localAnchor;
                bool same = (TryResolveDungeonAt(local.transform.position, out localAnchor) && localAnchor.UniqueId == dungeonId) ||
                            (local.transform.position - deathPos).sqrMagnitude <= 115f * 115f;
                if (same)
                {
                    if (MessageHud.instance != null)
                    {
                        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, EnemyLocalization.Loc("$ceo_cursed_cleansed_banner"), 0, null);
                    }
                    PlayOminousCurseAudio(local.transform.position, cleansed: true);
                    SetLocalCurseFogActive(false, Heightmap.Biome.BlackForest);
                }
            }
        }

        // ====================================================================
        // Admin Testing Helpers
        // ====================================================================
        public static void ForceCurseNearestDungeonForTesting(Vector3 playerPos, out string statusMsg)
        {
            DungeonAnchor anchor;
            if (!TryResolveDungeonAt(playerPos, out anchor))
            {
                statusMsg = "Enter a dungeon first (y > 3000m) to force-curse it!";
                return;
            }

            if (ZoneSystem.instance != null)
            {
                ZoneSystem.instance.RemoveGlobalKey(GlobalKeyCleansedPrefix + anchor.UniqueId);
            }

            ZNetView nv = GetAnchorNetView(anchor);
            if (nv != null && nv.IsValid() && nv.GetZDO() != null)
            {
                nv.ClaimOwnership();
                nv.GetZDO().Set(ZdoDungeonState, StateCursedActive);
                nv.GetZDO().Set(ZdoDungeonRuleVersion, CurrentCurseRuleVersion);
                nv.GetZDO().Set(ZdoDungeonAdminForced, true);
                nv.GetZDO().Set(ZdoDungeonWardenSpawned, false);
                nv.GetZDO().Set(ZdoDungeonBiome, (int)GuessBiomeFromAnchor(anchor));
            }

            s_currentAnnouncedDungeonId = string.Empty;
            s_hoverCachedUntil = 0f;
            s_cachedLiveWarden = null;
            s_processedDungeonMobIds.Clear();
            EnsureWardenAndBuffedMobsInDungeon(anchor, forceImmediateNearPlayerIfEmpty: true);
            statusMsg = "Current dungeon (" + anchor.UniqueId + ") is now CURSED & SEALED! Warden Champion spawned inside!";
        }

        public static void ForceCleanseNearestDungeonForAdmin(Vector3 playerPos, out string statusMsg)
        {
            DungeonAnchor anchor;
            if (!TryResolveDungeonAt(playerPos, out anchor))
            {
                statusMsg = "You are not inside a dungeon (y < 3000m).";
                return;
            }

            if (ZoneSystem.instance != null)
            {
                ZoneSystem.instance.SetGlobalKey(GlobalKeyCleansedPrefix + anchor.UniqueId);
            }

            ZNetView nv = GetAnchorNetView(anchor);
            if (nv != null && nv.IsValid() && nv.GetZDO() != null)
            {
                nv.ClaimOwnership();
                nv.GetZDO().Set(ZdoDungeonAdminForced, false);
                nv.GetZDO().Set(ZdoDungeonRuleVersion, CurrentCurseRuleVersion);
                nv.GetZDO().Set(ZdoDungeonState, StateCursedCleansed);
            }

            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_DungeonCleansed, anchor.UniqueId, playerPos);
            }
            else
            {
                OnRPC_DungeonCleansed(0L, anchor.UniqueId, playerPos);
            }

            statusMsg = "Unsealed & cleansed current dungeon (" + anchor.UniqueId + ")!";
        }

        private static readonly List<Minimap.PinData> s_adminRevealedPins = new List<Minimap.PinData>();

        private static bool IsEnterableDungeonLocationPrefab(string prefabName)
        {
            return GetCanonicalDungeonTypeGroup(prefabName) != null;
        }

        private static string GetReadableDungeonShortLabel(string prefabName)
        {
            string low = prefabName != null ? prefabName.ToLowerInvariant() : "";
            bool rus = EnemyLocalization.IsRussian();

            if (low.StartsWith("trollcave")) return rus ? "Прокл. Пещера тролля" : "Cursed Troll Cave";
            if (low.StartsWith("crypt") || low.StartsWith("hildir_crypt")) return rus ? "Прокл. Курган" : "Cursed Burial Chamber";
            if (low.StartsWith("sunkencrypt")) return rus ? "Прокл. Склеп" : "Cursed Sunken Crypt";
            if (low.StartsWith("mountaincave") || low.StartsWith("hildir_cave")) return rus ? "Прокл. Морозная пещера" : "Cursed Frost Cave";
            if (low.Contains("dvergrtown")) return rus ? "Прокл. Заражённая шахта" : "Cursed Infested Mine";
            if (low.StartsWith("morgencave")) return rus ? "Прокл. Пещера Моргена" : "Cursed Morgen Cave";
            if (low.StartsWith("charredtomb")) return rus ? "Прокл. Гробница" : "Cursed Tomb";
            return rus ? "Проклятый данж" : "Cursed Dungeon";
        }

        public static int ToggleOrShowAllCursedDungeonsOnMap(bool clearOnly, out string statusMsg)
        {
            if (Minimap.instance == null || ZoneSystem.instance == null)
            {
                statusMsg = "Minimap or ZoneSystem is not ready.";
                return 0;
            }

            // Remove previously placed admin cursed dungeon pins first
            int removedCount = 0;
            for (int i = s_adminRevealedPins.Count - 1; i >= 0; i--)
            {
                Minimap.PinData pin = s_adminRevealedPins[i];
                if (pin != null)
                {
                    Minimap.instance.RemovePin(pin);
                    removedCount++;
                }
            }
            s_adminRevealedPins.Clear();

            if (clearOnly)
            {
                statusMsg = string.Format("Cleared {0} Cursed Dungeon map pins.", removedCount);
                return 0;
            }

            Dictionary<Vector2s, ZoneSystem.LocationInstance> instances = ZoneSystem.instance.m_locationInstances;
            if (instances == null || instances.Count == 0)
            {
                statusMsg = "No world locations generated yet (run on host/singleplayer).";
                return 0;
            }

            EnsureWorldPerTypeCurseQuotaBuilt();

            int activePinned = 0;
            int totalEligibleDungeons = 0;
            int cleansedSkipped = 0;
            Vector3 playerPos = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
            float nearestDist = float.MaxValue;
            Vector3 nearestPos = Vector3.zero;
            string nearestName = "";

            foreach (KeyValuePair<Vector2s, ZoneSystem.LocationInstance> kv in instances)
            {
                ZoneSystem.LocationInstance inst = kv.Value;
                if (inst.m_location == null) continue;

                string prefabName = inst.m_location.m_prefabName;
                if (string.IsNullOrEmpty(prefabName) && inst.m_location.m_prefab.IsValid)
                {
                    prefabName = inst.m_location.m_prefab.Name;
                }
                if (!IsEnterableDungeonLocationPrefab(prefabName)) continue;
                totalEligibleDungeons++;

                Vector2s zoneId = kv.Key;
                string uniqueId = string.Format("dg_z_{0}_{1}", zoneId.x, zoneId.y);

                if (!IsDungeonInExactTypeQuota(uniqueId))
                {
                    continue;
                }

                if (IsDungeonCleansedGlobally(uniqueId))
                {
                    cleansedSkipped++;
                    continue;
                }

                string pinLabel = GetReadableDungeonShortLabel(prefabName);
                Minimap.PinData added = Minimap.instance.AddPin(
                    inst.m_position,
                    Minimap.PinType.Icon2,
                    pinLabel,
                    false,
                    false);

                if (added != null)
                {
                    s_adminRevealedPins.Add(added);
                }
                activePinned++;

                float d = Utils.DistanceXZ(playerPos, inst.m_position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestPos = inst.m_position;
                    nearestName = pinLabel;
                }
            }

            if (activePinned > 0)
            {
                statusMsg = string.Format(
                    "Marked {0} active Cursed Dungeons out of {1} total world dungeons (~10% per dungeon type; Cleansed: {2}). Nearest: {3} at ({4:F0}, {5:F0}) — {6:F0}m away!",
                    activePinned, totalEligibleDungeons, cleansedSkipped, nearestName, nearestPos.x, nearestPos.z, nearestDist);
            }
            else
            {
                statusMsg = "0 active Cursed Dungeons found (check CursedDungeonChance in config or run on server host).";
            }
            return activePinned;
        }

        public static string GetCurrentDungeonDebugInfo(Vector3 playerPos)
        {
            DungeonAnchor anchor;
            if (!TryResolveDungeonAt(playerPos, out anchor))
            {
                return "No DungeonAnchor found near " + playerPos;
            }
            int state = GetOrInitDungeonCurseState(anchor);
            Character warden = FindLiveWardenInDungeon(anchor);
            string wardenStr = warden != null
                ? string.Format("{0} at {1} ({2:F1}m away, HP {3:F0}/{4:F0})",
                    warden.name, warden.transform.position, Vector3.Distance(playerPos, warden.transform.position), warden.GetHealth(), warden.GetMaxHealth())
                : "NONE";

            return string.Format("DungeonId={0}, Root={1}, CurseState={2}, LiveWarden={3}",
                anchor.UniqueId, anchor.InteriorRoot.name, state, wardenStr);
        }

        private static void PlayOminousCurseAudio(Vector3 pos, bool cleansed)
        {
            if (ZNetScene.instance == null) return;
            string sfxName = cleansed ? "sfx_offering" : "sfx_wraith_hit";
            GameObject sfx = ZNetScene.instance.GetPrefab(sfxName);
            if (sfx == null) sfx = ZNetScene.instance.GetPrefab("sfx_thunder");
            if (sfx != null)
            {
                UnityEngine.Object.Instantiate(sfx, pos, Quaternion.identity);
            }
        }

        // ====================================================================
        // Dense Mystical Cursed Fog inside Sealed Dungeons
        // ====================================================================
        private static void SetLocalCurseFogActive(bool active, Heightmap.Biome biome)
        {
            if (!active)
            {
                if (!s_lastFogActiveState) return;
                s_lastFogActiveState = false;
                s_lastFogBiome = Heightmap.Biome.None;

                if (s_localCurseFogPs != null && s_localCurseFogPs.isPlaying)
                {
                    s_localCurseFogPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                if (s_localCurseAmbientLight != null)
                {
                    s_localCurseAmbientLight.enabled = false;
                }
                return;
            }

            if (s_lastFogActiveState && s_lastFogBiome == biome && s_localCurseFogRoot != null && s_localCurseFogRoot.activeSelf)
            {
                return;
            }
            s_lastFogActiveState = true;
            s_lastFogBiome = biome;

            if (s_localCurseFogRoot == null)
            {
                BuildLocalCurseFogObject();
            }

            Color fogTint = GetCurseFogColor(biome);
            if (s_localCurseAmbientLight != null)
            {
                s_localCurseAmbientLight.enabled = true;
                s_localCurseAmbientLight.color = Color.Lerp(fogTint, new Color(0.75f, 0.22f, 0.95f, 1f), 0.5f);
            }

            if (s_localCurseFogPs != null)
            {
                ParticleSystem.MainModule main = s_localCurseFogPs.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(fogTint.r, fogTint.g, fogTint.b, 0.22f),
                    new Color(fogTint.r * 0.7f, fogTint.g * 0.5f, fogTint.b, 0.34f));

                if (!s_localCurseFogRoot.activeSelf)
                {
                    s_localCurseFogRoot.SetActive(true);
                }
                if (!s_localCurseFogPs.isPlaying)
                {
                    s_localCurseFogPs.Play(true);
                }
            }
        }

        private static Color GetCurseFogColor(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Swamp: return new Color(0.35f, 0.78f, 0.42f, 1f);
                case Heightmap.Biome.Mountain: return new Color(0.42f, 0.55f, 0.96f, 1f);
                case Heightmap.Biome.Mistlands: return new Color(0.72f, 0.25f, 0.96f, 1f);
                case Heightmap.Biome.AshLands: return new Color(0.92f, 0.25f, 0.32f, 1f);
                default: return new Color(0.68f, 0.22f, 0.88f, 1f);
            }
        }

        private static void BuildLocalCurseFogObject()
        {
            s_localCurseFogRoot = new GameObject("CEO_CursedDungeonMysticFog");
            UnityEngine.Object.DontDestroyOnLoad(s_localCurseFogRoot);

            s_localCurseAmbientLight = s_localCurseFogRoot.AddComponent<Light>();
            s_localCurseAmbientLight.type = LightType.Point;
            s_localCurseAmbientLight.range = 22f;
            s_localCurseAmbientLight.intensity = 1.15f;
            s_localCurseAmbientLight.shadows = LightShadows.None;

            s_localCurseFogPs = s_localCurseFogRoot.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = s_localCurseFogPs.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(4.2f, 8.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = s_localCurseFogPs.emission;
            emission.rateOverTime = 26f;

            ParticleSystem.ShapeModule shape = s_localCurseFogPs.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 3.6f, 22f);

            ParticleSystem.ColorOverLifetimeModule colLife = s_localCurseFogPs.colorOverLifetime;
            colLife.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0.85f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colLife.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystemRenderer psr = s_localCurseFogRoot.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = GetSharedSoftParticleMaterial();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            GameObject wispsObj = new GameObject("CEO_CurseWisps");
            wispsObj.transform.SetParent(s_localCurseFogRoot.transform, false);
            ParticleSystem wispPs = wispsObj.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule wm = wispPs.main;
            wm.loop = true;
            wm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            wm.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            wm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            wm.startColor = new Color(0.92f, 0.40f, 1.00f, 0.85f);
            wm.simulationSpace = ParticleSystemSimulationSpace.World;
            wm.gravityModifier = -0.06f;
            wm.maxParticles = 60;

            ParticleSystem.EmissionModule we = wispPs.emission;
            we.rateOverTime = 12f;

            ParticleSystem.ShapeModule ws = wispPs.shape;
            ws.shapeType = ParticleSystemShapeType.Box;
            ws.scale = new Vector3(18f, 3.5f, 18f);

            ParticleSystemRenderer wpsr = wispsObj.GetComponent<ParticleSystemRenderer>();
            if (wpsr != null)
            {
                wpsr.sharedMaterial = GetSharedSoftParticleMaterial();
            }
        }

        private static void DestroyLocalCurseFog()
        {
            if (s_localCurseFogRoot != null)
            {
                UnityEngine.Object.Destroy(s_localCurseFogRoot);
                s_localCurseFogRoot = null;
                s_localCurseFogPs = null;
                s_localCurseAmbientLight = null;
            }
        }

        public static Material GetSharedSoftParticleMaterial()
        {
            if (s_softParticleMat != null) return s_softParticleMat;

            if (s_softSmokeTex == null)
            {
                s_softSmokeTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                s_softSmokeTex.name = "CEO_SoftMistPuff";
                s_softSmokeTex.wrapMode = TextureWrapMode.Clamp;
                Vector2 center = new Vector2(31.5f, 31.5f);
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center) / 31.5f;
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        s_softSmokeTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                s_softSmokeTex.Apply();
            }

            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");

            s_softParticleMat = new Material(sh);
            s_softParticleMat.mainTexture = s_softSmokeTex;

            if (s_softParticleMat.HasProperty("_Mode")) s_softParticleMat.SetFloat("_Mode", 2f);
            if (s_softParticleMat.HasProperty("_SrcBlend")) s_softParticleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (s_softParticleMat.HasProperty("_DstBlend")) s_softParticleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (s_softParticleMat.HasProperty("_ZWrite")) s_softParticleMat.SetInt("_ZWrite", 0);
            s_softParticleMat.renderQueue = 3000;
            s_softParticleMat.EnableKeyword("_ALPHABLEND_ON");

            return s_softParticleMat;
        }
    }
}
