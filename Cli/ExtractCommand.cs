using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GameRes;

namespace GARbro.Cli
{
    public static class ExtractCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.Input))
                return ResultBuilder.Error("extract", options.Input, ErrorCodes.INVALID_ARGUMENT, "--input is required");

            if (string.IsNullOrEmpty(options.OutDir))
                return ResultBuilder.Error("extract", options.Input, ErrorCodes.INVALID_ARGUMENT, "--out directory is required");

            if (!File.Exists(options.Input))
                return ResultBuilder.Error("extract", options.Input, ErrorCodes.INPUT_NOT_FOUND, "Input path not found");

            var overwriteError = CommandUtilities.ValidateOverwritePolicy("extract", options.Input, options.Overwrite);
            if (overwriteError != null)
                return overwriteError;

            var result = ResultBuilder.Success("extract", options.Input);
            result["output_dir"] = Path.GetFullPath(options.OutDir);

            var safeRootError = CommandUtilities.ValidateSafeRootOutput("extract", options.Input, options.OutDir, options.SafeRoot);
            if (safeRootError != null)
                return safeRootError;

            var safeRoot = string.IsNullOrEmpty(options.SafeRoot) ? Path.GetFullPath(options.OutDir) : Path.GetFullPath(options.SafeRoot);

            Dictionary<string, object> openError;
            using (var arc = CommandUtilities.TryOpenArchive("extract", options.Input, out openError))
            {
                if (arc == null)
                    return openError;

                var entries = arc.Dir.Select(e => new
                {
                    Entry = e,
                    ClassifiedType = EntryTypeClassifier.Classify(e)
                }).ToList();

                var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (options.Entries != null)
                {
                    foreach (var entryValue in options.Entries)
                    {
                        if (!string.IsNullOrWhiteSpace(entryValue))
                            targetPaths.Add(entryValue);
                    }
                }

                if (!string.IsNullOrEmpty(options.EntryFile) && File.Exists(options.EntryFile))
                {
                    foreach (var line in File.ReadAllLines(options.EntryFile))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            targetPaths.Add(line.Trim());
                    }
                }

                if (targetPaths.Count > 0)
                    entries = entries.Where(e => targetPaths.Contains(e.Entry.Name)).ToList();

                if (!string.IsNullOrEmpty(options.Type))
                {
                    entries = entries.Where(e => e.ClassifiedType.Equals(options.Type, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(options.Filter))
                {
                    string extFilter = options.Filter.StartsWith("*.") ? options.Filter.Substring(1) : options.Filter;
                    entries = entries.Where(e =>
                        e.Entry.Name.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase) ||
                        e.Entry.Name.IndexOf(extFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                entries = entries.OrderBy(e => e.Entry.Name, StringComparer.OrdinalIgnoreCase).ToList();

                var resultsList = new List<Dictionary<string, object>>();
                int succeeded = 0;
                int failed = 0;
                int requested = entries.Count;

                if (!options.DryRun && !Directory.Exists(options.OutDir))
                    Directory.CreateDirectory(options.OutDir);

                foreach (var item in entries)
                {
                    var entry = item.Entry;
                    var entryResult = new Dictionary<string, object>
                    {
                        { "entry", entry.Name },
                        { "size", entry.Size }
                    };

                    string relPath = options.Flatten ? Path.GetFileName(entry.Name) : entry.Name.Replace('/', Path.DirectorySeparatorChar);
                    string outPath = Path.GetFullPath(Path.Combine(options.OutDir, relPath));

                    if (!CommandUtilities.IsFilePathInsideRoot(outPath, safeRoot))
                    {
                        entryResult["status"] = "failed";
                        entryResult["error"] = "Path traversal attempted.";
                        failed++;
                        resultsList.Add(entryResult);
                        continue;
                    }

                    if (options.DryRun)
                    {
                        entryResult["status"] = "dry-run";
                        entryResult["output_path"] = outPath;
                        resultsList.Add(entryResult);
                        continue;
                    }

                    var resolution = OverwritePolicyResolver.Resolve(outPath, options.Overwrite);
                    entryResult["output_path"] = resolution.OutputPath;

                    if (!resolution.ShouldWrite)
                    {
                        entryResult["status"] = "skipped_existing";
                        resultsList.Add(entryResult);
                        continue;
                    }

                    try
                    {
                        OverwritePolicyResolver.WriteFile(resolution.OutputPath, resolution.ReplaceExisting, output =>
                        {
                            using (var input = arc.OpenEntry(entry))
                            {
                                input.CopyTo(output);
                            }
                        });

                        entryResult["status"] = "extracted";
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        entryResult["status"] = "failed";
                        entryResult["error"] = ex.Message;
                        failed++;
                    }

                    resultsList.Add(entryResult);
                }

                result["results"] = resultsList;
                result["summary"] = new Dictionary<string, object>
                {
                    { "requested", requested },
                    { "succeeded", succeeded },
                    { "failed", failed }
                };

                return result;
            }
        }
    }
}
