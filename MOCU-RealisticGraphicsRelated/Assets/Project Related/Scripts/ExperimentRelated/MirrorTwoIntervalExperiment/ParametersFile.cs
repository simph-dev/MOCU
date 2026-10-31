using System;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using UnityEngine;


namespace MirrorTwoIntervalExperiment
{
    /// Reads the experiment settings from a JSON file that sits in the same folder
    /// as the saved data, so parameters can be changed without recompiling.
    ///
    /// One optional command-line argument, so that each experiment can have a
    /// Windows shortcut of its own, pointing at a config file of its own:
    ///
    ///     -config <path>   reads this file instead of the default one; a relative
    ///                      path is taken from the shortcut's "Start in" folder
    ///
    /// In the Unity Editor there is none, so the default file is read.
    public static class ParametersFile
    {
        private const string FileName = "MirrorTwoIntervalConfig.json";
        private const string ConfigArgument = "-config";

        /// Replace, not Auto: a property that already holds an object is otherwise
        /// populated into rather than swapped out. See MirrorExperiment.ParametersFile.
        private static readonly JsonSerializerSettings ReadSettings = new()
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public static string DefaultFolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuM2I");

        /// The file actually read: -config's, or the default one.
        public static string FilePath => string.IsNullOrEmpty(ReadArgument(ConfigArgument))
            ? Path.Combine(DefaultFolderPath, FileName)
            : Path.GetFullPath(ReadArgument(ConfigArgument));


        /// Returns the settings from the file. With no file yet, writes one filled
        /// with the defaults first, as a complete template.
        ///
        /// Throws if the file exists but cannot be read, or -config has no file
        /// after it: running with silently substituted settings would be worse
        /// than not running.
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
                Debug.Log($"MirrorTwoIntervalExperiment: no config found, wrote defaults to {FilePath}");
                return defaults;
            }

            string json = File.ReadAllText(FilePath);
            ExperimentChoice.Expect(ExperimentKind.TwoInterval, json, FilePath);

            var loaded = JsonHelper.DeserializeJson<Parameters>(json, ReadSettings);
            Debug.Log($"MirrorTwoIntervalExperiment: loaded config from {FilePath} (design {loaded.Design})");

            return loaded;
        }

        /// Where sessions are saved: Parameters.DataFolder, a relative one taken from
        /// the config file's own folder, or with none the default folder. Throws on
        /// a path that cannot be a path.
        public static string DataFolderFor(Parameters parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.DataFolder))
                return DefaultFolderPath;

            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(FilePath), parameters.DataFolder));
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
