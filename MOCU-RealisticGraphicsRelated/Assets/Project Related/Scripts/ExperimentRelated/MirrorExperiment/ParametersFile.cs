using System;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using UnityEngine;


namespace MirrorExperiment
{
    /// Reads the experiment settings from a JSON file that sits in the same folder
    /// as the saved data, so parameters can be changed without recompiling.
    ///
    /// One optional command-line argument, so that a Windows shortcut can point
    /// the exe at a config file of its own:
    ///
    ///     -config <path>   reads this file instead of the default one; a relative
    ///                      path is taken from the shortcut's "Start in" folder
    ///
    /// In the Unity Editor there is none, so the default file is read.
    public static class ParametersFile
    {
        private const string FileName = "MirrorExperimentConfig.json";
        private const string ConfigArgument = "-config";

        /// Replace, not Auto.
        ///
        /// Json.NET's default for a collection property that already holds a value is
        /// to populate INTO it - i.e. Add - rather than swap it out. Parameters.Conditions
        /// is initialised with the seven default conditions in its field initialiser, so
        /// with the default handling a config file listing seven conditions yields
        /// fourteen: the seven built-in ones, all enabled, followed by whatever the file
        /// says.
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static string DefaultFolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuME");

        /// The file actually read: -config's, or the default one.
        public static string FilePath => string.IsNullOrEmpty(ReadArgument(ConfigArgument))
            ? Path.Combine(DefaultFolderPath, FileName)
            : Path.GetFullPath(ReadArgument(ConfigArgument));


        /// Returns the settings from the file. If there is no file yet, or it is
        /// empty, writes one filled with the defaults first - that way the very
        /// first run produces a complete template instead of leaving the field
        /// names to be guessed.
        ///
        /// Throws if the file exists but cannot be read, or -config has no file
        /// after it: running an experiment with silently substituted settings
        /// would be worse than not running it.
        public static Parameters Load()
        {
            if (ReadArgument(ConfigArgument) == string.Empty)
                throw new Exception($"{ConfigArgument} is given without a file after it");

            string folder = Path.GetDirectoryName(FilePath);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // An empty file counts as none yet: one made by hand - New, Text Document -
            // to be pointed at by a shortcut before the first run.
            if (!File.Exists(FilePath) || string.IsNullOrWhiteSpace(File.ReadAllText(FilePath)))
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

        /// The value after a command-line argument: null if the argument is not
        /// there, empty if nothing follows it - or only the next argument does.
        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : string.Empty;

            return null;
        }
    }
}
