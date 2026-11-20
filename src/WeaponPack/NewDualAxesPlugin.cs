using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using FinalMod;

namespace NewDualAxesMod
{
    public class NewDualAxesPlugin
    {
        public const string PluginGUID = "com.coarstal.weaponpack";
        public const string PluginName = "Coarstal's Weapon Pack";
        public const string PluginVersion = "1.0.2";

        public const string SwampPrefabName = "AxeDualSwamp";
        public const string PlainsPrefabName = "AxeDualPlains";

        internal static ManualLogSource ModLogger;

        // Swamp ("Bogroot Twin Axes") Configs
        public static ConfigEntry<float> SwampSlashDamage;
        public static ConfigEntry<float> SwampSlashPerLevel;
        public static ConfigEntry<float> SwampPoisonDamage;
        public static ConfigEntry<float> SwampPoisonPerLevel;
        public static ConfigEntry<float> SwampChopDamage;
        public static ConfigEntry<float> SwampChopPerLevel;
        public static ConfigEntry<int> SwampMinStationLevel;
        public static ConfigEntry<string> SwampRecipeConfig;
        public static ConfigEntry<float> SwampVisualScale;

        // Plains ("Berserker's Fang Axes") Configs
        public static ConfigEntry<float> PlainsSlashDamage;
        public static ConfigEntry<float> PlainsSlashPerLevel;
        public static ConfigEntry<float> PlainsChopDamage;
        public static ConfigEntry<float> PlainsChopPerLevel;
        public static ConfigEntry<int> PlainsMinStationLevel;
        public static ConfigEntry<string> PlainsRecipeConfig;
        public static ConfigEntry<float> PlainsVisualScale;

        // Fine-tune grip offset / rotation in bone space if user ever wants manual adjustment (0 = exact vanilla auto-align!)
        public static ConfigEntry<Vector3> GripPositionOffset;
        public static ConfigEntry<Vector3> GripRotationOffset;

        private static readonly NewDualAxesPlugin s_instance = new NewDualAxesPlugin();
        private static bool s_initialized;
        private bool m_itemsRegistered;

        public static bool IsActive
        {
            get { return FinalModulesConfig.EnableWeaponPack == null || FinalModulesConfig.EnableWeaponPack.Value; }
        }

        private static Mesh s_swampRightMesh;
        private static Mesh s_swampLeftMesh;
        private static Material s_swampRightMat;
        private static Material s_swampLeftMat;
        private static Sprite s_swampIcon;

        private static Mesh s_plainsRightMesh;
        private static Mesh s_plainsLeftMesh;
        private static Material s_plainsRightMat;
        private static Material s_plainsLeftMat;
        private static Sprite s_plainsIcon;

        // Cached exact grip poses extracted directly from vanilla AxeBerzerkr / AxeEarly via SkinnedMeshRenderer.BakeMesh!
        internal static bool s_cachedVanillaPoseValid;
        internal static int s_cachedRightBoneIdx = -1;
        internal static int s_cachedLeftBoneIdx = -1;
        internal static Quaternion s_cachedRightRotInBone = Quaternion.identity;
        internal static Quaternion s_cachedLeftRotInBone = Quaternion.identity;
        internal static Vector3 s_cachedRightGripInBone = Vector3.zero;
        internal static Vector3 s_cachedLeftGripInBone = Vector3.zero;
        internal static float s_cachedRightPommelRatio = 0.33f;
        internal static float s_cachedLeftPommelRatio = 0.33f;

        public static void BindConfigs(ConfigFile config)
        {
            ConfigurationManagerAttributes adminAttr = new ConfigurationManagerAttributes { IsAdminOnly = true };
            ConfigurationManagerAttributes clientAttr = new ConfigurationManagerAttributes { IsAdminOnly = false };

            const string secSwamp = "03. Weapon Pack - 1. Swamp Dual Axes";
            const string secPlains = "03. Weapon Pack - 2. Plains Dual Axes";
            const string secGrip = "03. Weapon Pack - 3. Grip Fine-Tuning";

            // Swamp Dual Axes: 65 Slash (+6/lvl) + 15 Poison (+5/lvl)
            SwampSlashDamage = config.Bind(secSwamp, "SlashDamage", 65f, new ConfigDescription("Base Slash damage at quality level 1.", null, adminAttr));
            SwampSlashPerLevel = config.Bind(secSwamp, "SlashDamagePerLevel", 6f, new ConfigDescription("Additional Slash damage per upgrade level.", null, adminAttr));
            SwampPoisonDamage = config.Bind(secSwamp, "PoisonDamage", 15f, new ConfigDescription("Base Poison damage at quality level 1.", null, adminAttr));
            SwampPoisonPerLevel = config.Bind(secSwamp, "PoisonDamagePerLevel", 5f, new ConfigDescription("Additional Poison damage per upgrade level.", null, adminAttr));
            SwampChopDamage = config.Bind(secSwamp, "ChopDamage", 50f, new ConfigDescription("Base tree Chop damage at quality level 1.", null, adminAttr));
            SwampChopPerLevel = config.Bind(secSwamp, "ChopDamagePerLevel", 3f, new ConfigDescription("Additional Chop damage per upgrade level.", null, adminAttr));
            SwampMinStationLevel = config.Bind(secSwamp, "MinForgeLevel", 2, new ConfigDescription("Minimum Forge level required to craft.", null, adminAttr));
            SwampRecipeConfig = config.Bind(
                secSwamp,
                "Recipe",
                "Iron:25:12,ElderBark:12:6,Root:2:1,Guck:6:3",
                new ConfigDescription("Crafting requirements in format Prefab:Amount:AmountPerLevel separated by commas.", null, adminAttr)
            );
            SwampVisualScale = config.Bind(secSwamp, "VisualScale", 1.0f, new ConfigDescription("Visual 3D model scale multiplier (1.0 = authentic ~0.76m axe length).", null, clientAttr));

            // Plains Dual Axes: 115 Slash (+6/lvl), pure physical Slash
            PlainsSlashDamage = config.Bind(secPlains, "SlashDamage", 115f, new ConfigDescription("Base Slash damage at quality level 1.", null, adminAttr));
            PlainsSlashPerLevel = config.Bind(secPlains, "SlashDamagePerLevel", 6f, new ConfigDescription("Additional Slash damage per upgrade level.", null, adminAttr));
            PlainsChopDamage = config.Bind(secPlains, "ChopDamage", 70f, new ConfigDescription("Base tree Chop damage at quality level 1.", null, adminAttr));
            PlainsChopPerLevel = config.Bind(secPlains, "ChopDamagePerLevel", 3f, new ConfigDescription("Additional Chop damage per upgrade level.", null, adminAttr));
            PlainsMinStationLevel = config.Bind(secPlains, "MinForgeLevel", 4, new ConfigDescription("Minimum Forge level required to craft.", null, adminAttr));
            PlainsRecipeConfig = config.Bind(
                secPlains,
                "Recipe",
                "BlackMetal:30:15,FineWood:10:5,LinenThread:15:8,LoxPelt:4:2",
                new ConfigDescription("Crafting requirements in format Prefab:Amount:AmountPerLevel separated by commas.", null, adminAttr)
            );
            PlainsVisualScale = config.Bind(secPlains, "VisualScale", 1.0f, new ConfigDescription("Visual 3D model scale multiplier (1.0 = authentic ~0.87m axe length).", null, clientAttr));

            GripPositionOffset = config.Bind(secGrip, "GripPositionOffsetMeters", Vector3.zero, new ConfigDescription("Optional fine-tuning offset in meters (X=side, Y=along handle, Z=blade forward) relative to auto-matched vanilla grip.", null, clientAttr));
            GripRotationOffset = config.Bind(secGrip, "GripRotationOffsetDegrees", Vector3.zero, new ConfigDescription("Optional fine-tuning Euler rotation offset in degrees relative to auto-matched vanilla grip.", null, clientAttr));

            MagicStavesModule.BindConfigs(config);
            EitrFoodsModule.BindConfigs(config);
        }

        public static void Init(ManualLogSource logger)
        {
            if (s_initialized) return;
            ModLogger = logger;
            s_instance.RegisterLocalizations();
            MagicStavesModule.RegisterLocalizations();
            EitrFoodsModule.RegisterLocalizations();

            PrefabManager.OnVanillaPrefabsAvailable += s_instance.RegisterCustomDualAxes;
            PrefabManager.OnVanillaPrefabsAvailable += MagicStavesModule.RegisterAllStavesAndParts;
            PrefabManager.OnVanillaPrefabsAvailable += EitrFoodsModule.RegisterAllEitrFoods;
            ItemManager.OnItemsRegistered += s_instance.OnJotunnItemsRegistered;
            ItemManager.OnItemsRegisteredFejd += s_instance.OnJotunnItemsRegistered;
            PieceManager.OnPiecesRegistered += s_instance.OnJotunnItemsRegistered;

            s_initialized = true;
            if (ModLogger != null) ModLogger.LogInfo("[PathOfTheFallen] WeaponPack module initialized.");
        }

        public static void Shutdown()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= s_instance.RegisterCustomDualAxes;
            PrefabManager.OnVanillaPrefabsAvailable -= MagicStavesModule.RegisterAllStavesAndParts;
            PrefabManager.OnVanillaPrefabsAvailable -= EitrFoodsModule.RegisterAllEitrFoods;
            ItemManager.OnItemsRegistered -= s_instance.OnJotunnItemsRegistered;
            ItemManager.OnItemsRegisteredFejd -= s_instance.OnJotunnItemsRegistered;
            PieceManager.OnPiecesRegistered -= s_instance.OnJotunnItemsRegistered;
            s_initialized = false;
        }

        private void OnJotunnItemsRegistered()
        {
            if (!IsActive) return;
            if (ObjectDB.instance != null)
            {
                EquipmentDropPrefabRepairHelper.EnsureCustomPrefabsReadyInObjectDB(ObjectDB.instance);
            }
        }

        private void RegisterLocalizations()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            string eng = "English";
            loc.AddTranslation(ref eng, new Dictionary<string, string>
            {
                { "item_axedual_swamp", "Bogroot Twin Axes" },
                { "item_axedual_swamp_desc", "An asymmetric pair of Swamp war-axes: a rune-etched forged iron blade for the off-hand and a cursed Draugr axe bound in living bog roots dripping with toxic guck." },
                { "item_axedual_plains", "Berserker's Fang Axes" },
                { "item_axedual_plains_desc", "Brutal Fuling Berserker twin war-axes forged from dark emerald Black Metal, armed with armor-splitting back-spikes and studded with predator fangs." }
            });

