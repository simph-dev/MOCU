using System;
using System.Collections.Generic;
using System.IO;

using DaemonsRelated;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;


namespace MirrorExperiment
{
    /// One folder per session, containing the settings that were used and one line
    /// of JSON per trial:
    ///
    ///     Documents/MocuME/2026-08-18_14-32-05/config.json
    ///     Documents/MocuME/2026-08-18_14-32-05/trials.jsonl
    ///
    /// Trials are appended, not rewritten. Rewriting the whole dataset after every
    /// trial put all of it in flight on every write, so a crash mid-write could take
    /// the lot; appending risks only the last line. The same session is mirrored
    /// into a hidden folder in the user profile as an independent backup.
    public class SessionWriter
    {
        private const string ConfigFileName = "config.json";
        private const string TrialsFileName = "trials.jsonl";

        // One object per line, so no indentation. Enums as names rather than
        // numbers, to keep the file readable years from now.
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
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MocuME");

            string vaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MocuME_Vault");

            _sessionFolders.Add(CreateFolder(documentsRoot, sessionName, hideRoot: false));
            _sessionFolders.Add(CreateFolder(vaultRoot, sessionName, hideRoot: true));

            // The full configuration, snapshotted at the moment the session began.
            // Written once, so the data explains itself without the config file next
            // to it having to still be the same one.
            string config = JsonHelper.SerializeJson(parameters);

            foreach (var folder in _sessionFolders)
                File.WriteAllText(Path.Combine(folder, ConfigFileName), config);

            Debug.Log($"MirrorExperiment: writing session to {MainFolder}");
        }

        public void AppendTrial(Trial trial)
        {
            string line = JsonConvert.SerializeObject(trial, LineSettings);

            foreach (var folder in _sessionFolders)
                File.AppendAllText(Path.Combine(folder, TrialsFileName), line + Environment.NewLine);
        }

        // ...........................................

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
