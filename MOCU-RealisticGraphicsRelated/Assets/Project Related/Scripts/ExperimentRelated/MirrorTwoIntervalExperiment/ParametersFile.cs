using System;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using UnityEngine;


namespace MirrorTwoIntervalExperiment
{
    /// Reads the experiment settings from a JSON file that sits in the same folder
    /// as the saved data, so parameters can be changed without recompiling.
    public static class ParametersFile
    {
        private const string FileName = "MirrorTwoIntervalConfig.json";

        /// Replace, not Auto: a property that already holds an object is otherwise
        /// populated into rather than swapped out. See MirrorExperiment.ParametersFile.
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static string FolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuM2I");

        public static string FilePath => Path.Combine(FolderPath, FileName);


        /// Returns the settings from the file. With no file yet, writes one filled
        /// with the defaults first, as a complete template.
        ///
        /// Throws if the file exists but cannot be read: running with silently
        /// substituted defaults would be worse than not running.
        public static Parameters Load()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            if (!File.Exists(FilePath))
            {
                var defaults = new Parameters();
                File.WriteAllText(FilePath, JsonHelper.SerializeJson(defaults));
                Debug.Log($"MirrorTwoIntervalExperiment: no config found, wrote defaults to {FilePath}");
                return defaults;
            }

            var loaded = JsonHelper.DeserializeJson<Parameters>(File.ReadAllText(FilePath), ReadSettings);
            Debug.Log($"MirrorTwoIntervalExperiment: loaded config from {FilePath} (design {loaded.Design})");

            return loaded;
        }
    }
}
