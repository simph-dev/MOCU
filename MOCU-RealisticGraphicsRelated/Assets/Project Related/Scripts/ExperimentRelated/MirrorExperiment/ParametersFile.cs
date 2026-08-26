using System;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using UnityEngine;


namespace MirrorExperiment
{
    /// Reads the experiment settings from a JSON file that sits in the same folder
    /// as the saved data, so parameters can be changed without recompiling.
    public static class ParametersFile
    {
        private const string FileName = "MirrorExperimentConfig.json";

        /// Replace, not Auto.
        ///
        /// Json.NET's default for a collection property that already holds a value is
        /// to populate INTO it - i.e. Add - rather than swap it out. Parameters.Conditions
        /// is initialised with the paper's five conditions in its field initialiser, so
        /// with the default handling a config file listing five conditions yields ten:
        /// the five built-in ones, all enabled, followed by whatever the file says.
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static string FolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuME");

        public static string FilePath => Path.Combine(FolderPath, FileName);


        /// Returns the settings from the file. If there is no file yet, writes one
        /// filled with the defaults first - that way the very first run produces a
        /// complete template instead of leaving the field names to be guessed.
        ///
        /// Throws if the file exists but cannot be read: running an experiment with
        /// silently substituted defaults would be worse than not running it.
        public static Parameters Load()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            if (!File.Exists(FilePath))
            {
                var defaults = new Parameters();
                File.WriteAllText(FilePath, JsonHelper.SerializeJson(defaults));
                Debug.Log($"MirrorExperiment: no config found, wrote defaults to {FilePath}");
                return defaults;
            }

            var loaded = JsonHelper.DeserializeJson<Parameters>(File.ReadAllText(FilePath), ReadSettings);

            int enabled = loaded.Conditions?.FindAll(c => c.Enabled).Count ?? 0;
            Debug.Log($"MirrorExperiment: loaded config from {FilePath} " +
                      $"({loaded.Conditions?.Count ?? 0} conditions, {enabled} enabled)");

            return loaded;
        }
    }
}
