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

            var result = ResultBuilder.Success("extract", options.Input);
            result["output_dir"] = Path.GetFullPath(options.OutDir);

            // Safe root validation
            string safeRoot = Path.GetFullPath(options.OutDir);
            if (!string.IsNullOrEmpty(options.SafeRoot))
            {
                safeRoot = Path.GetFullPath(options.SafeRoot);
                if (!Path.GetFullPath(options.OutDir).StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return ResultBuilder.Error("extract", options.Input, ErrorCodes.OUTSIDE_SAFE_ROOT, "Output directory is outside of the safe root.");
                }
            }

            try
            {
                VFS.ChDir(options.Input);
            }
            catch (Exception ex)
            {
                return ResultBuilder.Error("extract", options.Input, ErrorCodes.ARCHIVE_OPEN_FAILED, "Failed to open archive: " + ex.Message);
            }

            var arcFs = VFS.Top as ArchiveFileSystem;
            if (arcFs == null)
            {
                return ResultBuilder.Error("extract", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "Input is not a supported archive");
            }

            var arc = arcFs.Source;
            IEnumerable<Entry> entries = arc.Dir;

            // Gather required entries
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (options.Entries != null)
            {
                foreach (var ev in options.Entries) targetPaths.Add(ev);
            }

            if (!string.IsNullOrEmpty(options.EntryFile) && File.Exists(options.EntryFile))
            {
                foreach (var line in File.ReadAllLines(options.EntryFile))
                {
                    if (!string.IsNullOrWhiteSpace(line)) targetPaths.Add(line.Trim());
                }
            }

            bool useList = targetPaths.Count > 0;

            if (useList)
            {
                entries = entries.Where(e => targetPaths.Contains(e.Name));
            }

            if (!string.IsNullOrEmpty(options.Filter))
            {
                string extFilter = options.Filter.StartsWith("*.") ? options.Filter.Substring(1) : options.Filter;
                entries = entries.Where(e => e.Name.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase) || e.Name.Contains(extFilter));
            }

            if (!string.IsNullOrEmpty(options.Type))
            {
                entries = entries.Where(e => e.Type.Equals(options.Type, StringComparison.OrdinalIgnoreCase));
            }

            var resultsList = new List<Dictionary<string, object>>();
            int succeeded = 0;
            int failed = 0;
            int requested = entries.Count();

            if (!options.DryRun && !Directory.Exists(options.OutDir))
            {
                Directory.CreateDirectory(options.OutDir);
            }

            foreach (var entry in entries)
            {
                var entryResult = new Dictionary<string, object>
                {
                    { "entry", entry.Name },
                    { "size", entry.Size }
                };

                // Determine output path
                string relPath = options.Flatten ? Path.GetFileName(entry.Name) : entry.Name.Replace('/', Path.DirectorySeparatorChar);
                string outPath = Path.GetFullPath(Path.Combine(options.OutDir, relPath));
                
                // Ensure outPath is within safeRoot
                if (!outPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    entryResult["status"] = "failed";
                    entryResult["error"] = "Path traversal attempted.";
                    failed++;
                    resultsList.Add(entryResult);
                    continue;
                }

                entryResult["output_path"] = outPath;

                if (!options.DryRun)
                {
                    if (File.Exists(outPath) && options.Overwrite == "skip")
                    {
                        entryResult["status"] = "skipped";
                        resultsList.Add(entryResult);
                        continue;
                    }

                    try
                    {
                        string dir = Path.GetDirectoryName(outPath);
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        arc.Extract(entry);
                        
                        // Wait, ArcFile.Extract does not export to a specific dir natively unless we give it Stream!
                        // Let's implement extraction manually fetching stream from ArcView
                        using (var input = arc.OpenEntry(entry))
                        using (var output = File.Create(outPath))
                        {
                            input.CopyTo(output);
                        }

                        entryResult["status"] = "extracted";
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        entryResult["status"] = "failed";
                        entryResult["error"] = ex.Message;
                        failed++;
                    }
                }
                else
                {
                    entryResult["status"] = "dry-run";
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
