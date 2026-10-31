using System;
using System.IO;

using Newtonsoft.Json.Linq;
using UnityEngine;


/// The experiments in the car, either of which this exe runs.
public enum ExperimentKind
{
    /// MirrorExperiment: one movement, left or right of straight ahead.
    Heading,

    /// MirrorTwoIntervalExperiment: two movements, which went further.
    TwoInterval
}


/// What every experiment handler offers GeneralScript, which calibrates the head
/// once the headset is ready without knowing which experiment is running.
public interface IHeadCalibration
{
    void CalibrateHead();
}


/// Which experiment runs, decided once at startup (Bootstrap) by the config file
/// the exe is given with -config: its "Experiment" field, or - in a file written
/// before there was one - its shape. With no -config, as in the Unity Editor, the
/// default. Changing experiment means starting the exe again.
public static class ExperimentChoice
{
    public const ExperimentKind Default = ExperimentKind.TwoInterval;

    private const string ConfigArgument = "-config";

    public static ExperimentKind Read()
    {
        string path = ReadArgument(ConfigArgument);

        if (string.IsNullOrEmpty(path))
            return Default;

        try
        {
            string full = Path.GetFullPath(path);

            if (!File.Exists(full) || string.IsNullOrWhiteSpace(File.ReadAllText(full)))
                return Default;

            return KindOf(File.ReadAllText(full)) ?? Default;
        }
        catch (Exception e)
        {
            // The experiment's own loading reports the file's problem on Start.
            Debug.LogError($"ExperimentChoice: could not read {path} - running {Default}\n{e.Message}");
            return Default;
        }
    }

    /// Which experiment a config is for: its "Experiment" field, or failing that
    /// its shape. Null when neither tells.
    public static ExperimentKind? KindOf(string json)
    {
        var root = JObject.Parse(json);

        return NamedIn(root, out ExperimentKind named) == true ? named : ShapeOf(root);
    }

    /// Refuses a config that is not this experiment's - by its field, or by its
    /// shape, which must agree. Read as this one's, it would load with every
    /// setting it does not share silently at its default.
    public static void Expect(ExperimentKind kind, string json, string path)
    {
        var root = JObject.Parse(json);

        bool? isNamed = NamedIn(root, out ExperimentKind named);

        if (isNamed == false)
            throw new Exception($"{path}: \"Experiment\" must be one of {string.Join(", ", Enum.GetNames(typeof(ExperimentKind)))}");

        if (isNamed == true && named != kind)
            throw new Exception($"{path} is a config for the {named} experiment, not {kind}");

        ExperimentKind? shape = ShapeOf(root);

        if (shape != null && shape != kind)
            throw new Exception($"{path} is laid out as a config for the {shape} experiment, not {kind}");
    }

    /// The "Experiment" field: true with a known experiment, false with anything
    /// else in it, null when there is none.
    private static bool? NamedIn(JObject root, out ExperimentKind named)
    {
        named = default;

        if (!root.TryGetValue("Experiment", StringComparison.OrdinalIgnoreCase, out var value))
            return null;

        return Enum.TryParse(value.ToString(), ignoreCase: true, out named)
               && Enum.IsDefined(typeof(ExperimentKind), named);
    }

    /// "Design" is the two-interval experiment's, "Conditions" the heading one's.
    private static ExperimentKind? ShapeOf(JObject root) =>
        root.ContainsKey("Design") ? ExperimentKind.TwoInterval
        : root.ContainsKey("Conditions") ? ExperimentKind.Heading
        : null;

    /// The value after a command-line argument: null if the argument is not there,
    /// empty if nothing follows it - or only the next argument does.
    private static string ReadArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : string.Empty;

        return null;
    }
}
