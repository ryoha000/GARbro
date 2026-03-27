using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameRes;

namespace GARbro.Cli
{
    internal sealed class OverwriteResolution
    {
        public bool ShouldWrite { get; set; }
        public bool ReplaceExisting { get; set; }
        public string OutputPath { get; set; }
    }

    internal static class CommandUtilities
    {
        public static Dictionary<string, object> ValidateOverwritePolicy(string command, string input, string policy)
        {
            if (policy == "skip" || policy == "replace" || policy == "rename")
                return null;
            return ResultBuilder.Error(command, input, ErrorCodes.INVALID_ARGUMENT, "--overwrite must be one of skip, replace, rename");
        }

        public static Dictionary<string, object> ValidateSafeRootOutput(string command, string input, string outputDir, string safeRoot)
        {
            if (string.IsNullOrEmpty(outputDir))
                return null;

            var fullOutputDir = Path.GetFullPath(outputDir);
            var fullSafeRoot = string.IsNullOrEmpty(safeRoot) ? fullOutputDir : Path.GetFullPath(safeRoot);
            if (!IsPathInsideRoot(fullOutputDir, fullSafeRoot))
                return ResultBuilder.Error(command, input, ErrorCodes.OUTSIDE_SAFE_ROOT, "Output directory is outside of the safe root.");
            return null;
        }

        public static ArcFile TryOpenArchive(string command, string input, out Dictionary<string, object> error)
        {
            error = null;
            FormatCatalog.Instance.LastError = null;
            ArcFile arc = null;
            try
            {
                arc = ArcFile.TryOpen(input);
            }
            catch (Exception ex)
            {
                FormatCatalog.Instance.LastError = ex;
            }

            if (arc != null)
                return arc;

            var lastError = FormatCatalog.Instance.LastError;
            if (lastError != null)
            {
                if (ErrorCodes.RequiresAdditionalContext(lastError))
                {
                    error = ResultBuilder.Error(command, input, ErrorCodes.REQUIRES_ADDITIONAL_CONTEXT, "Failed to open archive: " + lastError.Message);
                    return null;
                }
                error = ResultBuilder.Error(command, input, ErrorCodes.ARCHIVE_OPEN_FAILED, "Failed to open archive: " + lastError.Message);
                return null;
            }

            error = ResultBuilder.Error(command, input, ErrorCodes.INPUT_NOT_SUPPORTED, "Input is not a supported archive");
            return null;
        }

        public static Entry FindEntry(ArcFile arc, string entryName)
        {
            return arc.Dir.FirstOrDefault(e => e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsPathInsideRoot(string path, string root)
        {
            var fullPath = AppendDirectorySeparator(Path.GetFullPath(path));
            var fullRoot = AppendDirectorySeparator(Path.GetFullPath(root));
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFilePathInsideRoot(string filePath, string root)
        {
            var fullPath = Path.GetFullPath(filePath);
            var fullRoot = AppendDirectorySeparator(Path.GetFullPath(root));
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }
    }

    internal static class PaginationHelper
    {
        public static Dictionary<string, object> Validate(string command, string input, int limit, int offset)
        {
            if (limit < 1 || limit > 1000)
                return ResultBuilder.Error(command, input, ErrorCodes.INVALID_ARGUMENT, "--limit must be between 1 and 1000");
            if (offset < 0)
                return ResultBuilder.Error(command, input, ErrorCodes.INVALID_ARGUMENT, "--offset must be 0 or greater");
            return null;
        }

        public static List<T> Apply<T>(IReadOnlyList<T> source, int limit, int offset)
        {
            return source.Skip(offset).Take(limit).ToList();
        }

        public static Dictionary<string, object> CreateMetadata(int limit, int offset, int returned, int total)
        {
            return new Dictionary<string, object>
            {
                { "limit", limit },
                { "offset", offset },
                { "returned", returned },
                { "total", total },
                { "has_more", offset + returned < total }
            };
        }
    }

    internal static class EntryTypeClassifier
    {
        static readonly HashSet<string> KnownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image",
            "audio",
            "script",
            "text",
            "archive",
            "binary",
            "unknown"
        };

        static readonly HashSet<string> ScriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ks", "kag", "scn", "scr", "ws2", "s", "sd", "asd", "tjs"
        };

        static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "txt", "csv", "json", "xml", "ini", "cfg", "log", "yml", "yaml", "md"
        };

        public static string Classify(Entry entry)
        {
            if (entry == null)
                return "unknown";

            if (!string.IsNullOrEmpty(entry.Type) && KnownTypes.Contains(entry.Type))
                return entry.Type.ToLowerInvariant();

            if ("image".Equals(entry.Type, StringComparison.OrdinalIgnoreCase) ||
                "audio".Equals(entry.Type, StringComparison.OrdinalIgnoreCase) ||
                "script".Equals(entry.Type, StringComparison.OrdinalIgnoreCase) ||
                "archive".Equals(entry.Type, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Type.ToLowerInvariant();
            }

            var ext = Path.GetExtension(entry.Name).TrimStart('.');
            if (!string.IsNullOrEmpty(ext))
            {
                if (ScriptExtensions.Contains(ext))
                    return "script";
                if (TextExtensions.Contains(ext))
                    return "text";
            }

            var inferred = FormatCatalog.Instance.GetTypeFromName(entry.Name);
            if ("image".Equals(inferred, StringComparison.OrdinalIgnoreCase))
                return "image";
            if ("audio".Equals(inferred, StringComparison.OrdinalIgnoreCase))
                return "audio";
            if ("archive".Equals(inferred, StringComparison.OrdinalIgnoreCase))
                return "archive";
            if ("script".Equals(inferred, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(ext) && TextExtensions.Contains(ext))
                    return "text";
                return "script";
            }

            if (!string.IsNullOrEmpty(ext))
                return "binary";

            return "unknown";
        }
    }

    internal static class OverwritePolicyResolver
    {
        public static OverwriteResolution Resolve(string outputPath, string policy)
        {
            if (!File.Exists(outputPath))
            {
                return new OverwriteResolution
                {
                    ShouldWrite = true,
                    ReplaceExisting = false,
                    OutputPath = outputPath
                };
            }

            if ("skip".Equals(policy, StringComparison.OrdinalIgnoreCase))
            {
                return new OverwriteResolution
                {
                    ShouldWrite = false,
                    ReplaceExisting = false,
                    OutputPath = outputPath
                };
            }

            if ("replace".Equals(policy, StringComparison.OrdinalIgnoreCase))
            {
                return new OverwriteResolution
                {
                    ShouldWrite = true,
                    ReplaceExisting = true,
                    OutputPath = outputPath
                };
            }

            return new OverwriteResolution
            {
                ShouldWrite = true,
                ReplaceExisting = false,
                OutputPath = ResolveRenamedPath(outputPath)
            };
        }

        public static void WriteFile(string outputPath, bool replaceExisting, Action<Stream> writeAction)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(directory ?? string.Empty, Path.GetFileName(outputPath) + ".tmp." + Guid.NewGuid().ToString("N"));
            try
            {
                using (var output = File.Create(tempPath))
                {
                    writeAction(output);
                }

                if (replaceExisting && File.Exists(outputPath))
                {
                    File.Replace(tempPath, outputPath, null, true);
                }
                else
                {
                    File.Move(tempPath, outputPath);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        static string ResolveRenamedPath(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            var extension = Path.GetExtension(outputPath);
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            for (int index = 1; ; ++index)
            {
                var candidate = Path.Combine(directory, string.Format("{0} ({1}){2}", baseName, index, extension));
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
    }
}
