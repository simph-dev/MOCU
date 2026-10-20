using System;
using System.Collections.Generic;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;


namespace MirrorTwoIntervalExperiment
{
    /// One folder per session, with the settings that were used and one line of
    /// JSON per trial, mirrored into a hidden backup folder - the same layout as
    /// MirrorExperiment, under a folder of its own:
    ///
    ///     Documents/MocuM2I/2026-10-19_14-32-05/config.json
    ///     Documents/MocuM2I/2026-10-19_14-32-05/trials.jsonl
    public class SessionWriter
    {
        private const string ConfigFileName = "config.json";
        private const string TrialsFileName = "trials.jsonl";

        private static readonly JsonSerializerSettings LineSettings = new()
        {
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter() }
        };

        private readonly List<string> _sessionFolders = new();

        public string MainFolder => _sessionFolders.Count > 0 ? _sessionFolders[0] : null;


        public SessionWriter(DateTimeOffset startedAt, Parameters parameters)
        {
            string sessionName = startedAt.ToString("yyyy-MM-dd_HH-mm-ss");

            string documentsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuM2I");

            string vaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MocuM2I_Vault");

            _sessionFolders.Add(CreateFolder(documentsRoot, sessionName, hideRoot: false));
            _sessionFolders.Add(CreateFolder(vaultRoot, sessionName, hideRoot: true));

            string config = JsonHelper.SerializeJson(parameters);

            foreach (var folder in _sessionFolders)
                File.WriteAllText(Path.Combine(folder, ConfigFileName), config);

            Debug.Log($"MirrorTwoIntervalExperiment: writing session to {MainFolder}");
        }

        public void AppendTrial(Trial trial)
        {
            string line = JsonConvert.SerializeObject(trial, LineSettings);

            foreach (var folder in _sessionFolders)
                File.AppendAllText(Path.Combine(folder, TrialsFileName), line + Environment.NewLine);
        }

        private static string CreateFolder(string root, string sessionName, bool hideRoot)
        {
            if (!Directory.Exists(root))
            {
                var created = Directory.CreateDirectory(root);

                if (hideRoot)
                    created.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }

            string sessionFolder = Path.Combine(root, sessionName);
            Directory.CreateDirectory(sessionFolder);

            return sessionFolder;
        }
    }
}
