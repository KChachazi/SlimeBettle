#if UNITY_EDITOR
using System.Collections.Generic;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Slimes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace SlimeBattle.EditorTools
{
    internal static class SlimeAnimationAssetBuilder
    {
        private const string AnimationFolder = "Assets/Game/Animations/Slime";
        private const string ClipFolder = AnimationFolder + "/Clips";
        private const string AtlasFolder = AnimationFolder + "/Atlases";
        private const string PrefabPath = "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab";

        private readonly struct ActionSource
        {
            public ActionSource(string stateName, string sourceFolder, string prefix, bool loops)
            {
                StateName = stateName;
                SourceFolder = sourceFolder;
                Prefix = prefix;
                Loops = loops;
            }

            public string StateName { get; }
            public string SourceFolder { get; }
            public string Prefix { get; }
            public bool Loops { get; }
        }

        private static readonly ActionSource[] NormalActions =
        {
            new ActionSource("Idle", "待机", "idle_", true),
            new ActionSource("Move", "移动", "jump_", true),
            new ActionSource("Attack", "攻击", "attack_", false),
            new ActionSource("Eat", "进食", "eat_", false),
            new ActionSource("Furnace", "烤火_新版", "warm_", false),
            new ActionSource("Hit", "受击", "hurt_", false),
            new ActionSource("Collision", "个体间碰撞", "collision_", false)
        };

        [MenuItem("SlimeBattle/Build Normal Slime Animations")]
        public static void BuildNormalAnimations()
        {
            EnsureFolders();
            PrepareSourceTexturesForAtlas();
            var clips = CreateNormalClips();
            var controller = CreateController(clips);
            CreateNormalAtlas();
            var idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Game/Art/史莱姆动作/待机/PNG序列/idle_000.png");
            if (idleSprite == null)
                throw new System.InvalidOperationException("Missing initial Idle sprite.");
            UpdatePrefab(idleSprite, controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built normal slime animations, atlas, and PF_Slime_Green. Death remains on ActorVisualFeedback fade.");
        }

        private static void PrepareSourceTexturesForAtlas()
        {
            foreach (var action in NormalActions)
            {
                var sourceFolder = "Assets/Game/Art/史莱姆动作/" + action.SourceFolder + "/PNG序列";
                for (var frameIndex = 0; frameIndex < 120; frameIndex++)
                {
                    var framePath = sourceFolder + "/" + action.Prefix + frameIndex.ToString("000") + ".png";
                    var importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
                    if (importer == null)
                        throw new System.InvalidOperationException("Missing texture importer: " + framePath);
                    if (importer.spriteImportMode != SpriteImportMode.Single ||
                        Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f)
                        throw new System.InvalidOperationException("Expected Single Sprite at source PPU 100: " + framePath);
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed) continue;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void EnsureFolders()
        {
            var folders = new[]
            {
                "Assets/Game/Animations",
                AnimationFolder,
                ClipFolder,
                AtlasFolder
            };
            foreach (var folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        private static AnimationClip[] CreateNormalClips()
        {
            var clips = new AnimationClip[NormalActions.Length];
            for (var actionIndex = 0; actionIndex < NormalActions.Length; actionIndex++)
            {
                var action = NormalActions[actionIndex];
                var sourceFolder = "Assets/Game/Art/史莱姆动作/" + action.SourceFolder + "/PNG序列";
                var keys = new ObjectReferenceKeyframe[120];
                for (var frameIndex = 0; frameIndex < keys.Length; frameIndex++)
                {
                    var framePath = sourceFolder + "/" + action.Prefix + frameIndex.ToString("000") + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                    if (sprite == null)
                        throw new System.InvalidOperationException("Missing imported Sprite: " + framePath);
                    if (Mathf.Abs(sprite.pixelsPerUnit - 100f) > 0.01f)
                        throw new System.InvalidOperationException("Expected source PPU 100: " + framePath);
                    keys[frameIndex] = new ObjectReferenceKeyframe
                    {
                        time = frameIndex / 24f,
                        value = sprite
                    };
                }

                var clipName = "Slime_Normal_" + action.StateName;
                var clipPath = ClipFolder + "/" + clipName + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clip.name = clipName;
                clip.frameRate = 24f;
                var binding = EditorCurveBinding.PPtrCurve(string.Empty,
                    typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0f;
                settings.stopTime = 5f;
                settings.loopTime = action.Loops;
                settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                clips[actionIndex] = clip;
            }
            return clips;
        }

        private static AnimatorController CreateController(AnimationClip[] clips)
        {
            const string controllerPath = AnimationFolder + "/AC_Slime_Normal.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var layers = controller.layers;
            var layer = layers[0];
            var stateMachine = layer.stateMachine;
            foreach (var childState in stateMachine.states)
                stateMachine.RemoveState(childState.state);
            foreach (var transition in stateMachine.anyStateTransitions)
                stateMachine.RemoveAnyStateTransition(transition);

            AnimatorState idleState = null;
            for (var i = 0; i < NormalActions.Length; i++)
            {
                var state = stateMachine.AddState(NormalActions[i].StateName);
                if (!EditorUtility.IsPersistent(state))
                    AssetDatabase.AddObjectToAsset(state, controller);
                state.motion = clips[i];
                if (i == 0) idleState = state;
            }
            stateMachine.defaultState = idleState;
            layer.stateMachine = stateMachine;
            layers[0] = layer;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateNormalAtlas()
        {
            const string atlasPath = AtlasFolder + "/SA_Slime_Normal.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            var packables = new List<Object>();
            foreach (var action in NormalActions)
            {
                var sourceFolder = "Assets/Game/Art/史莱姆动作/" + action.SourceFolder + "/PNG序列";
                var packable = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceFolder);
                if (packable == null)
                    throw new System.InvalidOperationException("Cannot add sprite folder to atlas: " + sourceFolder);
                packables.Add(packable);
            }
            var previousPackables = SpriteAtlasExtensions.GetPackables(atlas);
            if (previousPackables.Length > 0)
                SpriteAtlasExtensions.Remove(atlas, previousPackables);
            SpriteAtlasExtensions.Add(atlas, packables.ToArray());
            SpriteAtlasExtensions.SetPackingSettings(atlas, new SpriteAtlasPackingSettings
            {
                enableRotation = false,
                enableTightPacking = true,
                enableAlphaDilation = true,
                padding = 4,
                blockOffset = 1
            });
            SpriteAtlasExtensions.SetTextureSettings(atlas, new SpriteAtlasTextureSettings
            {
                generateMipMaps = false,
                readable = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            });
            SpriteAtlasExtensions.SetPlatformSettings(atlas, new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform",
                overridden = true,
                maxTextureSize = 4096,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 60,
                format = TextureImporterFormat.Automatic
            });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            EditorUtility.SetDirty(atlas);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, BuildTarget.StandaloneWindows64, false);
        }

        private static void UpdatePrefab(Sprite idleSprite, AnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var oldRenderer = root.GetComponent<SpriteRenderer>();
                var sortingLayerId = oldRenderer == null ? 0 : oldRenderer.sortingLayerID;
                var sortingOrder = oldRenderer == null ? 0 : oldRenderer.sortingOrder;
                var visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    var visualObject = new GameObject("Visual");
                    visual = visualObject.transform;
                    visual.SetParent(root.transform, false);
                }
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * 0.45f;
                visual.gameObject.layer = root.layer;

                var visualRenderer = visual.GetComponent<SpriteRenderer>();
                if (visualRenderer == null) visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
                visualRenderer.sprite = idleSprite;
                visualRenderer.color = Color.white;
                visualRenderer.sortingLayerID = sortingLayerId;
                visualRenderer.sortingOrder = sortingOrder;

                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.enabled = true;
                if (oldRenderer != null) Object.DestroyImmediate(oldRenderer, true);

                var presenter = root.GetComponent<SlimeVisualPresenter>();
                if (presenter == null) presenter = root.AddComponent<SlimeVisualPresenter>();
                var presenterObject = new SerializedObject(presenter);
                presenterObject.FindProperty("animator").objectReferenceValue = animator;
                presenterObject.FindProperty("stateOverrides").arraySize = 0;
                presenterObject.FindProperty("movementThreshold").floatValue = 0.001f;
                presenterObject.FindProperty("movementGraceSeconds").floatValue = 0.12f;
                presenterObject.FindProperty("collisionAnimationCooldown").floatValue = 0.35f;
                presenterObject.ApplyModifiedPropertiesWithoutUndo();

                var actor = root.GetComponent<SlimeActor>();
                var actorObject = new SerializedObject(actor);
                actorObject.FindProperty("gameplayPixelsPerUnit").floatValue = 64f;
                actorObject.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new System.InvalidOperationException("Failed to save prefab: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