            string rus = "Russian";
            loc.AddTranslation(ref rus, new Dictionary<string, string>
            {
                { "item_axedual_swamp", "Корнерубы Топей" },
                { "item_axedual_swamp_desc", "Асимметричная пара болотных топоров: кованая железная секира со скандинавскими рунами для левой руки и проклятое лезвие драугров на живом корневище, сочащееся ядовитой слизью." },
                { "item_axedual_plains", "Клыкастые Топоры Берсерка" },
                { "item_axedual_plains_desc", "Свирепые парные топоры берсерка фулингов из тёмно-изумрудного чёрного металла с пробивными шипами-клевцами на обухе и ошейником из хищных клыков." }
            });
        }

        private void RegisterCustomDualAxes()
        {
            if (!IsActive) return;
            try
            {
                if (m_itemsRegistered && ItemManager.Instance.GetItem(SwampPrefabName) != null)
                {
                    return;
                }

                Shader valheimShader = FindValheimStandardShader();
                EnsureAssetsLoaded(valheimShader);

                string donorName = "AxeBerzerkr";
                if (PrefabManager.Instance.GetPrefab(donorName) == null)
                {
                    donorName = "AxeEarly";
                }

                GameObject donorPrefab = PrefabManager.Instance.GetPrefab(donorName);
                if (donorPrefab == null)
                {
                    if (ModLogger != null) ModLogger.LogError("Could not find vanilla dual axe donor prefab (AxeBerzerkr / AxeEarly)!");
                    return;
                }

                // Extract exact vanilla dual-axe grip pose directly from donor SkinnedMeshRenderer via BakeMesh!
                // Unlike Mesh.vertices or AcquireReadOnlyMeshData (which throw when isReadable == false),
                // SkinnedMeshRenderer.BakeMesh(bakedMesh, true) ALWAYS writes a CPU-readable Mesh with exact vertices & triangles!
                CacheVanillaDualAxePoseFromDonor(donorPrefab);

                float swampTargetMeters = 0.76f * Mathf.Clamp(SwampVisualScale.Value, 0.25f, 3.0f);
                float plainsTargetMeters = 0.87f * Mathf.Clamp(PlainsVisualScale.Value, 0.25f, 3.0f);

                // 1) Swamp Dual Axes ("Bogroot Twin Axes")
                RegisterDualAxeWeapon(
                    prefabName: SwampPrefabName,
                    donorPrefabName: donorName,
                    nameToken: "$item_axedual_swamp",
                    descToken: "$item_axedual_swamp_desc",
                    icon: s_swampIcon,
                    rightMesh: s_swampRightMesh,
                    rightMat: s_swampRightMat,
                    leftMesh: s_swampLeftMesh,
                    leftMat: s_swampLeftMat,
                    targetWorldLengthMeters: swampTargetMeters,
                    addPoisonGlowLight: true,
                    minStationLevel: SwampMinStationLevel.Value,
                    recipeString: SwampRecipeConfig.Value,
                    slashDmg: SwampSlashDamage.Value,
                    slashPerLvl: SwampSlashPerLevel.Value,
                    poisonDmg: SwampPoisonDamage.Value,
                    poisonPerLvl: SwampPoisonPerLevel.Value,
                    chopDmg: SwampChopDamage.Value,
                    chopPerLvl: SwampChopPerLevel.Value,
                    toolTier: 2,
                    blockPower: 28f,
                    deflectionForce: 20f,
                    attackForce: 35f,
                    maxDurability: 175f,
                    attackStamina: 12f,
                    secondaryStamina: 24f
                );

                // 2) Plains Dual Axes ("Berserker's Fang Axes")
                RegisterDualAxeWeapon(
                    prefabName: PlainsPrefabName,
                    donorPrefabName: donorName,
                    nameToken: "$item_axedual_plains",
                    descToken: "$item_axedual_plains_desc",
                    icon: s_plainsIcon,
                    rightMesh: s_plainsRightMesh,
                    rightMat: s_plainsRightMat,
                    leftMesh: s_plainsLeftMesh,
                    leftMat: s_plainsLeftMat,
                    targetWorldLengthMeters: plainsTargetMeters,
                    addPoisonGlowLight: false,
                    minStationLevel: PlainsMinStationLevel.Value,
                    recipeString: PlainsRecipeConfig.Value,
                    slashDmg: PlainsSlashDamage.Value,
                    slashPerLvl: PlainsSlashPerLevel.Value,
                    poisonDmg: 0f,
                    poisonPerLvl: 0f,
                    chopDmg: PlainsChopDamage.Value,
                    chopPerLvl: PlainsChopPerLevel.Value,
                    toolTier: 4,
                    blockPower: 42f,
                    deflectionForce: 30f,
                    attackForce: 45f,
                    maxDurability: 200f,
                    attackStamina: 14f,
                    secondaryStamina: 28f
                );

                m_itemsRegistered = true;
                if (ModLogger != null) ModLogger.LogInfo("Registered custom dual axes cleanly: " + SwampPrefabName + " and " + PlainsPrefabName);
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogError("Failed to register custom dual axes: " + ex);
            }
        }

        /// <summary>
        /// Uses `SkinnedMeshRenderer.BakeMesh(baked, true)` on the unmodified vanilla `AxeBerzerkr` / `AxeEarly` prefab!
        /// Why does `BakeMesh` succeed 100% of the time when `orig.vertices` and `AcquireReadOnlyMeshData` fail?
        /// Because `BakeMesh` is a native Unity C++ skinning routine (`BakeMesh_Injected`) that reads internal mesh buffers
        /// without checking `isReadable`, skins them using the prefab's current `smr.bones` transforms into `smr.transform` space,
        /// and outputs a brand-new CPU-readable `Mesh` (`baked.vertices`, `baked.triangles`)!
        ///
        /// Then, for every vertex weighted to `rightBoneIdx` (or `leftBoneIdx`), its exact position in `bone[b]`'s local space is:
        ///   `p_bone = bone[b].InverseTransformPoint(smr.transform.TransformPoint(v_baked))`!
        /// (Because `BakeMesh(baked, true)` outputs `v_baked = smr.worldToLocal * bone.localToWorld * bindpose * v_orig`,
        /// multiplying by `bone.worldToLocal * smr.localToWorld` recovers `p_bone = bindpose * v_orig` with 100% mathematical precision!)
        /// </summary>
        private void CacheVanillaDualAxePoseFromDonor(GameObject donorPrefab)
        {
            s_cachedVanillaPoseValid = false;
            Transform attachSkin = donorPrefab.transform.Find("attach_skin");
            if (attachSkin == null) return;

            SkinnedMeshRenderer[] smrs = attachSkin.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer targetSmr = null;
            for (int i = 0; i < smrs.Length; i++)
            {
                if (smrs[i] != null && smrs[i].sharedMesh != null &&
                    smrs[i].sharedMesh.vertexCount >= 32 &&
                    !smrs[i].name.ToLowerInvariant().Contains("glow"))
                {
                    targetSmr = smrs[i];
                    break;
                }
            }
            if (targetSmr == null) return;

            Mesh orig = targetSmr.sharedMesh;
            Transform[] bones = targetSmr.bones;
            Matrix4x4[] bindposes = orig.bindposes;
            if (bones == null || bindposes == null || bones.Length == 0 || bindposes.Length == 0) return;

            BoneWeight[] weights = ExtractBoneWeightsReliable(orig);
            int rightIdx, leftIdx;
            SelectWeaponBoneIndices(bones, bindposes, weights, out rightIdx, out leftIdx);
            s_cachedRightBoneIdx = rightIdx;
            s_cachedLeftBoneIdx = leftIdx;

            Mesh baked = new Mesh();
            Vector3[] bakedVerts = null;
            int[] bakedTris = null;
            try
            {
                // BakeMesh(baked, true) applies smr.transform scale so smr.transform.TransformPoint(v) is exact world pos!
                targetSmr.BakeMesh(baked, true);
                bakedVerts = baked.vertices;
                bakedTris = baked.triangles;
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogWarning("[NewDualAxes] BakeMesh on donor failed: " + ex.Message);
            }
            finally
            {
                UnityEngine.Object.Destroy(baked);
            }

            if (bakedVerts == null || bakedVerts.Length < 24)
            {
                if (ModLogger != null) ModLogger.LogWarning("[NewDualAxes] BakeMesh returned insufficient vertices (" + (bakedVerts == null ? 0 : bakedVerts.Length) + ")");
                return;
            }

            bool okRight = ExtractSingleHandPoseFromBaked(
                targetSmr, bones, rightIdx, leftIdx, bakedVerts, bakedTris, weights,
                out s_cachedRightRotInBone, out s_cachedRightGripInBone, out s_cachedRightPommelRatio, "Right"
            );
            bool okLeft = ExtractSingleHandPoseFromBaked(
                targetSmr, bones, leftIdx, rightIdx, bakedVerts, bakedTris, weights,
                out s_cachedLeftRotInBone, out s_cachedLeftGripInBone, out s_cachedLeftPommelRatio, "Left"
            );

            s_cachedVanillaPoseValid = okRight && okLeft;
        }

        private bool ExtractSingleHandPoseFromBaked(
            SkinnedMeshRenderer smr,
            Transform[] bones,
            int boneIdx,
            int otherBoneIdx,
            Vector3[] bakedVerts,
            int[] bakedTris,
            BoneWeight[] weights,
            out Quaternion rotInBone,
            out Vector3 gripInBoneNormalized,
            out float pommelRatio,
            string sideLabel)
        {
            rotInBone = Quaternion.identity;
            gripInBoneNormalized = Vector3.zero;
            pommelRatio = 0.33f;

            if (boneIdx < 0 || boneIdx >= bones.Length || bones[boneIdx] == null) return false;
            Transform bone = bones[boneIdx];
            Transform otherBone = (otherBoneIdx >= 0 && otherBoneIdx < bones.Length) ? bones[otherBoneIdx] : null;

            // Matrix converting from BakeMesh(useScale=true) space into `bone` local space!
            // Wait: let's verify Unity's `SkinnedMeshRenderer.BakeMesh(mesh, useScale)` exact convention!
            // When `useScale == false`, BakeMesh bakes vertices in `smr.transform` local position/rotation WITHOUT `smr.transform.lossyScale`
            // (so 1 unit in BakeMesh(false) == 1 meter in world space!).
            // When `useScale == true`, BakeMesh multiplies by `smr.transform.localScale`.
            // Even cleaner: `bone.worldToLocalMatrix * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, smr.transform.localScale)`
            // OR let's check `smr.transform.TransformPoint` vs `bone.localToWorldMatrix`!
            // Actually, in `BakeMesh(mesh, false)`:
            // Unity transforms each vertex by `(TRS(smr.pos, smr.rot, Vector3.one))^-1 * bone.localToWorldMatrix * bindpose * v_orig`!
            // Let's re-bake with `useScale = false` directly or use `TRS(smr.pos, smr.rot, Vector3.one)` so there is ZERO ambiguity about parent scale!
            Mesh unscaledBake = new Mesh();
            Vector3[] unscaledVerts = null;
            try
            {
                smr.BakeMesh(unscaledBake, false);
                unscaledVerts = unscaledBake.vertices;
            }
            catch
            {
            }
            finally
            {
                UnityEngine.Object.Destroy(unscaledBake);
            }

            Vector3[] srcVerts = (unscaledVerts != null && unscaledVerts.Length == bakedVerts.Length) ? unscaledVerts : bakedVerts;
            Matrix4x4 bakeToWorld = (unscaledVerts != null && unscaledVerts.Length == bakedVerts.Length)
                ? Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one)
                : smr.transform.localToWorldMatrix;

            // Convert from world space into `bone` local space (including bone's 100x scale so `p_bone` is in exact bone-local coordinates,
            // AND also keep a pure meter-scale copy `p_boneMeters` with `Quaternion.Inverse(bone.rotation) * (p_world - bone.position)`!)
            // WAIT A SECOND! LOOK AT THAT EQUATION:
            //   `p_boneMeters = Quaternion.Inverse(bone.rotation) * (p_world - bone.position)`!!
            // STOP AND ADMIRE THIS!
            // In `Quaternion.Inverse(bone.rotation) * (p_world - bone.position)`:
            // - Origin `(0, 0, 0)` IS `bone.position` (the exact hand bone pivot in world space)!
            // - Axes X, Y, Z ARE `bone.rotation`'s exact local X, Y, Z axes!
            // - Units ARE **REAL WORLD METERS** (1.0 = 1 meter), completely immune to whether `bone.lossyScale` is `100`, `0.01`, or `1`!!
            List<Vector3> ptsBoneMeters = new List<Vector3>(srcVerts.Length / 2);
            Quaternion invBoneRot = Quaternion.Inverse(bone.rotation);
            Vector3 boneWorldPos = bone.position;

            // Method A: Filter vertices belonging to `boneIdx` using `BoneWeight[]`
            if (weights != null && weights.Length == srcVerts.Length)
            {
                for (int i = 0; i < srcVerts.Length; i++)
                {
                    BoneWeight bw = weights[i];
                    float w = 0f;
                    if (bw.boneIndex0 == boneIdx) w += bw.weight0;
                    if (bw.boneIndex1 == boneIdx) w += bw.weight1;
                    if (bw.boneIndex2 == boneIdx) w += bw.weight2;
                    if (bw.boneIndex3 == boneIdx) w += bw.weight3;
                    if (w >= 0.45f)
                    {
                        Vector3 pw = bakeToWorld.MultiplyPoint3x4(srcVerts[i]);
                        ptsBoneMeters.Add(invBoneRot * (pw - boneWorldPos));
                    }
                }
            }

            // Method B: If Unity stripped CPU boneWeights (`weights == null`), split the two axes by proximity to `RightHand` vs `LeftHand` in world space
            // or by connected triangle islands (`bakedTris`)!
            if (ptsBoneMeters.Count < 12)
            {
                ptsBoneMeters.Clear();
                if (otherBone != null && Vector3.Distance(bone.position, otherBone.position) > 0.02f)
                {
                    Vector3 otherPos = otherBone.position;
                    for (int i = 0; i < srcVerts.Length; i++)
                    {
                        Vector3 pw = bakeToWorld.MultiplyPoint3x4(srcVerts[i]);
                        if ((pw - boneWorldPos).sqrMagnitude <= (pw - otherPos).sqrMagnitude)
                        {
                            ptsBoneMeters.Add(invBoneRot * (pw - boneWorldPos));
                        }
                    }
                }
                else if (bakedTris != null && bakedTris.Length >= 12)
                {
                    bool[] mask = SplitConnectedComponentMask(srcVerts, bakedTris, sideLabel == "Right");
                    for (int i = 0; i < srcVerts.Length; i++)
                    {
                        if (mask == null || mask[i])
                        {
                            Vector3 pw = bakeToWorld.MultiplyPoint3x4(srcVerts[i]);
                            ptsBoneMeters.Add(invBoneRot * (pw - boneWorldPos));
                        }
                    }
                }
            }

            if (ptsBoneMeters.Count < 12)
            {
                if (ModLogger != null) ModLogger.LogWarning("[NewDualAxes] Could not isolate " + sideLabel + " axe cluster from BakeMesh (pts=" + ptsBoneMeters.Count + ")");
                return false;
            }

            // Now `ptsBoneMeters` contains the exact 3D point cloud of the vanilla Berserkir Axe in the hand bone's coordinate frame, IN METERS!
            // Let's find its exact principal 3D directions (NOT restricted to 90-degree axis snaps — even angled 15-degree hand grips are captured!)
            // using 3D PCA / Covariance + head/blade asymmetry!
            Vector3[] pts = ptsBoneMeters.ToArray();
            Vector3 shaftDir;
            Vector3 bladeDir;
            Vector3 gripPointMeters;
            float donorLenMeters;

            ComputeExact3DAxeFrame(pts, bone, out shaftDir, out bladeDir, out gripPointMeters, out donorLenMeters, out pommelRatio);

            rotInBone = Quaternion.LookRotation(bladeDir, shaftDir);
            gripInBoneNormalized = gripPointMeters; // Stored directly in bone-frame METERS!

            if (ModLogger != null)
            {
                ModLogger.LogInfo(string.Format(
                    "[NewDualAxes] Extracted EXACT vanilla {0} hand pose from '{1}' ({2} pts): donorLen={3:F3}m, shaftDir={4}, bladeDir={5}, gripMeters={6}, pommelRatio={7:F2}",
                    sideLabel, bone.name, pts.Length, donorLenMeters,
                    shaftDir.ToString("F3"), bladeDir.ToString("F3"), gripPointMeters.ToString("F3"), pommelRatio));
            }

            return true;
        }

        /// <summary>
        /// Computes the exact 3D orthonormal frame (`shaftDir` = pommel->head, `bladeDir` = shaft->cutting edge)
        /// and exact hand grip offset (in meters relative to bone origin `(0,0,0)`) from the vanilla axe's point cloud!
        /// Works for ANY arbitrary bone orientation (including angled Mixamo / Blender wrist bones)!
        /// </summary>
        private static void ComputeExact3DAxeFrame(
            Vector3[] pts,
            Transform handBone,
            out Vector3 shaftDir,
            out Vector3 bladeDir,
            out Vector3 gripPointMeters,
            out float donorLenMeters,
            out float pommelRatio)
        {
            // 1. Centroid
            Vector3 mean = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) mean += pts[i];
            mean /= pts.Length;

            // 2. Power iteration on 3x3 covariance matrix to get exact 1st principal axis (the long axe shaft!)
            float cxx = 0f, cxy = 0f, cxz = 0f, cyy = 0f, cyz = 0f, czz = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 d = pts[i] - mean;
                cxx += d.x * d.x; cxy += d.x * d.y; cxz += d.x * d.z;
                cyy += d.y * d.y; cyz += d.y * d.z; czz += d.z * d.z;
            }

            Vector3 v1 = new Vector3(1f, 1f, 1f);
            // Seed with bounding-box longest cardinal axis so power iteration never starts orthogonal
            Vector3 minBB = pts[0], maxBB = pts[0];
            for (int i = 1; i < pts.Length; i++) { minBB = Vector3.Min(minBB, pts[i]); maxBB = Vector3.Max(maxBB, pts[i]); }
            Vector3 bbSpan = maxBB - minBB;
            int seedAxis = GetLongestAxis(bbSpan);
            v1 = Vector3.zero;
            v1[seedAxis] = 1f;

            for (int iter = 0; iter < 24; iter++)
            {
                Vector3 next = new Vector3(
                    cxx * v1.x + cxy * v1.y + cxz * v1.z,
                    cxy * v1.x + cyy * v1.y + cyz * v1.z,
                    cxz * v1.x + cyz * v1.y + czz * v1.z
                );
                if (next.sqrMagnitude > 1e-12f) v1 = next.normalized;
            }
            shaftDir = v1.normalized;

            // Project all points onto shaftDir to find minS..maxS
            float minS = float.MaxValue, maxS = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float s = Vector3.Dot(pts[i], shaftDir);
                if (s < minS) minS = s;
                if (s > maxS) maxS = s;
            }
            donorLenMeters = Mathf.Max(0.05f, maxS - minS);
            float midS = 0.5f * (minS + maxS);

            // 3. Determine which end of shaftDir is the AXE HEAD (+shaftDir) vs POMMEL (-shaftDir):
            // Two independent checks that agree 100% on every axe:
            //   a) Radial spread perpendicular to shaftDir is much larger at the axe head (wide blade) than at the pommel (narrow handle)!
            //   b) Hand bone origin (0,0,0) grips the lower half of the handle (closer to pommel than to the top of the axe head)!
            float maxRadSqLow = 0f, maxRadSqHigh = 0f;
            // First compute handle centerline using points near the middle/narrow section
            for (int i = 0; i < pts.Length; i++)
            {
                float s = Vector3.Dot(pts[i], shaftDir);
                Vector3 perp = pts[i] - shaftDir * s;
                float rSq = perp.sqrMagnitude;
                if (s < midS)
                {
                    if (rSq > maxRadSqLow) maxRadSqLow = rSq;
                }
                else
                {
                    if (rSq > maxRadSqHigh) maxRadSqHigh = rSq;
                }
            }

            if (maxRadSqLow > maxRadSqHigh * 1.15f)
            {
                shaftDir = -shaftDir;
                float tmp = minS;
                minS = -maxS;
                maxS = -tmp;
            }
            else if (Mathf.Abs(maxRadSqHigh - maxRadSqLow) <= maxRadSqHigh * 0.15f)
            {
                // Tie-breaker: head extends further from hand origin (0,0,0) than pommel
                if (Mathf.Abs(minS) > Mathf.Abs(maxS))
                {
                    shaftDir = -shaftDir;
                    float tmp = minS;
                    minS = -maxS;
                    maxS = -tmp;
                }
            }

            // Now `minS` is the POMMEL end and `maxS` is the AXE HEAD end along `+shaftDir`!
            // 4. Find the exact handle centerline (perpendicular to shaftDir) using the lower/middle handle region (`minS + 0.10*L .. minS + 0.48*L`)!
            float hLo = minS + 0.08f * donorLenMeters;
            float hHi = minS + 0.48f * donorLenMeters;
            Vector3 handlePerpCenter = Vector3.zero;
            int hCount = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                float s = Vector3.Dot(pts[i], shaftDir);
                if (s >= hLo && s <= hHi)
                {
                    handlePerpCenter += (pts[i] - shaftDir * s);
                    hCount++;
                }
            }
            if (hCount > 0) handlePerpCenter /= hCount;

            // 5. Find the exact BLADE CUTTING EDGE direction (`+bladeDir`, perpendicular to `shaftDir`)!
            // In the head zone (`s >= minS + 0.60 * L`), measure vectors `u = (pt - shaftDir*s) - handlePerpCenter`:
            // - First find the 2D principal axis of `u` (the plane of the axe head)!
            // - Then pick the sign pointing toward the cutting edge (where `|u|` reaches its maximum reach forward from the handle shaft)!
            float headLo = minS + 0.58f * donorLenMeters;
            float bxx = 0f, bxy = 0f, bxz = 0f, byy = 0f, byz = 0f, bzz = 0f;
            Vector3 sumHeadOffset = Vector3.zero;
            int headCnt = 0;

            for (int i = 0; i < pts.Length; i++)
            {
                float s = Vector3.Dot(pts[i], shaftDir);
                if (s >= headLo)
                {
                    Vector3 u = (pts[i] - shaftDir * s) - handlePerpCenter;
                    bxx += u.x * u.x; bxy += u.x * u.y; bxz += u.x * u.z;
                    byy += u.y * u.y; byz += u.y * u.z; bzz += u.z * u.z;
                    sumHeadOffset += u;
                    headCnt++;
                }
            }

            // Seed v2 perpendicular to shaftDir
            Vector3 seedPerp = (Mathf.Abs(shaftDir.x) < 0.8f) ? Vector3.Cross(shaftDir, Vector3.right) : Vector3.Cross(shaftDir, Vector3.up);
            Vector3 v2 = seedPerp.normalized;
            for (int iter = 0; iter < 20; iter++)
            {
                Vector3 next = new Vector3(
                    bxx * v2.x + bxy * v2.y + bxz * v2.z,
                    bxy * v2.x + byy * v2.y + byz * v2.z,
                    bxz * v2.x + byz * v2.y + bzz * v2.z
                );
                // Keep strictly orthogonal to shaftDir
                next -= shaftDir * Vector3.Dot(next, shaftDir);
                if (next.sqrMagnitude > 1e-12f) v2 = next.normalized;
            }

            // Determine sign of v2 (cutting edge MUST point FORWARD out of the knuckles, NEVER backward into the wrist!):
            // Note: Vanilla `AxeBerzerkr` has a long poll spike on one axe that can match or exceed blade reach, which could flip +/-v2 by 180 deg!
            // Using the anatomical wrist->knuckles vector (`gripDir = handlePerpCenter`, which is +Y in both RightHand & LeftHand Mixamo bones!)
            // resolves the sign of `v2` with 100% certainty: the cutting edge ALWAYS faces forward along `gripDir` (`Dot(v2, gripDir) > 0`)!
            Vector3 forwardKnuckleRef = handlePerpCenter;
            if (handBone != null && handBone.childCount > 0)
            {
                Quaternion invHandRot = Quaternion.Inverse(handBone.rotation);
                Vector3 sumF = Vector3.zero;
                int fCnt = 0;
                for (int i = 0; i < handBone.childCount; i++)
                {
                    Transform c = handBone.GetChild(i);
                    if (c == null) continue;
                    string cn = c.name.ToLowerInvariant();
                    if (cn.Contains("index") || cn.Contains("middle") || cn.Contains("ring") || cn.Contains("pinky"))
                    {
                        sumF += invHandRot * (c.position - handBone.position);
                        fCnt++;
                    }
                }
                if (fCnt > 0 && sumF.sqrMagnitude > 1e-6f)
                {
                    forwardKnuckleRef = (sumF / fCnt);
                }
            }

            if (forwardKnuckleRef.sqrMagnitude > 1e-6f)
            {
                // Project forwardKnuckleRef perpendicular to shaftDir
                Vector3 fwdPerp = forwardKnuckleRef - shaftDir * Vector3.Dot(forwardKnuckleRef, shaftDir);
                if (fwdPerp.sqrMagnitude > 1e-6f)
                {
                    if (Vector3.Dot(v2, fwdPerp) < 0f)
                    {
                        v2 = -v2;
                    }
                }
            }
            else
            {
                float maxProjPos = 0f, maxProjNeg = 0f;
                for (int i = 0; i < pts.Length; i++)
                {
                    float s = Vector3.Dot(pts[i], shaftDir);
                    if (s >= headLo)
                    {
                        Vector3 u = (pts[i] - shaftDir * s) - handlePerpCenter;
                        float pr = Vector3.Dot(u, v2);
                        if (pr > maxProjPos) maxProjPos = pr;
                        if (-pr > maxProjNeg) maxProjNeg = -pr;
                    }
                }
                if (maxProjNeg > maxProjPos) v2 = -v2;
            }
            bladeDir = v2.normalized;

            // 6. Exact grip position in bone-frame meters!
            // `handlePerpCenter` is the exact lateral offset from the wrist bone `(0,0,0)` to the center of the vanilla wooden handle!
            // Along `shaftDir`, project the actual finger bones (`Index1`/`Middle1`/`Pinky1` average) onto `shaftDir` so our grip
            // sits dead-center between the index and pinky fingers of the closed fist!
            pommelRatio = Mathf.Clamp((-minS) / donorLenMeters, 0.05f, 0.65f);

            float targetS = 0f;
            if (handBone != null && handBone.childCount > 0)
            {
                Quaternion invHandRot = Quaternion.Inverse(handBone.rotation);
                Vector3 sumFingers = Vector3.zero;
                int fCnt = 0;
                for (int i = 0; i < handBone.childCount; i++)
                {
                    Transform c = handBone.GetChild(i);
                    if (c == null) continue;
                    string cn = c.name.ToLowerInvariant();
                    if (cn.Contains("index") || cn.Contains("middle") || cn.Contains("ring") || cn.Contains("pinky") || cn.Contains("little"))
                    {
                        sumFingers += invHandRot * (c.position - handBone.position);
                        fCnt++;
                    }
                }
                if (fCnt > 0)
                {
                    Vector3 palmKnuckleCenter = sumFingers / fCnt;
                    targetS = Vector3.Dot(palmKnuckleCenter, shaftDir);
                }
            }

            float sFist = Mathf.Clamp(targetS, minS + 0.12f * donorLenMeters, maxS - 0.35f * donorLenMeters);
            gripPointMeters = handlePerpCenter + shaftDir * sFist;
        }

        private static bool[] SplitConnectedComponentMask(Vector3[] verts, int[] tris, bool pickFirst)
        {
            int n = verts.Length;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            // Spatial weld so UV/normal seams don't split a single axe into pieces
            Dictionary<long, int> weld = new Dictionary<long, int>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = verts[i];
                long kx = (long)Mathf.RoundToInt(v.x * 2000f);
                long ky = (long)Mathf.RoundToInt(v.y * 2000f);
                long kz = (long)Mathf.RoundToInt(v.z * 2000f);
                long key = (kx * 73856093L) ^ (ky * 19349663L) ^ (kz * 83492791L);
                int prev;
                if (weld.TryGetValue(key, out prev))
                {
                    UnionSet(parent, i, prev);
                }
                else
                {
                    weld[key] = i;
                }
            }

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                UnionSet(parent, tris[t], tris[t + 1]);
                UnionSet(parent, tris[t + 1], tris[t + 2]);
            }

            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                int r = FindSet(parent, i);
                int c;
                counts.TryGetValue(r, out c);
                counts[r] = c + 1;
            }

            int rootA = -1, rootB = -1, cntA = 0, cntB = 0;
            foreach (KeyValuePair<int, int> kv in counts)
            {
                if (kv.Value > cntA)
                {
                    rootB = rootA; cntB = cntA;
                    rootA = kv.Key; cntA = kv.Value;
                }
                else if (kv.Value > cntB)
                {
                    rootB = kv.Key; cntB = kv.Value;
                }
            }

            if (rootA < 0 || rootB < 0 || cntB < 12) return null;
            int chosen = pickFirst ? rootA : rootB;
            bool[] mask = new bool[n];
            for (int i = 0; i < n; i++) mask[i] = (FindSet(parent, i) == chosen);
            return mask;
        }

        private static int FindSet(int[] p, int x)
        {
            while (p[x] != x)
            {
                p[x] = p[p[x]];
                x = p[x];
            }
            return x;
        }

        private static void UnionSet(int[] p, int a, int b)
        {
            int ra = FindSet(p, a);
            int rb = FindSet(p, b);
            if (ra != rb) p[ra] = rb;
        }

        private void RegisterDualAxeWeapon(
            string prefabName,
            string donorPrefabName,
            string nameToken,
            string descToken,
            Sprite icon,
            Mesh rightMesh,
            Material rightMat,
            Mesh leftMesh,
            Material leftMat,
            float targetWorldLengthMeters,
            bool addPoisonGlowLight,
            int minStationLevel,
            string recipeString,
            float slashDmg,
            float slashPerLvl,
            float poisonDmg,
            float poisonPerLvl,
            float chopDmg,
            float chopPerLvl,
            int toolTier,
            float blockPower,
            float deflectionForce,
            float attackForce,
            float maxDurability,
            float attackStamina,
            float secondaryStamina)
        {
            GameObject cloned = PrefabManager.Instance.CreateClonedPrefab(prefabName, donorPrefabName);
            if (cloned == null)
            {
                if (ModLogger != null) ModLogger.LogError("CreateClonedPrefab returned null for " + prefabName);
                return;
            }

            cloned.transform.localScale = Vector3.one;
            if (!cloned.activeSelf) cloned.SetActive(true);

            ApplyCustomDualAxeVisuals(
                cloned,
                rightMesh,
                rightMat,
                leftMesh,
                leftMat,
                targetWorldLengthMeters,
                addPoisonGlowLight
            );

            ItemDrop itemDrop = cloned.GetComponent<ItemDrop>();
            if (itemDrop != null && itemDrop.m_itemData != null && itemDrop.m_itemData.m_shared != null)
            {
                itemDrop.m_itemData.m_dropPrefab = cloned;
                ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
                shared.m_name = nameToken;
                shared.m_description = descToken;
                if (icon != null)
                {
                    shared.m_icons = new Sprite[] { icon };
                }

                shared.m_maxQuality = 4;
                shared.m_weight = 2.5f;
                shared.m_toolTier = toolTier;
                shared.m_skillType = Skills.SkillType.Axes;
                shared.m_scaleByQuality = 0f;

                HitData.DamageTypes dmg = new HitData.DamageTypes();
                dmg.m_slash = slashDmg;
                dmg.m_poison = poisonDmg;
                dmg.m_chop = chopDmg;
                shared.m_damages = dmg;

                HitData.DamageTypes dmgPerLvl = new HitData.DamageTypes();
                dmgPerLvl.m_slash = slashPerLvl;
                dmgPerLvl.m_poison = poisonPerLvl;
                dmgPerLvl.m_chop = chopPerLvl;
                shared.m_damagesPerLevel = dmgPerLvl;

                shared.m_blockPower = blockPower;
                shared.m_blockPowerPerLevel = 2f;
                shared.m_deflectionForce = deflectionForce;
                shared.m_deflectionForcePerLevel = 5f;
                shared.m_timedBlockBonus = 2f;
                shared.m_attackForce = attackForce;

                shared.m_useDurability = true;
                shared.m_maxDurability = maxDurability;
                shared.m_durabilityPerLevel = 50f;

                shared.m_dlc = "";
                shared.m_attackStatusEffect = null;
                shared.m_attackStatusEffectChance = 0f;

                if (shared.m_attack != null)
                {
                    shared.m_attack = shared.m_attack.Clone();
                    shared.m_attack.m_attackStamina = attackStamina;
                }
                if (shared.m_secondaryAttack != null)
                {
                    shared.m_secondaryAttack = shared.m_secondaryAttack.Clone();
                    shared.m_secondaryAttack.m_attackStamina = secondaryStamina;
                }
            }

            ItemConfig cfg = new ItemConfig();
            cfg.Name = nameToken;
            cfg.Description = descToken;
            cfg.Amount = 1;
            cfg.CraftingStation = CraftingStations.Forge;
            cfg.RepairStation = CraftingStations.Forge;
            cfg.MinStationLevel = minStationLevel;
            if (icon != null)
            {
                cfg.Icons = new Sprite[] { icon };
            }

            ParseAndAddRequirements(cfg, recipeString);

            CustomItem customItem = new CustomItem(cloned, false, cfg);
            ItemManager.Instance.AddItem(customItem);
        }

        internal static void ParseAndAddRequirements(ItemConfig cfg, string recipeStr)
        {
            if (string.IsNullOrEmpty(recipeStr)) return;
            string[] parts = recipeStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] tokens = parts[i].Trim().Split(':');
                if (tokens.Length >= 2)
                {
                    string resName = tokens[0].Trim();
                    int amount = 1;
                    int perLevel = 0;
                    int.TryParse(tokens[1].Trim(), out amount);
                    if (tokens.Length >= 3)
                    {
                        int.TryParse(tokens[2].Trim(), out perLevel);
                    }
                    if (!string.IsNullOrEmpty(resName) && (amount > 0 || perLevel > 0))
                    {
                        cfg.AddRequirement(new RequirementConfig(resName, amount, perLevel, true));
                    }
                }
            }
        }

        private void ApplyCustomDualAxeVisuals(
            GameObject prefab,
            Mesh rightMesh,
            Material rightMat,
            Mesh leftMesh,
            Material leftMat,
            float targetWorldLengthMeters,
            bool addPoisonGlowLight)
        {
            Transform attachRight = prefab.transform.Find("attach");
            Transform attachBack = prefab.transform.Find("attach_back");
            Transform attachSkin = prefab.transform.Find("attach_skin");

            // 1) World-drop MeshFilters
            MeshFilter[] allFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            int worldDropReplaced = 0;
            for (int i = 0; i < allFilters.Length; i++)
            {
                MeshFilter mf = allFilters[i];
                if (mf == null) continue;
                if (attachRight != null && mf.transform.IsChildOf(attachRight)) continue;
                if (attachBack != null && mf.transform.IsChildOf(attachBack)) continue;
                if (attachSkin != null && mf.transform.IsChildOf(attachSkin)) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                string mName = mf.name.ToLowerInvariant();
                if (mName.Contains("glow") || (mf.sharedMesh != null && mf.sharedMesh.vertexCount < 24))
                {
                    if (mName.Contains("glow")) mr.enabled = false;
                    continue;
                }

                Mesh srcMesh = (worldDropReplaced == 0) ? rightMesh : leftMesh;
                Material srcMat = (worldDropReplaced == 0) ? rightMat : leftMat;
                ReplaceStaticMeshFilterNormalized(mf, mr, prefab.transform, srcMesh, srcMat, targetWorldLengthMeters);
                worldDropReplaced++;
            }

            FitWorldDropColliders(prefab);

            // 2) Static 'attach' (ItemStand)
            if (attachRight != null)
            {
                ReplaceStaticAttachGroup(attachRight, rightMesh, rightMat, leftMesh, leftMat, targetWorldLengthMeters, addPoisonGlowLight);
            }

            // 3) Static 'attach_back' (Holstered on back / hips)
            if (attachBack != null)
            {
                ReplaceStaticAttachGroup(attachBack, rightMesh, rightMat, leftMesh, leftMat, targetWorldLengthMeters, false);
            }

            // 4) Skinned 'attach_skin' (Equipped dual axes in both hands!)
            if (attachSkin != null)
            {
                SkinnedMeshRenderer[] smrs = attachSkin.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool attachedPrimary = false;
                for (int i = 0; i < smrs.Length; i++)
                {
                    SkinnedMeshRenderer smr = smrs[i];
                    if (smr == null || smr.sharedMesh == null) continue;
                    string sName = smr.name.ToLowerInvariant();
                    if (smr.sharedMesh.vertexCount < 32 || sName.Contains("glow") || attachedPrimary)
                    {
                        smr.enabled = false;
                        continue;
                    }

                    BuildDualSkinnedMesh(smr, rightMesh, rightMat, leftMesh, leftMat, targetWorldLengthMeters, prefab.name);
                    attachedPrimary = true;
                }

                DualAxeSkinScaler scaler = attachSkin.gameObject.GetComponent<DualAxeSkinScaler>();
                if (scaler == null)
                {
                    scaler = attachSkin.gameObject.AddComponent<DualAxeSkinScaler>();
                }
                scaler.PrefabName = prefab.name;
                scaler.TargetWorldLength = targetWorldLengthMeters;
                scaler.RightMesh = rightMesh;
                scaler.RightMat = rightMat;
                scaler.LeftMesh = leftMesh;
                scaler.LeftMat = leftMat;
                scaler.AddPoisonGlow = addPoisonGlowLight;
            }
        }

        private void ReplaceStaticAttachGroup(
            Transform groupRoot,
            Mesh rightMesh,
            Material rightMat,
            Mesh leftMesh,
            Material leftMat,
            float targetWorldLengthMeters,
            bool addPoisonGlowLight)
        {
            MeshFilter[] filters = groupRoot.GetComponentsInChildren<MeshFilter>(true);
            int idx = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                string mName = mf.name.ToLowerInvariant();
                if (mName.Contains("glow") || mName.Contains("trail"))
                {
                    if (mName.Contains("glow")) mr.enabled = false;
                    continue;
                }
                if (mf.sharedMesh != null && mf.sharedMesh.vertexCount < 28) continue;

                Mesh srcMesh = (idx == 0) ? rightMesh : leftMesh;
                Material srcMat = (idx == 0) ? rightMat : leftMat;
                ReplaceStaticMeshFilterNormalized(mf, mr, groupRoot, srcMesh, srcMat, targetWorldLengthMeters);
                if (idx == 0 && addPoisonGlowLight)
                {
                    AttachSubtleSwampGlow(mf.transform, GetRelativeScaleMagnitude(mf.transform, groupRoot));
                }
                idx++;
            }
        }

        private void ReplaceStaticMeshFilterNormalized(
            MeshFilter mf,
            MeshRenderer mr,
            Transform referenceRoot,
            Mesh customMesh,
            Material customMat,
            float targetWorldLengthMeters)
        {
            float relScale = GetRelativeScaleMagnitude(mf.transform, referenceRoot);
            if (relScale < 1e-6f) relScale = 1.0f;

            Mesh donorMesh = mf.sharedMesh;
            Quaternion rot = Quaternion.identity;
            Vector3 localOffset = Vector3.zero;
            float desiredLocalLen = targetWorldLengthMeters / relScale;

            if (donorMesh != null && donorMesh.bounds.size.sqrMagnitude > 1e-8f)
            {
                Bounds db = donorMesh.bounds;
                Vector3 sz = db.size;
                int shaftAxis = GetLongestAxis(sz);
                int bladeAxis = GetSecondLongestAxis(sz, shaftAxis);

                float shaftSign = (db.center[shaftAxis] >= 0f) ? 1f : -1f;
                float bladeSign = (db.center[bladeAxis] >= 0f) ? 1f : -1f;

                Vector3 targetUp = Vector3.zero;
                targetUp[shaftAxis] = shaftSign;
                Vector3 targetFwd = Vector3.zero;
                targetFwd[bladeAxis] = bladeSign;
                rot = Quaternion.LookRotation(targetFwd, targetUp);

                float customLen = Mathf.Max(0.4f, customMesh.bounds.size.y);
                float meshScaleFactor = desiredLocalLen / customLen;
                Vector3 rotatedCustomCenter = rot * (customMesh.bounds.center * meshScaleFactor);
                localOffset = db.center - rotatedCustomCenter;
            }

            float customHeight = Mathf.Max(0.4f, customMesh.bounds.size.y);
            float finalVertexScale = desiredLocalLen / customHeight;

            mf.sharedMesh = BakeTransformedMesh(customMesh, localOffset, rot, finalVertexScale);
            mr.sharedMaterials = new Material[] { customMat };
        }

        internal static void FitWorldDropColliders(GameObject prefab)
        {
            Transform attachRight = prefab.transform.Find("attach");
            Transform attachBack = prefab.transform.Find("attach_back");
            Transform attachSkin = prefab.transform.Find("attach_skin");

            Bounds combinedWorld = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Matrix4x4 rootW2L = prefab.transform.worldToLocalMatrix;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                if (attachRight != null && mf.transform.IsChildOf(attachRight)) continue;
                if (attachBack != null && mf.transform.IsChildOf(attachBack)) continue;
                if (attachSkin != null && mf.transform.IsChildOf(attachSkin)) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;

                Matrix4x4 m = rootW2L * mf.transform.localToWorldMatrix;
                Bounds lb = mf.sharedMesh.bounds;
                Vector3 c = lb.center;
                Vector3 e = lb.extents;
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    for (int dy = -1; dy <= 1; dy += 2)
                    {
                        for (int dz = -1; dz <= 1; dz += 2)
                        {
                            Vector3 pt = m.MultiplyPoint3x4(c + new Vector3(dx * e.x, dy * e.y, dz * e.z));
                            if (!hasBounds)
                            {
                                combinedWorld = new Bounds(pt, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                combinedWorld.Encapsulate(pt);
                            }
                        }
                    }
                }
            }

            if (!hasBounds) return;

            BoxCollider[] boxes = prefab.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < boxes.Length; i++)
            {
                BoxCollider bc = boxes[i];
                if (bc == null) continue;
                if (attachRight != null && bc.transform.IsChildOf(attachRight)) continue;
                if (attachBack != null && bc.transform.IsChildOf(attachBack)) continue;
                if (attachSkin != null && bc.transform.IsChildOf(attachSkin)) continue;

                if (bc.transform == prefab.transform)
                {
                    bc.center = combinedWorld.center;
                    bc.size = new Vector3(
                        Mathf.Max(0.16f, combinedWorld.size.x),
                        Mathf.Max(0.16f, combinedWorld.size.y),
                        Mathf.Max(0.16f, combinedWorld.size.z)
                    );
                }
            }
        }

        internal static void BuildDualSkinnedMesh(
            SkinnedMeshRenderer smr,
            Mesh rightSrc,
            Material rightMat,
            Mesh leftSrc,
            Material leftMat,
            float targetWorldLengthMeters,
            string label)
        {
            Mesh orig = smr.sharedMesh;
            if (orig == null) return;

            Matrix4x4[] bindposes = orig.bindposes;
            Transform[] bones = smr.bones;
            if (bindposes == null || bindposes.Length == 0 || bones == null || bones.Length == 0)
            {
                if (ModLogger != null) ModLogger.LogWarning("attach_skin SkinnedMeshRenderer had no bones/bindposes on " + label);
                return;
            }

            int rightBoneIdx = s_cachedRightBoneIdx;
            int leftBoneIdx = s_cachedLeftBoneIdx;
            if (rightBoneIdx < 0 || leftBoneIdx < 0 || rightBoneIdx >= bones.Length || leftBoneIdx >= bones.Length)
            {
                BoneWeight[] origWeights = ExtractBoneWeightsReliable(orig);
                SelectWeaponBoneIndices(bones, bindposes, origWeights, out rightBoneIdx, out leftBoneIdx);
            }

            float rightBoneWorldScale = GetBoneWorldScale(bones, rightBoneIdx);
            float leftBoneWorldScale = GetBoneWorldScale(bones, leftBoneIdx);

            Matrix4x4 rightToMesh = BuildHandToMeshMatrix(
                bindposes[Mathf.Clamp(rightBoneIdx, 0, bindposes.Length - 1)],
                rightBoneWorldScale,
                rightSrc,
                targetWorldLengthMeters,
                isRightHand: true,
                boneTransform: (rightBoneIdx >= 0 && rightBoneIdx < bones.Length) ? bones[rightBoneIdx] : null
            );

            Matrix4x4 leftToMesh = BuildHandToMeshMatrix(
                bindposes[Mathf.Clamp(leftBoneIdx, 0, bindposes.Length - 1)],
                leftBoneWorldScale,
                leftSrc,
                targetWorldLengthMeters,
                isRightHand: false,
                boneTransform: (leftBoneIdx >= 0 && leftBoneIdx < bones.Length) ? bones[leftBoneIdx] : null
            );

            Mesh combined = AssembleDualSkinnedMesh(
                label + "_DualSkinnedMesh",
                rightSrc,
                rightToMesh,
                rightBoneIdx,
                leftSrc,
                leftToMesh,
                leftBoneIdx,
                bindposes
            );

            smr.sharedMesh = combined;
            smr.sharedMaterials = new Material[] { rightMat, leftMat };
        }

        /// <summary>
        /// Builds the exact `Matrix4x4` mapping our custom axe mesh (+Y = shaft up to head, +Z = cutting edge forward,
        /// (0,0,0) = palm grip, units = meters) into `SkinnedMeshRenderer` mesh space!
        ///
        /// Uses the EXACT pose (`s_cachedRightRotInBone` / `s_cachedRightGripInBone`) baked from vanilla `AxeBerzerkr`!
        /// If for any reason `s_cachedVanillaPoseValid` was false, computes the exact palm center & curl axis directly from
        /// the hand bone's finger children (`RightHandMiddle1` / `RightHandIndex1`)!
        /// </summary>
        internal static Matrix4x4 BuildHandToMeshMatrix(
            Matrix4x4 bindpose,
            float boneWorldScale,
            Mesh customMesh,
            float targetWorldLengthMeters,
            bool isRightHand,
            Transform boneTransform)
        {
            if (boneWorldScale < 1e-5f) boneWorldScale = 1.0f;
            float customLenMeters = Mathf.Max(0.4f, customMesh.bounds.size.y);

            // Convert from meters to bone-local units (`1 / boneWorldScale`, i.e. `0.01` when Armature scale is `100.0`)
            float metersToBoneUnits = 1.0f / boneWorldScale;
            float vertexScaleInBone = (targetWorldLengthMeters / customLenMeters) * metersToBoneUnits;

            Quaternion rotInBone;
            Vector3 gripInBoneMeters;

            if (s_cachedVanillaPoseValid)
            {
                rotInBone = isRightHand ? s_cachedRightRotInBone : s_cachedLeftRotInBone;
                gripInBoneMeters = isRightHand ? s_cachedRightGripInBone : s_cachedLeftGripInBone;
            }
            else
            {
                // Fallback derived from actual finger child bones on Valheim's Mixamo skeleton (`RightHand` -> `RightHandMiddle1`, `RightHandIndex1`, `RightHandPinky1`)!
                ComputeSkeletonHandGripFallback(boneTransform, boneWorldScale, isRightHand, out rotInBone, out gripInBoneMeters);
            }

            // Apply optional user config fine-tuning offsets (default (0,0,0))
            Vector3 userPosMeters = GripPositionOffset != null ? GripPositionOffset.Value : Vector3.zero;
            Vector3 userRotDeg = GripRotationOffset != null ? GripRotationOffset.Value : Vector3.zero;
            if (userRotDeg.sqrMagnitude > 1e-6f)
            {
                Vector3 mirroredRot = isRightHand ? userRotDeg : new Vector3(userRotDeg.x, -userRotDeg.y, -userRotDeg.z);
                rotInBone = rotInBone * Quaternion.Euler(mirroredRot);
            }
            if (userPosMeters.sqrMagnitude > 1e-8f)
            {
                Vector3 mirroredPos = isRightHand ? userPosMeters : new Vector3(-userPosMeters.x, userPosMeters.y, userPosMeters.z);
                gripInBoneMeters += rotInBone * mirroredPos;
            }

            Vector3 gripInBoneUnits = gripInBoneMeters * metersToBoneUnits;
            Matrix4x4 customToBone = Matrix4x4.TRS(gripInBoneUnits, rotInBone, Vector3.one * vertexScaleInBone);
            return bindpose.inverse * customToBone;
        }

        /// <summary>
        /// Anatomical hand-bone fallback (only used if `BakeMesh` were ever unavailable):
        /// Measures the exact finger positions (`Index1`, `Middle1`, `Pinky1`, `Thumb1`) relative to `RightHand` / `LeftHand`
        /// to place the handle dead-center inside the closed fist!
        /// </summary>
        internal static void ComputeSkeletonHandGripFallback(
            Transform handBone,
            float boneWorldScale,
            bool isRightHand,
            out Quaternion rotInBone,
            out Vector3 gripInBoneMeters)
        {
            if (handBone != null && handBone.childCount > 0)
            {
                Transform index1 = null, middle1 = null, pinky1 = null;
                for (int i = 0; i < handBone.childCount; i++)
                {
                    Transform c = handBone.GetChild(i);
                    if (c == null) continue;
                    string cn = c.name.ToLowerInvariant();
                    if (cn.Contains("index")) index1 = c;
                    else if (cn.Contains("middle")) middle1 = c;
                    else if (cn.Contains("pinky") || cn.Contains("little")) pinky1 = c;
                }

                if (middle1 != null && (index1 != null || pinky1 != null))
                {
                    Quaternion invHandRot = Quaternion.Inverse(handBone.rotation);
                    Vector3 wristToKnuckles = invHandRot * (middle1.position - handBone.position);
                    Vector3 acrossKnuckles;
                    if (index1 != null && pinky1 != null)
                    {
                        // Thumb/Index side is UP (+shaftDir) for an axe held in the fist!
                        acrossKnuckles = (invHandRot * (index1.position - pinky1.position)).normalized;
                    }
                    else
                    {
                        acrossKnuckles = isRightHand ? Vector3.forward : Vector3.back;
                    }

                    Vector3 fingerDir = wristToKnuckles.normalized;
                    Vector3 shaftDir = (acrossKnuckles - fingerDir * Vector3.Dot(acrossKnuckles, fingerDir)).normalized;
                    // Blade points forward in the direction the knuckles face!
                    Vector3 bladeDir = fingerDir;

                    rotInBone = Quaternion.LookRotation(bladeDir, shaftDir);
                    // Closed fist center is ~75% of the way from wrist to middle knuckle, slightly toward the palm!
                    Vector3 palmNormal = isRightHand
                        ? Vector3.Cross(fingerDir, shaftDir).normalized
                        : Vector3.Cross(shaftDir, fingerDir).normalized;
                    gripInBoneMeters = wristToKnuckles * 0.85f + palmNormal * 0.025f;
                    return;
                }
            }

            rotInBone = Quaternion.Euler(0f, 90f, -90f);
            gripInBoneMeters = new Vector3(0f, 0.085f, 0f);
        }

        internal static Mesh AssembleDualSkinnedMesh(
            string meshName,
            Mesh rightSrc,
            Matrix4x4 rightToMesh,
            int rightBoneIdx,
            Mesh leftSrc,
            Matrix4x4 leftToMesh,
            int leftBoneIdx,
            Matrix4x4[] bindposes)
        {
            Vector3[] rv = rightSrc.vertices;
            Vector3[] rn = rightSrc.normals;
            Vector2[] ruv = rightSrc.uv;
            int[] rt = rightSrc.triangles;

            Vector3[] lv = leftSrc.vertices;
            Vector3[] ln = leftSrc.normals;
            Vector2[] luv = leftSrc.uv;
            int[] lt = leftSrc.triangles;

            int totalV = rv.Length + lv.Length;
            Vector3[] allV = new Vector3[totalV];
            Vector3[] allN = new Vector3[totalV];
            Vector2[] allUV = new Vector2[totalV];
            BoneWeight[] allBW = new BoneWeight[totalV];

            bool flipRight = rightToMesh.determinant < 0f;
            bool flipLeft = leftToMesh.determinant < 0f;

            Matrix4x4 rightNormalMat = rightToMesh.inverse.transpose;
            Matrix4x4 leftNormalMat = leftToMesh.inverse.transpose;

            for (int i = 0; i < rv.Length; i++)
            {
                allV[i] = rightToMesh.MultiplyPoint3x4(rv[i]);
                allN[i] = rightNormalMat.MultiplyVector(rn[i]).normalized;
                allUV[i] = ruv[i];
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = rightBoneIdx;
                bw.weight0 = 1f;
                allBW[i] = bw;
            }

            int offset = rv.Length;
            for (int i = 0; i < lv.Length; i++)
            {
                int idx = offset + i;
                allV[idx] = leftToMesh.MultiplyPoint3x4(lv[i]);
                allN[idx] = leftNormalMat.MultiplyVector(ln[i]).normalized;
                allUV[idx] = luv[i];
                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = leftBoneIdx;
                bw.weight0 = 1f;
                allBW[idx] = bw;
            }

            int[] sub0 = new int[rt.Length];
            for (int i = 0; i < rt.Length; i += 3)
            {
                sub0[i] = rt[i];
                sub0[i + 1] = flipRight ? rt[i + 2] : rt[i + 1];
                sub0[i + 2] = flipRight ? rt[i + 1] : rt[i + 2];
            }

            int[] sub1 = new int[lt.Length];
            for (int i = 0; i < lt.Length; i += 3)
            {
                sub1[i] = offset + lt[i];
                sub1[i + 1] = offset + (flipLeft ? lt[i + 2] : lt[i + 1]);
                sub1[i + 2] = offset + (flipLeft ? lt[i + 1] : lt[i + 2]);
            }

            Mesh combined = new Mesh();
            combined.name = meshName;
            if (totalV > 65000)
            {
                combined.indexFormat = IndexFormat.UInt32;
            }
            combined.vertices = allV;
            combined.normals = allN;
            combined.uv = allUV;
            combined.boneWeights = allBW;
            combined.bindposes = bindposes;
            combined.subMeshCount = 2;
            combined.SetTriangles(sub0, 0);
            combined.SetTriangles(sub1, 1);
            combined.RecalculateBounds();
            combined.RecalculateTangents();
            return combined;
        }

        internal static float GetBoneWorldScale(Transform[] bones, int idx)
        {
            if (bones != null && idx >= 0 && idx < bones.Length && bones[idx] != null)
            {
                Vector3 ls = bones[idx].lossyScale;
                float mag = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
                if (mag > 1e-5f) return mag;
            }
            return 1.0f;
        }

        internal static void SelectWeaponBoneIndices(
            Transform[] bones,
            Matrix4x4[] bindposes,
            BoneWeight[] weights,
            out int rightIdx,
            out int leftIdx)
        {
            int boneCount = Mathf.Min(bones.Length, bindposes.Length);
            rightIdx = -1;
            leftIdx = -1;

            if (weights != null && weights.Length > 0 && boneCount >= 2)
            {
                float[] sums = new float[boneCount];
                for (int i = 0; i < weights.Length; i++)
                {
                    BoneWeight bw = weights[i];
                    if (bw.boneIndex0 >= 0 && bw.boneIndex0 < boneCount) sums[bw.boneIndex0] += bw.weight0;
                    if (bw.boneIndex1 >= 0 && bw.boneIndex1 < boneCount) sums[bw.boneIndex1] += bw.weight1;
                    if (bw.boneIndex2 >= 0 && bw.boneIndex2 < boneCount) sums[bw.boneIndex2] += bw.weight2;
                    if (bw.boneIndex3 >= 0 && bw.boneIndex3 < boneCount) sums[bw.boneIndex3] += bw.weight3;
                }

                int bestA = -1, bestB = -1;
                float sumA = 1f, sumB = 1f;
                for (int b = 0; b < boneCount; b++)
                {
                    if (sums[b] > sumA)
                    {
                        bestB = bestA; sumB = sumA;
                        bestA = b; sumA = sums[b];
                    }
                    else if (sums[b] > sumB)
                    {
                        bestB = b; sumB = sums[b];
                    }
                }

                if (bestA >= 0 && bestB >= 0)
                {
                    string nameA = (bones[bestA] != null) ? bones[bestA].name.ToLowerInvariant() : "";
                    string nameB = (bones[bestB] != null) ? bones[bestB].name.ToLowerInvariant() : "";

                    bool aIsLeft = nameA.Contains("left") || nameA.Contains("l_hand") || nameA.Contains("hand_l") || nameA.EndsWith(".l") || nameA.EndsWith("_l");
                    bool bIsRight = nameB.Contains("right") || nameB.Contains("r_hand") || nameB.Contains("hand_r") || nameB.EndsWith(".r") || nameB.EndsWith("_r");

                    if (aIsLeft || bIsRight)
                    {
                        leftIdx = bestA;
                        rightIdx = bestB;
                    }
                    else
                    {
                        rightIdx = bestA;
                        leftIdx = bestB;
                    }
                    return;
                }
            }

            int bestRightScore = -1;
            int bestLeftScore = -1;

            for (int b = 0; b < boneCount; b++)
            {
                if (bones[b] == null) continue;
                string bn = bones[b].name.ToLowerInvariant();

                if (bn.Contains("index") || bn.Contains("thumb") || bn.Contains("middle") ||
                    bn.Contains("ring") || bn.Contains("pinky") || bn.Contains("finger") ||
                    bn.Contains("toe") || bn.Contains("foot") || bn.Contains("leg") ||
                    bn.Contains("upleg") || bn.Contains("shoulder") || bn.Contains("forearm") ||
                    bn.Contains("arm"))
                {
                    continue;
                }

                int score = 0;
                if (bn.Contains("attach")) score += 100;
                if (bn.Contains("weapon")) score += 80;
                if (bn == "righthand" || bn == "lefthand" || bn == "mixamorig:righthand" || bn == "mixamorig:lefthand") score += 90;
                else if (bn.Contains("hand")) score += 50;

                if (score == 0) continue;

                bool isRight = bn.Contains("right") || bn.Contains("r_hand") || bn.Contains("hand_r") || bn.EndsWith(".r") || bn.EndsWith("_r");
                bool isLeft = bn.Contains("left") || bn.Contains("l_hand") || bn.Contains("hand_l") || bn.EndsWith(".l") || bn.EndsWith("_l");

                if (isRight && score > bestRightScore)
                {
                    bestRightScore = score;
                    rightIdx = b;
                }
                else if (isLeft && score > bestLeftScore)
                {
                    bestLeftScore = score;
                    leftIdx = b;
                }
            }

            if (rightIdx < 0) rightIdx = 0;
            if (leftIdx < 0) leftIdx = (boneCount > 1 && rightIdx == 0) ? 1 : 0;
        }

        internal static int GetLongestAxis(Vector3 v)
        {
            if (v.x >= v.y && v.x >= v.z) return 0;
            if (v.y >= v.x && v.y >= v.z) return 1;
            return 2;
        }

        internal static int GetSecondLongestAxis(Vector3 v, int excludeAxis)
        {
            int bestAxis = (excludeAxis == 1) ? 2 : 1;
            float bestVal = -1f;
            for (int a = 0; a < 3; a++)
            {
                if (a == excludeAxis) continue;
                if (v[a] > bestVal)
                {
                    bestVal = v[a];
                    bestAxis = a;
                }
            }
            return bestAxis;
        }

        internal static BoneWeight[] ExtractBoneWeightsReliable(Mesh mesh)
        {
            if (mesh == null) return null;

            try
            {
                List<BoneWeight> list = new List<BoneWeight>(mesh.vertexCount);
                mesh.GetBoneWeights(list);
                if (list.Count == mesh.vertexCount && list.Count > 0)
                {
                    return list.ToArray();
                }
            }
            catch
            {
            }

            try
            {
                Unity.Collections.NativeArray<byte> bpv = mesh.GetBonesPerVertex();
                Unity.Collections.NativeArray<BoneWeight1> bw1 = mesh.GetAllBoneWeights();
                if (bpv.IsCreated && bw1.IsCreated && bpv.Length > 0)
                {
                    BoneWeight[] res = new BoneWeight[bpv.Length];
                    int cursor = 0;
                    for (int v = 0; v < bpv.Length; v++)
                    {
                        int cnt = bpv[v];
                        BoneWeight bw = new BoneWeight();
                        if (cnt > 0 && cursor < bw1.Length) { bw.boneIndex0 = bw1[cursor].boneIndex; bw.weight0 = bw1[cursor].weight; cursor++; }
                        if (cnt > 1 && cursor < bw1.Length) { bw.boneIndex1 = bw1[cursor].boneIndex; bw.weight1 = bw1[cursor].weight; cursor++; }
                        if (cnt > 2 && cursor < bw1.Length) { bw.boneIndex2 = bw1[cursor].boneIndex; bw.weight2 = bw1[cursor].weight; cursor++; }
                        if (cnt > 3 && cursor < bw1.Length) { bw.boneIndex3 = bw1[cursor].boneIndex; bw.weight3 = bw1[cursor].weight; cursor++; }
                        for (int extra = 4; extra < cnt && cursor < bw1.Length; extra++) cursor++;
                        res[v] = bw;
                    }
                    return res;
                }
            }
            catch
            {
            }

            if (mesh.isReadable)
            {
                return mesh.boneWeights;
            }
            return null;
        }

        internal static float GetRelativeScaleMagnitude(Transform child, Transform root)
        {
            if (child == null || root == null) return 1.0f;
            Matrix4x4 m = root.worldToLocalMatrix * child.localToWorldMatrix;
            float sx = new Vector3(m.m00, m.m10, m.m20).magnitude;
            float sy = new Vector3(m.m01, m.m11, m.m21).magnitude;
            float sz = new Vector3(m.m02, m.m12, m.m22).magnitude;
            return (sx + sy + sz) / 3.0f;
        }

        internal static Mesh BakeTransformedMesh(Mesh src, Vector3 offset, Quaternion rot, float scale)
        {
            Mesh m = new Mesh();
            m.name = src.name + "_Baked";
            Matrix4x4 trs = Matrix4x4.TRS(offset, rot, Vector3.one * scale);
            Matrix4x4 nMat = trs.inverse.transpose;
            bool flip = trs.determinant < 0f;

            Vector3[] sv = src.vertices;
            Vector3[] sn = src.normals;
            int[] st = src.triangles;

            Vector3[] dv = new Vector3[sv.Length];
            Vector3[] dn = new Vector3[sn.Length];
            int[] dt = new int[st.Length];

            for (int i = 0; i < sv.Length; i++)
            {
                dv[i] = trs.MultiplyPoint3x4(sv[i]);
                dn[i] = nMat.MultiplyVector(sn[i]).normalized;
            }
            for (int i = 0; i < st.Length; i += 3)
            {
                dt[i] = st[i];
                dt[i + 1] = flip ? st[i + 2] : st[i + 1];
                dt[i + 2] = flip ? st[i + 1] : st[i + 2];
            }

            if (sv.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = dv;
            m.normals = dn;
            m.uv = src.uv;
            m.triangles = dt;
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        internal static void AttachSubtleSwampGlow(Transform parent, float parentRelScale)
        {
            if (parent == null) return;
            if (parent.Find("SwampGuckGlow") != null) return;
            if (parentRelScale < 1e-5f) parentRelScale = 1.0f;

            GameObject glowObj = new GameObject("SwampGuckGlow");
            glowObj.transform.SetParent(parent, false);
            glowObj.transform.localPosition = new Vector3(0f, 0.30f / parentRelScale, 0.06f / parentRelScale);
            Light l = glowObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.28f, 0.95f, 0.32f, 1f);
            l.range = 1.5f;
            l.intensity = 0.50f;
            l.shadows = LightShadows.None;
        }

        internal static Shader FindValheimStandardShader()
        {
            string[] weaponCandidates = new string[] { "AxeIron", "AxeBlackMetal", "SwordIron", "MaceIron" };
            for (int i = 0; i < weaponCandidates.Length; i++)
            {
                GameObject p = PrefabManager.Instance.GetPrefab(weaponCandidates[i]);
                if (p != null)
                {
                    MeshRenderer mr = p.GetComponentInChildren<MeshRenderer>(true);
                    if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null)
                    {
                        return mr.sharedMaterial.shader;
                    }
                }
            }

            Shader s = Shader.Find("Custom/Piece");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private void EnsureAssetsLoaded(Shader shader)
        {
            if (s_swampRightMesh == null)
            {
                s_swampRightMesh = LoadEmbeddedMesh("SwampAxe_Right.bin.gz", "SwampAxe_Right");
                s_swampLeftMesh = LoadEmbeddedMesh("SwampAxe_Left.bin.gz", "SwampAxe_Left");
                s_swampIcon = LoadEmbeddedSprite("SwampDualAxes_Icon.png");

                s_swampRightMat = BuildValheimMaterial(
                    shader,
                    "Mat_SwampAxe_Right",
                    "SwampAxe_Right_Albedo.png",
                    "SwampAxe_Right_Roughness.png",
                    "SwampAxe_Right_Normal.png",
                    "SwampAxe_Right_Emission.png",
                    baseMetallic: 0.55f,
                    emissionColor: new Color(0.45f, 1.0f, 0.35f, 1f) * 1.8f
                );

                s_swampLeftMat = BuildValheimMaterial(
                    shader,
                    "Mat_SwampAxe_Left",
                    "SwampAxe_Left_Albedo.png",
                    "SwampAxe_Left_Roughness.png",
                    "SwampAxe_Left_Normal.png",
                    null,
                    baseMetallic: 0.72f,
                    emissionColor: Color.black
                );
            }

            if (s_plainsRightMesh == null)
            {
                s_plainsRightMesh = LoadEmbeddedMesh("PlainsAxe_Right.bin.gz", "PlainsAxe_Right");
                s_plainsLeftMesh = LoadEmbeddedMesh("PlainsAxe_Left.bin.gz", "PlainsAxe_Left");
                s_plainsIcon = LoadEmbeddedSprite("PlainsDualAxes_Icon.png");

                s_plainsRightMat = BuildValheimMaterial(
                    shader,
                    "Mat_PlainsAxe_Right",
                    "PlainsAxe_Right_Albedo.png",
                    "PlainsAxe_Right_Roughness.png",
                    "PlainsAxe_Right_Normal.png",
                    null,
                    baseMetallic: 0.78f,
                    emissionColor: Color.black
                );

                s_plainsLeftMat = BuildValheimMaterial(
                    shader,
                    "Mat_PlainsAxe_Left",
                    "PlainsAxe_Left_Albedo.png",
                    "PlainsAxe_Left_Roughness.png",
                    "PlainsAxe_Left_Normal.png",
                    null,
                    baseMetallic: 0.78f,
                    emissionColor: Color.black
                );
            }
        }

        internal static Material BuildValheimMaterial(
            Shader shader,
            string matName,
            string albedoRes,
            string roughnessRes,
            string normalRes,
            string emissionRes,
            float baseMetallic,
            Color emissionColor)
        {
            Material mat = new Material(shader);
            mat.name = matName;

            Texture2D albedo = LoadEmbeddedTexture(albedoRes, false, FilterMode.Point);
            Texture2D rough = LoadEmbeddedTexture(roughnessRes, true, FilterMode.Point);
            Texture2D normal = LoadEmbeddedNormalMap(normalRes, FilterMode.Point);
            Texture2D emiss = !string.IsNullOrEmpty(emissionRes) ? LoadEmbeddedTexture(emissionRes, false, FilterMode.Point) : null;

            Texture2D metGloss = BuildMetallicGlossMap(albedo, rough, baseMetallic);

            mat.color = Color.white;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (albedo != null)
            {
                mat.mainTexture = albedo;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            if (metGloss != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metGloss);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", baseMetallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.55f);

            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (emiss != null)
            {
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emiss);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }

            return mat;
        }

        internal static Texture2D BuildMetallicGlossMap(Texture2D albedo, Texture2D rough, float metalPeak)
        {
            if (rough == null) return null;
            int w = rough.width;
            int h = rough.height;
            Color[] rPx = rough.GetPixels();
            Color[] aPx = (albedo != null && albedo.width == w && albedo.height == h) ? albedo.GetPixels() : null;
            Color[] outPx = new Color[rPx.Length];

            for (int i = 0; i < rPx.Length; i++)
            {
                float roughness = Mathf.Clamp01(rPx[i].r);
                float smoothness = Mathf.Clamp01(1.0f - roughness);
                float metalMask = Mathf.Clamp01((0.62f - roughness) / 0.25f);
                float metallic = metalPeak * metalMask;
                if (aPx != null)
                {
                    Color c = aPx[i];
                    if (c.r > 0.35f && c.g > 0.30f && (c.r - c.b) > 0.12f)
                    {
                        metallic *= 0.15f;
                    }
                }
                outPx[i] = new Color(metallic, metallic, metallic, smoothness);
            }

            Texture2D mg = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            mg.filterMode = FilterMode.Point;
            mg.wrapMode = TextureWrapMode.Clamp;
            mg.SetPixels(outPx);
            mg.Apply();
            return mg;
        }

        internal static Texture2D LoadEmbeddedNormalMap(string resName, FilterMode filter)
        {
            Texture2D tex = LoadEmbeddedTexture(resName, true, filter);
            if (tex == null) return null;
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                px[i] = new Color(c.r, c.g, c.b, 1.0f);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        internal static Texture2D LoadEmbeddedTexture(string resName, bool linear, FilterMode filter)
        {
            try
            {
                byte[] bytes = ReadEmbeddedBytes(resName);
                if (bytes == null) return null;
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
                if (ImageConversion.LoadImage(tex, bytes))
                {
                    tex.name = resName;
                    tex.filterMode = filter;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    return tex;
                }
            }
            catch (Exception ex)
            {
                if (ModLogger != null) ModLogger.LogWarning("Failed to load texture " + resName + ": " + ex.Message);
            }
            return null;
        }

        internal static Sprite LoadEmbeddedSprite(string resName)
        {
            Texture2D tex = LoadEmbeddedTexture(resName, false, FilterMode.Bilinear);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        internal static Mesh LoadEmbeddedMesh(string resName, string meshName)
        {
            byte[] gzBytes = ReadEmbeddedBytes(resName);
            if (gzBytes == null)
            {
                if (ModLogger != null) ModLogger.LogError("Missing embedded mesh resource: " + resName);
                return null;
            }

            using (MemoryStream inMs = new MemoryStream(gzBytes))
            using (GZipStream gz = new GZipStream(inMs, CompressionMode.Decompress))
            using (MemoryStream outMs = new MemoryStream())
            {
                byte[] buf = new byte[16384];
                int read;
                while ((read = gz.Read(buf, 0, buf.Length)) > 0)
                {
                    outMs.Write(buf, 0, read);
                }
                outMs.Position = 0;

                using (BinaryReader br = new BinaryReader(outMs))
                {
                    uint vCount = br.ReadUInt32();
                    uint iCount = br.ReadUInt32();

                    Vector3[] verts = new Vector3[vCount];
                    Vector3[] norms = new Vector3[vCount];
                    Vector2[] uvs = new Vector2[vCount];
                    int[] tris = new int[iCount];

                    for (int i = 0; i < vCount; i++)
                    {
                        verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    }
                    for (int i = 0; i < vCount; i++)
                    {
                        norms[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    }
                    for (int i = 0; i < vCount; i++)
                    {
                        uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    }
                    for (int i = 0; i < iCount; i++)
                    {
                        tris[i] = (int)br.ReadUInt32();
                    }

                    Mesh m = new Mesh();
                    m.name = meshName;
                    if (vCount > 65000)
                    {
                        m.indexFormat = IndexFormat.UInt32;
                    }
                    m.vertices = verts;
                    m.normals = norms;
                    m.uv = uvs;
                    m.triangles = tris;
                    m.RecalculateBounds();
                    m.RecalculateTangents();
                    return m;
                }
            }
        }

        internal static byte[] ReadEmbeddedBytes(string resName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resName))
            {
                if (s == null) return null;
                byte[] data = new byte[s.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int r = s.Read(data, offset, data.Length - offset);
                    if (r <= 0) break;
                    offset += r;
                }
                return data;
            }
        }
    }

    public class DualAxeSkinScaler : MonoBehaviour
    {
        public string PrefabName;
        public float TargetWorldLength = 0.80f;
        public Mesh RightMesh;
        public Material RightMat;
        public Mesh LeftMesh;
        public Material LeftMat;
        public bool AddPoisonGlow;

        private bool m_calibrated;

        public void CalibrateForVisEquipment(VisEquipment vis)
        {
            if (m_calibrated) return;
            m_calibrated = true;

            if (vis == null || vis.m_bodyModel == null) return;

            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) return;

            transform.localScale = Vector3.one;

            // Rebuild against live Player m_bodyModel.bones if boneWorldScale differs from prefab!
            Transform[] liveBones = smr.bones;
            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            if (liveBones != null && bindposes != null && RightMesh != null && LeftMesh != null)
            {
                int rIdx = NewDualAxesPlugin.s_cachedRightBoneIdx;
                int lIdx = NewDualAxesPlugin.s_cachedLeftBoneIdx;
                if (rIdx >= 0 && lIdx >= 0 && rIdx < liveBones.Length && lIdx < liveBones.Length)
                {
                    float rWorldScale = NewDualAxesPlugin.GetBoneWorldScale(liveBones, rIdx);
                    float lWorldScale = NewDualAxesPlugin.GetBoneWorldScale(liveBones, lIdx);

                    Matrix4x4 rightToMesh = NewDualAxesPlugin.BuildHandToMeshMatrix(
                        bindposes[Mathf.Clamp(rIdx, 0, bindposes.Length - 1)],
                        rWorldScale,
                        RightMesh,
                        TargetWorldLength,
                        isRightHand: true,
                        boneTransform: liveBones[rIdx]
                    );
                    Matrix4x4 leftToMesh = NewDualAxesPlugin.BuildHandToMeshMatrix(
                        bindposes[Mathf.Clamp(lIdx, 0, bindposes.Length - 1)],
                        lWorldScale,
                        LeftMesh,
                        TargetWorldLength,
                        isRightHand: false,
                        boneTransform: liveBones[lIdx]
                    );

                    smr.sharedMesh = NewDualAxesPlugin.AssembleDualSkinnedMesh(
                        PrefabName + "_LiveDualSkinnedMesh",
                        RightMesh,
                        rightToMesh,
                        rIdx,
                        LeftMesh,
                        leftToMesh,
                        lIdx,
                        bindposes
                    );
                }
            }

            if (AddPoisonGlow && vis.m_rightHand != null)
            {
                GameObject lightFollow = new GameObject("SwampGuckHandGlow");
                lightFollow.transform.SetParent(transform, false);
                SwampGlowFollower follower = lightFollow.AddComponent<SwampGlowFollower>();
                follower.TargetHand = vis.m_rightHand;
            }
        }
    }

    public class SwampGlowFollower : MonoBehaviour
    {
        public Transform TargetHand;
        private Light m_light;

        private void Awake()
        {
            m_light = gameObject.AddComponent<Light>();
            m_light.type = LightType.Point;
            m_light.color = new Color(0.28f, 0.95f, 0.32f, 1f);
            m_light.range = 1.6f;
            m_light.intensity = 0.45f;
            m_light.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (TargetHand != null)
            {
                transform.position = TargetHand.position;
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
    public static class VisEquipment_AttachItem_Patch
    {
        private static void Postfix(VisEquipment __instance, GameObject __result)
        {
            if (__result == null || __instance == null) return;
            DualAxeSkinScaler scaler = __result.GetComponent<DualAxeSkinScaler>();
            if (scaler != null)
            {
                scaler.CalibrateForVisEquipment(__instance);
            }
            StaffVisualCalibrator staffCal = __result.GetComponent<StaffVisualCalibrator>();
            if (staffCal != null)
            {
                staffCal.CalibrateForVisEquipment(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "AttachBackItem")]
    public static class VisEquipment_AttachBackItem_Patch
    {
        private static void Postfix(VisEquipment __instance, GameObject __result)
        {
            if (__result == null || __instance == null) return;
            StaffVisualCalibrator staffCal = __result.GetComponent<StaffVisualCalibrator>();
            if (staffCal != null)
            {
                staffCal.CalibrateForVisEquipment(__instance);
            }
        }
    }

    /// <summary>
    /// Self-healing equipment safety net:
    /// 1) Ensures every custom weapon & relic prefab in `ObjectDB.instance` is active (`activeSelf == true`) and has `m_dropPrefab` assigned
    ///    so `Inventory.AddItem` / `ItemDrop.Awake` always populates `m_dropPrefab` when crafting or spawning!
    /// 2) Repairs `item.m_dropPrefab` on any already-crafted / saved inventory item if `m_dropPrefab == null` BEFORE `Humanoid.EquipItem`
    ///    or `Humanoid.SetupVisEquipment` runs, AND clears any broken/missing items from removed mods so `Humanoid.SetupVisEquipment`
    ///    NEVER throws `NullReferenceException` before attaching weapon 3D models!
    /// </summary>
    public static class EquipmentDropPrefabRepairHelper
    {
        private static readonly FieldInfo[] s_humanoidSlotFields = new FieldInfo[]
        {
            AccessTools.Field(typeof(Humanoid), "m_leftItem"),
            AccessTools.Field(typeof(Humanoid), "m_rightItem"),
            AccessTools.Field(typeof(Humanoid), "m_hiddenLeftItem"),
            AccessTools.Field(typeof(Humanoid), "m_hiddenRightItem"),
            AccessTools.Field(typeof(Humanoid), "m_chestItem"),
            AccessTools.Field(typeof(Humanoid), "m_legItem"),
            AccessTools.Field(typeof(Humanoid), "m_helmetItem"),
            AccessTools.Field(typeof(Humanoid), "m_shoulderItem"),
            AccessTools.Field(typeof(Humanoid), "m_utilityItem"),
            AccessTools.Field(typeof(Humanoid), "m_trinketItem")
        };

        public static void EnsureCustomPrefabsReadyInObjectDB(ObjectDB odb)
        {
            if (odb == null || odb.m_items == null) return;

            string[] customNames = new string[]
            {
                NewDualAxesPlugin.SwampPrefabName,
                NewDualAxesPlugin.PlainsPrefabName,
                MagicStavesModule.PartCrystalPrefab,
                MagicStavesModule.PartHandlePrefab,
                MagicStavesModule.PartBindingPrefab,
                MagicStavesModule.MeadowsStaffPrefab,
                MagicStavesModule.BlackForestStaffPrefab,
                MagicStavesModule.SwampStaffPrefab,
                MagicStavesModule.MountainStaffPrefab,
                MagicStavesModule.PlainsStaffPrefab,
                EitrFoodsModule.StuffedNeckPrefab,
                EitrFoodsModule.NecroSoupPrefab,
                EitrFoodsModule.SadAuferPrefab,
                EitrFoodsModule.PlainsTartPrefab
            };

            for (int i = 0; i < customNames.Length; i++)
            {
                GameObject p = odb.GetItemPrefab(customNames[i]);
                if (p == null) continue;
                if (!p.activeSelf) p.SetActive(true);
                ItemDrop id = p.GetComponent<ItemDrop>();
                if (id != null && id.m_itemData != null)
                {
                    id.m_itemData.m_dropPrefab = p;
                }
            }

            EitrFoodsModule.EnsureFoodsReadyInObjectDB(odb);
            EnsureForgeOfPotentialUpgraderSupport(odb);
        }

        private struct CustomWeaponUpgraderMapping
        {
            public string CustomPrefab;
            public int IdolTier;
            public string[] DonorWeapons;

            public CustomWeaponUpgraderMapping(string customPrefab, int idolTier, string[] donorWeapons)
            {
                CustomPrefab = customPrefab;
                IdolTier = idolTier;
                DonorWeapons = donorWeapons;
            }
        }

        private static bool s_loggedUpgraderOnce;

        /// <summary>
        /// Registers Valheim 1.0 Forge of Potential (`CraftingStation.m_upgrader == true`)
        /// Battle Idol (`Upgrader0Weapon`..`Upgrader4Weapon`, `Piece.Requirement.m_upgraderResource = true`) support
        /// on all 2 custom dual axes and 5 custom magic staves!
        /// </summary>
        public static void EnsureForgeOfPotentialUpgraderSupport(ObjectDB odb)
        {
            if (odb == null || odb.m_recipes == null || odb.m_items == null) return;

            CustomWeaponUpgraderMapping[] mappings = new CustomWeaponUpgraderMapping[]
            {
                // Meadows Wind Staff -> Tier 0 Battle Idol (Wooden: Upgrader0Weapon)
                new CustomWeaponUpgraderMapping(
                    MagicStavesModule.MeadowsStaffPrefab,
                    0,
                    new string[] { "Club", "SpearFlint", "Bow" }
                ),
                // Black Forest Blood-Dominion Staff -> Tier 1 Battle Idol (Bronze: Upgrader1Weapon)
                new CustomWeaponUpgraderMapping(
                    MagicStavesModule.BlackForestStaffPrefab,
                    1,
                    new string[] { "SwordBronze", "MaceBronze", "AtgeirBronze", "SpearBronze" }
                ),
                // Swamp Miasma Censer Staff -> Tier 2 Battle Idol (Iron: Upgrader2Weapon)
                new CustomWeaponUpgraderMapping(
                    MagicStavesModule.SwampStaffPrefab,
                    2,
                    new string[] { "SwordIron", "MaceIron", "Battleaxe", "SledgeIron" }
                ),
                // Swamp Dual Axes ("Bogroot Twin Axes") -> Tier 2 Battle Idol (Iron: Upgrader2Weapon)
                new CustomWeaponUpgraderMapping(
                    NewDualAxesPlugin.SwampPrefabName,
                    2,
                    new string[] { "SwordIron", "Battleaxe", "MaceIron" }
                ),
                // Mountain Railgun Staff -> Tier 3 Battle Idol (Silver: Upgrader3Weapon)
                new CustomWeaponUpgraderMapping(
                    MagicStavesModule.MountainStaffPrefab,
                    3,
                    new string[] { "SwordSilver", "MaceSilver", "BattleaxeCrystal", "SpearWolfFang", "BowDraugrFang" }
                ),
                // Plains Blood-Ritual Totem Staff -> Tier 4 Battle Idol (Black Metal: Upgrader4Weapon)
                new CustomWeaponUpgraderMapping(
                    MagicStavesModule.PlainsStaffPrefab,
                    4,
                    new string[] { "SwordBlackmetal", "AtgeirBlackmetal", "KnifeBlackMetal", "MaceNeedle" }
                ),
                // Plains Dual Axes ("Berserker's Fang Axes") -> Tier 4 Battle Idol (Black Metal: Upgrader4Weapon)
                new CustomWeaponUpgraderMapping(
                    NewDualAxesPlugin.PlainsPrefabName,
                    4,
                    new string[] { "SwordBlackmetal", "AtgeirBlackmetal", "AxeBerzerkr" }
                )
            };

            int configuredCount = 0;
            for (int i = 0; i < mappings.Length; i++)
            {
                if (ConfigureWeaponUpgraderRecipe(odb, mappings[i]))
                {
                    configuredCount++;
                }
            }

            if (configuredCount > 0 && !s_loggedUpgraderOnce && NewDualAxesPlugin.ModLogger != null)
            {
                s_loggedUpgraderOnce = true;
                NewDualAxesPlugin.ModLogger.LogInfo("[ForgeOfPotential] Enabled Battle Idol upgrades at Forge of Potential for " + configuredCount + " custom weapons.");
            }
        }

        public static bool IsCustomModWeaponToken(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName)) return false;
            return sharedName == "$item_axedual_swamp"
                || sharedName == "$item_axedual_plains"
                || sharedName == "$item_staff_meadows"
                || sharedName == "$item_staff_blackforest"
                || sharedName == "$item_staff_swamp"
                || sharedName == "$item_staff_mountain"
                || sharedName == "$item_staff_plains";
        }

        public static bool IsCustomModWeaponRecipe(Recipe rec)
        {
            if (rec == null || rec.m_item == null || rec.m_item.m_itemData == null || rec.m_item.m_itemData.m_shared == null)
            {
                return false;
            }
            return IsCustomModWeaponToken(rec.m_item.m_itemData.m_shared.m_name);
        }

        private static bool ConfigureWeaponUpgraderRecipe(ObjectDB odb, CustomWeaponUpgraderMapping mapping)
        {
            GameObject customGo = odb.GetItemPrefab(mapping.CustomPrefab);
            if (customGo == null) return false;
            ItemDrop customDrop = customGo.GetComponent<ItemDrop>();
            if (customDrop == null || customDrop.m_itemData == null || customDrop.m_itemData.m_shared == null) return false;

            // Find the main crafting/upgrade recipe for our custom weapon in ObjectDB.m_recipes
            Recipe customMainRecipe = FindMainRecipeForItem(odb, customDrop);
            if (customMainRecipe == null) return false;

            // 1) Copy shared upgrader fields (success/break chance, steps, return ratio) from a pure combat weapon donor
            List<Piece.Requirement> fallbackDonorReqs = new List<Piece.Requirement>();
            if (mapping.DonorWeapons != null)
            {
                for (int d = 0; d < mapping.DonorWeapons.Length; d++)
                {
                    GameObject donorGo = odb.GetItemPrefab(mapping.DonorWeapons[d]);
                    if (donorGo == null) continue;
                    ItemDrop donorDrop = donorGo.GetComponent<ItemDrop>();
                    if (donorDrop == null || donorDrop.m_itemData == null || donorDrop.m_itemData.m_shared == null) continue;

                    CopyWeaponUpgraderSharedFields(donorDrop.m_itemData.m_shared, customDrop.m_itemData.m_shared);

                    for (int r = 0; r < odb.m_recipes.Count; r++)
                    {
                        Recipe rec = odb.m_recipes[r];
                        if (rec == null || rec.m_item == null) continue;
                        if (rec.m_item != donorDrop && rec.m_item.name != donorDrop.name) continue;
                        if (rec.m_resources == null) continue;

                        List<Piece.Requirement> found = ExtractUpgraderRequirements(rec);
                        if (found.Count > 0)
                        {
                            fallbackDonorReqs = found;
                            break;
                        }
                    }

                    if (fallbackDonorReqs.Count > 0) break;
                }
            }

            // 2) PRIMARY: Resolve the exact Battle Idol prefab for `mapping.IdolTier` directly (`Upgrader0Weapon`..`Upgrader4Weapon`)!
            //    - Upgrader0Weapon = Wooden Battle Idol ($item_upgrader_tier0)
            //    - Upgrader1Weapon = Bronze Battle Idol ($item_upgrader_tier1)
            //    - Upgrader2Weapon = Iron Battle Idol ($item_upgrader_tier2)
            //    - Upgrader3Weapon = Silver Battle Idol ($item_upgrader_tier3)
            //    - Upgrader4Weapon = Black Metal Battle Idol ($item_upgrader_tier4)
            List<Piece.Requirement> targetUpgraderReqs = new List<Piece.Requirement>();
            string idolPrefabName = "Upgrader" + mapping.IdolTier + "Weapon";
            GameObject idolGo = odb.GetItemPrefab(idolPrefabName);
            if (idolGo == null && PrefabManager.Instance != null)
            {
                idolGo = PrefabManager.Instance.GetPrefab(idolPrefabName);
            }
            if (idolGo != null)
            {
                ItemDrop idolDrop = idolGo.GetComponent<ItemDrop>();
                if (idolDrop != null)
                {
                    Piece.Requirement req = new Piece.Requirement();
                    req.m_resItem = idolDrop;
                    req.m_amount = 1;
                    req.m_amountPerLevel = 0;
                    req.m_extraAmountOnlyOneIngredient = 0;
                    req.m_recover = false;
                    req.m_upgraderResource = true;
                    targetUpgraderReqs.Add(req);
                }
            }

            if (targetUpgraderReqs.Count == 0)
            {
                targetUpgraderReqs = fallbackDonorReqs;
            }

            if (targetUpgraderReqs.Count == 0) return false;

            // 3) Standard Valheim 1.0 pattern: `m_upgraderResource = true` lives inside `customMainRecipe.m_resources`.
            // Strip any previous `m_upgraderResource == true` entries first so our exact tier idol is the ONLY idol requirement!
            List<Piece.Requirement> merged = new List<Piece.Requirement>();
            if (customMainRecipe.m_resources != null)
            {
                for (int i = 0; i < customMainRecipe.m_resources.Length; i++)
                {
                    Piece.Requirement existing = customMainRecipe.m_resources[i];
                    if (existing != null && existing.m_resItem != null && !existing.m_upgraderResource)
                    {
                        merged.Add(existing);
                    }
                }
            }

            for (int i = 0; i < targetUpgraderReqs.Count; i++)
            {
                Piece.Requirement upgReq = targetUpgraderReqs[i];
                if (upgReq == null || upgReq.m_resItem == null) continue;
                merged.Add(CloneSingleRequirement(upgReq));
            }

            customMainRecipe.m_resources = merged.ToArray();

            if (!s_loggedUpgraderOnce && NewDualAxesPlugin.ModLogger != null && targetUpgraderReqs[0].m_resItem != null)
            {
                NewDualAxesPlugin.ModLogger.LogInfo(string.Format(
                    "[ForgeOfPotential] {0} -> idol '{1}' (tier {2})",
                    mapping.CustomPrefab,
                    targetUpgraderReqs[0].m_resItem.name,
                    mapping.IdolTier));
            }

            return HasAnyUpgraderResource(customMainRecipe);
        }

        private static Recipe FindMainRecipeForItem(ObjectDB odb, ItemDrop targetDrop)
        {
            string sharedName = (targetDrop.m_itemData != null && targetDrop.m_itemData.m_shared != null)
                ? targetDrop.m_itemData.m_shared.m_name
                : null;

            for (int i = 0; i < odb.m_recipes.Count; i++)
            {
                Recipe rec = odb.m_recipes[i];
                if (rec == null || rec.m_item == null || rec.m_noCraftOnlyUpgrade) continue;
                if (rec.m_item == targetDrop || rec.m_item.name == targetDrop.name) return rec;
                if (!string.IsNullOrEmpty(sharedName) &&
                    rec.m_item.m_itemData != null &&
                    rec.m_item.m_itemData.m_shared != null &&
                    rec.m_item.m_itemData.m_shared.m_name == sharedName)
                {
                    return rec;
                }
            }
            return null;
        }

        private static List<Piece.Requirement> ExtractUpgraderRequirements(Recipe rec)
        {
            List<Piece.Requirement> list = new List<Piece.Requirement>();
            if (rec == null || rec.m_resources == null) return list;
            for (int i = 0; i < rec.m_resources.Length; i++)
            {
                Piece.Requirement req = rec.m_resources[i];
                if (req != null && req.m_upgraderResource && req.m_resItem != null)
                {
                    list.Add(CloneSingleRequirement(req));
                }
            }
            return list;
        }

        private static bool HasAnyUpgraderResource(Recipe rec)
        {
            if (rec == null || rec.m_resources == null) return false;
            for (int i = 0; i < rec.m_resources.Length; i++)
            {
                if (rec.m_resources[i] != null && rec.m_resources[i].m_upgraderResource && rec.m_resources[i].m_resItem != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static Piece.Requirement CloneSingleRequirement(Piece.Requirement src)
        {
            Piece.Requirement copy = new Piece.Requirement();
            copy.m_resItem = src.m_resItem;
            copy.m_amount = (src.m_amount > 0) ? src.m_amount : 1;
            copy.m_amountPerLevel = src.m_amountPerLevel;
            copy.m_extraAmountOnlyOneIngredient = src.m_extraAmountOnlyOneIngredient;
            copy.m_recover = false;
            copy.m_upgraderResource = true;
            return copy;
        }

        private static List<Piece.Requirement> CloneRequirementsList(List<Piece.Requirement> srcList)
        {
            List<Piece.Requirement> res = new List<Piece.Requirement>(srcList.Count);
            for (int i = 0; i < srcList.Count; i++)
            {
                res.Add(CloneSingleRequirement(srcList[i]));
            }
            return res;
        }

        private static void CopyWeaponUpgraderSharedFields(ItemDrop.ItemData.SharedData donor, ItemDrop.ItemData.SharedData dst)
        {
            if (donor == null || dst == null) return;
            if (donor.m_upgradeChance > 0f) dst.m_upgradeChance = donor.m_upgradeChance;
            if (donor.m_breakChance > 0f) dst.m_breakChance = donor.m_breakChance;
            if (donor.m_successUpgradeSteps > 0) dst.m_successUpgradeSteps = donor.m_successUpgradeSteps;
            if (donor.m_breakReturnIngreientsAmount > 0f) dst.m_breakReturnIngreientsAmount = donor.m_breakReturnIngreientsAmount;
        }

        public static GameObject ResolveDropPrefabForItem(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            if (item.m_dropPrefab != null) return item.m_dropPrefab;
            if (ObjectDB.instance == null || item.m_shared == null) return null;

            string sharedName = item.m_shared.m_name;
            if (string.IsNullOrEmpty(sharedName)) return null;

            // Fast exact token map for our 10 custom items
            string exactPrefabName = null;
            if (sharedName == "$item_axedual_swamp") exactPrefabName = NewDualAxesPlugin.SwampPrefabName;
            else if (sharedName == "$item_axedual_plains") exactPrefabName = NewDualAxesPlugin.PlainsPrefabName;
            else if (sharedName == "$item_part_windcrystal") exactPrefabName = MagicStavesModule.PartCrystalPrefab;
            else if (sharedName == "$item_part_carvedhandle") exactPrefabName = MagicStavesModule.PartHandlePrefab;
            else if (sharedName == "$item_part_runicbinding") exactPrefabName = MagicStavesModule.PartBindingPrefab;
            else if (sharedName == "$item_staff_meadows") exactPrefabName = MagicStavesModule.MeadowsStaffPrefab;
            else if (sharedName == "$item_staff_blackforest") exactPrefabName = MagicStavesModule.BlackForestStaffPrefab;
            else if (sharedName == "$item_staff_swamp") exactPrefabName = MagicStavesModule.SwampStaffPrefab;
            else if (sharedName == "$item_staff_mountain") exactPrefabName = MagicStavesModule.MountainStaffPrefab;
            else if (sharedName == "$item_staff_plains") exactPrefabName = MagicStavesModule.PlainsStaffPrefab;
            else if (sharedName == "$item_food_stuffedneck") exactPrefabName = EitrFoodsModule.StuffedNeckPrefab;
            else if (sharedName == "$item_food_necrosoup") exactPrefabName = EitrFoodsModule.NecroSoupPrefab;
            else if (sharedName == "$item_food_sadaufer") exactPrefabName = EitrFoodsModule.SadAuferPrefab;
            else if (sharedName == "$item_food_plainstart") exactPrefabName = EitrFoodsModule.PlainsTartPrefab;

            if (exactPrefabName != null)
            {
                GameObject p = ObjectDB.instance.GetItemPrefab(exactPrefabName);
                if (p != null)
                {
                    item.m_dropPrefab = p;
                    return p;
                }
            }

            // General fallback across ObjectDB.instance.m_items by matching `m_shared.m_name`
            List<GameObject> allItems = ObjectDB.instance.m_items;
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    GameObject cand = allItems[i];
                    if (cand == null) continue;
                    ItemDrop id = cand.GetComponent<ItemDrop>();
                    if (id != null && id.m_itemData != null && id.m_itemData.m_shared != null &&
                        id.m_itemData.m_shared.m_name == sharedName)
                    {
                        item.m_dropPrefab = cand;
                        return cand;
                    }
                }
            }

            return null;
        }

        public static void RepairInventoryItems(Inventory inv)
        {
            if (inv == null) return;
            List<ItemDrop.ItemData> list = inv.GetAllItems();
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                ItemDrop.ItemData it = list[i];
                if (it != null && it.m_dropPrefab == null)
                {
                    ResolveDropPrefabForItem(it);
                }
            }

            EitrFoodsModule.SyncInventoryVanillaFoodEitr(inv);
        }

        public static void SanitizeHumanoidEquipmentSlots(Humanoid h)
        {
            if (h == null) return;
            for (int i = 0; i < s_humanoidSlotFields.Length; i++)
            {
                FieldInfo fi = s_humanoidSlotFields[i];
                if (fi == null) continue;
                ItemDrop.ItemData slotItem = fi.GetValue(h) as ItemDrop.ItemData;
                if (slotItem != null && slotItem.m_dropPrefab == null)
                {
                    GameObject resolved = ResolveDropPrefabForItem(slotItem);
                    if (resolved == null)
                    {
                        // Orphaned item from an uninstalled mod whose m_dropPrefab is null: clear slot reference so SetupVisEquipment never crashes!
                        slotItem.m_equipped = false;
                        fi.SetValue(h, null);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDB_Awake_EnsureCustomPrefabs_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ObjectDB __instance)
        {
            EquipmentDropPrefabRepairHelper.EnsureCustomPrefabsReadyInObjectDB(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    public static class ObjectDB_CopyOtherDB_EnsureCustomPrefabs_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ObjectDB __instance)
        {
            EquipmentDropPrefabRepairHelper.EnsureCustomPrefabsReadyInObjectDB(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), "Load", new Type[] { typeof(ZPackage), typeof(bool) })]
    public static class Inventory_Load_RepairDropPrefabs_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Inventory __instance)
        {
            EquipmentDropPrefabRepairHelper.RepairInventoryItems(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), "Load", new Type[] { typeof(ZPackage) })]
    public static class Inventory_LoadSingle_RepairDropPrefabs_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Inventory __instance)
        {
            EquipmentDropPrefabRepairHelper.RepairInventoryItems(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData) })]
    public static class Inventory_AddItemData_EnsureDropPrefab_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ItemDrop.ItemData item)
        {
            if (item != null && item.m_dropPrefab == null)
            {
                EquipmentDropPrefabRepairHelper.ResolveDropPrefabForItem(item);
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
    public static class Inventory_AddItemSlot_EnsureDropPrefab_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ItemDrop.ItemData item)
        {
            if (item != null && item.m_dropPrefab == null)
            {
                EquipmentDropPrefabRepairHelper.ResolveDropPrefabForItem(item);
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    public static class Humanoid_EquipItem_RepairDropPrefab_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (item != null && item.m_dropPrefab == null)
            {
                EquipmentDropPrefabRepairHelper.ResolveDropPrefabForItem(item);
            }
            EquipmentDropPrefabRepairHelper.SanitizeHumanoidEquipmentSlots(__instance);
        }
    }

    [HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
    public static class Humanoid_SetupVisEquipment_SafeGuard_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance)
        {
            EquipmentDropPrefabRepairHelper.SanitizeHumanoidEquipmentSlots(__instance);
        }
    }

    /// <summary>
    /// Ensures that whenever a player already possesses any of our 5 custom magic staves or 2 custom dual axes in their inventory
    /// (whether spawned via console on a test character, loaded from a save, or traded on a multiplayer server without having unlocked
    /// every raw crafting ingredient in `Player.m_knownRecipes`), its recipe is automatically recognized in `GetAvailableRecipes`
    /// so it ALWAYS appears in the Upgrade list at both Forge of Potential (`m_upgrader == true`) and Forge/Workbench!
    /// </summary>
    [HarmonyPatch(typeof(Player), "GetAvailableRecipes")]
    public static class Player_GetAvailableRecipes_CustomWeapons_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            Player __instance,
            ref List<Recipe> available,
            HashSet<string> ___m_knownRecipes,
            bool ___m_noPlacementCost)
        {
            if (__instance == null || available == null || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null)
            {
                return;
            }

            Inventory inv = __instance.GetInventory();
            if (inv == null) return;

            EquipmentDropPrefabRepairHelper.EnsureForgeOfPotentialUpgraderSupport(ObjectDB.instance);

            List<Recipe> allRecipes = ObjectDB.instance.m_recipes;
            for (int i = 0; i < allRecipes.Count; i++)
            {
                Recipe rec = allRecipes[i];
                if (rec == null || !rec.m_enabled || !EquipmentDropPrefabRepairHelper.IsCustomModWeaponRecipe(rec))
                {
                    continue;
                }

                string sharedName = rec.m_item.m_itemData.m_shared.m_name;
                if (!inv.HaveItem(sharedName))
                {
                    continue;
                }

                if (___m_knownRecipes != null && !___m_knownRecipes.Contains(sharedName))
                {
                    ___m_knownRecipes.Add(sharedName);
                }

                if (!available.Contains(rec) && (__instance.RequiredCraftingStation(rec, 1, false) || ___m_noPlacementCost))
                {
                    available.Add(rec);
                }
            }
        }
    }

    /// <summary>
    /// Fixes vanilla Valheim 1.0's recipe-discovery quirk where standing near a Workbench/Forge (`currentStation != null`)
    /// during `UpdateKnownRecipesList` (`discover == true`) checks `m_upgraderResource` Battle Idols before unlocking the base recipe.
    /// </summary>
    [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
    public static class Player_HaveRequirementItems_IgnoreUpgraderOnDiscover_Patch
    {
        private static void Postfix(Player __instance, Recipe piece, bool discover, ref bool __result)
        {
            if (!discover || __result || __instance == null || !EquipmentDropPrefabRepairHelper.IsCustomModWeaponRecipe(piece))
            {
                return;
            }

            if (piece.m_resources == null) return;
            for (int i = 0; i < piece.m_resources.Length; i++)
            {
                Piece.Requirement req = piece.m_resources[i];
                if (req == null || req.m_upgraderResource || req.m_resItem == null || req.m_amount <= 0)
                {
                    continue;
                }
                if (req.m_resItem.m_itemData == null || req.m_resItem.m_itemData.m_shared == null)
                {
                    continue;
                }
                if (!__instance.IsKnownMaterial(req.m_resItem.m_itemData.m_shared.m_name))
                {
                    return;
                }
            }

            __result = true;
        }
    }
}
