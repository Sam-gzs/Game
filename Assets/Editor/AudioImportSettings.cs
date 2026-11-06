using UnityEditor;
using UnityEngine;

/// <summary>
/// Import rules for the jam SFX folder: short 2D one-shots want PCM, mono and preloaded,
/// otherwise Unity may keep them compressed and the first hit of a sound can arrive late.
/// </summary>
public class AudioImportSettings : AssetPostprocessor
{
    const string Folder = "/Resources/Audio/";

    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(Folder)) return;

        var importer = assetImporter as AudioImporter;
        if (importer == null) return;

        importer.forceToMono = true;
        importer.loadInBackground = false;

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.preloadAudioData = true;   // short one-shots: first hit must not stream in
        settings.quality = 1f;
        importer.defaultSampleSettings = settings;
    }

    [MenuItem("Tools/Audio/Reimport SFX Folder")]
    static void ReimportSfx()
    {
        const string folder = "Assets/Resources/Audio";
        AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        Debug.Log("Reimported " + folder + " with PCM / mono / preload settings.");
    }
}
