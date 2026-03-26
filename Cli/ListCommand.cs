using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using GameRes;

namespace GARbro.Cli
{
    public static class ListCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.Input))
                return ResultBuilder.Error("list", options.Input, ErrorCodes.INVALID_ARGUMENT, "--input is required");

            if (!File.Exists(options.Input))
                return ResultBuilder.Error("list", options.Input, ErrorCodes.INPUT_NOT_FOUND, "Input path not found");

            var result = ResultBuilder.Success("list", options.Input);

            try
            {
                VFS.ChDir(options.Input);
            }
            catch (Exception ex)
            {
                var rootError = FormatCatalog.Instance.LastError ?? ex;
                if (ErrorCodes.RequiresAdditionalContext(ex) || ErrorCodes.RequiresAdditionalContext(rootError))
                    return ResultBuilder.Error("list", options.Input, ErrorCodes.REQUIRES_ADDITIONAL_CONTEXT, "Failed to open archive: " + rootError.Message);
                return ResultBuilder.Error("list", options.Input, ErrorCodes.ARCHIVE_OPEN_FAILED, "Failed to open archive: " + ex.Message);
            }

            var arcFs = VFS.Top as ArchiveFileSystem;
            if (arcFs == null)
            {
                return ResultBuilder.Error("list", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "Input is not a supported archive");
            }

            var arc = arcFs.Source;
            result["container"] = new Dictionary<string, string> { { "kind", "archive" }, { "format", "Unknown" } };

            IEnumerable<Entry> entries = arc.Dir;
            
            // filters
            if (!string.IsNullOrEmpty(options.Filter))
            {
                // Very basic glob (e.g. *.ks)
                string extFilter = options.Filter.StartsWith("*.") ? options.Filter.Substring(1) : options.Filter;
                entries = entries.Where(e => e.Name.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase) || e.Name.Contains(extFilter));
            }

            if (!string.IsNullOrEmpty(options.Type))
            {
                entries = entries.Where(e => e.Type.Equals(options.Type, StringComparison.OrdinalIgnoreCase));
            }

            // sort
            if (options.Sort == "path") entries = entries.OrderBy(e => e.Name);
            else if (options.Sort == "type") entries = entries.OrderBy(e => e.Type);
            else if (options.Sort == "size") entries = entries.OrderBy(e => e.Size);

            // counts
            int totalMatching = entries.Count();

            // pagination
            if (options.Offset > 0) entries = entries.Skip(options.Offset);
            if (options.Limit > 0) entries = entries.Take(options.Limit);

            var entryList = new List<Dictionary<string, object>>();
            foreach (var e in entries)
            {
                entryList.Add(new Dictionary<string, object>
                {
                    { "path", e.Name },
                    { "name", Path.GetFileName(e.Name) },
                    { "type", e.Type },
                    { "size", e.Size },
                    { "packed_size", e.Size } // We may properly fetch packed size if exposed
                });
            }

            result["entries"] = entryList;
            result["summary"] = new Dictionary<string, object>
            {
                { "entry_count", entryList.Count },
                { "total_matching", totalMatching }
            };

            return result;
        }
    }
}
