using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameRes;

namespace GARbro.Cli
{
    internal sealed class ProbeEntryInfo
    {
        public Entry Entry { get; set; }
        public string Type { get; set; }
    }

    public static class ProbeCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.Input))
                return ResultBuilder.Error("probe", options.Input, ErrorCodes.INVALID_ARGUMENT, "--input is required");

            if (!File.Exists(options.Input))
                return ResultBuilder.Error("probe", options.Input, ErrorCodes.INPUT_NOT_FOUND, "Input path not found");

            Dictionary<string, object> openError;
            using (var arc = CommandUtilities.TryOpenArchive("probe", options.Input, out openError))
            {
                if (arc != null)
                    return ProbeArchive(options, arc);
            }

            if (openError != null && openError.ContainsKey("error"))
            {
                var error = (Dictionary<string, object>)openError["error"];
                var code = error["code"] as string;
                if (code == ErrorCodes.REQUIRES_ADDITIONAL_CONTEXT)
                    return openError;
            }

            return ProbeFile(options);
        }

        static Dictionary<string, object> ProbeArchive(CliOptions options, ArcFile arc)
        {
            var result = ResultBuilder.Success("probe", options.Input);
            result["kind"] = "archive";
            result["format"] = new Dictionary<string, object>
            {
                { "tag", arc.Tag },
                { "description", arc.Description }
            };

            var entries = arc.Dir.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var typedEntries = entries
                .Select(e => new ProbeEntryInfo { Entry = e, Type = EntryTypeClassifier.Classify(e) })
                .ToList();

            result["entry_count"] = typedEntries.Count;
            result["top_types"] = typedEntries
                .GroupBy(e => e.Type)
                .Select(group => new Dictionary<string, object>
                {
                    { "type", group.Key },
                    { "count", group.Count() }
                })
                .OrderByDescending(item => (int)item["count"])
                .ThenBy(item => (string)item["type"], StringComparer.OrdinalIgnoreCase)
                .ToList();

            var samples = new List<string>();
            AddSamples(samples, typedEntries, "script");
            AddSamples(samples, typedEntries, "image");
            AddSamples(samples, typedEntries, "audio");
            foreach (var item in typedEntries.Where(e => e.Type != "script" && e.Type != "image" && e.Type != "audio"))
            {
                if (samples.Count >= 10)
                    break;
                if (!samples.Contains(item.Entry.Name))
                    samples.Add(item.Entry.Name);
            }
            result["samples"] = samples;
            result["recommended_next_actions"] = new List<string>
            {
                string.Format("list --input {0} --limit 100 --offset 0", CommandUtilities.QuoteArgument(options.Input)),
                string.Format("list --input {0} --type script --limit 100 --offset 0", CommandUtilities.QuoteArgument(options.Input)),
                string.Format("extract --input {0} --type script --out <dir>", CommandUtilities.QuoteArgument(options.Input))
            };
            return result;
        }

        static Dictionary<string, object> ProbeFile(CliOptions options)
        {
            var result = ResultBuilder.Success("probe", options.Input);
            result["kind"] = "file";

            var format = DetectFileFormat(options.Input);
            result["format"] = new Dictionary<string, object>
            {
                { "tag", format.Item1 },
                { "description", format.Item2 }
            };

            var entry = new Entry { Name = options.Input, Type = format.Item3 };
            var classifiedType = EntryTypeClassifier.Classify(entry);
            result["entry_count"] = 1;
            result["top_types"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "type", classifiedType },
                    { "count", 1 }
                }
            };
            result["samples"] = new List<string> { Path.GetFileName(options.Input) };

            var actions = new List<string>
            {
                string.Format("identify --input {0}", CommandUtilities.QuoteArgument(options.Input))
            };
            if ("image".Equals(classifiedType, StringComparison.OrdinalIgnoreCase))
                actions.Add(string.Format("convert --input {0} --to png --out <dir>", CommandUtilities.QuoteArgument(options.Input)));
            else if ("audio".Equals(classifiedType, StringComparison.OrdinalIgnoreCase))
                actions.Add(string.Format("convert --input {0} --to wav --out <dir>", CommandUtilities.QuoteArgument(options.Input)));
            result["recommended_next_actions"] = actions;
            return result;
        }

        static Tuple<string, string, string> DetectFileFormat(string inputPath)
        {
            using (var file = BinaryStream.FromFile(inputPath))
            {
                var image = ImageFormat.FindFormat(file);
                if (image != null)
                    return Tuple.Create(image.Item1.Tag, image.Item1.Description, "image");

                file.Position = 0;
                var script = ScriptFormat.FindFormat(file);
                if (script != null)
                {
                    var scriptType = Path.GetExtension(inputPath).Equals(".txt", StringComparison.OrdinalIgnoreCase) ? "text" : "script";
                    return Tuple.Create(script.Tag, script.Description, scriptType);
                }

                file.Position = 0;
                using (var sound = AudioFormat.Read(file))
                {
                    if (sound != null)
                    {
                        var sourceTag = (sound.SourceFormat ?? string.Empty).ToUpperInvariant();
                        var audioFormat = FormatCatalog.Instance.LookupExtension(sound.SourceFormat ?? string.Empty)
                            .OfType<AudioFormat>()
                            .FirstOrDefault();
                        if (audioFormat != null)
                            return Tuple.Create(audioFormat.Tag, audioFormat.Description, "audio");
                        if (!string.IsNullOrEmpty(sourceTag))
                            return Tuple.Create(sourceTag, "Audio file", "audio");
                        return Tuple.Create("AUDIO", "Audio file", "audio");
                    }
                }
            }

            var fallback = FormatCatalog.Instance.LookupFileName(inputPath).FirstOrDefault();
            if (fallback != null)
                return Tuple.Create(fallback.Tag, fallback.Description, fallback.Type);

            return Tuple.Create("UNKNOWN", "Unknown format", "unknown");
        }

        static void AddSamples(List<string> samples, IEnumerable<ProbeEntryInfo> entries, string type)
        {
            foreach (var item in entries.Where(e => e.Type == type))
            {
                if (samples.Count >= 10)
                    break;
                if (!samples.Contains(item.Entry.Name))
                    samples.Add(item.Entry.Name);
            }
        }
    }
}
