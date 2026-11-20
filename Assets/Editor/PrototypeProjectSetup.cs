using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RewindNotary.Editor
{
    [InitializeOnLoad]
    public static class PrototypeProjectSetup
    {
        private const string MainPath = "Assets/Scenes/Main.unity";
        static PrototypeProjectSetup() { EditorApplication.delayCall += Configure; }
        private static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath);
            if (main == null) return;
            // A fresh Hub import can initially open Untitled. Play always starts Main.
            EditorSceneManager.playModeStartScene = main;
            Scene current = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path) && !current.isDirty)
                EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        }
        [MenuItem("Rewind Notary/Open Main Scene")]
        private static void OpenMain()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        }
    }

    public sealed class GameArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/GameArt/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            bool compress = assetPath.StartsWith("Assets/Resources/GameArt/backgrounds/", System.StringComparison.Ordinal)
                || assetPath.StartsWith("Assets/Resources/GameArt/portraits/", System.StringComparison.Ordinal)
                || assetPath.StartsWith("Assets/Resources/GameArt/ui/", System.StringComparison.Ordinal);
            importer.textureCompression = compress ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = compress;
            importer.compressionQuality = compress ? 75 : 50;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }

    public sealed class GameAudioImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            bool ost = assetPath.StartsWith("Assets/Resources/Audio/Bgm/track-", System.StringComparison.Ordinal)
                && assetPath.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase);
            settings.loadType = ost ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = ost ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = ost ? 0.7f : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !ost;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = ost;
        }
    }
}
