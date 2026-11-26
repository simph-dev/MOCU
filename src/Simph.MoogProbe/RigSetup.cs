using System;
using System.IO;
using System.Net;
using System.Text.Json;

using Simph.Core.Geometry;
using Simph.Core.Motion.Limits;
using Simph.Core.Platform;

namespace Simph.MoogProbe
{
    /// <summary>
    /// The lab's platform as described by config/rig-setup.json: where the MBC is,
    /// how the machine responds to a command, and what the lab allows it to do.
    /// Read field by field, so that a mistake in the file names the field.
    /// </summary>
    public sealed class RigSetup
    {
        private const double Degrees = Math.PI / 180.0;

        private RigSetup(string name, IPEndPoint host, IPEndPoint mbc, Pose6 neutral,
            PlatformModel model, MotionEnvelope envelope)
        {
            Name = name;
            Host = host;
            Mbc = mbc;
            Neutral = neutral;
            Model = model;
            Envelope = envelope;
        }

        public string Name { get; }
        public IPEndPoint Host { get; }
        public IPEndPoint Mbc { get; }
        public Pose6 Neutral { get; }
        public PlatformModel Model { get; }
        public MotionEnvelope Envelope { get; }

        /// <summary>Next to the executable; the build copies config/rig-setup.json there.</summary>
        public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "rig-setup.json");

        public static RigSetup Load(string path) => Parse(File.ReadAllText(path));

        public static RigSetup Parse(string json)
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            using var document = JsonDocument.Parse(json, options);
            var root = document.RootElement;

            var model = Field(root, "model");
            var frame = TimeSpan.FromMilliseconds(1);
            var driveLag = TimeSpan.FromMilliseconds(Number(model, "driveLagMilliseconds"));

            // Optional, unlike the rest: a setup without it describes a pure
            // lag, and that is what zero means.
            double driveDirectShare = model.TryGetProperty("driveDirectShare", out _)
                ? Number(model, "driveDirectShare")
                : 0.0;

            var geometry = ReadGeometry(Field(model, "geometry"));

            var limits = Field(root, "safetyLimits");

            // Optional: a setup without it leaves the legs unchecked.
            LegLimits? legs = null;
            if (limits.TryGetProperty("legs", out var l))
            {
                double retracted = Number(l, "retractedLength");
                legs = new LegLimits(geometry, retracted + Number(l, "minStroke"), retracted + Number(l, "maxStroke"));
            }

            return new RigSetup(
                Text(root, "name"),
                ProbeOptions.ParseEndPoint(Text(root, "host"), 16386),
                ProbeOptions.ParseEndPoint(Text(root, "mbc"), 16384),
                ReadPose(Field(root, "neutral")),
                new PlatformModel(ReadPose(Field(model, "filterAcceleration")), driveLag, driveDirectShare, geometry, frame),
                new MotionEnvelope(
                    roll: Rotation(Field(limits, "roll")),
                    pitch: Rotation(Field(limits, "pitch")),
                    yaw: Rotation(Field(limits, "yaw")),
                    surge: Translation(Field(limits, "surge")),
                    sway: Translation(Field(limits, "sway")),
                    heave: Translation(Field(limits, "heave")),
                    legs: legs));
        }

        private static HexapodGeometry ReadGeometry(JsonElement g) => new HexapodGeometry(
            Number(g, "baseRadial"), Number(g, "baseTransverse"),
            Number(g, "platformRadial"), Number(g, "platformTransverse"),
            Number(g, "settledJointSeparation"));

        private static Pose6 ReadPose(JsonElement p) => new Pose6(
            roll: Number(p, "roll"), pitch: Number(p, "pitch"), yaw: Number(p, "yaw"),
            surge: Number(p, "surge"), sway: Number(p, "sway"), heave: Number(p, "heave"));

        /// <summary>A null bound means not established, and is left unchecked.</summary>
        private static AxisLimits Translation(JsonElement a) => new AxisLimits(
            Bound(a, "min", double.NegativeInfinity), Bound(a, "max", double.PositiveInfinity),
            Number(a, "speed"), Number(a, "acceleration"));

        private static AxisLimits Rotation(JsonElement a) => new AxisLimits(
            Bound(a, "minDegrees", double.NegativeInfinity) * Degrees, Bound(a, "maxDegrees", double.PositiveInfinity) * Degrees,
            Number(a, "speedDegrees") * Degrees, Number(a, "acceleration"));

        private static JsonElement Field(JsonElement parent, string name) =>
            parent.TryGetProperty(name, out var value)
                ? value
                : throw new FormatException($"rig setup: \"{name}\" is missing.");

        private static double Number(JsonElement parent, string name)
        {
            var value = Field(parent, name);
            return value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : throw new FormatException($"rig setup: \"{name}\" must be a number.");
        }

        private static double Bound(JsonElement parent, string name, double open)
        {
            var value = Field(parent, name);
            return value.ValueKind == JsonValueKind.Null ? open : Number(parent, name);
        }

        private static string Text(JsonElement parent, string name)
        {
            var value = Field(parent, name);
            return value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : throw new FormatException($"rig setup: \"{name}\" must be text.");
        }
    }
}
