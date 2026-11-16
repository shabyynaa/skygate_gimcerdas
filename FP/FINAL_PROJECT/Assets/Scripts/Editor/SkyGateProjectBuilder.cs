#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;
using SkyGate.Audio;
using SkyGate.Combat;
using SkyGate.Core;
using SkyGate.DDA;
using SkyGate.EnemyAI;
using SkyGate.EnemyAI.Behaviors;
using SkyGate.MathGate;
using SkyGate.PCG;
using SkyGate.Player;
using SkyGate.UI;
using SkyGate.World;
using SkyGate.World.Challenges;
using SkyGate.World.Interactables;

namespace SkyGate.EditorTools
{
    public static class SkyGateProjectBuilder
    {
        private static TMP_FontAsset cachedDefaultFont;

        [MenuItem("SkyGate/Build Entire Project (M0-M5)")]
        public static void BuildAll()
        {
            Debug.Log("[SkyGateBuilder] Starting full M0-M5 asset, prefab, and scene build (Total Visual Overhaul)...");
            EnsureDirectories();
            CleanupObsoleteAssets();
            EnsureTMPResources();
            ConfigurePhysicsMatrix();
            ConfigureAssetImporters();

            // 1. Materials & Stylized Skybox
            var mats = CreateAllMaterials();

            // 2. Humanoid Animator Controllers (KayKit Rig_Medium & Rig_Large)
            AnimatorController playerAc = CreatePlayerAnimatorController();
            var enemyAcs = new Dictionary<string, AnimatorController>
            {
                ["BreezeImp"] = CreateEnemyAnimatorController("AC_BreezeImp", isLargeRig: false, role: EnemyAnimRole.MeleeDual),
                ["SkySprite"] = CreateEnemyAnimatorController("AC_SkySprite", isLargeRig: false, role: EnemyAnimRole.RangedCrossbow),
                ["DuskWisp"] = CreateEnemyAnimatorController("AC_DuskWisp", isLargeRig: false, role: EnemyAnimRole.MageStaff),
                ["CrystalGolem"] = CreateEnemyAnimatorController("AC_CrystalGolem", isLargeRig: true, role: EnemyAnimRole.HeavySlam),
                ["ZephyrGuardian"] = CreateEnemyAnimatorController("AC_ZephyrGuardian", isLargeRig: true, role: EnemyAnimRole.BossWarden)
            };

            // 3. ScriptableObjects (Enemy Archetypes & MathGates)
            var archetypes = CreateEnemyArchetypes();
            CreateMathGatePresetSO();

            // 4. Environment, Gameplay, Enemy & Core Prefabs
            var envPrefabs = CreateEnvironmentPrefabs(mats);
            var gameplayPrefabs = CreateGameplayPrefabs(mats);
            ProjectileController projPrefab = CreateProjectilePrefab(mats);
            var enemyPrefabs = CreateEnemyPrefabs(mats, enemyAcs, archetypes, projPrefab);
            PCGConfigSO pcgConfig = CreatePCGConfigSO(envPrefabs, gameplayPrefabs, enemyPrefabs, archetypes);
            TMP_Text floatingPopupPrefab = CreateFloatingDamagePopupPrefab();
            var corePrefabs = CreateCorePrefabs(mats, playerAc, pcgConfig, floatingPopupPrefab);

            // 5. Build all 4 Scenes & register in Build Settings
            CreateAssetShowcaseScene(mats, envPrefabs, gameplayPrefabs, enemyPrefabs, corePrefabs);
            CreateCombatSandboxScene(mats, envPrefabs, gameplayPrefabs, enemyPrefabs, corePrefabs, archetypes);
            CreateGameplayScene(mats, corePrefabs);
            CreateMainMenuScene(mats, envPrefabs, gameplayPrefabs, enemyPrefabs, corePrefabs);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SkyGateBuilder] Full M0-M5 Total Visual Overhaul build completed successfully!");
        }

