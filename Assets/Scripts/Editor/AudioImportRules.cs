using System;
using UnityEditor;
using UnityEngine;

namespace Wolfenstein.Editor
{
    /// <summary>
    /// How clips under Assets/Audio are imported, set the first time each one is imported so later changes in the Inspector
    /// stand. Effects are decompressed when loaded, for instant playback; ambience loops stay compressed in memory; music
    /// streams from disk. A real recording dropped into the same folders is imported the same way.
    /// </summary>
    internal sealed class AudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/", StringComparison.Ordinal) || !assetImporter.importSettingsMissing)
                return;

            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            bool loop = assetPath.Contains("/Ambience/") || assetPath.Contains("/Machines/");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = music ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = music ? 0.6f : loop ? 0.5f : 0.7f;
            importer.defaultSampleSettings = settings;

            // Music may arrive in stereo and should keep it; everything else is placed in the world or plays as one voice.
            importer.forceToMono = !music;
            importer.loadInBackground = music;
        }
    }
}
