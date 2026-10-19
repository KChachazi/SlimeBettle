using System;
using System.IO;
using System.Linq;
using SlimeBattle.UI;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Imports the original layered PNG exports without changing their pixels.</summary>
    public static class TapTapUiArt
    {
        public const string Folder = "Assets/Game/UI/Art/TapTap/";
        [Serializable] public sealed class Entry
        {
            public string key, source, file;
            public int width, height, x, y, w, h;
        }
        [Serializable] private sealed class Manifest { public Entry[] entries; }
        private static Entry[] entries;
        private static Entry[] Entries => entries ?? (entries =
            JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "source-map.json")).entries);
        public static Entry Find(string key) => Entries.Single(e => e.key == key);
        public static Sprite Sprite(string key)
        {
            var entry = Find(key);
            var sprite = AssetDatabase.LoadAllAssetsAtPath(Folder + entry.file).OfType<Sprite>().SingleOrDefault();
            if (sprite == null) throw new InvalidOperationException("Import TapTap UI art first: " + key);
            return sprite;
        }

        [MenuItem("SlimeBattle/UI/Import and Apply TapTap UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating UI assets.");
            Import();
            var selection = ReferenceUiPrefabBuilder.BuildSelection();
            ReferenceUiPrefabBuilder.SaveUiPrefab(selection.gameObject, FirstLevelUiPrefabBuilder.SelectionPath);
            UnityEngine.Object.DestroyImmediate(selection.gameObject);
            var hud = ReferenceUiPrefabBuilder.BuildHud();
            ReferenceUiPrefabBuilder.SaveUiPrefab(hud.gameObject, FirstLevelUiPrefabBuilder.HudPath);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
            ReferenceUiPrefabBuilder.ApplyMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("TapTap UI updated: HUD, mechanism toolbar, main menu and auxiliary pages.");
        }

        public static void Import()
        {
            entries = null;
            foreach (var entry in Entries)
            {
                var path = Folder + entry.file;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var expectedRect = new Rect(entry.x, entry.height - entry.y - entry.h, entry.w, entry.h);
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Multiple &&
                    settings.spriteMeshType == SpriteMeshType.FullRect &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 100) &&
                    !importer.mipmapEnabled && !importer.isReadable && importer.alphaIsTransparency &&
                    importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear &&
                    importer.maxTextureSize == 2048 && importer.textureCompression == TextureImporterCompression.CompressedHQ)
                {
                    var current = factory.GetSpriteEditorDataProviderFromObject(importer);
                    current.InitSpriteEditorDataProvider();
                    var slices = current.GetSpriteRects();
                    if (slices.Length == 1 && slices[0].name == entry.key && slices[0].rect == expectedRect) continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 100;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().FirstOrDefault(s => s.name == entry.key);
                var slice = new SpriteRect
                {
                    name = entry.key,
                    rect = expectedRect,
                    pivot = Vector2.one * 0.5f,
                    alignment = SpriteAlignment.Center,
                    spriteID = existing != null ? existing.spriteID : GUID.Generate()
                };
                provider.SetSpriteRects(new[] { slice });
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                    new[] { new SpriteNameFileIdPair(slice.name, slice.spriteID) });
                provider.Apply();
                importer.SaveAndReimport();
            }
        }

        public static void Capture()
        {
            ReferenceUiPrefabBuilder.CaptureLayouts();
            FirstLevelUiPrefabBuilder.CaptureCurrentLayouts();
        }

        public static void ApplyAndCapture()
        {
            Apply();
            Capture();
        }
    }
}