        [MenuItem("SkyGate/Rebuild MainMenu Only")]
        public static void RebuildMainMenuOnly()
        {
            EnsureDirectories();
            EnsureTMPResources();
            var mats = CreateAllMaterials();
            var envPrefabs = new Dictionary<string, GameObject>
            {
                ["IslandSlab"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/IslandSlab_Base.prefab"),
                ["BackdropWindmill"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Backdrop_Windmill.prefab"),
                ["BackdropFountain"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Backdrop_Fountain.prefab"),
                ["BackdropArch"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Backdrop_Arch.prefab"),
                ["BackdropTree"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Backdrop_PineCluster.prefab")
            };
            var gameplayPrefabs = new Dictionary<string, GameObject>
            {
                ["SunBeacon"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/SunBeacon.prefab"),
                ["MathGateDoor"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/MathGate_Door.prefab")
            };
            var enemyPrefabs = new Dictionary<string, GameObject>
            {
                ["BreezeImp"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_BreezeImp.prefab"),
                ["SkySprite"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_SkySprite.prefab")
            };
            var corePrefabs = new Dictionary<string, GameObject>
            {
                ["Player"] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Core/Player.prefab")
            };
            CreateMainMenuScene(mats, envPrefabs, gameplayPrefabs, enemyPrefabs, corePrefabs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SkyGateBuilder] Rebuilt MainMenu scene cleanly.");
        }

        [MenuItem("SkyGate/Simulate PlayMode Room Clear & Export Telemetry")]
        public static void SimulatePlayModeVerification()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SkyGateBuilder] Must be in PlayMode to run verification.");
                return;
            }

            BranchingIslandGenerator gen = Object.FindAnyObjectByType<BranchingIslandGenerator>();
            if (gen != null && gen.SpawnedIslands.Count > 0)
            {
                IslandNode startIsland = gen.SpawnedIslands[0];
                var enemies = new List<EnemyBrain>(startIsland.SpawnedEnemies);
                foreach (EnemyBrain eb in enemies)
                {
                    if (eb != null && eb.Health != null && eb.Health.IsAlive)
                    {
                        eb.Health.TakeDamage(new DamageInfo
                        {
                            amount = 999f,
                            sourcePosition = eb.transform.position,
                            knockbackDirection = Vector3.forward,
                            knockbackForce = 2f,
                            isCritical = true,
                            bypassArmor = true
                        });
                    }
                }
            }
        }

        private static void EnsureTMPResources()
        {
            cachedDefaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            if (cachedDefaultFont == null)
            {
                AssetDatabase.Refresh();
                cachedDefaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }
        }

        private static void EnsureDirectories()
        {
            string[] dirs = new string[]
            {
                "Assets/Materials",
                "Assets/Animations/Player",
                "Assets/Animations/Enemies",
                "Assets/Data/Enemies",
                "Assets/Data/MathGates",
                "Assets/Data/PCG",
                "Assets/Prefabs/Core",
                "Assets/Prefabs/Environment",
                "Assets/Prefabs/Gameplay",
                "Assets/Prefabs/Enemies",
                "Assets/Scenes"
            };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        private static void ConfigurePhysicsMatrix()
        {
            // ASSET_CONTRACT.md Section 4.2 Physics Matrix
            int ground = LayerMask.NameToLayer("Ground");
            int wall = LayerMask.NameToLayer("Wall");
            int obstacle = LayerMask.NameToLayer("Obstacle");
            int player = LayerMask.NameToLayer("Player");
            int enemy = LayerMask.NameToLayer("Enemy");
            int enemyProj = LayerMask.NameToLayer("EnemyProjectile");
            int triggerZone = LayerMask.NameToLayer("TriggerZone");
            int bgProp = LayerMask.NameToLayer("BackgroundProp");

            if (enemyProj >= 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    bool shouldCollide = (i == wall || i == obstacle || i == player);
                    Physics.IgnoreLayerCollision(enemyProj, i, !shouldCollide);
                }
            }

            if (bgProp >= 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    Physics.IgnoreLayerCollision(bgProp, i, true);
                }
            }

            if (triggerZone >= 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    bool shouldTrigger = (i == player);
                    Physics.IgnoreLayerCollision(triggerZone, i, !shouldTrigger);
                }
            }
        }

        private enum EnemyAnimRole
        {
            MeleeDual,
            RangedCrossbow,
            MageStaff,
            HeavySlam,
            BossWarden
        }

        private static void CleanupObsoleteAssets()
        {
            string[] obsoleteMaterials = new string[]
            {
                "Assets/Materials/MAT_Palette_Main.mat",
                "Assets/Materials/MAT_Platformer_Main.mat",
                "Assets/Materials/MAT_DungeonProp.mat",
                "Assets/Materials/MAT_Enemy_BreezeImp.mat",
                "Assets/Materials/MAT_Enemy_SkySprite.mat",
                "Assets/Materials/MAT_Enemy_DuskWisp.mat",
                "Assets/Materials/MAT_Enemy_CrystalGolem.mat",
                "Assets/Materials/MAT_Enemy_ZephyrGuardian.mat"
            };
            foreach (string matPath in obsoleteMaterials)
            {
                if (File.Exists(matPath))
                    AssetDatabase.DeleteAsset(matPath);
            }
        }

        private static bool IsHumanoidRigOrCharacterFbx(string assetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (fileName.StartsWith("Rig_Medium_") || fileName.StartsWith("Rig_Large_"))
                return true;

            switch (fileName)
            {
                case "Paladin":
                case "Paladin_with_Helmet":
                case "Tiefling":
                case "AvianSwordsman":
                case "Necromancer":
                case "Skeleton_Mage":
                case "FrostGolem":
                case "Skeleton_Golem":
                case "BlackKnight":
                    return true;
                default:
                    return false;
            }
        }

        private static void ConfigureAssetImporters()
        {
            // 1. Character & Rig FBXs -> Humanoid Avatar + clean clip looping
            string[] animFolders = new string[] { "Assets/Art/Characters", "Assets/Art/Enemies" };
            foreach (string folder in animFolders)
            {
                if (!Directory.Exists(folder)) continue;
                string[] fbxFiles = Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly);
                foreach (string fbx in fbxFiles)
                {
                    string assetPath = fbx.Replace("\\", "/");
                    ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (IsHumanoidRigOrCharacterFbx(assetPath))
                    {
                        if (importer.animationType != ModelImporterAnimationType.Human)
                        {
                            importer.animationType = ModelImporterAnimationType.Human;
                            changed = true;
                        }
                        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                        {
                            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                            changed = true;
                        }
                    }

                    if (importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0)
                    {
                        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                        for (int i = 0; i < clips.Length; i++)
                        {
                            string lower = clips[i].name.ToLowerInvariant();
                            if (lower.Contains("idle") || lower.Contains("run") || lower.Contains("walk") ||
                                lower.Contains("strafe") || lower.Contains("blocking") || lower.Contains("spin"))
                            {
                                if (!clips[i].loopTime)
                                {
                                    clips[i].loopTime = true;
                                    changed = true;
                                }
                            }
                        }
                        if (changed)
                            importer.clipAnimations = clips;
                    }

                    if (changed)
                        importer.SaveAndReimport();
                }
            }

            // 2. Skyboxes -> Clamp wrap mode, no mipmaps for seamless horizons
            if (Directory.Exists("Assets/Art/Skyboxes"))
            {
                foreach (string png in Directory.GetFiles("Assets/Art/Skyboxes", "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = png.Replace("\\", "/");
                    TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (ti != null && (ti.wrapMode != TextureWrapMode.Clamp || ti.mipmapEnabled))
                    {
                        ti.wrapMode = TextureWrapMode.Clamp;
                        ti.mipmapEnabled = false;
                        ti.SaveAndReimport();
                    }
                }
            }

            // 3. UI Sprites -> Sprite import + clean 9-slice borders on buttons/slabs
            if (Directory.Exists("Assets/Art/UI"))
            {
                foreach (string png in Directory.GetFiles("Assets/Art/UI", "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = png.Replace("\\", "/");
                    TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (ti == null) continue;

                    string fname = Path.GetFileNameWithoutExtension(assetPath);
                    Vector4 targetBorder = Vector4.zero;
                    if (fname == "ui_btn")
                        targetBorder = new Vector4(44f, 44f, 44f, 44f);
                    else if (fname == "ui_stone_slab")
                        targetBorder = new Vector4(64f, 64f, 64f, 64f);

                    bool needsReimport = ti.textureType != TextureImporterType.Sprite ||
                                         ti.spriteImportMode != SpriteImportMode.Single ||
                                         (targetBorder != Vector4.zero && ti.spriteBorder != targetBorder);
                    if (needsReimport)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.mipmapEnabled = false;
                        if (targetBorder != Vector4.zero)
                            ti.spriteBorder = targetBorder;
                        ti.SaveAndReimport();
                    }
                }
            }
        }

        private static Dictionary<string, Material> CreateAllMaterials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Texture2D dungeonTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/DungeonRemastered/dungeon_texture.png");
            Texture2D hexTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/MedievalHex/hexagons_medieval.png");
            Texture2D halloweenTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/HalloweenBits/halloweenbits_texture.png");
            Texture2D rangerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Enemies/ranger_texture.png");
            Texture2D paladinTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/paladin_texture_A.png");
            Texture2D knightTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/knight_texture.png");

            // 6-Sided CloudyCrown Midday Skybox
            Shader skyShader = Shader.Find("Skybox/6 Sided");
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MAT_Skybox_Midday.mat");
            if (skyMat == null)
            {
                skyMat = new Material(skyShader);
                AssetDatabase.CreateAsset(skyMat, "Assets/Materials/MAT_Skybox_Midday.mat");
            }
            else if (skyShader != null)
            {
                skyMat.shader = skyShader;
            }
            skyMat.SetTexture("_FrontTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Front.png"));
            skyMat.SetTexture("_BackTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Back.png"));
            skyMat.SetTexture("_LeftTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Left.png"));
            skyMat.SetTexture("_RightTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Right.png"));
            skyMat.SetTexture("_UpTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Up.png"));
            skyMat.SetTexture("_DownTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Midday/CloudyCrown_Midday_Down.png"));
            skyMat.SetColor("_Tint", new Color(0.56f, 0.82f, 0.99f, 1f));
            skyMat.SetFloat("_Exposure", 0.96f);
            skyMat.SetFloat("_Rotation", 25f);
            EditorUtility.SetDirty(skyMat);
            dict["SkyboxMidday"] = skyMat;

            // 6-Sided CloudyCrown Sunset Skybox (Moon Tribe cobalt-to-lavender/peach horizon feel)
            Material skySunset = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MAT_Skybox_Sunset.mat");
            if (skySunset == null)
            {
                skySunset = new Material(skyShader);
                AssetDatabase.CreateAsset(skySunset, "Assets/Materials/MAT_Skybox_Sunset.mat");
            }
            else if (skyShader != null)
            {
                skySunset.shader = skyShader;
            }
            skySunset.SetTexture("_FrontTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Front.png"));
            skySunset.SetTexture("_BackTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Back.png"));
            skySunset.SetTexture("_LeftTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Left.png"));
            skySunset.SetTexture("_RightTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Right.png"));
            skySunset.SetTexture("_UpTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Up.png"));
            skySunset.SetTexture("_DownTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Skyboxes/Sunset/CloudyCrown_Sunset_Down.png"));
            skySunset.SetColor("_Tint", new Color(0.76f, 0.78f, 0.88f, 1f));
            skySunset.SetFloat("_Exposure", 1.18f);
            skySunset.SetFloat("_Rotation", 35f);
            EditorUtility.SetDirty(skySunset);
            dict["SkyboxSunset"] = skySunset;

            // Core Stylized Asset Pack Atlas Materials
            dict["Dungeon"] = CreateOrUpdateMat("Assets/Materials/MAT_DungeonRemastered.mat", urpLit, Color.white, 0.22f, dungeonTex);
            dict["MedievalHex"] = CreateOrUpdateMat("Assets/Materials/MAT_MedievalHex.mat", urpLit, Color.white, 0.14f, hexTex);
            dict["HalloweenBits"] = CreateOrUpdateMat("Assets/Materials/MAT_HalloweenBits.mat", urpLit, Color.white, 0.18f, halloweenTex);
            dict["Weapons"] = CreateOrUpdateMat("Assets/Materials/MAT_AdventurerWeapons.mat", urpLit, Color.white, 0.35f, rangerTex);
            dict["Weapons"].SetFloat("_Metallic", 0.25f);
            dict["PlayerHero"] = CreateOrUpdateMat("Assets/Materials/MAT_Player_Hero.mat", urpLit, Color.white, 0.25f, paladinTex != null ? paladinTex : knightTex);

            // Stylized Architectural & Terrain Tints (Moon Tribe vibrant lime grass & terracotta earth)
            dict["CloudWhite"] = CreateOrUpdateMat("Assets/Materials/MAT_CloudWhite.mat", urpLit, HexColor("#FAF8FF"), 0.05f, null, new Color(0.22f, 0.20f, 0.28f));
            dict["EmeraldGrass"] = CreateOrUpdateMat("Assets/Materials/MAT_EmeraldGrass.mat", urpLit, HexColor("#73D028"), 0.08f);
            dict["CreamStone"] = CreateOrUpdateMat("Assets/Materials/MAT_CreamStone.mat", urpLit, HexColor("#EDE3CF"), 0.14f);
            dict["TerracottaCliff"] = CreateOrUpdateMat("Assets/Materials/MAT_TerracottaCliff.mat", urpLit, HexColor("#A45D37"), 0.10f);
            dict["BridgeStone"] = CreateOrUpdateMat("Assets/Materials/MAT_BridgeStone.mat", urpLit, HexColor("#EFE7D6"), 0.18f, dungeonTex);
            dict["BlueSlateRoof"] = CreateOrUpdateMat("Assets/Materials/MAT_BlueSlateRoof.mat", urpLit, HexColor("#2D6CDF"), 0.28f);
            dict["GoldTrim"] = CreateOrUpdateMat("Assets/Materials/MAT_GoldTrim.mat", urpLit, HexColor("#FFD166"), 0.72f, null, HexColor("#FFB703") * 0.22f);
            dict["GoldTrim"].SetFloat("_Metallic", 0.65f);

            // Quaternius Stylized Nature & KayKit Forest Materials (CC0)
            Texture2D barkTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Bark_NormalTree.png");
            Texture2D barkNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Bark_NormalTree_Normal.png");
            Texture2D leavesNormTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Leaves_NormalTree_C.png");
            Texture2D leavesGenericTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Leaves.png");
            Texture2D flowersTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Flowers.png");
            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Grass.png");
            Texture2D rocksDesertTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Rocks_Desert_Diffuse.png");
            Texture2D rocksMossyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Rocks_Diffuse.png");
            Texture2D pathRocksTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/PathRocks_Diffuse.png");
            Texture2D mushTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Mushrooms.png");
            Texture2D pineLeafTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/StylizedNature/Textures/Leaf_Pine_C.png");
            Texture2D kayForestTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/KayKitForest/forest_texture.png");

            dict["NatureBark"] = CreateNatureMat("Assets/Materials/MAT_Nature_Bark.mat", urpLit, Color.white, 0.12f, barkTex, barkNorm, false);
            dict["NatureLeaves"] = CreateNatureMat("Assets/Materials/MAT_Nature_Leaves_Lime.mat", urpLit, HexColor("#D8FF6B"), 0.08f, leavesNormTex, null, true);
            dict["NatureLeavesGeneric"] = CreateNatureMat("Assets/Materials/MAT_Nature_Leaves_Generic.mat", urpLit, HexColor("#B6F54C"), 0.08f, leavesGenericTex, null, true);
            dict["NaturePine"] = CreateNatureMat("Assets/Materials/MAT_Nature_Pine.mat", urpLit, HexColor("#B2F449"), 0.08f, pineLeafTex, null, true);
            dict["NatureMushroom"] = CreateNatureMat("Assets/Materials/MAT_Nature_Mushroom.mat", urpLit, Color.white, 0.14f, mushTex, null, false);
            dict["NatureFlowers"] = CreateNatureMat("Assets/Materials/MAT_Nature_Flowers.mat", urpLit, Color.white, 0.12f, flowersTex, null, true);
            dict["NatureGrass"] = CreateNatureMat("Assets/Materials/MAT_Nature_Grass.mat", urpLit, HexColor("#99EB3B"), 0.08f, grassTex, null, false, true);
            dict["NatureRockTerracotta"] = CreateOrUpdateMat("Assets/Materials/MAT_Nature_Rock_Terracotta.mat", urpLit, HexColor("#E89B72"), 0.12f, rocksDesertTex);
            dict["NatureRockMossy"] = CreateOrUpdateMat("Assets/Materials/MAT_Nature_Rock_Mossy.mat", urpLit, Color.white, 0.14f, rocksMossyTex);
            dict["NaturePathRocks"] = CreateOrUpdateMat("Assets/Materials/MAT_Nature_PathRocks.mat", urpLit, HexColor("#F6EFE2"), 0.16f, pathRocksTex);
            dict["KayKitForest"] = CreateOrUpdateMat("Assets/Materials/MAT_KayKit_Forest.mat", urpLit, Color.white, 0.14f, kayForestTex);
            dict["StreamWater"] = CreateOrUpdateMat("Assets/Materials/MAT_Menu_StreamWater.mat", urpLit, new Color(0.24f, 0.85f, 1.00f, 0.90f), 0.72f, null, new Color(0.14f, 0.65f, 0.92f) * 0.45f);

            // Shuriken Sky Islands URP Lit Material Palette
            BuildSkyIslandMaterials(dict, urpLit);

            // Emissive VFX / Telegraphs / Portals / Crystals
            dict["Waterfall"] = CreateOrUpdateMat("Assets/Materials/MAT_Waterfall_Scroll.mat", urpLit, new Color(0.24f, 0.85f, 0.98f, 0.88f), 0.62f, null, new Color(0.18f, 0.72f, 0.95f) * 0.65f);
            dict["TelegraphDanger"] = CreateOrUpdateMat("Assets/Materials/MAT_Telegraph_Danger.mat", urpLit, new Color(0.95f, 0.20f, 0.25f, 0.62f), 0.15f, null, new Color(0.95f, 0.18f, 0.22f) * 0.65f);
            dict["SafeZoneGold"] = CreateOrUpdateMat("Assets/Materials/MAT_SafeZone_Gold.mat", urpLit, new Color(1.00f, 0.78f, 0.12f, 0.34f), 0.25f, null, new Color(1.00f, 0.72f, 0.05f) * 0.45f);
            dict["LockdownBarrier"] = CreateOrUpdateMat("Assets/Materials/MAT_Lockdown_Barrier.mat", urpLit, new Color(0.92f, 0.22f, 0.32f, 0.62f), 0.30f, null, new Color(0.92f, 0.22f, 0.30f) * 0.85f);
            dict["PortalSafe"] = CreateOrUpdateMat("Assets/Materials/MAT_Portal_Safe.mat", urpLit, new Color(0.24f, 0.82f, 0.95f, 0.72f), 0.45f, null, new Color(0.24f, 0.82f, 0.95f) * 0.90f);
            dict["PortalRisk"] = CreateOrUpdateMat("Assets/Materials/MAT_Portal_Risk.mat", urpLit, new Color(1.00f, 0.68f, 0.08f, 0.72f), 0.45f, null, new Color(1.00f, 0.68f, 0.08f) * 0.90f);
            dict["RuneCrystal"] = CreateOrUpdateMat("Assets/Materials/MAT_Rune_Crystal.mat", urpLit, new Color(0.22f, 0.85f, 1.00f, 1f), 0.78f, null, new Color(0.15f, 0.72f, 0.98f) * 0.85f);
            dict["StormCrimson"] = CreateOrUpdateMat("Assets/Materials/MAT_Storm_Crimson.mat", urpLit, new Color(0.92f, 0.18f, 0.38f, 1f), 0.75f, null, new Color(0.88f, 0.15f, 0.35f) * 0.85f);
            dict["HealEmerald"] = CreateOrUpdateMat("Assets/Materials/MAT_Heal_Emerald.mat", urpLit, new Color(0.28f, 0.94f, 0.56f, 1f), 0.80f, null, new Color(0.22f, 0.88f, 0.48f) * 0.95f);
            dict["GlowArcane"] = CreateOrUpdateMat("Assets/Materials/MAT_Glow_Arcane.mat", urpLit, new Color(0.68f, 0.38f, 1.00f, 1f), 0.80f, null, new Color(0.62f, 0.30f, 1.00f) * 1.4f);

            return dict;
        }

        private static Material CreateNatureMat(
            string path,
            Shader shader,
            Color tint,
            float smoothness,
            Texture2D mainTex,
            Texture2D normalTex = null,
            bool alphaClip = false,
            bool doubleSided = false)
        {
            Material mat = CreateOrUpdateMat(path, shader, tint, smoothness, mainTex);
            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 0.75f);
            }
            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.35f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            if (alphaClip || doubleSided)
            {
                mat.SetFloat("_Cull", 0f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildSkyIslandMaterials(Dictionary<string, Material> dict, Shader urpLit)
        {
            (string key, string matFile, string hex, float smooth, float metal, string emHex, float emMul)[] defs =
            {
                ("Sky_foliage",       "MAT_Sky_Foliage.mat",      "#7EE028", 0.08f, 0f,    null, 0f),
                ("Sky_foliagedark",   "MAT_Sky_FoliageDark.mat",  "#4DB320", 0.08f, 0f,    null, 0f),
                ("Sky_soil",          "MAT_Sky_Soil.mat",         "#7D4424", 0.06f, 0f,    null, 0f),
                ("Sky_stone",         "MAT_Sky_Stone.mat",        "#A35C36", 0.12f, 0f,    null, 0f),
                ("Sky_stonedark",     "MAT_Sky_StoneDark.mat",    "#733D22", 0.10f, 0f,    null, 0f),
                ("Sky_rockviolet",    "MAT_Sky_RockViolet.mat",   "#7E557C", 0.14f, 0f,    null, 0f),
                ("Sky_rockvioletdark","MAT_Sky_RockVioletDark.mat","#5A3B5E",0.12f, 0f,    null, 0f),
                ("Sky_groundviolet",  "MAT_Sky_GroundViolet.mat", "#6FD02A", 0.08f, 0f,    null, 0f),
                ("Sky_groundlavender","MAT_Sky_GroundLavender.mat","#6FD02A",0.08f, 0f,    null, 0f),
                ("Sky_crystal",       "MAT_Sky_Crystal.mat",      "#38E0FF", 0.80f, 0f,    "#18BDF5", 0.90f),
                ("Sky_magic",         "MAT_Sky_Magic.mat",        "#529BFF", 0.72f, 0f,    "#2C7CFF", 1.15f),
                ("Sky_water",         "MAT_Sky_Water.mat",        "#29C7EE", 0.70f, 0f,    "#1286B8", 0.45f),
                ("Sky_cream",         "MAT_Sky_Cream.mat",        "#F7F4EC", 0.12f, 0f,    null, 0f),
                ("Sky_wood",          "MAT_Sky_Wood.mat",         "#BD804C", 0.12f, 0f,    null, 0f),
                ("Sky_wooddark",      "MAT_Sky_WoodDark.mat",     "#7C4C29", 0.10f, 0f,    null, 0f),
                ("Sky_paintred",      "MAT_Sky_PaintRed.mat",     "#DC543E", 0.22f, 0f,    null, 0f),
                ("Sky_paintblue",     "MAT_Sky_PaintBlue.mat",    "#3E82DB", 0.22f, 0f,    null, 0f),
                ("Sky_paintorange",   "MAT_Sky_PaintOrange.mat",  "#EF8E3E", 0.22f, 0f,    null, 0f),
                ("Sky_paintyellow",   "MAT_Sky_PaintYellow.mat",  "#F5CE4E", 0.20f, 0f,    null, 0f),
                ("Sky_paintpink",     "MAT_Sky_PaintPink.mat",    "#F07CA4", 0.20f, 0f,    null, 0f),
                ("Sky_paintpurple",   "MAT_Sky_PaintPurple.mat",  "#9D6CE0", 0.20f, 0f,    null, 0f),
                ("Sky_wheat",         "MAT_Sky_Wheat.mat",        "#ECC546", 0.14f, 0f,    null, 0f),
                ("Sky_floweryellow",  "MAT_Sky_FlowerYellow.mat", "#F8D64E", 0.16f, 0f,    null, 0f),
                ("Sky_flowerpurple",  "MAT_Sky_FlowerPurple.mat", "#AE72EB", 0.16f, 0f,    null, 0f),
                ("Sky_metal",         "MAT_Sky_Metal.mat",        "#9BA6B2", 0.58f, 0.55f, null, 0f),
                ("Sky_iron",          "MAT_Sky_Iron.mat",         "#5A626E", 0.52f, 0.50f, null, 0f),
                ("Sky_gold",          "MAT_Sky_Gold.mat",         "#F5BA36", 0.70f, 0.65f, null, 0f),
                ("Sky_fire",          "MAT_Sky_Fire.mat",         "#FFA132", 0.35f, 0f,    "#FF6E1A", 1.15f),
                ("Sky_rubber",        "MAT_Sky_Rubber.mat",       "#363840", 0.15f, 0f,    null, 0f)
            };

            foreach (var d in defs)
            {
                Color em = !string.IsNullOrEmpty(d.emHex) ? HexColor(d.emHex) * d.emMul : default;
                Material m = CreateOrUpdateMat("Assets/Materials/" + d.matFile, urpLit, HexColor(d.hex), d.smooth, null, em);
                if (d.metal > 0.01f)
                {
                    m.SetFloat("_Metallic", d.metal);
                    EditorUtility.SetDirty(m);
                }
                dict[d.key] = m;
            }
        }

        private static Material CreateOrUpdateMat(string path, Shader shader, Color color, float smoothness, Texture2D mainTex = null, Color emission = default)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);

            if (color.a < 0.99f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1;
            }

            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.mainTexture = mainTex;
            }

            if (emission.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AnimatorController CreatePlayerAnimatorController()
        {
            string path = "Assets/Animations/Player/AC_Player.controller";
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ac.AddParameter("AttackStep", AnimatorControllerParameterType.Int);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

            AnimationClip idleClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_General.fbx", new[] { "Idle_A", "Idle_B" });
            AnimationClip runClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_MovementBasic.fbx", new[] { "Running_A", "Running_B", "Walking_A" });
            AnimationClip atk1Clip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_CombatMelee.fbx", new[] { "Melee_1H_Attack_Slice_Diagonal", "Melee_1H_Attack_Chop" });
            AnimationClip atk2Clip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_CombatMelee.fbx", new[] { "Melee_1H_Attack_Slice_Horizontal", "Melee_1H_Attack_Stab" }) ?? atk1Clip;
            AnimationClip atk3Clip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_CombatMelee.fbx", new[] { "Melee_1H_Attack_Jump_Chop", "Melee_2H_Attack_Spin" }) ?? atk1Clip;
            AnimationClip dodgeClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_MovementAdvanced.fbx", new[] { "Dodge_Forward", "Running_Strafe_Left" })
                ?? FindClipByKeywords("Assets/Art/Characters/Rig_Medium_MovementBasic.fbx", new[] { "Jump_Full_Short", "Running_B" }) ?? runClip;
            AnimationClip interactClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_General.fbx", new[] { "Interact", "PickUp" }) ?? idleClip;
            AnimationClip hitClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_General.fbx", new[] { "Hit_A", "Hit_B" }) ?? idleClip;
            AnimationClip dieClip = FindClipByKeywords("Assets/Art/Characters/Rig_Medium_General.fbx", new[] { "Death_A", "Death_B" }) ?? hitClip;

            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle", new Vector3(250, 100, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState runState = sm.AddState("Run", new Vector3(520, 100, 0));
            runState.motion = runClip;

            AnimatorState atk1State = sm.AddState("Attack1", new Vector3(340, -40, 0));
            atk1State.motion = atk1Clip;
            atk1State.speed = 1.15f;

            AnimatorState atk2State = sm.AddState("Attack2", new Vector3(540, -40, 0));
            atk2State.motion = atk2Clip;
            atk2State.speed = 1.15f;

            AnimatorState atk3State = sm.AddState("Attack3", new Vector3(740, -40, 0));
            atk3State.motion = atk3Clip;
            atk3State.speed = 1.2f;

            AnimatorState dodgeState = sm.AddState("Dodge", new Vector3(440, 220, 0));
            dodgeState.motion = dodgeClip;
            dodgeState.speed = 1.25f;

            AnimatorState interactState = sm.AddState("Interact", new Vector3(200, 220, 0));
            interactState.motion = interactClip;

            AnimatorState hitState = sm.AddState("Hit", new Vector3(140, -80, 0));
            hitState.motion = hitClip;

            AnimatorState deathState = sm.AddState("Death", new Vector3(30, -160, 0));
            deathState.motion = dieClip;

            var toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.08f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            var toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            // 3-Hit Combo routing via AttackStep (1, 2, 3)
            var anyToAtk1 = sm.AddAnyStateTransition(atk1State);
            anyToAtk1.hasExitTime = false;
            anyToAtk1.duration = 0.06f;
            anyToAtk1.canTransitionToSelf = false;
            anyToAtk1.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAtk1.AddCondition(AnimatorConditionMode.Less, 2, "AttackStep");

            var anyToAtk2 = sm.AddAnyStateTransition(atk2State);
            anyToAtk2.hasExitTime = false;
            anyToAtk2.duration = 0.06f;
            anyToAtk2.canTransitionToSelf = false;
            anyToAtk2.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAtk2.AddCondition(AnimatorConditionMode.Equals, 2, "AttackStep");

            var anyToAtk3 = sm.AddAnyStateTransition(atk3State);
            anyToAtk3.hasExitTime = false;
            anyToAtk3.duration = 0.06f;
            anyToAtk3.canTransitionToSelf = false;
            anyToAtk3.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAtk3.AddCondition(AnimatorConditionMode.Greater, 2, "AttackStep");

            foreach (var atkState in new[] { atk1State, atk2State, atk3State })
            {
                var atkToIdle = atkState.AddTransition(idleState);
                atkToIdle.hasExitTime = true;
                atkToIdle.exitTime = 0.82f;
                atkToIdle.duration = 0.08f;
            }

            var anyToDodge = sm.AddAnyStateTransition(dodgeState);
            anyToDodge.hasExitTime = false;
            anyToDodge.duration = 0.05f;
            anyToDodge.canTransitionToSelf = false;
            anyToDodge.AddCondition(AnimatorConditionMode.If, 0, "Dodge");

            var dodgeToIdle = dodgeState.AddTransition(idleState);
            dodgeToIdle.hasExitTime = true;
            dodgeToIdle.exitTime = 0.72f;
            dodgeToIdle.duration = 0.08f;

            var anyToInteract = sm.AddAnyStateTransition(interactState);
            anyToInteract.hasExitTime = false;
            anyToInteract.duration = 0.06f;
            anyToInteract.canTransitionToSelf = false;
            anyToInteract.AddCondition(AnimatorConditionMode.If, 0, "Interact");

            var interactToIdle = interactState.AddTransition(idleState);
            interactToIdle.hasExitTime = true;
            interactToIdle.exitTime = 0.75f;
            interactToIdle.duration = 0.08f;

            var anyToHit = sm.AddAnyStateTransition(hitState);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.05f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyToHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.75f;
            hitToIdle.duration = 0.08f;

            var anyToDie = sm.AddAnyStateTransition(deathState);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(ac);
            return ac;
        }

        private static AnimatorController CreateEnemyAnimatorController(string controllerName, bool isLargeRig, EnemyAnimRole role)
        {
            string path = $"Assets/Animations/Enemies/{controllerName}.controller";
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            ac.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Windup", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IsCharging", AnimatorControllerParameterType.Bool);
            ac.AddParameter("IsStunned", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

            AnimationClip idleClip, runClip, windupClip, atkClip, hitClip, dieClip;
            if (!isLargeRig)
            {
                string gen = "Assets/Art/Characters/Rig_Medium_General.fbx";
                string mov = "Assets/Art/Characters/Rig_Medium_MovementBasic.fbx";
                string mel = "Assets/Art/Characters/Rig_Medium_CombatMelee.fbx";
                string rng = "Assets/Art/Characters/Rig_Medium_CombatRanged.fbx";
                string spc = "Assets/Art/Characters/Rig_Medium_Special.fbx";

                idleClip = role == EnemyAnimRole.MageStaff
                    ? (FindClipByKeywords(spc, new[] { "Skeletons_Idle" }) ?? FindClipByKeywords(gen, new[] { "Idle_B", "Idle_A" }))
                    : FindClipByKeywords(gen, new[] { "Idle_A", "Idle_B" });
                runClip = FindClipByKeywords(mov, new[] { "Running_A", "Running_B", "Walking_A" });
                hitClip = FindClipByKeywords(gen, new[] { "Hit_A", "Hit_B" }) ?? idleClip;
                dieClip = FindClipByKeywords(gen, new[] { "Death_A", "Death_B" }) ?? hitClip;

                switch (role)
                {
                    case EnemyAnimRole.RangedCrossbow:
                        windupClip = FindClipByKeywords(rng, new[] { "Ranged_1H_Aiming", "Ranged_1H_Reload" }) ?? idleClip;
                        atkClip = FindClipByKeywords(rng, new[] { "Ranged_1H_Shoot", "Ranged_1H_Shooting" }) ?? windupClip;
                        break;
                    case EnemyAnimRole.MageStaff:
                        windupClip = FindClipByKeywords(rng, new[] { "Ranged_Magic_Raise" }) ?? idleClip;
                        atkClip = FindClipByKeywords(rng, new[] { "Ranged_Magic_Spellcasting", "Ranged_Magic_Shoot" }) ?? windupClip;
                        break;
                    default:
                        windupClip = FindClipByKeywords(mel, new[] { "Melee_Unarmed_Idle", "Melee_1H_Attack_Chop" }) ?? idleClip;
                        atkClip = FindClipByKeywords(mel, new[] { "Melee_1H_Attack_Slice_Diagonal", "Melee_1H_Attack_Horizontal" }) ?? windupClip;
                        break;
                }
            }
            else
            {
                string gen = "Assets/Art/Characters/Rig_Large_General.fbx";
                string mov = "Assets/Art/Characters/Rig_Large_MovementBasic.fbx";
                string mel = "Assets/Art/Characters/Rig_Large_CombatMelee.fbx";

                idleClip = FindClipByKeywords(gen, new[] { "Idle_A", "Idle_B" });
                runClip = FindClipByKeywords(mov, new[] { "Running_A", "Walking_A" });
                windupClip = FindClipByKeywords(mel, new[] { "Melee_2H_Idle", "Melee_2H_Attack" }) ?? idleClip;
                atkClip = role == EnemyAnimRole.BossWarden
                    ? FindClipByKeywords(mel, new[] { "Melee_Dualwield_SlashCombo", "Melee_Dualwield_Slash", "Melee_2H_Slam" })
                    : FindClipByKeywords(mel, new[] { "Melee_2H_Slam", "Melee_2H_Attack", "Melee_Unarmed_Smash" });
                hitClip = FindClipByKeywords(gen, new[] { "Hit_A" }) ?? idleClip;
                dieClip = FindClipByKeywords(gen, new[] { "Death_A" }) ?? hitClip;
            }

            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle", new Vector3(260, 120, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState moveState = sm.AddState("Move", new Vector3(520, 120, 0));
            moveState.motion = runClip;

            AnimatorState windupState = sm.AddState("Windup", new Vector3(360, -20, 0));
            windupState.motion = windupClip;

            AnimatorState attackState = sm.AddState("Attack", new Vector3(580, -20, 0));
            attackState.motion = atkClip;

            AnimatorState chargeState = sm.AddState("Charge", new Vector3(520, 240, 0));
            chargeState.motion = runClip;
            chargeState.speed = 1.45f;

            AnimatorState stunnedState = sm.AddState("Stunned", new Vector3(260, 240, 0));
            stunnedState.motion = hitClip;
            stunnedState.speed = 0.4f;

            AnimatorState hitState = sm.AddState("Hit", new Vector3(140, -60, 0));
            hitState.motion = hitClip;

            AnimatorState dieState = sm.AddState("Death", new Vector3(30, -140, 0));
            dieState.motion = dieClip;

            var toMove = idleState.AddTransition(moveState);
            toMove.hasExitTime = false;
            toMove.duration = 0.1f;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            var toIdle = moveState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            var idleToCharge = idleState.AddTransition(chargeState);
            idleToCharge.hasExitTime = false;
            idleToCharge.duration = 0.08f;
            idleToCharge.AddCondition(AnimatorConditionMode.If, 0, "IsCharging");

            var moveToCharge = moveState.AddTransition(chargeState);
            moveToCharge.hasExitTime = false;
            moveToCharge.duration = 0.08f;
            moveToCharge.AddCondition(AnimatorConditionMode.If, 0, "IsCharging");

            var chargeToIdle = chargeState.AddTransition(idleState);
            chargeToIdle.hasExitTime = false;
            chargeToIdle.duration = 0.1f;
            chargeToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCharging");

            var anyToWindup = sm.AddAnyStateTransition(windupState);
            anyToWindup.hasExitTime = false;
            anyToWindup.duration = 0.08f;
            anyToWindup.canTransitionToSelf = false;
            anyToWindup.AddCondition(AnimatorConditionMode.If, 0, "Windup");
            anyToWindup.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var windupToAtk = windupState.AddTransition(attackState);
            windupToAtk.hasExitTime = false;
            windupToAtk.duration = 0.06f;
            windupToAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var idleToAtk = idleState.AddTransition(attackState);
            idleToAtk.hasExitTime = false;
            idleToAtk.duration = 0.06f;
            idleToAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var moveToAtk = moveState.AddTransition(attackState);
            moveToAtk.hasExitTime = false;
            moveToAtk.duration = 0.06f;
            moveToAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var atkToIdle = attackState.AddTransition(idleState);
            atkToIdle.hasExitTime = true;
            atkToIdle.exitTime = 0.85f;
            atkToIdle.duration = 0.1f;

            var anyToStun = sm.AddAnyStateTransition(stunnedState);
            anyToStun.hasExitTime = false;
            anyToStun.duration = 0.08f;
            anyToStun.canTransitionToSelf = false;
            anyToStun.AddCondition(AnimatorConditionMode.If, 0, "IsStunned");
            anyToStun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var stunToIdle = stunnedState.AddTransition(idleState);
            stunToIdle.hasExitTime = false;
            stunToIdle.duration = 0.1f;
            stunToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStunned");

            var anyToHit = sm.AddAnyStateTransition(hitState);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.05f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyToHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            anyToHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStunned");

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.8f;
            hitToIdle.duration = 0.08f;

            var anyToDie = sm.AddAnyStateTransition(dieState);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(ac);
            return ac;
        }

        private static AnimationClip FindClipByKeywords(string assetPath, string[] keywords)
        {
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (objs == null) return null;

            // Exact case-insensitive match first so e.g. "Idle_A" or "Melee_1H_Attack_Chop" wins cleanly
            foreach (string kw in keywords)
            {
                foreach (Object o in objs)
                {
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__") &&
                        string.Equals(clip.name, kw, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return clip;
                    }
                }
            }

            foreach (string kw in keywords)
            {
                string lowerKw = kw.ToLowerInvariant();
                foreach (Object o in objs)
                {
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__") &&
                        clip.name.ToLowerInvariant().Contains(lowerKw))
                    {
                        return clip;
                    }
                }
            }
            return FindClipInAsset(assetPath);
        }

        private static AnimationClip FindClipInAsset(string assetPath)
        {
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (objs == null) return null;
            foreach (Object o in objs)
            {
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        private static Dictionary<string, EnemyArchetypeSO> CreateEnemyArchetypes()
        {
            var dict = new Dictionary<string, EnemyArchetypeSO>();

            // 1. Breeze Imp (GDD Section 6.1)
            var imp = CreateOrLoadSO<EnemyArchetypeSO>("Assets/Data/Enemies/EA_BreezeImp.asset");
            imp.enemyName = "Breeze Imp";
            imp.roleType = EnemyRoleType.MeleeFlanker;
            imp.maxHP = 70f;
            imp.baseDamage = 10f;
            imp.patrolSpeed = 2.8f;
            imp.chaseSpeed = 5.8f;
            imp.attackRange = 1.8f;
            imp.windupDuration = 0.40f;
            imp.attackCooldown = 1.1f;
            imp.detectionRadius = 12.0f;
            imp.fovAngle = 130f;
            imp.canFlee = true;
            imp.fleeThresholdRatio = 0.25f;
            imp.maxFleeDuration = 2.5f;
            EditorUtility.SetDirty(imp);
            dict["BreezeImp"] = imp;

            // 2. Sky Sprite (GDD Section 6.1)
            var sprite = CreateOrLoadSO<EnemyArchetypeSO>("Assets/Data/Enemies/EA_SkySprite.asset");
            sprite.enemyName = "Sky Sprite";
            sprite.roleType = EnemyRoleType.RangedKiter;
            sprite.maxHP = 55f;
            sprite.baseDamage = 12f;
            sprite.patrolSpeed = 2.5f;
            sprite.chaseSpeed = 4.2f;
            sprite.attackRange = 11.0f;
            sprite.windupDuration = 0.50f;
            sprite.attackCooldown = 1.8f;
            sprite.detectionRadius = 14.0f;
            sprite.fovAngle = 140f;
            sprite.isRanged = true;
            sprite.minOptimalDistance = 6.0f;
            sprite.maxOptimalDistance = 10.0f;
            sprite.retreatDistance = 4.0f;
            sprite.projectileSpeed = 11.5f;
            EditorUtility.SetDirty(sprite);
            dict["SkySprite"] = sprite;

            // 3. Dusk Wisp (GDD Section 6.1)
            var wisp = CreateOrLoadSO<EnemyArchetypeSO>("Assets/Data/Enemies/EA_DuskWisp.asset");
            wisp.enemyName = "Dusk Wisp";
            wisp.roleType = EnemyRoleType.ShadowStalker;
            wisp.maxHP = 85f;
            wisp.baseDamage = 14f;
            wisp.patrolSpeed = 2.6f;
            wisp.chaseSpeed = 4.8f;
            wisp.attackRange = 2.0f;
            wisp.windupDuration = 0.45f;
            wisp.attackCooldown = 1.4f;
            wisp.detectionRadius = 13.0f;
            wisp.fovAngle = 140f;
            wisp.avoidsSunBeacon = true;
            wisp.lifeStealAmount = 6.0f;
            EditorUtility.SetDirty(wisp);
            dict["DuskWisp"] = wisp;

            // 4. Crystal Golem (GDD Section 6.1)
            var golem = CreateOrLoadSO<EnemyArchetypeSO>("Assets/Data/Enemies/EA_CrystalGolem.asset");
            golem.enemyName = "Crystal Golem";
            golem.roleType = EnemyRoleType.HeavyBruiser;
            golem.maxHP = 180f;
            golem.baseDamage = 22f;
            golem.patrolSpeed = 1.8f;
            golem.chaseSpeed = 2.6f;
            golem.attackRange = 2.5f;
            golem.windupDuration = 0.80f;
            golem.attackCooldown = 2.2f;
            golem.detectionRadius = 14.0f;
            golem.fovAngle = 150f;
            golem.hasArmor = true;
            golem.armorReduction = 0.50f;
            golem.canBullRush = true;
            golem.chargeSpeed = 9.0f;
            golem.chargeDamage = 26f;
            golem.chargeCooldown = 7.0f;
            golem.stunDuration = 3.5f;
            golem.stunDamageTakenMultiplier = 1.5f;
            EditorUtility.SetDirty(golem);
            dict["CrystalGolem"] = golem;

            // 5. The Zephyr Guardian (GDD Section 6.1)
            var boss = CreateOrLoadSO<EnemyArchetypeSO>("Assets/Data/Enemies/EA_ZephyrGuardian.asset");
            boss.enemyName = "The Zephyr Guardian";
            boss.roleType = EnemyRoleType.Boss;
            boss.maxHP = 480f;
            boss.baseDamage = 20f;
            boss.patrolSpeed = 3.0f;
            boss.chaseSpeed = 5.2f;
            boss.attackRange = 3.0f;
            boss.windupDuration = 0.65f;
            boss.attackCooldown = 1.5f;
            boss.detectionRadius = 22.0f;
            boss.fovAngle = 180f;
            EditorUtility.SetDirty(boss);
            dict["ZephyrGuardian"] = boss;

            return dict;
        }

        private static MathGateSO CreateMathGatePresetSO()
        {
            var preset = CreateOrLoadSO<MathGateSO>("Assets/Data/MathGates/MG_DefaultPresets.asset");
            preset.safeOptions.Clear();
            preset.riskyOptions.Clear();

            preset.safeOptions.Add(new MathGateOptionData
            {
                gateTitle = "GATE A [SAFE]",
                isHighRisk = false,
                positiveModifier = new MathGateModifier { effectType = MathGateEffectType.HealHP, value = 25f, allowOverhealShield = true }
            });
            preset.riskyOptions.Add(new MathGateOptionData
            {
                gateTitle = "GATE B [RISK]",
                isHighRisk = true,
                positiveModifier = new MathGateModifier { effectType = MathGateEffectType.DamageFlat, value = 6f },
                negativeModifier = new MathGateModifier { effectType = MathGateEffectType.TakeFlatDamage, value = 15f }
            });
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static Dictionary<string, GameObject> CreateEnvironmentPrefabs(Dictionary<string, Material> mats)
        {
            var dict = new Dictionary<string, GameObject>();
            int groundLayer = LayerMask.NameToLayer("Ground");
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            int bgLayer = LayerMask.NameToLayer("BackgroundProp");
            int groundVal = groundLayer >= 0 ? groundLayer : 0;

            // 1. IslandSlab_Base.prefab (Moon Tribe Lush Floating Island + Courtyard + Tapered Cliff & Dangling Roots)
            GameObject islandRoot = new GameObject("IslandSlab_Base");
            islandRoot.layer = groundVal;

            // Keep exact 22x3x22 BoxCollider for walkability & NavMesh; hide sharp cube renderer
            GameObject topSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSlab.name = "PlatformSlab";
            topSlab.layer = groundVal;
            topSlab.transform.SetParent(islandRoot.transform, false);
            topSlab.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            topSlab.transform.localScale = new Vector3(22f, 3.0f, 22f);
            Renderer slabRend = topSlab.GetComponent<Renderer>();
            slabRend.sharedMaterial = mats["EmeraldGrass"];
            slabRend.enabled = false;

            // Modular visual hierarchy scaled proportionally by BranchingIslandGenerator
            GameObject visualArch = new GameObject("IslandVisualArch");
            visualArch.transform.SetParent(islandRoot.transform, false);

            // Lush lime-chartreuse turf crown at Y = 0 (cylinder height=2 -> scaleY=0.06 gives top at -0.015f)
            GameObject grassCrown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            grassCrown.name = "ArenaGrassCrown";
            Object.DestroyImmediate(grassCrown.GetComponent<Collider>());
            grassCrown.transform.SetParent(visualArch.transform, false);
            grassCrown.transform.localPosition = new Vector3(0f, -0.075f, 0f);
            grassCrown.transform.localScale = new Vector3(23.8f, 0.06f, 23.8f);
            grassCrown.GetComponent<Renderer>().sharedMaterial = mats["EmeraldGrass"];

            // Organic tapered rock cliff, soil rim, dangling roots, and orbiting mini-islets below Y = 0
            GameObject cliffAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/SkyIslands_Shuriken/meadow-sky-islets.glb");
            if (cliffAsset != null)
            {
                GameObject cliffPivot = new GameObject("CliffUnderbellyPivot");
                cliffPivot.transform.SetParent(visualArch.transform, false);
                cliffPivot.transform.localPosition = new Vector3(0.08f, -12.30f, -0.11f);
                cliffPivot.transform.localScale = new Vector3(1.60f, 1.08f, 1.56f);

                GameObject isletInst = (GameObject)PrefabUtility.InstantiatePrefab(cliffAsset);
                if (PrefabUtility.IsPartOfAnyPrefab(isletInst))
                    PrefabUtility.UnpackPrefabInstance(isletInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // Collect all descendants bottom-up; keep ONLY the cliff shell, side stones, roots & orbiting float rocks
                var allTransforms = new List<Transform>(isletInst.GetComponentsInChildren<Transform>(true));
                for (int i = allTransforms.Count - 1; i >= 0; i--)
                {
                    Transform t = allTransforms[i];
                    if (t == null || t == isletInst.transform) continue;
                    string cn = t.name;
                    bool keep = cn == "MeadowIslet_Bush" ||
                                cn == "MeadowIslet_Bush_SideStones" ||
                                cn == "MeadowIslet_Bush_Roots" ||
                                cn.StartsWith("MeadowIslet_Bush_FloatRock_");
                    if (!keep)
                        Object.DestroyImmediate(t.gameObject);
                }

                RemoveAllColliders(isletInst);
                ApplySkyIslandMaterials(isletInst, mats);
                isletInst.transform.SetParent(cliffPivot.transform, false);
            }
            else
            {
                GameObject cliffFallback = InstantiateVisualOnly(
                    "Assets/Art/Environment/DungeonRemastered/floor_foundation_allsides.obj",
                    PrimitiveType.Cube, new Vector3(18f, 4.5f, 18f), mats["TerracottaCliff"]);
                cliffFallback.name = "TerracottaCliffUnderbelly";
                cliffFallback.transform.SetParent(visualArch.transform, false);
                cliffFallback.transform.localPosition = new Vector3(0f, -6.2f, 0f);
                cliffFallback.transform.localScale = new Vector3(7.4f, 2.2f, 7.4f);
            }

            // Central 3x3 Runic Sanctuary Plaza (-4, 0, 4 => crisp stonework hub surrounded by lush meadow grass)
            float[] tileCoords = { -4f, 0f, 4f };
            for (int ix = 0; ix < tileCoords.Length; ix++)
            {
                for (int iz = 0; iz < tileCoords.Length; iz++)
                {
                    GameObject tile = InstantiateVisualOnly(
                        "Assets/Art/Environment/DungeonRemastered/floor_tile_large.obj",
                        PrimitiveType.Cube, new Vector3(4f, 0.1f, 4f), mats["Dungeon"]);
                    tile.name = $"CourtyardTile_{ix}_{iz}";
                    tile.transform.SetParent(visualArch.transform, false);
                    tile.transform.localPosition = new Vector3(tileCoords[ix], -0.025f, tileCoords[iz]);
                    tile.transform.localRotation = Quaternion.Euler(0f, ((ix + iz) % 4) * 90f, 0f);
                }
            }

            // Natural stepping-stone cobble trails extending from central sanctuary to gates & sides
            Vector3[] pathNodes = {
                new Vector3(-0.35f, -0.02f,  6.5f), new Vector3( 0.40f, -0.02f,  8.3f), new Vector3(-0.20f, -0.02f, 10.0f),
                new Vector3( 0.35f, -0.02f, -6.5f), new Vector3(-0.40f, -0.02f, -8.3f), new Vector3( 0.20f, -0.02f,-10.0f),
                new Vector3( 6.4f,  -0.02f,  0.3f), new Vector3( 8.2f,  -0.02f, -0.35f),
                new Vector3(-6.4f,  -0.02f, -0.3f), new Vector3(-8.2f,  -0.02f,  0.35f)
            };
            for (int i = 0; i < pathNodes.Length; i++)
            {
                string stepPath = (i % 3 == 0)
                    ? "Assets/Art/Environment/StylizedNature/FBX/RockPath_Round_Wide.fbx"
                    : "Assets/Art/Environment/StylizedNature/FBX/RockPath_Round_Small_" + ((i % 3)) + ".fbx";
                GameObject stepStone = InstantiateNatureProp(stepPath, Vector3.one * 0.92f, mats);
                stepStone.name = $"SteppingStone_{i}";
                stepStone.transform.SetParent(visualArch.transform, false);
                stepStone.transform.localPosition = pathNodes[i];
                stepStone.transform.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
            }

            // Corner Terraces & Landmarks (Tall puffy trees on NORTH corners only; low foliage/crystals on SOUTH so camera stays clear)
            Vector2[] corners = {
                new Vector2(-9.35f,  9.35f), // NW
                new Vector2( 9.35f,  9.35f), // NE
                new Vector2(-9.35f, -9.35f), // SW (low profile)
                new Vector2( 9.35f, -9.35f)  // SE (low profile)
            };
            for (int c = 0; c < corners.Length; c++)
            {
                float cx = corners[c].x;
                float cz = corners[c].y;
                bool isNorth = cz > 0f;

                // Terracotta cliff outcropping anchoring corner ledge underneath
                string rimRockPath = (c % 2 == 0)
                    ? "Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_1.fbx"
                    : "Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_2.fbx";
                GameObject ledgeRock = InstantiateNatureProp(rimRockPath, new Vector3(1.55f, 1.35f, 1.55f), mats, true);
                ledgeRock.name = $"CornerCliffOutcrop_{c}";
                ledgeRock.transform.SetParent(visualArch.transform, false);
                ledgeRock.transform.localPosition = new Vector3(cx * 1.03f, -1.65f, cz * 1.03f);
                ledgeRock.transform.localRotation = Quaternion.Euler(0f, c * 83f, 0f);

                // Small surface accent rock
                GameObject edgeBoulder = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_3.fbx", Vector3.one * 0.52f, mats, true);
                edgeBoulder.name = $"CornerBoulder_{c}";
                edgeBoulder.transform.SetParent(visualArch.transform, false);
                edgeBoulder.transform.localPosition = new Vector3(cx + (cx > 0 ? -0.95f : 0.95f), -0.05f, cz + (cz > 0 ? 0.35f : -0.35f));
                edgeBoulder.transform.localRotation = Quaternion.Euler(0f, c * 65f + 22f, 0f);

                // Stylized corner stone post + warm torch
                GameObject cornerCol = InstantiateVisualOnly(
                    "Assets/Art/Environment/DungeonRemastered/column.obj",
                    PrimitiveType.Cylinder, new Vector3(0.6f, 1.2f, 0.6f), mats["Dungeon"]);
                cornerCol.name = $"CornerColumn_{c}";
                cornerCol.transform.SetParent(visualArch.transform, false);
                cornerCol.transform.localPosition = new Vector3(cx * 0.88f, 0f, cz * 0.98f);
                cornerCol.transform.localScale = Vector3.one * 0.85f;

                GameObject torch = InstantiateVisualOnly(
                    "Assets/Art/Environment/DungeonRemastered/torch_lit.obj",
                    PrimitiveType.Cylinder, new Vector3(0.35f, 0.9f, 0.35f), mats["Dungeon"]);
                torch.name = $"CornerTorch_{c}";
                torch.transform.SetParent(cornerCol.transform, false);
                torch.transform.localPosition = new Vector3(0f, 1.60f, 0f);

                // Glowing cyan crystal spire echoing Moon Tribe crystal clusters
                GameObject gem = InstantiateVisualOnly(
                    isNorth ? "Assets/Art/Environment/Props/Gem_Large.fbx" : "Assets/Art/Environment/Props/Gem_Medium.fbx",
                    PrimitiveType.Cube, new Vector3(0.7f, 1.3f, 0.7f), mats["RuneCrystal"]);
                gem.name = $"CornerCrystal_{c}";
                gem.transform.SetParent(visualArch.transform, false);
                gem.transform.localPosition = new Vector3(cx + (cx > 0 ? 0.45f : -0.45f), 0.1f, cz + (cz > 0 ? -1.15f : 1.15f));
                gem.transform.localRotation = Quaternion.Euler(6f * (cz > 0 ? -1f : 1f), c * 40f, 8f * (cx > 0 ? 1f : -1f));
                gem.transform.localScale = Vector3.one * (isNorth ? 1.55f : 1.15f);

                // Flowering bush & meadow flowers
                GameObject bush = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Bush_Common_Flowers.fbx", Vector3.one * 0.65f, mats);
                bush.name = $"CornerBush_{c}";
                bush.transform.SetParent(visualArch.transform, false);
                bush.transform.localPosition = new Vector3(cx, 0f, cz + (isNorth ? -0.95f : 0.75f));
                bush.transform.localRotation = Quaternion.Euler(0f, c * 95f, 0f);

                GameObject flower = InstantiateNatureProp(
                    (c % 2 == 0) ? "Assets/Art/Environment/StylizedNature/FBX/Flower_3_Group.fbx" : "Assets/Art/Environment/StylizedNature/FBX/Flower_4_Group.fbx",
                    Vector3.one * 0.36f, mats);
                flower.name = $"CornerFlowers_{c}";
                flower.transform.SetParent(visualArch.transform, false);
                flower.transform.localPosition = new Vector3(cx + (cx > 0 ? -1.45f : 1.45f), 0f, cz * 0.90f);

                if (isNorth)
                {
                    string treePath = (c == 0)
                        ? "Assets/Art/Environment/StylizedNature/FBX/CommonTree_1.fbx"
                        : "Assets/Art/Environment/StylizedNature/FBX/CommonTree_3.fbx";
                    GameObject tree = InstantiateNatureProp(treePath, Vector3.one * 0.62f, mats);
                    tree.name = $"CornerTree_{c}";
                    tree.transform.SetParent(visualArch.transform, false);
                    tree.transform.localPosition = new Vector3(cx * 1.04f, 0f, cz * 1.02f);
                    tree.transform.localRotation = Quaternion.Euler(0f, c * 115f + 20f, 0f);
                }
            }

            // Lush East/West Rim Foliage, Grass Tufts & Banners (open vista, zero fortress walls!)
            foreach (float sideSign in new[] { -1f, 1f })
            {
                foreach (float zOff in new[] { -5.2f, -2.0f, 2.2f, 5.4f })
                {
                    GameObject grass = InstantiateNatureProp(
                        zOff > 0 ? "Assets/Art/Environment/StylizedNature/FBX/Grass_Common_Tall.fbx" : "Assets/Art/Environment/StylizedNature/FBX/Grass_Common_Short.fbx",
                        Vector3.one * 0.58f, mats);
                    grass.name = $"RimGrass_{(sideSign < 0 ? "W" : "E")}_{zOff:0}";
                    grass.transform.SetParent(visualArch.transform, false);
                    grass.transform.localPosition = new Vector3(sideSign * 10.15f, -0.02f, zOff);
                    grass.transform.localRotation = Quaternion.Euler(0f, zOff * 37f, 0f);
                }

                GameObject midBush = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Bush_Common_Flowers.fbx", Vector3.one * 0.58f, mats);
                midBush.name = sideSign < 0 ? "WestRimBush" : "EastRimBush";
                midBush.transform.SetParent(visualArch.transform, false);
                midBush.transform.localPosition = new Vector3(sideSign * 10.35f, -0.02f, 3.8f);

                GameObject sideRock = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_1.fbx", new Vector3(1.1f, 0.9f, 1.1f), mats, true);
                sideRock.name = sideSign < 0 ? "WestCliffLedge" : "EastCliffLedge";
                sideRock.transform.SetParent(visualArch.transform, false);
                sideRock.transform.localPosition = new Vector3(sideSign * 11.45f, -1.65f, -4.4f);

                GameObject banner = InstantiateVisualOnly(
                    "Assets/Art/Environment/DungeonRemastered/banner_patternA_blue.obj",
                    PrimitiveType.Cube, new Vector3(1.2f, 2.4f, 0.2f), mats["Dungeon"]);
                banner.name = sideSign < 0 ? "WestBanner" : "EastBanner";
                banner.transform.SetParent(visualArch.transform, false);
                banner.transform.localPosition = new Vector3(sideSign * 10.25f, -0.10f, -3.8f);
                banner.transform.localRotation = Quaternion.Euler(0f, sideSign < 0 ? 90f : -90f, 0f);
            }

            // North/South Gatehouse Piers leaving 8.4m clear gate/bridge openings (X in [-4.2, +4.2])
            foreach (float zSign in new[] { -1f, 1f })
            {
                foreach (float xSign in new[] { -1f, 1f })
                {
                    GameObject gatePier = InstantiateVisualOnly(
                        "Assets/Art/Environment/DungeonRemastered/column.obj",
                        PrimitiveType.Cylinder, new Vector3(0.8f, 1.6f, 0.8f), mats["Dungeon"]);
                    gatePier.name = $"GatePier_{(zSign > 0 ? "N" : "S")}_{(xSign > 0 ? "E" : "W")}";
                    gatePier.transform.SetParent(visualArch.transform, false);
                    gatePier.transform.localPosition = new Vector3(xSign * 4.45f, 0f, zSign * 10.45f);
                    gatePier.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);

                    GameObject gateTorch = InstantiateVisualOnly(
                        "Assets/Art/Environment/DungeonRemastered/torch_lit.obj",
                        PrimitiveType.Cylinder, new Vector3(0.4f, 1.0f, 0.4f), mats["Dungeon"]);
                    gateTorch.name = $"GateTorch_{(zSign > 0 ? "N" : "S")}_{(xSign > 0 ? "E" : "W")}";
                    gateTorch.transform.SetParent(gatePier.transform, false);
                    gateTorch.transform.localPosition = new Vector3(0f, 1.55f, 0f);

                    GameObject pierBush = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Grass_Common_Short.fbx", Vector3.one * 0.78f, mats);
                    pierBush.name = $"PierTuft_{(zSign > 0 ? "N" : "S")}_{(xSign > 0 ? "E" : "W")}";
                    pierBush.transform.SetParent(visualArch.transform, false);
                    pierBush.transform.localPosition = new Vector3(xSign * 5.65f, -0.02f, zSign * 10.15f);

                    if (zSign > 0f)
                    {
                        GameObject ptObj = new GameObject("TorchLight");
                        ptObj.transform.SetParent(gateTorch.transform, false);
                        ptObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                        Light tl = ptObj.AddComponent<Light>();
                        tl.type = LightType.Point;
                        tl.color = HexColor("#FFB74D");
                        tl.intensity = 1.6f;
                        tl.range = 6.5f;
                        tl.shadows = LightShadows.None;
                    }
                }
            }

            // Soft Floating Sky Clouds hugging cliff root tip
            Vector3[] cloudOffsets = {
                new Vector3(-7.5f, -8.8f, -5.5f), new Vector3(7.2f, -9.2f, 5.2f), new Vector3(-2.5f, -10.8f, 6.8f)
            };
            for (int c = 0; c < cloudOffsets.Length; c++)
            {
                string cPath = c == 0
                    ? "Assets/Art/Environment/MedievalHex/cloud_big.obj"
                    : "Assets/Art/Environment/MedievalHex/cloud_small.obj";
                GameObject cld = InstantiateVisualOnly(cPath, PrimitiveType.Sphere, Vector3.one, mats["CloudWhite"]);
                cld.name = $"IslandCloud_{c}";
                cld.transform.SetParent(visualArch.transform, false);
                cld.transform.localPosition = cloudOffsets[c];
                cld.transform.localRotation = Quaternion.Euler(0f, c * 55f, 0f);
                cld.transform.localScale = new Vector3(1.65f, 1.15f, 1.65f);
            }

            // Cascading Cyan Waterfall pouring off East cliff edge framed by terracotta boulders + foam clouds
            GameObject waterfall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterfall.name = "CascadingWaterfall";
            Object.DestroyImmediate(waterfall.GetComponent<Collider>());
            waterfall.transform.SetParent(visualArch.transform, false);
            waterfall.transform.localPosition = new Vector3(11.65f, -4.6f, 0f);
            waterfall.transform.localScale = new Vector3(0.32f, 8.8f, 3.0f);
            waterfall.GetComponent<Renderer>().sharedMaterial = mats["Waterfall"];
            waterfall.AddComponent<UVWaterfallScroller>();

            foreach (float zRock in new[] { -1.75f, 1.75f })
            {
                GameObject lipRock = InstantiateNatureProp("Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_2.fbx", Vector3.one * 0.72f, mats, true);
                lipRock.name = "WaterfallLipRock";
                lipRock.transform.SetParent(visualArch.transform, false);
                lipRock.transform.localPosition = new Vector3(11.2f, -0.45f, zRock);
                lipRock.transform.localRotation = Quaternion.Euler(0f, zRock * 45f, 0f);
            }

            GameObject mistCloud = InstantiateVisualOnly(
                "Assets/Art/Environment/MedievalHex/cloud_small.obj",
                PrimitiveType.Sphere, Vector3.one, mats["CloudWhite"]);
            mistCloud.name = "WaterfallMistCloud";
            mistCloud.transform.SetParent(visualArch.transform, false);
            mistCloud.transform.localPosition = new Vector3(11.7f, -9.1f, 0f);
            mistCloud.transform.localScale = new Vector3(1.65f, 0.95f, 2.0f);

            dict["IslandSlab"] = SaveAsPrefab(islandRoot, "Assets/Prefabs/Environment/IslandSlab_Base.prefab");

            // 2. ArchedStoneBridge.prefab (Moon Tribe fantasy sunlit skyway bridge + terracotta rock arch supports)
            GameObject bridgeRoot = new GameObject("ArchedStoneBridge");
            bridgeRoot.layer = groundVal;

            GameObject bridgeCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bridgeCollider.name = "BridgeDeckCollider";
            bridgeCollider.layer = groundVal;
            bridgeCollider.transform.SetParent(bridgeRoot.transform, false);
            bridgeCollider.transform.localPosition = new Vector3(0f, -0.225f, 0f);
            bridgeCollider.transform.localScale = new Vector3(4.2f, 0.45f, 4.0f);
            bridgeCollider.GetComponent<Renderer>().enabled = false;

            GameObject tileTemplate = new GameObject("BridgeTileTemplate");
            tileTemplate.transform.SetParent(bridgeRoot.transform, false);
            tileTemplate.SetActive(false);

            GameObject deckMesh = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/floor_tile_large.obj",
                PrimitiveType.Cube, new Vector3(4f, 0.15f, 4f), mats["BridgeStone"]);
            deckMesh.name = "BridgeDeckMesh";
            deckMesh.transform.SetParent(tileTemplate.transform, false);
            deckMesh.transform.localPosition = new Vector3(0f, -0.035f, 0f);
            deckMesh.transform.localScale = new Vector3(1.04f, 1f, 1f);

            GameObject leftRail = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/barrier.obj",
                PrimitiveType.Cube, new Vector3(0.4f, 1.1f, 4f), mats["Dungeon"]);
            leftRail.name = "LeftBalustrade";
            leftRail.transform.SetParent(tileTemplate.transform, false);
            leftRail.transform.localPosition = new Vector3(-1.95f, 0f, 0f);
            leftRail.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject rightRail = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/barrier.obj",
                PrimitiveType.Cube, new Vector3(0.4f, 1.1f, 4f), mats["Dungeon"]);
            rightRail.name = "RightBalustrade";
            rightRail.transform.SetParent(tileTemplate.transform, false);
            rightRail.transform.localPosition = new Vector3(1.95f, 0f, 0f);
            rightRail.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            GameObject underArch = InstantiateNatureProp(
                "Assets/Art/Environment/StylizedNature/FBX/Rock_Medium_2.fbx",
                new Vector3(1.25f, 0.85f, 1.35f), mats, true);
            underArch.name = "BridgeUnderRock";
            underArch.transform.SetParent(tileTemplate.transform, false);
            underArch.transform.localPosition = new Vector3(0f, -1.60f, 0f);

            dict["Bridge"] = SaveAsPrefab(bridgeRoot, "Assets/Prefabs/Environment/ArchedStoneBridge.prefab");

            // 3. StunPillar_Monolith.prefab (ASSET_CONTRACT Section 3.2: 3.2m height, Tag = StunPillar, Layer = Obstacle)
            GameObject pillarRoot = new GameObject("StunPillar_Monolith");
            pillarRoot.tag = "StunPillar";
            pillarRoot.layer = obsLayer >= 0 ? obsLayer : 0;
            CapsuleCollider pCol = pillarRoot.AddComponent<CapsuleCollider>();
            pCol.radius = 0.65f;
            pCol.height = 3.2f;
            pCol.center = new Vector3(0f, 1.6f, 0f);

            GameObject pillarModel = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/pillar_decorated.obj",
                PrimitiveType.Cylinder, new Vector3(1.1f, 1.6f, 1.1f), mats["Dungeon"]);
            pillarModel.name = "PillarMesh";
            pillarModel.transform.SetParent(pillarRoot.transform, false);
            pillarModel.transform.localPosition = Vector3.zero;
            pillarModel.transform.localScale = new Vector3(0.72f, 0.80f, 0.72f);
            pillarModel.layer = obsLayer >= 0 ? obsLayer : 0;

            GameObject pBanner = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/banner_shield_blue.obj",
                PrimitiveType.Cube, Vector3.one, mats["Dungeon"]);
            pBanner.name = "PillarShieldBanner";
            pBanner.transform.SetParent(pillarRoot.transform, false);
            pBanner.transform.localPosition = new Vector3(0f, -0.35f, -0.15f);
            pBanner.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            pBanner.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            GameObject pTorch = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/torch_mounted.obj",
                PrimitiveType.Cube, Vector3.one, mats["Dungeon"]);
            pTorch.name = "PillarTorch";
            pTorch.transform.SetParent(pillarRoot.transform, false);
            pTorch.transform.localPosition = new Vector3(0f, 2.0f, -0.58f);
            pTorch.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            dict["StunPillar"] = SaveAsPrefab(pillarRoot, "Assets/Prefabs/Environment/StunPillar_Monolith.prefab");

            // 4. Perimeter Stylized Floating Island Dioramas (Shuriken 3D Sky Islands + Cloud Wisps)
            dict["BackdropWindmill"] = CreateSkyIslandBackdrop(
                mats, bgLayer, "Backdrop_Windmill", "Assets/Prefabs/Environment/Backdrop_Windmill.prefab",
                "Assets/Art/Environment/SkyIslands_Shuriken/windmill-farm-island.glb",
                0.16f, new Vector3(0f, -4.6f, 0f), 0.0f, "Sails", new Vector3(0f, 0f, 38f));
            dict["BackdropFountain"] = CreateSkyIslandBackdrop(
                mats, bgLayer, "Backdrop_Fountain", "Assets/Prefabs/Environment/Backdrop_Fountain.prefab",
                "Assets/Art/Environment/SkyIslands_Shuriken/meadow-sky-island.glb",
                0.095f, new Vector3(0f, -5.5f, 0f), 1.4f);
            dict["BackdropArch"] = CreateSkyIslandBackdrop(
                mats, bgLayer, "Backdrop_Arch", "Assets/Prefabs/Environment/Backdrop_Arch.prefab",
                "Assets/Art/Environment/SkyIslands_Shuriken/rune-circle-sky-island.glb",
                0.145f, new Vector3(0f, -4.35f, 0f), 2.7f);
            dict["BackdropTree"] = CreateSkyIslandBackdrop(
                mats, bgLayer, "Backdrop_PineCluster", "Assets/Prefabs/Environment/Backdrop_PineCluster.prefab",
                "Assets/Art/Environment/SkyIslands_Shuriken/crystal-cave-island.glb",
                0.16f, new Vector3(0f, -4.8f, 0f), 4.1f);

            return dict;
        }

        private static GameObject CreateSkyIslandBackdrop(
            Dictionary<string, Material> mats,
            int bgLayer,
            string rootName,
            string prefabPath,
            string glbPath,
            float uniformScale,
            Vector3 modelOffset,
            float bobPhase,
            string spinChildName = null,
            Vector3 spinDegPerSec = default)
        {
            GameObject root = new GameObject(rootName);

            GameObject bobPivot = new GameObject("FloatPivot");
            bobPivot.transform.SetParent(root.transform, false);
            bobPivot.transform.localPosition = new Vector3(0f, Mathf.Sin(bobPhase) * 0.25f, 0f);
            bobPivot.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 1.4f, 0f), 0.35f, 0.85f);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (modelAsset != null)
            {
                GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                if (PrefabUtility.IsPartOfAnyPrefab(modelInst))
                    PrefabUtility.UnpackPrefabInstance(modelInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                RemoveAllColliders(modelInst);
                ApplySkyIslandMaterials(modelInst, mats);
                modelInst.transform.SetParent(bobPivot.transform, false);
                modelInst.transform.localPosition = modelOffset;
                modelInst.transform.localScale = Vector3.one * uniformScale;

                if (!string.IsNullOrEmpty(spinChildName))
                {
                    Transform spinTarget = FindChildRecursive(modelInst.transform, spinChildName);
                    if (spinTarget != null)
                        spinTarget.gameObject.AddComponent<SimpleAmbientMotion>().Configure(spinDegPerSec);
                }
            }
            else
            {
                GameObject fallback = InstantiateVisualOnly(
                    "Assets/Art/Environment/MedievalHex/hex_grass.obj",
                    PrimitiveType.Cylinder, new Vector3(5f, 2f, 5f), mats["EmeraldGrass"]);
                fallback.transform.SetParent(bobPivot.transform, false);
            }

            // Fluffy cloud wisps hugging bottom cliff tip
            GameObject cloudA = InstantiateVisualOnly(
                "Assets/Art/Environment/MedievalHex/cloud_big.obj",
                PrimitiveType.Sphere, Vector3.one, mats["CloudWhite"]);
            cloudA.name = "CloudWisp_Main";
            cloudA.transform.SetParent(bobPivot.transform, false);
            cloudA.transform.localPosition = new Vector3(0f, -6.2f, 0f);
            cloudA.transform.localScale = new Vector3(2.25f, 1.25f, 2.25f);

            GameObject cloudB = InstantiateVisualOnly(
                "Assets/Art/Environment/MedievalHex/cloud_small.obj",
                PrimitiveType.Sphere, Vector3.one, mats["CloudWhite"]);
            cloudB.name = "CloudWisp_Accent";
            cloudB.transform.SetParent(bobPivot.transform, false);
            cloudB.transform.localPosition = new Vector3(3.2f, -4.8f, -1.8f);
            cloudB.transform.localRotation = Quaternion.Euler(0f, 65f, 0f);
            cloudB.transform.localScale = new Vector3(1.65f, 1.0f, 1.65f);

            if (bgLayer >= 0)
                SetLayerRecursively(root, bgLayer);

            return SaveAsPrefab(root, prefabPath);
        }

        private static Dictionary<string, GameObject> CreateGameplayPrefabs(Dictionary<string, Material> mats)
        {
            var dict = new Dictionary<string, GameObject>();
            int wallLayer = LayerMask.NameToLayer("Wall");
            int obsLayer = LayerMask.NameToLayer("Obstacle");
            int triggerLayer = LayerMask.NameToLayer("TriggerZone");
            int enemyLayer = LayerMask.NameToLayer("Enemy");

            // 1. LockdownBarrier.prefab (Sculpted stone gateposts + arcane crimson forcefield)
            GameObject barrierRoot = new GameObject("LockdownBarrier");
            if (wallLayer >= 0) barrierRoot.layer = wallLayer;
            BoxCollider bWallCol = barrierRoot.AddComponent<BoxCollider>();
            bWallCol.size = new Vector3(8.5f, 2.8f, 0.5f);
            bWallCol.center = new Vector3(0f, 1.4f, 0f);

            GameObject energyWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            energyWall.name = "CrimsonEnergyWall";
            Object.DestroyImmediate(energyWall.GetComponent<Collider>());
            energyWall.transform.SetParent(barrierRoot.transform, false);
            energyWall.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            energyWall.transform.localScale = new Vector3(8.2f, 2.5f, 0.25f);
            energyWall.GetComponent<Renderer>().sharedMaterial = mats["LockdownBarrier"];

            foreach (float xPost in new[] { -4.1f, 4.1f })
            {
                GameObject post = InstantiateVisualOnly("Assets/Art/Environment/DungeonRemastered/pillar.obj", PrimitiveType.Cylinder, Vector3.one, mats["Dungeon"]);
                post.name = "BarrierPost";
                post.transform.SetParent(barrierRoot.transform, false);
                post.transform.localPosition = new Vector3(xPost, 0f, 0f);
                post.transform.localScale = new Vector3(0.55f, 0.72f, 0.55f);
            }
            dict["LockdownBarrier"] = SaveAsPrefab(barrierRoot, "Assets/Prefabs/Gameplay/LockdownBarrier.prefab");

            // 2. MathGate_Door.prefab (Sculpted HalloweenBits Arch + Spinning Gem + Glowing Portal + Banners)
            GameObject gateRoot = new GameObject("MathGate_Door");
            gateRoot.tag = "MathGate";
            if (triggerLayer >= 0) gateRoot.layer = triggerLayer;
            BoxCollider gateCol = gateRoot.AddComponent<BoxCollider>();
            gateCol.isTrigger = true;
            gateCol.size = new Vector3(3.8f, 3.6f, 1.6f);
            gateCol.center = new Vector3(0f, 1.8f, 0f);

            GameObject archModel = InstantiateVisualOnly(
                "Assets/Art/Environment/HalloweenBits/arch_gate.obj",
                PrimitiveType.Cube, new Vector3(4.0f, 4.0f, 0.7f), mats["HalloweenBits"]);
            archModel.name = "StoneArchGate";
            archModel.transform.SetParent(gateRoot.transform, false);
            archModel.transform.localScale = new Vector3(0.95f, 0.90f, 1.0f);

            foreach (float xSide in new[] { -1.95f, 1.95f })
            {
                GameObject b = InstantiateVisualOnly(
                    "Assets/Art/Environment/DungeonRemastered/banner_patternA_blue.obj",
                    PrimitiveType.Cube, Vector3.one, mats["Dungeon"]);
                b.name = "GateBanner";
                b.transform.SetParent(gateRoot.transform, false);
                b.transform.localPosition = new Vector3(xSide, -0.2f, 0.1f);
                b.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                b.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            }

            GameObject portalPane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portalPane.name = "PortalPane";
            Object.DestroyImmediate(portalPane.GetComponent<Collider>());
            portalPane.transform.SetParent(gateRoot.transform, false);
            portalPane.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            portalPane.transform.localScale = new Vector3(2.75f, 2.95f, 0.12f);
            Renderer portalRend = portalPane.GetComponent<Renderer>();
            portalRend.sharedMaterial = mats["PortalSafe"];

            GameObject crownGem = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Medium.fbx",
                PrimitiveType.Sphere, Vector3.one, mats["PortalSafe"]);
            crownGem.name = "GateCrownCrystal";
            crownGem.transform.SetParent(gateRoot.transform, false);
            crownGem.transform.localPosition = new Vector3(0f, 4.35f, 0f);
            crownGem.transform.localScale = new Vector3(1.55f, 1.55f, 1.55f);
            crownGem.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 65f, 0f), 0.10f, 2.4f);
            Renderer crystalRend = crownGem.GetComponentInChildren<Renderer>();

            GameObject gateLightObj = new GameObject("GateLight");
            gateLightObj.transform.SetParent(gateRoot.transform, false);
            gateLightObj.transform.localPosition = new Vector3(0f, 2.4f, -0.65f);
            Light gateLight = gateLightObj.AddComponent<Light>();
            gateLight.type = LightType.Point;
            gateLight.color = HexColor("#48CAE4");
            gateLight.intensity = 2.6f;
            gateLight.range = 8.0f;
            gateLight.shadows = LightShadows.None;

            GameObject lockedSeal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lockedSeal.name = "LockedBarrierVisual";
            Object.DestroyImmediate(lockedSeal.GetComponent<Collider>());
            lockedSeal.transform.SetParent(gateRoot.transform, false);
            lockedSeal.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            lockedSeal.transform.localScale = new Vector3(3.05f, 3.15f, 0.22f);
            lockedSeal.GetComponent<Renderer>().sharedMaterial = mats["LockdownBarrier"];
            lockedSeal.SetActive(false);

            TextMeshPro titleTmp = Create3DText(gateRoot.transform, "TitleText", new Vector3(0f, 5.10f, -0.25f), "GATE A [SAFE]", 3.2f, Color.white);
            TextMeshPro posTmp = Create3DText(gateRoot.transform, "PositiveText", new Vector3(0f, 2.35f, -0.25f), "+25 HP", 4.2f, HexColor("#57CC68"));
            TextMeshPro negTmp = Create3DText(gateRoot.transform, "NegativeText", new Vector3(0f, 1.40f, -0.25f), "", 3.8f, HexColor("#E63946"));

            MathGateDoor doorComp = gateRoot.AddComponent<MathGateDoor>();
            SerializedObject doorSo = new SerializedObject(doorComp);
            doorSo.FindProperty("titleText").objectReferenceValue = titleTmp;
            doorSo.FindProperty("positiveText").objectReferenceValue = posTmp;
            doorSo.FindProperty("negativeText").objectReferenceValue = negTmp;
            doorSo.FindProperty("portalRenderer").objectReferenceValue = portalRend;
            SerializedProperty cryProp = doorSo.FindProperty("crystalRenderer");
            if (cryProp != null) cryProp.objectReferenceValue = crystalRend;
            SerializedProperty glProp = doorSo.FindProperty("gateLight");
            if (glProp != null) glProp.objectReferenceValue = gateLight;
            SerializedProperty lbProp = doorSo.FindProperty("lockedBarrierVisual");
            if (lbProp != null) lbProp.objectReferenceValue = lockedSeal;
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            dict["MathGateDoor"] = SaveAsPrefab(gateRoot, "Assets/Prefabs/Gameplay/MathGate_Door.prefab");

            // 3. SunBeacon.prefab (Cleric_Font carved basin + hovering solar Gem_Large + Sanctum aura disc)
            GameObject beaconRoot = new GameObject("SunBeacon");
            beaconRoot.tag = "Interactable";
            if (obsLayer >= 0) beaconRoot.layer = obsLayer;
            SphereCollider bCol = beaconRoot.AddComponent<SphereCollider>();
            bCol.radius = 0.85f;
            bCol.center = new Vector3(0f, 1.0f, 0f);

            GameObject bPedestal = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Cleric_Font.fbx",
                PrimitiveType.Cylinder, Vector3.one, mats["CreamStone"], preserveTex: true);
            bPedestal.name = "SunSanctumFont";
            bPedestal.transform.SetParent(beaconRoot.transform, false);
            bPedestal.transform.localScale = new Vector3(1.1f, 1.15f, 1.1f);

            GameObject bFlame = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Large.fbx",
                PrimitiveType.Sphere, Vector3.one, mats["PortalRisk"]);
            bFlame.name = "FlameOrb";
            bFlame.transform.SetParent(beaconRoot.transform, false);
            bFlame.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            bFlame.transform.localScale = new Vector3(1.45f, 1.45f, 1.45f);
            bFlame.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 55f, 0f), 0.14f, 2.0f);

            GameObject bRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bRing.name = "SafeZoneRing";
            Object.DestroyImmediate(bRing.GetComponent<Collider>());
            bRing.transform.SetParent(beaconRoot.transform, false);
            bRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            bRing.transform.localScale = new Vector3(9.0f, 0.025f, 9.0f);
            Renderer ringRend = bRing.GetComponent<Renderer>();
            ringRend.sharedMaterial = mats["SafeZoneGold"];

            GameObject lightObj = new GameObject("BeaconPointLight");
            lightObj.transform.SetParent(beaconRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            Light ptLight = lightObj.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = HexColor("#FFB703");
            ptLight.intensity = 3.5f;
            ptLight.range = 8.5f;

            SunBeacon sb = beaconRoot.AddComponent<SunBeacon>();
            SerializedObject sbSo = new SerializedObject(sb);
            sbSo.FindProperty("beaconLight").objectReferenceValue = ptLight;
            sbSo.FindProperty("flameVisual").objectReferenceValue = bFlame;
            sbSo.FindProperty("ringRenderer").objectReferenceValue = ringRend;
            sbSo.ApplyModifiedPropertiesWithoutUndo();

            dict["SunBeacon"] = SaveAsPrefab(beaconRoot, "Assets/Prefabs/Gameplay/SunBeacon.prefab");

            // 4. RuneAnchor.prefab (HalloweenBits Shrine pedestal + hovering Rune Crystal + Active Aura Ring)
            GameObject runeRoot = new GameObject("RuneAnchor");
            runeRoot.tag = "Interactable";
            if (obsLayer >= 0) runeRoot.layer = obsLayer;
            SphereCollider rCol = runeRoot.AddComponent<SphereCollider>();
            rCol.radius = 0.85f;
            rCol.center = new Vector3(0f, 1.0f, 0f);

            GameObject rShrine = InstantiateVisualOnly(
                "Assets/Art/Environment/HalloweenBits/shrine.obj",
                PrimitiveType.Cube, Vector3.one, mats["HalloweenBits"]);
            rShrine.name = "RuneShrinePedestal";
            rShrine.transform.SetParent(runeRoot.transform, false);
            rShrine.transform.localScale = new Vector3(0.88f, 0.82f, 0.88f);

            GameObject rCrystal = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Large.fbx",
                PrimitiveType.Cube, Vector3.one, mats["RuneCrystal"]);
            rCrystal.name = "RuneCrystalCore";
            rCrystal.transform.SetParent(runeRoot.transform, false);
            rCrystal.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            rCrystal.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
            rCrystal.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 75f, 0f), 0.14f, 2.4f);
            Renderer rRend = rCrystal.GetComponentInChildren<Renderer>();

            GameObject rLightObj = new GameObject("RunePointLight");
            rLightObj.transform.SetParent(runeRoot.transform, false);
            rLightObj.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            Light rLight = rLightObj.AddComponent<Light>();
            rLight.type = LightType.Point;
            rLight.color = HexColor("#48CAE4");
            rLight.intensity = 2.8f;
            rLight.range = 7.0f;

            GameObject rAura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rAura.name = "RuneActiveAura";
            Object.DestroyImmediate(rAura.GetComponent<Collider>());
            rAura.transform.SetParent(runeRoot.transform, false);
            rAura.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            rAura.transform.localScale = new Vector3(3.8f, 0.025f, 3.8f);
            rAura.GetComponent<Renderer>().sharedMaterial = mats["PortalSafe"];
            rAura.SetActive(false);

            RuneAnchor ra = runeRoot.AddComponent<RuneAnchor>();
            SerializedObject raSo = new SerializedObject(ra);
            raSo.FindProperty("crystalRenderer").objectReferenceValue = rRend;
            SerializedProperty rlProp = raSo.FindProperty("runeLight");
            if (rlProp != null) rlProp.objectReferenceValue = rLight;
            SerializedProperty rauraProp = raSo.FindProperty("activeAuraVisual");
            if (rauraProp != null) rauraProp.objectReferenceValue = rAura;
            raSo.ApplyModifiedPropertiesWithoutUndo();

            dict["RuneAnchor"] = SaveAsPrefab(runeRoot, "Assets/Prefabs/Gameplay/RuneAnchor.prefab");

            // 5. StormCrystal.prefab (Dark Altar Base + Spinning Crimson Gem_Large + Orbiting Shard + Telegraph)
            GameObject crystalRoot = new GameObject("StormCrystal");
            crystalRoot.tag = "Enemy";
            if (enemyLayer >= 0) crystalRoot.layer = enemyLayer;
            CapsuleCollider cCol = crystalRoot.AddComponent<CapsuleCollider>();
            cCol.radius = 0.75f;
            cCol.height = 2.6f;
            cCol.center = new Vector3(0f, 1.3f, 0f);

            GameObject cBase = InstantiateVisualOnly(
                "Assets/Art/Environment/HalloweenBits/shrine_candles.obj",
                PrimitiveType.Cylinder, Vector3.one, mats["HalloweenBits"]);
            cBase.name = "StormAltarBase";
            cBase.transform.SetParent(crystalRoot.transform, false);
            cBase.transform.localScale = new Vector3(0.95f, 0.72f, 0.95f);

            GameObject intactVis = new GameObject("IntactStormCrystalVisual");
            intactVis.transform.SetParent(crystalRoot.transform, false);
            intactVis.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 68f, 0f), 0.12f, 2.5f);

            GameObject cMesh = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Large.fbx",
                PrimitiveType.Cylinder, Vector3.one, mats["StormCrimson"]);
            cMesh.name = "CrystalObelisk";
            cMesh.transform.SetParent(intactVis.transform, false);
            cMesh.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            cMesh.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);

            GameObject shard = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Small.fbx",
                PrimitiveType.Cube, Vector3.one, mats["StormCrimson"]);
            shard.name = "OrbitShard";
            shard.transform.SetParent(intactVis.transform, false);
            shard.transform.localPosition = new Vector3(0.85f, 1.9f, 0f);
            shard.transform.localRotation = Quaternion.Euler(18f, 0f, -22f);
            shard.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);

            crystalRoot.AddComponent<HealthComponent>();
            crystalRoot.AddComponent<HitFeedback>();
            AttackTelegraph cTel = CreateTelegraphChild(crystalRoot.transform, mats["TelegraphDanger"]);
            StormCrystal sc = crystalRoot.AddComponent<StormCrystal>();
            SerializedObject scSo = new SerializedObject(sc);
            scSo.FindProperty("telegraph").objectReferenceValue = cTel;
            scSo.FindProperty("intactVisual").objectReferenceValue = intactVis;
            scSo.ApplyModifiedPropertiesWithoutUndo();

            dict["StormCrystal"] = SaveAsPrefab(crystalRoot, "Assets/Prefabs/Gameplay/StormCrystal.prefab");

            // 6. HealOrb.prefab (Floating Radiant Emerald Gem_Medium)
            GameObject orbRoot = new GameObject("HealOrb");
            orbRoot.name = "HealOrb";
            orbRoot.tag = "Interactable";
            if (triggerLayer >= 0) orbRoot.layer = triggerLayer;
            SphereCollider orbCol = orbRoot.AddComponent<SphereCollider>();
            orbCol.isTrigger = true;
            orbCol.radius = 0.7f;
            orbCol.center = new Vector3(0f, 0.9f, 0f);

            GameObject orbVisual = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Medium.fbx",
                PrimitiveType.Sphere, Vector3.one, mats["HealEmerald"]);
            orbVisual.name = "HealGemVisual";
            orbVisual.transform.SetParent(orbRoot.transform, false);
            orbVisual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            orbVisual.transform.localScale = new Vector3(1.65f, 1.65f, 1.65f);
            orbVisual.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 95f, 0f), 0.14f, 2.8f);

            orbRoot.AddComponent<HealOrb>();
            dict["HealOrb"] = SaveAsPrefab(orbRoot, "Assets/Prefabs/Gameplay/HealOrb.prefab");

            // 7. VictoryChest.prefab (Decorated Stone Dais + DungeonRemastered Gold Chest & Hinged Lid + Glow)
            GameObject chestRoot = new GameObject("VictoryChest");
            chestRoot.tag = "Interactable";
            if (obsLayer >= 0) chestRoot.layer = obsLayer;
            BoxCollider chCol = chestRoot.AddComponent<BoxCollider>();
            chCol.size = new Vector3(2.2f, 1.8f, 2.0f);
            chCol.center = new Vector3(0f, 0.9f, 0f);

            GameObject dais = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/floor_tile_small_decorated.obj",
                PrimitiveType.Cube, Vector3.one, mats["Dungeon"]);
            dais.name = "VictoryDais";
            dais.transform.SetParent(chestRoot.transform, false);
            dais.transform.localScale = new Vector3(1.35f, 0.72f, 1.35f);

            GameObject chestBase = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/chest_gold.obj",
                PrimitiveType.Cube, new Vector3(1.4f, 1.0f, 1.0f), mats["Dungeon"]);
            chestBase.name = "ChestBase";
            chestBase.transform.SetParent(chestRoot.transform, false);
            chestBase.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            GameObject lidPivot = new GameObject("ChestLidPivot");
            lidPivot.transform.SetParent(chestRoot.transform, false);
            lidPivot.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            GameObject chestLid = InstantiateVisualOnly(
                "Assets/Art/Environment/DungeonRemastered/chest_gold_lid.obj",
                PrimitiveType.Cube, Vector3.one, mats["Dungeon"]);
            chestLid.name = "ChestLid";
            chestLid.transform.SetParent(lidPivot.transform, false);

            GameObject chestGlowObj = new GameObject("ChestGlowEffect");
            chestGlowObj.transform.SetParent(chestRoot.transform, false);
            chestGlowObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Light chestLight = chestGlowObj.AddComponent<Light>();
            chestLight.type = LightType.Point;
            chestLight.color = HexColor("#FFD166");
            chestLight.intensity = 4.0f;
            chestLight.range = 8.0f;
            GameObject rewardCrown = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Medium.fbx",
                PrimitiveType.Sphere, Vector3.one, mats["GoldTrim"]);
            rewardCrown.transform.SetParent(chestGlowObj.transform, false);
            rewardCrown.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            rewardCrown.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            rewardCrown.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 90f, 0f), 0.15f, 2.5f);
            chestGlowObj.SetActive(false);

            VictoryChest vc = chestRoot.AddComponent<VictoryChest>();
            SerializedObject vcSo = new SerializedObject(vc);
            SerializedProperty lidProp = vcSo.FindProperty("lidTransform");
            if (lidProp != null) lidProp.objectReferenceValue = lidPivot.transform;
            SerializedProperty glowProp = vcSo.FindProperty("glowEffect");
            if (glowProp != null) glowProp.objectReferenceValue = chestGlowObj;
            vcSo.ApplyModifiedPropertiesWithoutUndo();

            dict["VictoryChest"] = SaveAsPrefab(chestRoot, "Assets/Prefabs/Gameplay/VictoryChest.prefab");

            return dict;
        }

        private static TMP_Text CreateFloatingDamagePopupPrefab()
        {
            GameObject obj = new GameObject("FloatingDamagePopup");
            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            if (cachedDefaultFont != null) tmp.font = cachedDefaultFont;
            tmp.text = "18";
            tmp.fontSize = 5.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(3.0f, 1.0f);
            GameObject saved = SaveAsPrefab(obj, "Assets/Prefabs/Gameplay/FloatingDamagePopup.prefab");
            return saved.GetComponent<TMP_Text>();
        }

        private static ProjectileController CreateProjectilePrefab(Dictionary<string, Material> mats)
        {
            int projLayer = LayerMask.NameToLayer("EnemyProjectile");
            GameObject projObj = new GameObject("EnemyProjectile");
            projObj.tag = "Projectile";
            if (projLayer >= 0) projObj.layer = projLayer;

            SphereCollider col = projObj.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.28f;

            GameObject orbCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orbCore.name = "BoltCore";
            Object.DestroyImmediate(orbCore.GetComponent<Collider>());
            orbCore.transform.SetParent(projObj.transform, false);
            orbCore.transform.localScale = new Vector3(0.32f, 0.32f, 0.55f);
            orbCore.GetComponent<Renderer>().sharedMaterial = mats["GlowArcane"];

            GameObject shard = InstantiateVisualOnly(
                "Assets/Art/Environment/Props/Gem_Small.fbx",
                PrimitiveType.Sphere, Vector3.one, mats["PortalRisk"]);
            shard.name = "BoltCrystalTip";
            shard.transform.SetParent(projObj.transform, false);
            shard.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shard.transform.localScale = new Vector3(1.25f, 1.45f, 1.25f);
            shard.AddComponent<SimpleAmbientMotion>().Configure(new Vector3(0f, 0f, 240f));

            Rigidbody rb = projObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            projObj.AddComponent<ProjectileController>();
            GameObject saved = SaveAsPrefab(projObj, "Assets/Prefabs/Enemies/EnemyProjectile.prefab");
            return saved.GetComponent<ProjectileController>();
        }

        private static Dictionary<string, GameObject> CreateEnemyPrefabs(
            Dictionary<string, Material> mats,
            Dictionary<string, AnimatorController> enemyAcs,
            Dictionary<string, EnemyArchetypeSO> archetypes,
            ProjectileController projPrefab)
        {
            var dict = new Dictionary<string, GameObject>();

            // 1. BreezeImp -> KayKit Tiefling Dual-Blader (fast skirmisher)
            dict["BreezeImp"] = BuildSingleEnemyPrefab(
                "Enemy_BreezeImp",
                "Assets/Art/Enemies/Tiefling.fbx",
                mats["Weapons"],
                null,
                new Vector3(0.72f, 0.72f, 0.72f),
                colliderRadius: 0.42f,
                colliderHeight: 1.85f,
                uiHeightOffset: 2.35f,
                enemyAcs["BreezeImp"],
                archetypes["BreezeImp"],
                mats["TelegraphDanger"],
                null,
                rightHandWeaponPath: "Assets/Art/Enemies/Tiefling_Sword.fbx",
                leftHandWeaponPath: "Assets/Art/Enemies/Tiefling_Sword.fbx",
                weaponFallbackMat: mats["Weapons"]
            );

            // 2. SkySprite -> KayKit AvianSwordsman Marksman with Crossbow
            dict["SkySprite"] = BuildSingleEnemyPrefab(
                "Enemy_SkySprite",
                "Assets/Art/Enemies/AvianSwordsman.fbx",
                mats["Weapons"],
                null,
                new Vector3(0.72f, 0.72f, 0.72f),
                colliderRadius: 0.42f,
                colliderHeight: 1.85f,
                uiHeightOffset: 2.35f,
                enemyAcs["SkySprite"],
                archetypes["SkySprite"],
                mats["TelegraphDanger"],
                projPrefab,
                extraBehavior: typeof(RangedKiteBehavior),
                rightHandWeaponPath: "Assets/Art/Enemies/crossbow_1handed.fbx",
                leftHandWeaponPath: null,
                weaponFallbackMat: mats["Weapons"]
            );

            // 3. DuskWisp -> KayKit Necromancer with Arcane Scythe & Staff + Glow eyes
            dict["DuskWisp"] = BuildSingleEnemyPrefab(
                "Enemy_DuskWisp",
                "Assets/Art/Enemies/Necromancer.fbx",
                mats["Weapons"],
                mats["GlowArcane"],
                new Vector3(0.74f, 0.74f, 0.74f),
                colliderRadius: 0.46f,
                colliderHeight: 2.0f,
                uiHeightOffset: 2.50f,
                enemyAcs["DuskWisp"],
                archetypes["DuskWisp"],
                mats["TelegraphDanger"],
                null,
                extraBehavior: typeof(PhotophobiaBehavior),
                rightHandWeaponPath: "Assets/Art/Enemies/Skeleton_Scythe.fbx",
                leftHandWeaponPath: "Assets/Art/Enemies/Skeleton_Staff.fbx",
                weaponFallbackMat: mats["Weapons"]
            );

            // 4. CrystalGolem -> KayKit FrostGolem Heavyweight with Great Axe
            dict["CrystalGolem"] = BuildSingleEnemyPrefab(
                "Enemy_CrystalGolem",
                "Assets/Art/Enemies/FrostGolem.fbx",
                mats["Weapons"],
                null,
                new Vector3(0.62f, 0.62f, 0.62f),
                colliderRadius: 0.72f,
                colliderHeight: 2.65f,
                uiHeightOffset: 3.15f,
                enemyAcs["CrystalGolem"],
                archetypes["CrystalGolem"],
                mats["TelegraphDanger"],
                null,
                extraBehavior: typeof(BullRushBehavior),
                rightHandWeaponPath: "Assets/Art/Enemies/FrostGolem_Axe_Large.fbx",
                leftHandWeaponPath: null,
                weaponFallbackMat: mats["Weapons"]
            );

            // 5. ZephyrGuardian -> KayKit BlackKnight Boss with Greatsword & Tower Shield
            dict["ZephyrGuardian"] = BuildSingleEnemyPrefab(
                "Enemy_ZephyrGuardian",
                "Assets/Art/Enemies/BlackKnight.fbx",
                mats["Weapons"],
                mats["GlowArcane"],
                new Vector3(0.70f, 0.70f, 0.70f),
                colliderRadius: 0.90f,
                colliderHeight: 3.60f,
                uiHeightOffset: 4.15f,
                enemyAcs["ZephyrGuardian"],
                archetypes["ZephyrGuardian"],
                mats["TelegraphDanger"],
                projPrefab,
                extraBehavior: typeof(BossPhaseBehavior),
                rightHandWeaponPath: "Assets/Art/Enemies/BlackKnight_Sword_Large.fbx",
                leftHandWeaponPath: "Assets/Art/Enemies/BlackKnight_Shield_Large.fbx",
                weaponFallbackMat: mats["Weapons"]
            );

            return dict;
        }

        private static GameObject BuildSingleEnemyPrefab(
            string prefabName,
            string modelPath,
            Material fallbackMat,
            Material glowMat,
            Vector3 modelScale,
            float colliderRadius,
            float colliderHeight,
            float uiHeightOffset,
            AnimatorController ac,
            EnemyArchetypeSO archetype,
            Material telegraphMat,
            ProjectileController projPrefab,
            System.Type extraBehavior = null,
            string rightHandWeaponPath = null,
            string leftHandWeaponPath = null,
            Material weaponFallbackMat = null)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            GameObject root = new GameObject(prefabName);
            root.tag = "Enemy";
            if (enemyLayer >= 0) root.layer = enemyLayer;

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = colliderRadius;
            col.height = colliderHeight;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = col.radius;
            agent.height = col.height;
            agent.speed = archetype.chaseSpeed;
            agent.angularSpeed = 720f;
            agent.acceleration = 24f;

            GameObject visual = InstantiateVisualOnly(modelPath, PrimitiveType.Capsule, modelScale, fallbackMat, preserveTex: true, glowMat: glowMat);
            visual.name = "ModelVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = modelScale;

            if (!string.IsNullOrEmpty(rightHandWeaponPath))
            {
                Transform rHand = FindDeepChild(visual.transform, "handslot.r");
                if (rHand != null)
                {
                    GameObject rw = InstantiateVisualOnly(rightHandWeaponPath, PrimitiveType.Cube, new Vector3(0.15f, 0.8f, 0.15f), weaponFallbackMat ?? fallbackMat, preserveTex: true, glowMat: glowMat);
                    rw.transform.SetParent(rHand, false);
                }
            }

            if (!string.IsNullOrEmpty(leftHandWeaponPath))
            {
                Transform lHand = FindDeepChild(visual.transform, "handslot.l");
                if (lHand != null)
                {
                    GameObject lw = InstantiateVisualOnly(leftHandWeaponPath, PrimitiveType.Cube, new Vector3(0.35f, 0.6f, 0.15f), weaponFallbackMat ?? fallbackMat, preserveTex: true, glowMat: glowMat);
                    lw.transform.SetParent(lHand, false);
                }
            }

            Animator anim = visual.GetComponent<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;

            GameObject spawnPt = new GameObject("ProjectileSpawnPoint");
            spawnPt.transform.SetParent(root.transform, false);
            spawnPt.transform.localPosition = new Vector3(0f, colliderHeight * 0.62f, colliderRadius + 0.35f);

            CreateTelegraphChild(root.transform, telegraphMat);

            HealthComponent health = root.AddComponent<HealthComponent>();
            root.AddComponent<EnemyPerception>();
            root.AddComponent<EnemyMotor>();
            EnemyCombat combat = root.AddComponent<EnemyCombat>();
            if (projPrefab != null)
                combat.SetProjectilePrefab(projPrefab);
            SerializedObject combatSo = new SerializedObject(combat);
            SerializedProperty spProp = combatSo.FindProperty("projectileSpawnPoint");
            if (spProp != null)
            {
                spProp.objectReferenceValue = spawnPt.transform;
                combatSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (extraBehavior != null)
                root.AddComponent(extraBehavior);

            EnemyBrain brain = root.AddComponent<EnemyBrain>();
            SerializedObject brainSo = new SerializedObject(brain);
            brainSo.FindProperty("archetype").objectReferenceValue = archetype;
            brainSo.FindProperty("animator").objectReferenceValue = anim;
            brainSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<HitFeedback>();

            // World-space Overhead Health Bar & State Badge
            CreateWorldHealthBarChild(root.transform, health, brain, uiHeightOffset);

            return SaveAsPrefab(root, $"Assets/Prefabs/Enemies/{prefabName}.prefab");
        }

        private static AttackTelegraph CreateTelegraphChild(Transform parent, Material mat)
        {
            GameObject telRoot = new GameObject("AttackTelegraph");
            telRoot.transform.SetParent(parent, false);
            telRoot.transform.localPosition = Vector3.zero;

            GameObject lineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineObj.name = "TelegraphLine";
            Object.DestroyImmediate(lineObj.GetComponent<Collider>());
            lineObj.transform.SetParent(telRoot.transform, false);
            lineObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            lineObj.transform.localScale = new Vector3(1.2f, 0.03f, 6.0f);
            lineObj.GetComponent<Renderer>().sharedMaterial = mat;
            lineObj.SetActive(false);

            GameObject circleObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circleObj.name = "TelegraphCircle";
            Object.DestroyImmediate(circleObj.GetComponent<Collider>());
            circleObj.transform.SetParent(telRoot.transform, false);
            circleObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            circleObj.transform.localScale = new Vector3(2.4f, 0.025f, 2.4f);
            circleObj.GetComponent<Renderer>().sharedMaterial = mat;
            circleObj.SetActive(false);

            AttackTelegraph tel = telRoot.AddComponent<AttackTelegraph>();
            SerializedObject so = new SerializedObject(tel);
            SerializedProperty lineProp = so.FindProperty("lineTransform");
            if (lineProp != null) lineProp.objectReferenceValue = lineObj.transform;
            SerializedProperty circleProp = so.FindProperty("circleTransform");
            if (circleProp != null) circleProp.objectReferenceValue = circleObj.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tel;
        }

        private static void CreateWorldHealthBarChild(Transform parent, HealthComponent health, EnemyBrain brain, float heightOffset)
        {
            GameObject canvasObj = new GameObject("WorldHealthCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObj.transform.SetParent(parent, false);

            Canvas c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 38f);
            rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            rt.localPosition = new Vector3(0f, heightOffset, 0f);

            GameObject bgObj = CreateUIImage(canvasObj.transform, "HP_BG", new Vector2(130f, 12f), new Vector2(0f, -8f), new Color(0.1f, 0.12f, 0.16f, 0.85f));
            Image fillImg = CreateUIImage(bgObj.transform, "HP_Fill", new Vector2(126f, 8f), Vector2.zero, HexColor("#E63946")).GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            GameObject badgeBg = CreateUIImage(canvasObj.transform, "Badge_BG", new Vector2(115f, 18f), new Vector2(0f, 9f), new Color(0.06f, 0.09f, 0.14f, 0.82f));
            TMP_Text badgeTxt = CreateUIText(badgeBg.transform, "StateBadge", new Vector2(112f, 18f), Vector2.zero, "PATROL", 12f, Color.white);

            WorldHealthBar whb = canvasObj.AddComponent<WorldHealthBar>();
            SerializedObject so = new SerializedObject(whb);
            so.FindProperty("targetHealth").objectReferenceValue = health;
            so.FindProperty("targetBrain").objectReferenceValue = brain;
            so.FindProperty("fillImage").objectReferenceValue = fillImg;
            so.FindProperty("stateBadgeText").objectReferenceValue = badgeTxt;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PCGConfigSO CreatePCGConfigSO(
            Dictionary<string, GameObject> envPrefabs,
            Dictionary<string, GameObject> gameplayPrefabs,
            Dictionary<string, GameObject> enemyPrefabs,
            Dictionary<string, EnemyArchetypeSO> archetypes)
        {
            PCGConfigSO cfg = CreateOrLoadSO<PCGConfigSO>("Assets/Data/PCG/PCGConfig_Default.asset");
            cfg.terraceElevationStep = 2.5f;
            cfg.depthForwardSpacing = 32.0f;
            cfg.branchLateralOffset = 14.5f;
            cfg.normalArenaSize = new Vector2(22f, 22f);
            cfg.bossArenaSize = new Vector2(26f, 26f);

            cfg.safeZoneRadius = 5.0f;
            cfg.pillarMinSpacing = 3.5f;
            cfg.objectiveMinSpacing = 4.5f;
            cfg.enemyMinSpacing = 2.5f;
            cfg.maxAttemptsPerObject = 30;

            cfg.islandPlatformPrefab = envPrefabs["IslandSlab"];
            cfg.archedBridgePrefab = envPrefabs["Bridge"];
            cfg.stunPillarPrefab = envPrefabs["StunPillar"];

            cfg.lockdownBarrierPrefab = gameplayPrefabs["LockdownBarrier"];
            cfg.mathGateDoorPrefab = gameplayPrefabs["MathGateDoor"];
            cfg.sunBeaconPrefab = gameplayPrefabs["SunBeacon"];
            cfg.runeAnchorPrefab = gameplayPrefabs["RuneAnchor"];
            cfg.stormCrystalPrefab = gameplayPrefabs["StormCrystal"];
            cfg.healOrbPrefab = gameplayPrefabs["HealOrb"];
            cfg.victoryChestPrefab = gameplayPrefabs["VictoryChest"];

            cfg.backdropPrefabs.Clear();
            cfg.backdropPrefabs.Add(envPrefabs["BackdropWindmill"]);
            cfg.backdropPrefabs.Add(envPrefabs["BackdropFountain"]);
            cfg.backdropPrefabs.Add(envPrefabs["BackdropArch"]);
            cfg.backdropPrefabs.Add(envPrefabs["BackdropTree"]);

            cfg.breezeImpPrefab = enemyPrefabs["BreezeImp"];
            cfg.breezeImpArchetype = archetypes["BreezeImp"];
            cfg.skySpritePrefab = enemyPrefabs["SkySprite"];
            cfg.skySpriteArchetype = archetypes["SkySprite"];
            cfg.duskWispPrefab = enemyPrefabs["DuskWisp"];
            cfg.duskWispArchetype = archetypes["DuskWisp"];
            cfg.crystalGolemPrefab = enemyPrefabs["CrystalGolem"];
            cfg.crystalGolemArchetype = archetypes["CrystalGolem"];
            cfg.zephyrGuardianPrefab = enemyPrefabs["ZephyrGuardian"];
            cfg.zephyrGuardianArchetype = archetypes["ZephyrGuardian"];

            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        private static Dictionary<string, GameObject> CreateCorePrefabs(
            Dictionary<string, Material> mats,
            AnimatorController playerAc,
            PCGConfigSO pcgConfig,
            TMP_Text floatingPopupPrefab)
        {
            var dict = new Dictionary<string, GameObject>();
            int playerLayer = LayerMask.NameToLayer("Player");

            // 1. Player.prefab (ASSET_CONTRACT Section 3.3: 1.8m height, pivot at bottom-center)
            GameObject playerRoot = new GameObject("Player");
            playerRoot.tag = "Player";
            if (playerLayer >= 0) playerRoot.layer = playerLayer;

            CharacterController cc = playerRoot.AddComponent<CharacterController>();
            cc.radius = 0.42f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.45f;

            GameObject paladinVisual = InstantiateVisualOnly(
                "Assets/Art/Characters/Paladin_with_Helmet.fbx",
                PrimitiveType.Capsule, Vector3.one, mats["PlayerHero"], preserveTex: true);
            paladinVisual.name = "HeroModel";
            paladinVisual.transform.SetParent(playerRoot.transform, false);
            paladinVisual.transform.localPosition = Vector3.zero;
            paladinVisual.transform.localScale = new Vector3(0.70f, 0.70f, 0.70f);

            // Attach KayKit Paladin Hammer & Shield to hand slots
            Transform rightHand = FindDeepChild(paladinVisual.transform, "handslot.r");
            if (rightHand != null)
            {
                GameObject weapon = InstantiateVisualOnly(
                    "Assets/Art/Characters/paladin_hammer.fbx",
                    PrimitiveType.Cube, new Vector3(0.15f, 0.85f, 0.15f), mats["PlayerHero"], preserveTex: true);
                weapon.transform.SetParent(rightHand, false);
            }

            Transform leftHand = FindDeepChild(paladinVisual.transform, "handslot.l");
            if (leftHand != null)
            {
                GameObject shield = InstantiateVisualOnly(
                    "Assets/Art/Characters/paladin_shield.fbx",
                    PrimitiveType.Cube, new Vector3(0.45f, 0.55f, 0.12f), mats["PlayerHero"], preserveTex: true);
                shield.transform.SetParent(leftHand, false);
            }

            Animator anim = paladinVisual.GetComponent<Animator>();
            if (anim == null) anim = paladinVisual.AddComponent<Animator>();
            anim.runtimeAnimatorController = playerAc;
            anim.applyRootMotion = false;

            HealthComponent playerHealth = playerRoot.AddComponent<HealthComponent>();
            SerializedObject phSo = new SerializedObject(playerHealth);
            phSo.FindProperty("isPlayer").boolValue = true;
            phSo.ApplyModifiedPropertiesWithoutUndo();
            playerRoot.AddComponent<PlayerStats>();
            PlayerController pc = playerRoot.AddComponent<PlayerController>();
            PlayerCombat pCombat = playerRoot.AddComponent<PlayerCombat>();
            playerRoot.AddComponent<HitFeedback>();

            SerializedObject pcSo = new SerializedObject(pc);
            pcSo.FindProperty("animator").objectReferenceValue = anim;
            pcSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pCombatSo = new SerializedObject(pCombat);
            pCombatSo.FindProperty("animator").objectReferenceValue = anim;
            pCombatSo.ApplyModifiedPropertiesWithoutUndo();

            dict["Player"] = SaveAsPrefab(playerRoot, "Assets/Prefabs/Core/Player.prefab");

            // 2. CameraRig.prefab
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 56f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<ThirdPersonCamera>();
            dict["CameraRig"] = SaveAsPrefab(camObj, "Assets/Prefabs/Core/CameraRig.prefab");

            // 3. HUD_Canvas.prefab (Saved UI Prefab as mandated by AI_RULES Section 1.2)
            dict["HUDCanvas"] = CreateHUDCanvasPrefab();

            // 4. GameSystems.prefab
            GameObject sysRoot = new GameObject("GameSystems");
            sysRoot.AddComponent<GameInputReader>();
            CheckpointManager cpMgr = sysRoot.AddComponent<CheckpointManager>();
            sysRoot.AddComponent<TelemetryManager>();
            sysRoot.AddComponent<PlayerModelManager>();
            DDAManager ddaMgr = sysRoot.AddComponent<DDAManager>();
            RunLogExporter exporter = sysRoot.AddComponent<RunLogExporter>();
            sysRoot.AddComponent<IslandContentSpawner>();
            sysRoot.AddComponent<RuntimeNavMeshBuilder>();
            BranchingIslandGenerator gen = sysRoot.AddComponent<BranchingIslandGenerator>();
            gen.SetConfig(pcgConfig);

            FloatingDamageSpawner dmgSpawner = sysRoot.AddComponent<FloatingDamageSpawner>();
            SerializedObject dmgSo = new SerializedObject(dmgSpawner);
            dmgSo.FindProperty("floatingTextPrefab").objectReferenceValue = floatingPopupPrefab;
            dmgSo.ApplyModifiedPropertiesWithoutUndo();

            AudioManager audioMgr = sysRoot.AddComponent<AudioManager>();
            SerializedObject audioSo = new SerializedObject(audioMgr);
            audioSo.FindProperty("sfxSwing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/chop.ogg");
            audioSo.FindProperty("sfxHitImpact").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/impactPunch_heavy_001.ogg");
            audioSo.FindProperty("sfxHeavyImpact").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/impactMining_001.ogg");
            audioSo.FindProperty("sfxDodge").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/switch1.ogg");
            audioSo.FindProperty("sfxGateChoose").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bookOpen.ogg");
            audioSo.FindProperty("sfxRoomClear").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click1.ogg");
            audioSo.FindProperty("sfxPlayerHurt").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/impactWood_medium_001.ogg");
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            GameManager gm = sysRoot.AddComponent<GameManager>();
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("islandGenerator").objectReferenceValue = gen;
            gmSo.FindProperty("ddaManager").objectReferenceValue = ddaMgr;
            gmSo.FindProperty("checkpointManager").objectReferenceValue = cpMgr;
            gmSo.FindProperty("runLogExporter").objectReferenceValue = exporter;
            gmSo.FindProperty("playerPrefab").objectReferenceValue = dict["Player"];
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            dict["GameSystems"] = SaveAsPrefab(sysRoot, "Assets/Prefabs/Core/GameSystems.prefab");
            return dict;
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static GameObject CreateHUDCanvasPrefab()
        {
            Sprite hpFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ui_health_grayscale.png");
            Sprite iconHealth = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/icon_health.png");
            Sprite iconDmg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/icon_dmg.png");

            GameObject canvasRoot = new GameObject("HUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = canvasRoot.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Color cardBg = new Color(0.06f, 0.09f, 0.16f, 0.86f);
            Color goldAccent = HexColor("#FFB703");
            Color cyanAccent = HexColor("#48CAE4");

            // Top-Left Player Vitals & Stats Panel
            GameObject vitalsPanel = CreateAnchoredPanel(canvasRoot.transform, "VitalsPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(440f, 138f), cardBg, cyanAccent);

            if (iconHealth != null)
            {
                Image hIcon = CreateUIImage(vitalsPanel.transform, "HealthIcon", new Vector2(30f, 30f), new Vector2(-186f, 36f), Color.white).GetComponent<Image>();
                hIcon.sprite = iconHealth;
                hIcon.preserveAspect = true;
            }

            GameObject hpBg = CreateUIImage(vitalsPanel.transform, "HPBar_BG", new Vector2(354f, 26f), new Vector2(16f, 36f), new Color(0.11f, 0.14f, 0.21f, 0.96f));
            Image hpFill = CreateUIImage(hpBg.transform, "HPBar_Fill", new Vector2(350f, 22f), Vector2.zero, HexColor("#57CC68"), hpFillSprite).GetComponent<Image>();
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;

            Image shieldFill = CreateUIImage(hpBg.transform, "ShieldBar_Fill", new Vector2(350f, 7f), new Vector2(0f, -9f), cyanAccent).GetComponent<Image>();
            shieldFill.type = Image.Type.Filled;
            shieldFill.fillMethod = Image.FillMethod.Horizontal;
            shieldFill.fillAmount = 0f;

            TMP_Text hpText = CreateUIText(hpBg.transform, "HPValueText", new Vector2(344f, 24f), Vector2.zero, "HP: 100 / 100", 16f, Color.white);
            TMP_Text livesText = CreateUIText(vitalsPanel.transform, "LivesText", new Vector2(390f, 26f), new Vector2(0f, 1f), "LIVES: 3 [***]", 17f, goldAccent);

            if (iconDmg != null)
            {
                Image dIcon = CreateUIImage(vitalsPanel.transform, "DamageIcon", new Vector2(22f, 28f), new Vector2(-186f, -34f), Color.white).GetComponent<Image>();
                dIcon.sprite = iconDmg;
                dIcon.preserveAspect = true;
            }
            TMP_Text statsText = CreateUIText(vitalsPanel.transform, "StatsLineText", new Vector2(360f, 26f), new Vector2(14f, -34f), "ATK: 18  |  SPD: 6.5  |  ASPD: 100%", 15f, new Color(0.90f, 0.94f, 1.0f, 1f));

            // Top-Center Room Progress Banner (compact width so never overlaps right debug panel at 1280x720)
            GameObject bannerPanel = CreateAnchoredPanel(canvasRoot.transform, "ObjectiveBannerPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-30f, -18f), new Vector2(560f, 84f), cardBg, goldAccent);
            TMP_Text depthText = CreateUIText(bannerPanel.transform, "IslandDepthText", new Vector2(540f, 26f), new Vector2(0f, 16f), "DEPTH 0 / 4  (ELEVATION +0.0m)  —  STARTING SKIRMISH", 15f, cyanAccent);
            TMP_Text objText = CreateUIText(bannerPanel.transform, "ObjectiveText", new Vector2(540f, 30f), new Vector2(0f, -15f), "ENTER ISLAND ARENA TO BEGIN", 19f, goldAccent);

            // Boss Bar Panel (below Top-Center Banner)
            GameObject bossPanel = CreateAnchoredPanel(canvasRoot.transform, "BossBarPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-30f, -110f), new Vector2(560f, 74f), new Color(0.18f, 0.06f, 0.09f, 0.92f), HexColor("#E63946"));
            TMP_Text bossTitle = CreateUIText(bossPanel.transform, "BossTitleText", new Vector2(530f, 24f), new Vector2(0f, 18f), "THE ZEPHYR GUARDIAN", 17f, Color.white);
            TMP_Text bossPhase = CreateUIText(bossPanel.transform, "BossPhaseText", new Vector2(530f, 20f), new Vector2(0f, -2f), "PHASE I: SKY SANCTUM WARDEN", 13f, goldAccent);
            GameObject bossHpBg = CreateUIImage(bossPanel.transform, "BossHP_BG", new Vector2(510f, 16f), new Vector2(0f, -21f), new Color(0.14f, 0.07f, 0.09f, 1f));
            Image bossHpFill = CreateUIImage(bossHpBg.transform, "BossHP_Fill", new Vector2(506f, 12f), Vector2.zero, HexColor("#E63946"), hpFillSprite).GetComponent<Image>();
            bossHpFill.type = Image.Type.Filled;
            bossHpFill.fillMethod = Image.FillMethod.Horizontal;

            // Bottom-Center Interaction Prompt
            GameObject promptPanel = CreateAnchoredPanel(canvasRoot.transform, "InteractionPromptPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(440f, 52f), cardBg, goldAccent);
            TMP_Text promptText = CreateUIText(promptPanel.transform, "InteractionPromptText", new Vector2(416f, 34f), Vector2.zero, "[E] Interact", 19f, goldAccent);

            // Top-Right AI & DDA Debug Overlay (F1)
            GameObject debugPanel = CreateAnchoredPanel(canvasRoot.transform, "AIDebugOverlayPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(475f, 224f), new Color(0.05f, 0.08f, 0.14f, 0.88f), cyanAccent);
            TMP_Text debugText = CreateUIText(debugPanel.transform, "DebugContentText", new Vector2(445f, 154f), new Vector2(0f, 22f), "[F1] AI & DDA TELEMETRY OVERLAY", 13f, Color.white);
            debugText.alignment = TextAlignmentOptions.TopLeft;
            Button toggleDdaBtn = CreateUIButton(debugPanel.transform, "ToggleDDAButton", new Vector2(210f, 34f), new Vector2(-112f, -84f), "Toggle Static/Adaptive", HexColor("#2D6CDF"));
            Button newSeedBtn = CreateUIButton(debugPanel.transform, "NewSeedButton", new Vector2(210f, 34f), new Vector2(112f, -84f), "New Random Seed", HexColor("#3EA355"));

            // Center Victory / GameOver Modal Panel
            GameObject modalPanel = CreateAnchoredPanel(canvasRoot.transform, "ModalPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 400f), new Color(0.06f, 0.09f, 0.16f, 0.95f), goldAccent);
            TMP_Text modalTitle = CreateUIText(modalPanel.transform, "ModalTitleText", new Vector2(680f, 48f), new Vector2(0f, 130f), "VICTORY!", 32f, HexColor("#57CC68"));
            TMP_Text modalSummary = CreateUIText(modalPanel.transform, "ModalSummaryText", new Vector2(660f, 150f), new Vector2(0f, 20f), "Run Summary", 18f, Color.white);
            Button retryBtn = CreateUIButton(modalPanel.transform, "RetrySameSeedBtn", new Vector2(204f, 48f), new Vector2(-218f, -125f), "Retry Same Seed", HexColor("#2D6CDF"));
            Button randBtn = CreateUIButton(modalPanel.transform, "NewRandomSeedBtn", new Vector2(204f, 48f), new Vector2(0f, -125f), "New Random Run", HexColor("#3EA355"));
            Button menuBtn = CreateUIButton(modalPanel.transform, "MainMenuBtn", new Vector2(204f, 48f), new Vector2(218f, -125f), "Main Menu", HexColor("#A47148"));

            // Wire HUDController
            HUDController hud = canvasRoot.AddComponent<HUDController>();
            SerializedObject hudSo = new SerializedObject(hud);
            hudSo.FindProperty("hpFillBar").objectReferenceValue = hpFill;
            hudSo.FindProperty("shieldFillBar").objectReferenceValue = shieldFill;
            hudSo.FindProperty("hpValueText").objectReferenceValue = hpText;
            hudSo.FindProperty("livesValueText").objectReferenceValue = livesText;
            hudSo.FindProperty("statsLineText").objectReferenceValue = statsText;
            hudSo.FindProperty("islandDepthText").objectReferenceValue = depthText;
            hudSo.FindProperty("objectiveBannerText").objectReferenceValue = objText;
            hudSo.FindProperty("interactionPromptRoot").objectReferenceValue = promptPanel;
            hudSo.FindProperty("interactionPromptText").objectReferenceValue = promptText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire BossBarUI
            BossBarUI bossUi = canvasRoot.AddComponent<BossBarUI>();
            SerializedObject bossSo = new SerializedObject(bossUi);
            bossSo.FindProperty("bossPanelRoot").objectReferenceValue = bossPanel;
            bossSo.FindProperty("bossTitleText").objectReferenceValue = bossTitle;
            bossSo.FindProperty("bossPhaseText").objectReferenceValue = bossPhase;
            bossSo.FindProperty("bossHpFillBar").objectReferenceValue = bossHpFill;
            bossSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire AIDebugOverlay
            AIDebugOverlay dbg = canvasRoot.AddComponent<AIDebugOverlay>();
            SerializedObject dbgSo = new SerializedObject(dbg);
            dbgSo.FindProperty("debugPanelRoot").objectReferenceValue = debugPanel;
            dbgSo.FindProperty("debugContentText").objectReferenceValue = debugText;
            dbgSo.FindProperty("toggleDdaModeButton").objectReferenceValue = toggleDdaBtn;
            dbgSo.FindProperty("newRandomSeedButton").objectReferenceValue = newSeedBtn;
            dbgSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire ModalController
            ModalController modal = canvasRoot.AddComponent<ModalController>();
            SerializedObject modalSo = new SerializedObject(modal);
            modalSo.FindProperty("modalPanelRoot").objectReferenceValue = modalPanel;
            modalSo.FindProperty("modalTitleText").objectReferenceValue = modalTitle;
            modalSo.FindProperty("modalSummaryText").objectReferenceValue = modalSummary;
            modalSo.FindProperty("retrySameSeedButton").objectReferenceValue = retryBtn;
            modalSo.FindProperty("newRandomSeedButton").objectReferenceValue = randBtn;
            modalSo.FindProperty("mainMenuButton").objectReferenceValue = menuBtn;
            modalSo.ApplyModifiedPropertiesWithoutUndo();

            bossPanel.SetActive(false);
            promptPanel.SetActive(false);
            modalPanel.SetActive(false);

            return SaveAsPrefab(canvasRoot, "Assets/Prefabs/Core/HUD_Canvas.prefab");
        }

        private static void CreateGameplayScene(Dictionary<string, Material> mats, Dictionary<string, GameObject> corePrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupDaylightAtmosphere(mats);
            CreateEventSystem();

            PrefabUtility.InstantiatePrefab(corePrefabs["CameraRig"]);
            PrefabUtility.InstantiatePrefab(corePrefabs["HUDCanvas"]);
            PrefabUtility.InstantiatePrefab(corePrefabs["GameSystems"]);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Gameplay.unity");
        }

        private static void CreateCombatSandboxScene(
            Dictionary<string, Material> mats,
            Dictionary<string, GameObject> envPrefabs,
            Dictionary<string, GameObject> gameplayPrefabs,
            Dictionary<string, GameObject> enemyPrefabs,
            Dictionary<string, GameObject> corePrefabs,
            Dictionary<string, EnemyArchetypeSO> archetypes)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupDaylightAtmosphere(mats);
            CreateEventSystem();

            GameObject camRig = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabs["CameraRig"]);
            PrefabUtility.InstantiatePrefab(corePrefabs["HUDCanvas"]);

            GameObject sysObj = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabs["GameSystems"]);
            GameManager gm = sysObj.GetComponent<GameManager>();
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("autoGenerateOnStart").boolValue = false;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            // Place a 22x22 Sandbox Island + Scenic Floating Backdrops
            GameObject islandObj = (GameObject)PrefabUtility.InstantiatePrefab(envPrefabs["IslandSlab"]);
            islandObj.transform.position = Vector3.zero;
            IslandNode node = islandObj.AddComponent<IslandNode>();
            node.ConfigureIsland(0, 0, IslandEncounterType.ColossusArena, new Vector2(22f, 22f), islandObj.transform);

            PlaceScenicBackdrop(envPrefabs["BackdropWindmill"], new Vector3(-24f, -1.5f, 18f), 25f);
            PlaceScenicBackdrop(envPrefabs["BackdropFountain"], new Vector3(24f, -1.0f, 18f), -35f);
            PlaceScenicBackdrop(envPrefabs["BackdropTree"], new Vector3(0f, -2.5f, 30f), 10f);

            // Place Stun Pillars, SunBeacon, RuneAnchor
            GameObject p1 = (GameObject)PrefabUtility.InstantiatePrefab(envPrefabs["StunPillar"]);
            p1.transform.position = new Vector3(-4.5f, 0f, 3.0f);
            GameObject p2 = (GameObject)PrefabUtility.InstantiatePrefab(envPrefabs["StunPillar"]);
            p2.transform.position = new Vector3(4.5f, 0f, 3.0f);

            GameObject sb = (GameObject)PrefabUtility.InstantiatePrefab(gameplayPrefabs["SunBeacon"]);
            sb.transform.position = new Vector3(-5.5f, 0f, -2.0f);
            node.RegisterSunBeacon(sb.GetComponent<SunBeacon>());

            GameObject ra = (GameObject)PrefabUtility.InstantiatePrefab(gameplayPrefabs["RuneAnchor"]);
            ra.transform.position = new Vector3(5.5f, 0f, -2.0f);
            node.RegisterRuneAnchor(ra.GetComponent<RuneAnchor>());

            // Bake NavMesh before spawning NavMeshAgent enemies
            sysObj.GetComponent<RuntimeNavMeshBuilder>().RebuildNavMesh();

            // Place Player & Enemies
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabs["Player"]);
            player.transform.position = new Vector3(0f, 0.2f, -7.5f);
            camRig.GetComponent<ThirdPersonCamera>().SetFollowTarget(player.transform);

            GameObject imp = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefabs["BreezeImp"]);
            imp.transform.position = new Vector3(-3.5f, 0f, 5.5f);
            imp.GetComponent<EnemyBrain>().SetupEnemy(archetypes["BreezeImp"], node, DifficultyContext.Default);
            node.RegisterSpawnedEnemy(imp.GetComponent<EnemyBrain>());

            GameObject golem = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefabs["CrystalGolem"]);
            golem.transform.position = new Vector3(2.5f, 0f, 6.5f);
            golem.GetComponent<EnemyBrain>().SetupEnemy(archetypes["CrystalGolem"], node, DifficultyContext.Default);
            node.RegisterSpawnedEnemy(golem.GetComponent<EnemyBrain>());

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/CombatSandbox.unity");
        }

        private static void CreateAssetShowcaseScene(
            Dictionary<string, Material> mats,
            Dictionary<string, GameObject> envPrefabs,
            Dictionary<string, GameObject> gameplayPrefabs,
            Dictionary<string, GameObject> enemyPrefabs,
            Dictionary<string, GameObject> corePrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupDaylightAtmosphere(mats);
            CreateEventSystem();

            GameObject camObj = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabs["CameraRig"]);
            // Keep overview static in Showcase so camera frames all 6 characters, 7 props, and floating backdrops
            ThirdPersonCamera tpc = camObj.GetComponent<ThirdPersonCamera>();
            if (tpc != null) Object.DestroyImmediate(tpc);
            camObj.transform.position = new Vector3(0f, 8.6f, -18.5f);
            camObj.transform.rotation = Quaternion.Euler(21f, 0f, 0f);

            GameObject floor = (GameObject)PrefabUtility.InstantiatePrefab(envPrefabs["IslandSlab"]);
            floor.transform.position = Vector3.zero;
            floor.AddComponent<RuntimeNavMeshBuilder>().RebuildNavMesh();

            PlaceScenicBackdrop(envPrefabs["BackdropWindmill"], new Vector3(-22f, -1.5f, 18f), 20f);
            PlaceScenicBackdrop(envPrefabs["BackdropFountain"], new Vector3(22f, -1.0f, 18f), -30f);
            PlaceScenicBackdrop(envPrefabs["BackdropArch"], new Vector3(-24f, -2.5f, -6f), 45f);
            PlaceScenicBackdrop(envPrefabs["BackdropTree"], new Vector3(24f, -2.5f, -6f), -45f);

            // Line up Hero + 5 New KayKit Enemy Archetypes + Props for M0 Normalization Inspection
            GameObject pShow = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabs["Player"]);
            pShow.name = "Showcase_Player";
            pShow.transform.position = new Vector3(-7.2f, 0.05f, -2.2f);
            pShow.transform.rotation = Quaternion.Euler(0f, 165f, 0f);

            string[] enemyKeys = { "BreezeImp", "SkySprite", "DuskWisp", "CrystalGolem", "ZephyrGuardian" };
            for (int i = 0; i < enemyKeys.Length; i++)
            {
                GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefabs[enemyKeys[i]]);
                e.transform.position = new Vector3(-4.0f + i * 2.85f, 0.05f, -2.2f);
                e.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            string[] gpKeys = { "StunPillar", "SunBeacon", "RuneAnchor", "StormCrystal", "HealOrb", "VictoryChest", "MathGateDoor" };
            for (int i = 0; i < gpKeys.Length; i++)
            {
                GameObject src = envPrefabs.ContainsKey(gpKeys[i]) ? envPrefabs[gpKeys[i]] : gameplayPrefabs[gpKeys[i]];
                GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(src);
                g.transform.position = new Vector3(-7.8f + i * 2.6f, 0.05f, 4.2f);
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/AssetShowcase.unity");
        }

        private static void CreateMainMenuScene(
            Dictionary<string, Material> mats,
            Dictionary<string, GameObject> envPrefabs,
            Dictionary<string, GameObject> gameplayPrefabs,
            Dictionary<string, GameObject> enemyPrefabs,
            Dictionary<string, GameObject> corePrefabs)
        {
            SkyGateMenuArtGenerator.EnsureMenuArtGenerated(out TMP_FontAsset luckiestFont, out TMP_FontAsset fredokaFont, out Material menuParticleMat);
            Material luckiestOutlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/LuckiestGuy-Regular SDF - Cartoon.mat");
            Material fredokaOutlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Fredoka-Bold SDF - Cartoon.mat");

            Sprite sprPost = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_WoodPost.png");
            Sprite sprPlankR = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_SignPlank_Right.png");
            Sprite sprPlankL = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_SignPlank_Left.png");
            Sprite sprCrest = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_LogoCrest_Stone.png");
            Sprite sprFlag = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_LogoFlag.png");
            Sprite sprMushroom = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_MushroomProp.png");
            Sprite sprGrass = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_GrassTuft.png");
            Sprite sprBoard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_WoodBoardPanel.png");
            Sprite sprPill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/MainMenu/SPR_WoodButtonPill.png");

            AudioClip sfxHover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/switch1.ogg");
            AudioClip sfxWoodClick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/impactWood_heavy_000.ogg")
                ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click1.ogg");
            AudioClip sfxPlayStart = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/click1.ogg");
            AudioClip sfxBookOpen = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bookOpen.ogg");
            AudioClip sfxBookClose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bookClose.ogg");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupDaylightAtmosphere(mats);

            // Bright azure-aqua skybox + sunny horizon atmosphere matching the reference visual tone
            if (mats != null && mats.TryGetValue("SkyboxMidday", out Material middaySky) && middaySky != null)
                RenderSettings.skybox = middaySky;
            RenderSettings.ambientSkyColor = HexColor("#A8E6FF");
            RenderSettings.ambientEquatorColor = HexColor("#DCF5FF");
            RenderSettings.ambientGroundColor = HexColor("#7A7058");
            RenderSettings.fogColor = HexColor("#BCEBFF");
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 250f;
            DynamicGI.UpdateEnvironment();

            CreateEventSystem();

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 46f;
            cam.farClipPlane = 320f;
            camObj.transform.position = new Vector3(-6.8f, 8.2f, -18.0f);
            camObj.transform.rotation = Quaternion.Euler(16f, 18f, 0f);
            camObj.AddComponent<AudioListener>();

            // === LIVE ANIMATED 3D SKY-ISLAND DIORAMA ===
            GameObject dioramaRoot = new GameObject("MainMenuDiorama");
            GameObject mainGroup = new GameObject("MainIslandGroup");
            mainGroup.transform.SetParent(dioramaRoot.transform, false);
            mainGroup.transform.position = new Vector3(0f, 0f, 4f);

            var swayTargets = new List<Transform>();

            if (envPrefabs.TryGetValue("IslandSlab", out GameObject slabPrefab) && slabPrefab != null)
            {
                GameObject vistaIsland = (GameObject)PrefabUtility.InstantiatePrefab(slabPrefab);
                vistaIsland.transform.SetParent(mainGroup.transform, false);
                vistaIsland.transform.localPosition = Vector3.zero;
                CollectSwayTransformsFromIsland(vistaIsland.transform, swayTargets);
            }

            // Scenic winding sky-stream reaching both cliff rims + front/back waterfalls + arched bridge
            Vector3 streamCenter = new Vector3(4.8f, 0.018f, -0.3f);
            Quaternion streamRot = Quaternion.Euler(0f, -12f, 0f);
            Vector3 streamForward = streamRot * Vector3.forward;
            const float halfStreamLen = 10.65f;

            GameObject streamObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            streamObj.name = "MenuFlowingStream";
            Object.DestroyImmediate(streamObj.GetComponent<Collider>());
            streamObj.transform.SetParent(mainGroup.transform, false);
            streamObj.transform.localPosition = streamCenter;
            streamObj.transform.localRotation = streamRot;
            streamObj.transform.localScale = new Vector3(2.15f, 0.045f, halfStreamLen * 2f);
            Material streamVisualMat = mats.TryGetValue("Waterfall", out Material wfMat)
                ? wfMat
                : (mats.TryGetValue("StreamWater", out Material wm) ? wm : null);
            if (streamVisualMat != null)
                streamObj.GetComponent<Renderer>().sharedMaterial = streamVisualMat;
            streamObj.AddComponent<UVWaterfallScroller>();

            // Vertical cascading waterfall drops off both cliff rims of the meadow island
            for (int wIdx = 0; wIdx < 2; wIdx++)
            {
                float dirSign = wIdx == 0 ? -1f : 1f;
                GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                drop.name = $"MenuWaterfallDrop_{wIdx}";
                Object.DestroyImmediate(drop.GetComponent<Collider>());
                drop.transform.SetParent(mainGroup.transform, false);
                drop.transform.localPosition = streamCenter + streamForward * (dirSign * halfStreamLen) + new Vector3(0f, -2.55f, 0f);
                drop.transform.localRotation = streamRot;
                drop.transform.localScale = new Vector3(2.10f, 5.20f, 0.44f);
                if (streamVisualMat != null)
                    drop.GetComponent<Renderer>().sharedMaterial = streamVisualMat;
                drop.AddComponent<UVWaterfallScroller>();
            }

            // Natural stone pebbles lining both banks of the stream
            if (mats.TryGetValue("NaturePathRocks", out Material _))
            {
                Vector3[] bankRocks =
                {
                    new Vector3(3.4f, 0.03f, -5.2f),
                    new Vector3(4.9f, 0.03f, -6.4f),
                    new Vector3(6.8f, 0.03f, -4.6f),
                    new Vector3(2.6f, 0.03f,  4.2f),
                    new Vector3(4.9f, 0.03f,  5.0f),
                    new Vector3(6.2f, 0.03f, -2.4f)
                };
                for (int rIdx = 0; rIdx < bankRocks.Length; rIdx++)
                {
                    string[] rockNames =
                    {
                        "Assets/Art/Environment/StylizedNature/FBX/RockPath_Round_Small_1.fbx",
                        "Assets/Art/Environment/StylizedNature/FBX/RockPath_Round_Small_2.fbx",
                        "Assets/Art/Environment/StylizedNature/FBX/RockPath_Round_Small_3.fbx"
                    };
                    GameObject rk = InstantiateNatureProp(rockNames[rIdx % rockNames.Length], Vector3.one * (0.48f + (rIdx % 2) * 0.14f), mats);
                    rk.transform.SetParent(mainGroup.transform, false);
                    rk.transform.localPosition = bankRocks[rIdx];
                    rk.transform.localRotation = Quaternion.Euler(0f, rIdx * 57f, 0f);
                }
            }

            GameObject woodBridge = BuildMenuWoodenBridge(mats);
            woodBridge.transform.SetParent(mainGroup.transform, false);
            woodBridge.transform.localPosition = new Vector3(4.8f, 0.08f, -0.2f);
            woodBridge.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);

            // Stylized Nature pines, flowering bushes & spotted mushrooms ringing perimeter
            AddMenuNatureDecor(mainGroup.transform, mats, swayTargets);

            // Glowing Sun Beacon + Open Rune Archway (no locked text labels cluttering logo)
            if (gameplayPrefabs.TryGetValue("SunBeacon", out GameObject beaconPrefab) && beaconPrefab != null)
            {
                GameObject beaconShow = (GameObject)PrefabUtility.InstantiatePrefab(beaconPrefab);
                beaconShow.transform.SetParent(mainGroup.transform, false);
                beaconShow.transform.localPosition = new Vector3(-5.8f, 0.05f, -1.6f);
            }
            if (gameplayPrefabs.TryGetValue("MathGateDoor", out GameObject gatePrefab) && gatePrefab != null)
            {
                GameObject gateShow = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab);
                gateShow.transform.SetParent(mainGroup.transform, false);
                gateShow.transform.localPosition = new Vector3(0f, 0.05f, 10.6f);
                foreach (TMP_Text gateTmp in gateShow.GetComponentsInChildren<TMP_Text>(true))
                    gateTmp.gameObject.SetActive(false);
                Transform lbl = FindChildRecursive(gateShow.transform, "Label");
                if (lbl != null) lbl.gameObject.SetActive(false);
                Transform safePortal = FindChildRecursive(gateShow.transform, "PortalCrystal");
                if (safePortal != null && mats.TryGetValue("PortalSafe", out Material pSafeMat))
                {
                    Renderer pr = safePortal.GetComponent<Renderer>();
                    if (pr != null) pr.sharedMaterial = pSafeMat;
                }
            }

            // Living Animated Characters standing across the stream bridge (visual Animator idle only)
            if (corePrefabs.TryGetValue("Player", out GameObject playerPrefab) && playerPrefab != null)
                InstantiateScenicCharacter(playerPrefab, mainGroup.transform, new Vector3(-2.6f, 0.05f, -1.5f), 96f);
            if (enemyPrefabs != null)
            {
                if (enemyPrefabs.TryGetValue("BreezeImp", out GameObject impPrefab) && impPrefab != null)
                {
                    InstantiateScenicCharacter(impPrefab, mainGroup.transform, new Vector3(7.3f, 0.05f, 0.8f), -102f);
                    InstantiateScenicCharacter(impPrefab, mainGroup.transform, new Vector3(8.5f, 0.05f, -2.5f), -78f);
                }
                if (enemyPrefabs.TryGetValue("SkySprite", out GameObject spritePrefab) && spritePrefab != null)
                    InstantiateScenicCharacter(spritePrefab, mainGroup.transform, new Vector3(6.8f, 0.05f, 4.1f), -125f);
            }

            // Orbiting Sky Islands ring placed strictly OUTSIDE camera orbit radius (r = 36..44m)
            var orbitingIslands = new List<Transform>();
            var islandConfigs = new (string key, Vector3 pos, float yaw)[]
            {
                ("BackdropWindmill", new Vector3(-27f, 3.2f, 29f), 26f),
                ("BackdropFountain", new Vector3(28f, 2.4f, 28f), -34f),
                ("BackdropArch",     new Vector3(-31f, 0.6f, -20f), 58f),
                ("BackdropTree",     new Vector3(31f, 1.0f, -18f), -52f)
            };
            foreach (var ic in islandConfigs)
            {
                if (envPrefabs.TryGetValue(ic.key, out GameObject bPrefab) && bPrefab != null)
                {
                    GameObject bInst = (GameObject)PrefabUtility.InstantiatePrefab(bPrefab);
                    bInst.transform.SetParent(dioramaRoot.transform, false);
                    bInst.transform.position = ic.pos;
                    bInst.transform.rotation = Quaternion.Euler(0f, ic.yaw, 0f);
                    orbitingIslands.Add(bInst.transform);
                }
            }

            // Multi-altitude drifting 3D cloud ring (r = 34..54m)
            var driftingClouds = new List<MainMenuShowcaseDiorama.CloudOrbitItem>();
            mats.TryGetValue("CloudWhite", out Material cloudMat);
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 36f * Mathf.Deg2Rad;
                float radius = 36f + (i % 3) * 7.5f;
                float yPos = -1.5f + (i % 4) * 3.1f;
                string cMesh = (i % 2 == 0)
                    ? "Assets/Art/Environment/MedievalHex/cloud_big.obj"
                    : "Assets/Art/Environment/MedievalHex/cloud_small.obj";
                GameObject cld = InstantiateVisualOnly(cMesh, PrimitiveType.Sphere, Vector3.one, cloudMat);
                cld.name = $"MenuOrbitCloud_{i}";
                cld.transform.SetParent(dioramaRoot.transform, false);
                cld.transform.position = new Vector3(Mathf.Cos(ang) * radius, yPos, 4f + Mathf.Sin(ang) * radius);
                cld.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                cld.transform.localScale = Vector3.one * (1.85f + (i % 3) * 0.65f);

                driftingClouds.Add(new MainMenuShowcaseDiorama.CloudOrbitItem
                {
                    target = cld.transform,
                    orbitDegreesPerSec = (i % 2 == 0 ? 1.15f : 0.72f),
                    bobAmplitude = 0.45f + (i % 3) * 0.15f,
                    bobFrequency = 0.45f + (i % 2) * 0.2f,
                    phaseOffset = i * 1.1f
                });
            }

            // Lightweight Golden / Sky-Cyan Floating Sparkle Particles (prewarmed, max 45)
            CreateMenuFloatingParticles(dioramaRoot.transform, menuParticleMat);

            MainMenuShowcaseDiorama dioramaComp = dioramaRoot.AddComponent<MainMenuShowcaseDiorama>();
            dioramaComp.EditorConfigure(cam, mainGroup.transform, swayTargets, orbitingIslands, driftingClouds);

            // === STYLIZED CARTOON MAINMENU UI (WOODEN SIGNPOST + ROCKY CREST) ===
            GameObject canvasRoot = new GameObject("MainMenu_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas c = canvasRoot.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            AudioSource uiAudio = canvasRoot.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;

            TMP_FontAsset uiDisplayFont = luckiestFont != null ? luckiestFont : cachedDefaultFont;
            TMP_FontAsset uiBodyFont = fredokaFont != null ? fredokaFont : cachedDefaultFont;

            // 1. Top-Center Rocky Logo Crest + Animated Waving Pennant Flag
            GameObject crestGroup = CreateUIRect(canvasRoot.transform, "LogoCrestGroup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(680f, 300f));

            GameObject flagObj = CreateUISpriteObject(crestGroup.transform, "LogoBannerFlag", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(154f, 52f), new Vector2(212f, 168f), sprFlag, Color.white, false);
            RectTransform flagRt = flagObj.GetComponent<RectTransform>();
            flagRt.pivot = new Vector2(0.14f, 0.18f);
            flagRt.localRotation = Quaternion.Euler(0f, 0f, -6f);

            CreateUISpriteObject(crestGroup.transform, "LogoCrestStone", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 300f), sprCrest, Color.white, false);

            TMP_Text logoTop = CreateStyledText(crestGroup.transform, "TitlePrimary", new Vector2(540f, 110f), new Vector2(-2f, 12f), "SKYGATE", 80f, HexColor("#FAF3DE"), uiDisplayFont, luckiestOutlineMat);
            logoTop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.4f);

            CreateStyledText(crestGroup.transform, "TitleSub", new Vector2(420f, 56f), new Vector2(0f, -74f), "TACTICS", 40f, HexColor("#99E638"), uiDisplayFont, luckiestOutlineMat);

            // 2. Bottom-Right Stylized Wooden Signpost (PLAY, SETTINGS, EXIT) + Toadstool Foot
            GameObject signpostGroup = CreateUIRect(canvasRoot.transform, "SignpostGroup", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-295f, -12f), new Vector2(580f, 660f));
            signpostGroup.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

            GameObject trunk = CreateUISpriteObject(signpostGroup.transform, "SignpostTrunk", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 295f), new Vector2(146f, 600f), sprPost, Color.white, false);
            trunk.transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);

            Button playBtn = CreateWoodPlankButton(signpostGroup.transform, "PlayPlankButton", new Vector2(36f, 492f), new Vector2(458f, 134f), 5.4f, "PLAY", 52f, new Vector2(-18f, 2f), sprPlankR, uiDisplayFont, luckiestOutlineMat, uiAudio, sfxHover, sfxWoodClick);
            Button settingsBtn = CreateWoodPlankButton(signpostGroup.transform, "SettingsPlankButton", new Vector2(-34f, 350f), new Vector2(496f, 136f), -1.8f, "SETTINGS", 38f, new Vector2(22f, 2f), sprPlankL, uiDisplayFont, luckiestOutlineMat, uiAudio, sfxHover, sfxWoodClick);
            Button exitBtn = CreateWoodPlankButton(signpostGroup.transform, "ExitPlankButton", new Vector2(-22f, 208f), new Vector2(448f, 130f), 3.1f, "EXIT", 46f, new Vector2(18f, 2f), sprPlankL, uiDisplayFont, luckiestOutlineMat, uiAudio, sfxHover, sfxWoodClick);

            // Grass tufts & cartoon mushrooms nestled around trunk base
            CreateUISpriteObject(signpostGroup.transform, "GrassFoot_L", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-94f, 44f), new Vector2(142f, 76f), sprGrass, Color.white, false);
            CreateUISpriteObject(signpostGroup.transform, "GrassFoot_R", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(102f, 40f), new Vector2(146f, 78f), sprGrass, Color.white, false);
            GameObject mL = CreateUISpriteObject(signpostGroup.transform, "Mushroom_BaseL", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-118f, 54f), new Vector2(76f, 68f), sprMushroom, Color.white, false);
            mL.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            GameObject mR = CreateUISpriteObject(signpostGroup.transform, "Mushroom_BaseR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(138f, 58f), new Vector2(96f, 86f), sprMushroom, Color.white, false);
            mR.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            GameObject mFR = CreateUISpriteObject(signpostGroup.transform, "Mushroom_BaseFR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(232f, 38f), new Vector2(58f, 52f), sprMushroom, Color.white, false);
            mFR.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

            // 3. Bottom-Left "credits" Button + Mushroom & Bottom-Center Footer
            GameObject creditsObj = CreateUIRect(canvasRoot.transform, "CreditsButton", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(162f, 68f), new Vector2(236f, 74f));
            Image credHit = creditsObj.AddComponent<Image>();
            credHit.color = Color.clear;
            Button creditsBtn = creditsObj.AddComponent<Button>();
            creditsBtn.transition = Selectable.Transition.None;
            creditsBtn.targetGraphic = credHit;
            TMP_Text credTxt = CreateStyledText(creditsObj.transform, "Label", new Vector2(230f, 68f), Vector2.zero, "credits", 48f, HexColor("#F8FAFD"), uiBodyFont, fredokaOutlineMat);
            MainMenuButtonFX credFx = creditsObj.AddComponent<MainMenuButtonFX>();
            credFx.ConfigureMotion(1.10f, 2.0f, 0f, 1f);
            credFx.ConfigureTintTarget(credTxt, HexColor("#F8FAFD"), HexColor("#FFE880"), HexColor("#D8E4F0"));
            credFx.ConfigureAudio(uiAudio, sfxHover, sfxWoodClick);

            GameObject mCred = CreateUISpriteObject(canvasRoot.transform, "CreditsMushroom", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(284f, 46f), new Vector2(58f, 50f), sprMushroom, Color.white, false);
            mCred.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

            GameObject footerObj = CreateUIRect(canvasRoot.transform, "FooterBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(820f, 36f));
            CreateStyledText(footerObj.transform, "FooterText", new Vector2(820f, 36f), Vector2.zero, "© SkyGate Tactics  •  3D Step-Terrace Action Roguelike (PCG + AI + DDA)", 15f, HexColor("#F5FAFF"), uiBodyFont, fredokaOutlineMat);

            // 4. Stylized Wooden Settings Modal (Audio, Orbit Motion, DDA, Seed + Bonus Modes)
            BuildSettingsModal(
                canvasRoot.transform, sprBoard, sprPill, uiDisplayFont, luckiestOutlineMat, uiBodyFont, fredokaOutlineMat,
                uiAudio, sfxHover, sfxWoodClick,
                out GameObject settingsRoot, out CanvasGroup settingsGroup, out RectTransform settingsCard,
                out Slider volSlider, out TMP_Text volPct, out Toggle orbitToggle, out Toggle ddaToggle,
                out TMP_InputField seedInput, out Button randSeedBtn, out Button combatBtn, out Button showcaseBtn, out Button closeSettingsBtn);

            // 5. Stylized Credits Modal
            BuildCreditsModal(
                canvasRoot.transform, sprBoard, sprPill, uiDisplayFont, luckiestOutlineMat, uiBodyFont, fredokaOutlineMat,
                uiAudio, sfxHover, sfxWoodClick,
                out GameObject creditsRoot, out CanvasGroup creditsGroup, out RectTransform creditsCard, out Button closeCreditsBtn);

            // 6. Full-Screen Smooth Transition Veil (top-most layer)
            GameObject fadeObj = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            fadeObj.transform.SetParent(canvasRoot.transform, false);
            RectTransform fadeRt = fadeObj.GetComponent<RectTransform>();
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = Vector2.zero;
            fadeRt.offsetMax = Vector2.zero;
            Image fadeImg = fadeObj.GetComponent<Image>();
            fadeImg.color = HexColor("#E6F7FF");
            fadeImg.raycastTarget = false;
            CanvasGroup fadeGroup = fadeObj.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;

            // Wire MainMenuController cleanly
            MainMenuController ctrl = canvasRoot.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("startRunButton").objectReferenceValue = playBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("quitButton").objectReferenceValue = exitBtn;
            so.FindProperty("creditsButton").objectReferenceValue = creditsBtn;

            so.FindProperty("crestRect").objectReferenceValue = crestGroup.GetComponent<RectTransform>();
            so.FindProperty("flagRect").objectReferenceValue = flagRt;
            so.FindProperty("signpostGroupRect").objectReferenceValue = signpostGroup.GetComponent<RectTransform>();
            so.FindProperty("screenFadeGroup").objectReferenceValue = fadeGroup;
            so.FindProperty("diorama").objectReferenceValue = dioramaComp;

            so.FindProperty("settingsModalRoot").objectReferenceValue = settingsRoot;
            so.FindProperty("settingsModalGroup").objectReferenceValue = settingsGroup;
            so.FindProperty("settingsModalCard").objectReferenceValue = settingsCard;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = volSlider;
            so.FindProperty("volumePercentText").objectReferenceValue = volPct;
            so.FindProperty("orbitCameraToggle").objectReferenceValue = orbitToggle;
            so.FindProperty("adaptiveDdaToggle").objectReferenceValue = ddaToggle;
            so.FindProperty("seedInputField").objectReferenceValue = seedInput;
            so.FindProperty("randomizeSeedButton").objectReferenceValue = randSeedBtn;
            so.FindProperty("closeSettingsButton").objectReferenceValue = closeSettingsBtn;

            so.FindProperty("creditsModalRoot").objectReferenceValue = creditsRoot;
            so.FindProperty("creditsModalGroup").objectReferenceValue = creditsGroup;
            so.FindProperty("creditsModalCard").objectReferenceValue = creditsCard;
            so.FindProperty("closeCreditsButton").objectReferenceValue = closeCreditsBtn;

            so.FindProperty("openCombatSandboxButton").objectReferenceValue = combatBtn;
            so.FindProperty("openAssetShowcaseButton").objectReferenceValue = showcaseBtn;

            so.FindProperty("uiAudioSource").objectReferenceValue = uiAudio;
            so.FindProperty("sfxModalOpen").objectReferenceValue = sfxBookOpen;
            so.FindProperty("sfxModalClose").objectReferenceValue = sfxBookClose;
            so.FindProperty("sfxPlayStart").objectReferenceValue = sfxPlayStart;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        private static GameObject BuildMenuWoodenBridge(Dictionary<string, Material> mats)
        {
            GameObject bridge = new GameObject("MenuWoodenBridge");
            mats.TryGetValue("Sky_wood", out Material woodLight);
            mats.TryGetValue("Sky_wooddark", out Material woodDark);

            // 6 arched plank slats crossing X in [-1.9..+1.9]
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 0.72f;
                float archY = Mathf.Cos((i / 2.5f) * Mathf.PI * 0.5f) * 0.34f;
                GameObject slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = $"BridgePlank_{i + 2}";
                Object.DestroyImmediate(slat.GetComponent<Collider>());
                slat.transform.SetParent(bridge.transform, false);
                slat.transform.localPosition = new Vector3(x, archY, 0f);
                slat.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 6.5f);
                slat.transform.localScale = new Vector3(0.58f, 0.14f, 2.15f);
                if (woodLight != null) slat.GetComponent<Renderer>().sharedMaterial = woodLight;
            }

            foreach (float zSign in new[] { -1f, 1f })
            {
                foreach (float xPost in new[] { -1.45f, 1.45f })
                {
                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "BridgePost";
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                    post.transform.SetParent(bridge.transform, false);
                    post.transform.localPosition = new Vector3(xPost, 0.42f, zSign * 0.98f);
                    post.transform.localScale = new Vector3(0.18f, 0.48f, 0.18f);
                    if (woodDark != null) post.GetComponent<Renderer>().sharedMaterial = woodDark;
                }

                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "BridgeRail";
                Object.DestroyImmediate(rail.GetComponent<Collider>());
                rail.transform.SetParent(bridge.transform, false);
                rail.transform.localPosition = new Vector3(0f, 0.78f, zSign * 0.98f);
                rail.transform.localScale = new Vector3(3.1f, 0.12f, 0.14f);
                if (woodDark != null) rail.GetComponent<Renderer>().sharedMaterial = woodDark;
            }

            return bridge;
        }

        private static void CollectSwayTransformsFromIsland(Transform islandRoot, List<Transform> swayTargets)
        {
            if (islandRoot == null || swayTargets == null) return;
            foreach (Transform t in islandRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("CornerTree_") || n.StartsWith("CornerBush_") || n.StartsWith("CornerFlowers_") || n.EndsWith("Banner"))
                    swayTargets.Add(t);
            }
        }

        private static void AddMenuNatureDecor(Transform parent, Dictionary<string, Material> mats, List<Transform> swayTargets)
        {
            (string fbx, Vector3 pos, float yaw, float scale, bool sway)[] props =
            {
                ("Assets/Art/Environment/StylizedNature/FBX/Pine_1.fbx",          new Vector3(-7.6f, 0f,  5.2f),  24f, 0.56f, true),
                ("Assets/Art/Environment/StylizedNature/FBX/Pine_2.fbx",          new Vector3(-8.8f, 0f,  0.8f), -30f, 0.52f, true),
                ("Assets/Art/Environment/StylizedNature/FBX/CommonTree_2.fbx",    new Vector3( 8.6f, 0f, -5.8f),  48f, 0.48f, true),
                ("Assets/Art/Environment/StylizedNature/FBX/Pine_1.fbx",          new Vector3( 8.2f, 0f,  6.2f), -18f, 0.54f, true),
                ("Assets/Art/Environment/StylizedNature/FBX/Mushroom_Common.fbx", new Vector3(-4.6f, 0f, -5.4f),  15f, 0.65f, false),
                ("Assets/Art/Environment/StylizedNature/FBX/Mushroom_Common.fbx", new Vector3( 2.2f, 0f, -4.8f),  70f, 0.52f, false),
                ("Assets/Art/Environment/StylizedNature/FBX/Mushroom_Common.fbx", new Vector3( 7.6f, 0f,  5.0f), -40f, 0.62f, false),
                ("Assets/Art/Environment/StylizedNature/FBX/Flower_3_Group.fbx",  new Vector3(-3.2f, 0f, -6.2f),  12f, 0.36f, true),
                ("Assets/Art/Environment/StylizedNature/FBX/Flower_4_Group.fbx",  new Vector3( 6.8f, 0f, -3.2f), -25f, 0.36f, true)
            };

            foreach (var p in props)
            {
                GameObject go = InstantiateNatureProp(p.fbx, Vector3.one * p.scale, mats);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = p.pos;
                go.transform.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
                if (p.sway && swayTargets != null)
                    swayTargets.Add(go.transform);
            }
        }

        private static GameObject InstantiateScenicCharacter(GameObject prefab, Transform parent, Vector3 localPos, float localYaw)
        {
            GameObject actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (PrefabUtility.IsPartOfAnyPrefab(actor))
                PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            actor.name = "Scenic_" + prefab.name;
            actor.transform.SetParent(parent, false);
            actor.transform.localPosition = localPos;
            actor.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);

            foreach (Canvas c in actor.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(c.gameObject);

            StripGameplayComponentsSafely(actor);

            foreach (Animator anim in actor.GetComponentsInChildren<Animator>(true))
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            return actor;
        }

        private static void StripGameplayComponentsSafely(GameObject root)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                bool removedAny = false;
                Component[] all = root.GetComponentsInChildren<Component>(true);
                for (int i = all.Length - 1; i >= 0; i--)
                {
                    Component c = all[i];
                    if (c == null) continue;
                    bool isRemovable =
                        c is MonoBehaviour ||
                        c is NavMeshAgent ||
                        c is CharacterController ||
                        c is Rigidbody ||
                        c is Collider ||
                        c is AudioSource;
                    if (!isRemovable || HasDependentComponent(c))
                        continue;

                    Object.DestroyImmediate(c);
                    removedAny = true;
                }

                if (!removedAny) break;
            }
        }

        private static bool HasDependentComponent(Component target)
        {
            if (target == null) return false;
            System.Type targetType = target.GetType();
            Component[] siblings = target.GetComponents<Component>();
            for (int i = 0; i < siblings.Length; i++)
            {
                Component sibling = siblings[i];
                if (sibling == null || sibling == target) continue;
                object[] attrs = sibling.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                for (int a = 0; a < attrs.Length; a++)
                {
                    if (attrs[a] is RequireComponent req &&
                        ((req.m_Type0 != null && req.m_Type0.IsAssignableFrom(targetType)) ||
                         (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(targetType)) ||
                         (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(targetType))))
                        return true;
                }
            }
            return false;
        }

        private static void CreateMenuFloatingParticles(Transform parent, Material particleMat)
        {
            GameObject psObj = new GameObject("MenuFloatingParticles");
            psObj.transform.SetParent(parent, false);
            psObj.transform.position = new Vector3(0f, 2.8f, 4f);

            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.gravityModifier = -0.025f;
            main.maxParticles = 45;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(HexColor("#FFE875"), 0f), new GradientColorKey(HexColor("#78E8FF"), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(HexColor("#FFF2A6"), HexColor("#85EBFF"));

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            colorLife.color = new ParticleSystem.MinMaxGradient(grad);

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 6.5f, 26f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.45f;

            ParticleSystemRenderer pr = psObj.GetComponent<ParticleSystemRenderer>();
            if (particleMat != null) pr.sharedMaterial = particleMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
        }

        private static GameObject CreateUIRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go;
        }

        private static GameObject CreateUISpriteObject(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Sprite sprite, Color tint, bool raycast)
        {
            GameObject go = CreateUIRect(parent, name, anchorMin, anchorMax, anchoredPos, size);
            Image img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0.1f ? Image.Type.Sliced : Image.Type.Simple;
            }
            img.color = tint;
            img.raycastTarget = raycast;
            return go;
        }

        private static TMP_Text CreateStyledText(
            Transform parent, string name, Vector2 size, Vector2 pos, string text,
            float fontSize, Color color, TMP_FontAsset font, Material presetMat = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject go = CreateUIRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            if (presetMat != null) tmp.fontSharedMaterial = presetMat;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateWoodPlankButton(
            Transform parent, string name, Vector2 anchoredPos, Vector2 size, float angleDeg,
            string label, float fontSize, Vector2 labelOffset, Sprite plankSprite,
            TMP_FontAsset font, Material outlineMat, AudioSource audio, AudioClip hoverClip, AudioClip clickClip)
        {
            GameObject go = CreateUISpriteObject(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), anchoredPos, size, plankSprite, Color.white, true);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

            Button btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            CreateStyledText(go.transform, "Label", new Vector2(size.x * 0.8f, size.y * 0.72f), labelOffset, label, fontSize, HexColor("#FAF3DE"), font, outlineMat);

            MainMenuButtonFX fx = go.AddComponent<MainMenuButtonFX>();
            fx.ConfigureMotion(1.08f, angleDeg >= 0f ? 2.6f : -2.6f, 0.6f, 1.2f);
            fx.ConfigureAudio(audio, hoverClip, clickClip);
            return btn;
        }

        private static Button CreateCartoonPillButton(
            Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label, float fontSize,
            Color tintColor, Sprite pillSprite, TMP_FontAsset font, Material outlineMat,
            AudioSource audio, AudioClip hoverClip, AudioClip clickClip)
        {
            GameObject go = CreateUISpriteObject(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size, pillSprite, tintColor, true);
            Button btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            CreateStyledText(go.transform, "Label", new Vector2(size.x - 16f, size.y - 8f), Vector2.zero, label, fontSize, HexColor("#FAF8F0"), font, outlineMat);

            MainMenuButtonFX fx = go.AddComponent<MainMenuButtonFX>();
            fx.ConfigureMotion(1.06f, 0f, 0f, 1f);
            fx.ConfigureTintTarget(
                go.GetComponent<Image>(),
                tintColor,
                Color.Lerp(tintColor, Color.white, 0.22f),
                new Color(tintColor.r * 0.82f, tintColor.g * 0.82f, tintColor.b * 0.82f, 1f));
            fx.ConfigureAudio(audio, hoverClip, clickClip);
            return btn;
        }

        private static void BuildSettingsModal(
            Transform canvasTransform, Sprite boardSprite, Sprite pillSprite,
            TMP_FontAsset displayFont, Material displayOutline, TMP_FontAsset bodyFont, Material bodyOutline,
            AudioSource audio, AudioClip sfxHover, AudioClip sfxClick,
            out GameObject modalRoot, out CanvasGroup modalGroup, out RectTransform modalCard,
            out Slider volSlider, out TMP_Text volPct, out Toggle orbitToggle, out Toggle ddaToggle,
            out TMP_InputField seedInput, out Button randSeedBtn, out Button combatBtn, out Button showcaseBtn, out Button closeBtn)
        {
            modalRoot = new GameObject("SettingsModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            modalRoot.transform.SetParent(canvasTransform, false);
            RectTransform scrimRt = modalRoot.GetComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;
            modalRoot.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.62f);
            modalGroup = modalRoot.GetComponent<CanvasGroup>();

            GameObject cardObj = CreateUISpriteObject(modalRoot.transform, "SettingsBoardCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 590f), boardSprite, Color.white, true);
            modalCard = cardObj.GetComponent<RectTransform>();

            CreateStyledText(cardObj.transform, "Header", new Vector2(560f, 70f), new Vector2(0f, 232f), "SETTINGS", 48f, HexColor("#FFF5DD"), displayFont, displayOutline);

            // Row 1: Master Volume Slider
            CreateStyledText(cardObj.transform, "VolLabel", new Vector2(240f, 38f), new Vector2(-175f, 148f), "Master Volume", 22f, HexColor("#3E2B19"), bodyFont, null, TextAlignmentOptions.Left);
            volPct = CreateStyledText(cardObj.transform, "VolPct", new Vector2(80f, 38f), new Vector2(255f, 148f), "85%", 22f, HexColor("#4A6D20"), bodyFont, null, TextAlignmentOptions.Right);

            GameObject sliderObj = CreateUIRect(cardObj.transform, "VolumeSlider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(75f, 148f), new Vector2(250f, 28f));
            Image sliderBg = sliderObj.AddComponent<Image>();
            if (pillSprite != null) { sliderBg.sprite = pillSprite; sliderBg.type = Image.Type.Sliced; }
            sliderBg.color = HexColor("#5B4631");
            GameObject fillArea = CreateUIRect(sliderObj.transform, "Fill Area", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8f, -8f));
            GameObject fill = CreateUIRect(fillArea.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fillImg = fill.AddComponent<Image>();
            if (pillSprite != null) { fillImg.sprite = pillSprite; fillImg.type = Image.Type.Sliced; }
            fillImg.color = HexColor("#77D42C");
            GameObject handleArea = CreateUIRect(sliderObj.transform, "Handle Slide Area", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, 0f));
            GameObject handle = CreateUIRect(handleArea.transform, "Handle", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(26f, 34f));
            Image handleImg = handle.AddComponent<Image>();
            if (pillSprite != null) { handleImg.sprite = pillSprite; handleImg.type = Image.Type.Sliced; }
            handleImg.color = HexColor("#FAF3DE");
            volSlider = sliderObj.AddComponent<Slider>();
            volSlider.fillRect = fill.GetComponent<RectTransform>();
            volSlider.handleRect = handle.GetComponent<RectTransform>();
            volSlider.targetGraphic = handleImg;
            volSlider.minValue = 0f;
            volSlider.maxValue = 1f;
            volSlider.value = 0.85f;

            // Row 2: Live 360 Camera Orbit Toggle
            orbitToggle = CreateWoodToggle(cardObj.transform, "OrbitToggle", new Vector2(0f, 84f), "Live 360° Background Orbit", true, bodyFont, pillSprite);

            // Row 3: Adaptive DDA Toggle
            ddaToggle = CreateWoodToggle(cardObj.transform, "DdaToggle", new Vector2(0f, 22f), "Adaptive AI Difficulty (DDA)", true, bodyFont, pillSprite);

            // Row 4: World Seed Input + Randomize Pill
            CreateStyledText(cardObj.transform, "SeedLabel", new Vector2(210f, 38f), new Vector2(-190f, -44f), "World Seed", 22f, HexColor("#3E2B19"), bodyFont, null, TextAlignmentOptions.Left);
            seedInput = CreateWoodInputField(cardObj.transform, "SeedInput", new Vector2(22f, -44f), new Vector2(180f, 44f), "12345", bodyFont, pillSprite);
            randSeedBtn = CreateCartoonPillButton(cardObj.transform, "RandomSeedBtn", new Vector2(205f, -44f), new Vector2(150f, 46f), "Random", 20f, HexColor("#4E9D32"), pillSprite, bodyFont, bodyOutline, audio, sfxHover, sfxClick);

            // Row 5: Training / Showcase Quick Access
            CreateStyledText(cardObj.transform, "ModesLabel", new Vector2(560f, 30f), new Vector2(0f, -108f), "PRACTICE & ASSET SANDBOX MODES", 16f, HexColor("#6A523B"), bodyFont);
            combatBtn = CreateCartoonPillButton(cardObj.transform, "CombatSandboxBtn", new Vector2(-140f, -152f), new Vector2(250f, 48f), "Combat Sandbox", 20f, HexColor("#3B84DE"), pillSprite, bodyFont, bodyOutline, audio, sfxHover, sfxClick);
            showcaseBtn = CreateCartoonPillButton(cardObj.transform, "AssetShowcaseBtn", new Vector2(140f, -152f), new Vector2(250f, 48f), "Asset Showcase", 20f, HexColor("#7A6143"), pillSprite, bodyFont, bodyOutline, audio, sfxHover, sfxClick);

            // Bottom Close / Done Button
            closeBtn = CreateCartoonPillButton(cardObj.transform, "CloseSettingsBtn", new Vector2(0f, -230f), new Vector2(220f, 56f), "DONE", 28f, HexColor("#57B638"), pillSprite, displayFont, displayOutline, audio, sfxHover, sfxClick);
            modalRoot.SetActive(false);
        }

        private static void BuildCreditsModal(
            Transform canvasTransform, Sprite boardSprite, Sprite pillSprite,
            TMP_FontAsset displayFont, Material displayOutline, TMP_FontAsset bodyFont, Material bodyOutline,
            AudioSource audio, AudioClip sfxHover, AudioClip sfxClick,
            out GameObject modalRoot, out CanvasGroup modalGroup, out RectTransform modalCard, out Button closeBtn)
        {
            modalRoot = new GameObject("CreditsModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            modalRoot.transform.SetParent(canvasTransform, false);
            RectTransform scrimRt = modalRoot.GetComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;
            modalRoot.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.62f);
            modalGroup = modalRoot.GetComponent<CanvasGroup>();

            GameObject cardObj = CreateUISpriteObject(modalRoot.transform, "CreditsBoardCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 490f), boardSprite, Color.white, true);
            modalCard = cardObj.GetComponent<RectTransform>();

            CreateStyledText(cardObj.transform, "Header", new Vector2(540f, 68f), new Vector2(0f, 182f), "CREDITS", 46f, HexColor("#FFF5DD"), displayFont, displayOutline);

            string creditsCopy =
                "<b>SKYGATE TACTICS</b>\n" +
                "3D Step-Terrace Action Roguelike (PCG + Tactical AI + DDA)\n\n" +
                "<b>Open-License Art, Fonts & Audio Credits:</b>\n" +
                "• KayKit Adventurers, Skeletons & Forest Packs (CC0)\n" +
                "• Quaternius Stylized Nature MegaKit Standard (CC0)\n" +
                "• Shuriken Studio Sky Islands (Free Game License)\n" +
                "• Kenney Game Audio SFX Pack (CC0)\n" +
                "• LuckiestGuy (Apache 2.0) & Fredoka (SIL OFL)";

            TMP_Text body = CreateStyledText(cardObj.transform, "CreditsBody", new Vector2(580f, 250f), new Vector2(0f, 18f), creditsCopy, 20f, HexColor("#3E2B19"), bodyFont);
            body.lineSpacing = 6f;

            closeBtn = CreateCartoonPillButton(cardObj.transform, "CloseCreditsBtn", new Vector2(0f, -172f), new Vector2(210f, 54f), "CLOSE", 28f, HexColor("#57B638"), pillSprite, displayFont, displayOutline, audio, sfxHover, sfxClick);
            modalRoot.SetActive(false);
        }

        private static Toggle CreateWoodToggle(Transform parent, string name, Vector2 pos, string labelText, bool defaultOn, TMP_FontAsset font, Sprite pillSprite)
        {
            GameObject row = CreateUIRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(560f, 44f));
            CreateStyledText(row.transform, "Label", new Vector2(460f, 40f), new Vector2(-42f, 0f), labelText, 22f, HexColor("#3E2B19"), font, null, TextAlignmentOptions.Left);

            GameObject box = CreateUIRect(row.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 0f), new Vector2(36f, 36f));
            Image boxImg = box.AddComponent<Image>();
            if (pillSprite != null) { boxImg.sprite = pillSprite; boxImg.type = Image.Type.Sliced; }
            boxImg.color = HexColor("#5B4631");

            GameObject check = CreateUIRect(box.transform, "Checkmark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image checkImg = check.AddComponent<Image>();
            if (pillSprite != null) { checkImg.sprite = pillSprite; checkImg.type = Image.Type.Sliced; }
            checkImg.color = HexColor("#77D42C");

            Toggle t = row.AddComponent<Toggle>();
            t.targetGraphic = boxImg;
            t.graphic = checkImg;
            t.isOn = defaultOn;
            return t;
        }

        private static TMP_InputField CreateWoodInputField(Transform parent, string name, Vector2 pos, Vector2 size, string defaultText, TMP_FontAsset font, Sprite pillSprite)
        {
            GameObject go = CreateUIRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image bgImg = go.AddComponent<Image>();
            if (pillSprite != null) { bgImg.sprite = pillSprite; bgImg.type = Image.Type.Sliced; }
            bgImg.color = HexColor("#FFF9EB");

            GameObject viewport = CreateUIRect(go.transform, "Text Area", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, -8f));
            viewport.AddComponent<RectMask2D>();

            GameObject phObj = CreateUIRect(viewport.transform, "Placeholder", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI phTmp = phObj.AddComponent<TextMeshProUGUI>();
            if (font != null) phTmp.font = font;
            phTmp.text = "Seed...";
            phTmp.fontSize = 20f;
            phTmp.color = new Color(0.5f, 0.42f, 0.34f, 0.6f);
            phTmp.alignment = TextAlignmentOptions.Center;

            GameObject txtObj = CreateUIRect(viewport.transform, "Text", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI txtTmp = txtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) txtTmp.font = font;
            txtTmp.fontSize = 22f;
            txtTmp.color = HexColor("#3A2818");
            txtTmp.alignment = TextAlignmentOptions.Center;

            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = txtTmp;
            input.placeholder = phTmp;
            input.characterLimit = 8;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            input.text = defaultText;
            return input;
        }

        private static void PlaceScenicBackdrop(GameObject prefab, Vector3 pos, float yRot)
        {
            if (prefab == null) return;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/CombatSandbox.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/AssetShowcase.unity", true)
            };
        }

        private static void SetupDaylightAtmosphere(Dictionary<string, Material> mats)
        {
            // Dreamy Moon-Tribe Cerulean-to-Lavender/Peach Horizon Skybox + Warm Sunlit Island Pop
            Material skyMat = null;
            if (mats != null && (!mats.TryGetValue("SkyboxSunset", out skyMat) || skyMat == null))
                mats.TryGetValue("SkyboxMidday", out skyMat);
            if (skyMat != null)
                RenderSettings.skybox = skyMat;

            GameObject dirLightObj = new GameObject("Directional Light (Sun)");
            Light sun = dirLightObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = HexColor("#FFF5DF");
            sun.intensity = 1.42f;
            sun.shadows = LightShadows.Soft;
            dirLightObj.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            GameObject fillLightObj = new GameObject("Directional Light (Sky Fill)");
            Light fill = fillLightObj.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = HexColor("#AED8FF");
            fill.intensity = 0.42f;
            fill.shadows = LightShadows.None;
            fillLightObj.transform.rotation = Quaternion.Euler(-30f, 145f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = HexColor("#9ED8FC");
            RenderSettings.ambientEquatorColor = HexColor("#FCE0E8");
            RenderSettings.ambientGroundColor = HexColor("#8A6A5C");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = HexColor("#D7E5FA");
            RenderSettings.fogStartDistance = 85f;
            RenderSettings.fogEndDistance = 230f;
            DynamicGI.UpdateEnvironment();
        }

        private static void CreateEventSystem()
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject InstantiateVisualOnly(
            string assetPath,
            PrimitiveType fallbackType,
            Vector3 fallbackScale,
            Material fallbackMat,
            bool preserveTex = false,
            Material glowMat = null)
        {
            GameObject inst = InstantiateModelOrFallback(assetPath, fallbackType, fallbackScale, fallbackMat);
            RemoveAllColliders(inst);
            if (preserveTex)
                PolishCharacterMaterials(inst, fallbackMat, glowMat);
            else if (fallbackMat != null)
                ApplyMaterialRecursively(inst, fallbackMat);
            return inst;
        }

        private static GameObject InstantiateNatureProp(
            string assetPath,
            Vector3 multScale,
            Dictionary<string, Material> mats,
            bool terracottaRock = false)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelAsset == null)
            {
                Material fbMat = null;
                if (mats != null) mats.TryGetValue("EmeraldGrass", out fbMat);
                return InstantiateVisualOnly(assetPath, PrimitiveType.Sphere, multScale, fbMat);
            }

            // Preserve Blender FBX root transform (scale=100, rotation=-90X) by scaling via Vector3.Scale inside a clean pivot
            GameObject pivot = new GameObject(modelAsset.name);
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            RemoveAllColliders(inst);
            inst.transform.SetParent(pivot.transform, false);
            inst.transform.localScale = Vector3.Scale(inst.transform.localScale, multScale);

            if (mats != null)
            {
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] current = r.sharedMaterials;
                    if (current == null || current.Length == 0) continue;
                    Material[] updated = new Material[current.Length];
                    for (int i = 0; i < current.Length; i++)
                    {
                        string mName = current[i] != null ? current[i].name : string.Empty;
                        Material mapped = null;
                        if (mName.Contains("PathRocks"))
                            mats.TryGetValue("NaturePathRocks", out mapped);
                        else if (mName.Contains("Rocks"))
                            mats.TryGetValue(terracottaRock ? "NatureRockTerracotta" : "NatureRockMossy", out mapped);
                        else if (mName.Contains("Flowers"))
                            mats.TryGetValue("NatureFlowers", out mapped);
                        else if (mName.Contains("Grass"))
                            mats.TryGetValue("NatureGrass", out mapped);
                        else if (mName.Contains("Bark"))
                            mats.TryGetValue("NatureBark", out mapped);
                        else if (mName.Contains("Mushroom"))
                            mats.TryGetValue("NatureMushroom", out mapped);
                        else if (mName.Contains("Leaves_NormalTree"))
                            mats.TryGetValue("NatureLeaves", out mapped);
                        else if (mName.Contains("Leaves_Pine"))
                            mats.TryGetValue("NaturePine", out mapped);
                        else if (mName.Contains("Leaves"))
                            mats.TryGetValue("NatureLeavesGeneric", out mapped);
                        else if (mName.Contains("forest"))
                            mats.TryGetValue("KayKitForest", out mapped);

                        updated[i] = mapped != null ? mapped : current[i];
                    }
                    r.sharedMaterials = updated;
                }
            }

            return pivot;
        }

        private static void ApplySkyIslandMaterials(GameObject root, Dictionary<string, Material> mats)
        {
            if (root == null || mats == null) return;
            mats.TryGetValue("Sky_stone", out Material fallbackStone);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] current = r.sharedMaterials;
                if (current == null || current.Length == 0) continue;
                Material[] updated = new Material[current.Length];
                for (int i = 0; i < current.Length; i++)
                {
                    Material src = current[i];
                    Material mapped = null;
                    if (src != null)
                    {
                        string raw = src.name.Replace(" (Instance)", string.Empty).Trim().ToLowerInvariant();
                        if (!mats.TryGetValue("Sky_" + raw, out mapped))
                        {
                            if (raw.Contains("foliage") || raw.Contains("grass") || raw.Contains("leaf"))
                                mats.TryGetValue("Sky_foliage", out mapped);
                            else if (raw.Contains("water"))
                                mats.TryGetValue("Sky_water", out mapped);
                            else if (raw.Contains("crystal"))
                                mats.TryGetValue("Sky_crystal", out mapped);
                            else if (raw.Contains("magic"))
                                mats.TryGetValue("Sky_magic", out mapped);
                            else if (raw.Contains("wood"))
                                mats.TryGetValue("Sky_wood", out mapped);
                            else if (raw.Contains("cloud") || raw.Contains("white"))
                                mats.TryGetValue("CloudWhite", out mapped);
                        }
                    }
                    updated[i] = mapped != null ? mapped : (fallbackStone != null ? fallbackStone : current[i]);
                }
                r.sharedMaterials = updated;
            }
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName)) return null;
            if (root.name == targetName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindChildRecursive(root.GetChild(i), targetName);
                if (hit != null) return hit;
            }
            return null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0) return;
            root.layer = layer;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        private static GameObject InstantiateModelOrFallback(string assetPath, PrimitiveType fallbackType, Vector3 fallbackScale, Material fallbackMat)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelAsset != null)
            {
                return (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            }
            Debug.LogWarning($"[SkyGateBuilder] Asset not found at '{assetPath}', using primitive fallback.");
            GameObject prim = GameObject.CreatePrimitive(fallbackType);
            Object.DestroyImmediate(prim.GetComponent<Collider>());
            prim.transform.localScale = fallbackScale;
            if (fallbackMat != null)
                prim.GetComponent<Renderer>().sharedMaterial = fallbackMat;
            return prim;
        }

        private static void RemoveAllColliders(GameObject root)
        {
            if (root == null) return;
            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                Object.DestroyImmediate(c);
            }
        }

        private static void PolishCharacterMaterials(GameObject root, Material fallbackMat, Material glowMat = null)
        {
            if (root == null) return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] current = r.sharedMaterials;
                if (current == null || current.Length == 0)
                {
                    if (fallbackMat != null) r.sharedMaterial = fallbackMat;
                    continue;
                }

                bool modified = false;
                Material[] updated = new Material[current.Length];
                for (int i = 0; i < current.Length; i++)
                {
                    Material src = current[i];
                    if (src != null && src.name.ToLowerInvariant().Contains("glow") && glowMat != null)
                    {
                        updated[i] = glowMat;
                        modified = true;
                    }
                    else if (src != null && src.mainTexture != null)
                    {
                        updated[i] = src;
                    }
                    else
                    {
                        updated[i] = fallbackMat != null ? fallbackMat : src;
                        modified = true;
                    }
                }

                if (modified)
                    r.sharedMaterials = updated;
            }
        }

        private static void ApplyMaterialRecursively(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rends)
            {
                Material[] mats = r.sharedMaterials;
                if (mats == null || mats.Length <= 1)
                {
                    r.sharedMaterial = mat;
                }
                else
                {
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        private static T CreateOrLoadSO<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static GameObject SaveAsPrefab(GameObject instance, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateAnchoredPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color bgColor, Color topTrimColor = default)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image img = obj.GetComponent<Image>();
            img.color = bgColor;

            if (topTrimColor.a > 0.01f)
            {
                GameObject trim = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
                trim.transform.SetParent(obj.transform, false);
                RectTransform trt = trim.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.anchoredPosition = Vector2.zero;
                trt.sizeDelta = new Vector2(0f, 4f);
                trim.GetComponent<Image>().color = topTrimColor;
            }
            return obj;
        }

        private static GameObject CreateUIImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color, Sprite sprite = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            Image img = obj.GetComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border.sqrMagnitude > 0.1f ? Image.Type.Sliced : Image.Type.Simple;
            }
            return obj;
        }

        private static TMP_Text CreateUIText(Transform parent, string name, Vector2 size, Vector2 pos, string content, float fontSize, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (cachedDefaultFont != null) tmp.font = cachedDefaultFont;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static Button CreateUIButton(Transform parent, string name, Vector2 size, Vector2 pos, string label, Color bgColor)
        {
            GameObject btnObj = CreateUIImage(parent, name, size, pos, bgColor);
            Button btn = btnObj.AddComponent<Button>();

            GameObject hiBar = new GameObject("TopBevel", typeof(RectTransform), typeof(Image));
            hiBar.transform.SetParent(btnObj.transform, false);
            RectTransform hrt = hiBar.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, 3f);
            hiBar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.28f);

            CreateUIText(btnObj.transform, "Label", size, Vector2.zero, label, 16f, Color.white);
            return btn;
        }

        private static TextMeshPro Create3DText(Transform parent, string name, Vector3 localPos, string text, float fontSize, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            if (cachedDefaultFont != null) tmp.font = cachedDefaultFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(4.5f, 1.2f);
            return tmp;
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return Color.white;
        }
    }
}
#endif
