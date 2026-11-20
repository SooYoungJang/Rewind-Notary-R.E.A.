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
            importer.textureCompression = TextureImporterCompression.Uncompressed;
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
            // Long-form OST assets stream; preserve legacy WAV and SFX settings.
            bool ost = assetPath.StartsWith("Assets/Resources/Audio/Bgm/track-", System.StringComparison.Ordinal)
                && assetPath.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase);
            settings.loadType = ost ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = ost ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
        }
    }
}
