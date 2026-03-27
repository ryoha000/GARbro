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

            var paginationError = PaginationHelper.Validate("list", options.Input, options.Limit, options.Offset);
            if (paginationError != null)
                return paginationError;

            var result = ResultBuilder.Success("list", options.Input);
            Dictionary<string, object> openError;
            using (var arc = CommandUtilities.TryOpenArchive("list", options.Input, out openError))
            {
                if (arc == null)
                    return openError;

                result["container"] = new Dictionary<string, object>
                {
                    { "kind", "archive" },
                    { "format", new Dictionary<string, object>
                        {
                            { "tag", arc.Tag },
                            { "description", arc.Description }
                        }
                    }
                };

                var entries = arc.Dir
                    .Select(e => new
                    {
                        Entry = e,
                        ClassifiedType = EntryTypeClassifier.Classify(e)
                    })
                    .ToList();

                if (!string.IsNullOrEmpty(options.Type))
                {
                    entries = entries
                        .Where(e => e.ClassifiedType.Equals(options.Type, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrEmpty(options.Filter))
                {
                    string extFilter = options.Filter.StartsWith("*.") ? options.Filter.Substring(1) : options.Filter;
                    entries = entries
                        .Where(e => e.Entry.Name.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase)
                                 || e.Entry.Name.IndexOf(extFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                entries = entries
                    .OrderBy(e => e.Entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int totalMatching = entries.Count;
                var page = PaginationHelper.Apply(entries, options.Limit, options.Offset);

                var entryList = page.Select(e => new Dictionary<string, object>
                {
                    { "path", e.Entry.Name },
                    { "name", Path.GetFileName(e.Entry.Name) },
                    { "type", e.ClassifiedType },
                    { "size", e.Entry.Size },
                    { "packed_size", e.Entry.Size }
                }).ToList();

                result["entries"] = entryList;
                result["pagination"] = PaginationHelper.CreateMetadata(options.Limit, options.Offset, entryList.Count, totalMatching);
                result["summary"] = new Dictionary<string, object>
                {
                    { "entry_count", entryList.Count },
                    { "total_matching", totalMatching }
                };
                return result;
            }
        }
    }
}
