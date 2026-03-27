using System.Collections.Generic;
using System.Linq;
using GameRes;

namespace GARbro.Cli
{
    public static class FormatsCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            var result = ResultBuilder.Success("formats", options.Input);
            result.Remove("input");
            var formats = FormatCatalog.Instance.Formats
                .Select(format => new Dictionary<string, object>
                {
                    { "tag", format.Tag },
                    { "kind", NormalizeKind(format.Type) },
                    { "description", format.Description }
                })
                .OrderBy(item => (string)item["kind"])
                .ThenBy(item => (string)item["tag"])
                .ToList();

            result["formats"] = formats;
            return result;
        }

        static string NormalizeKind(string type)
        {
            switch ((type ?? string.Empty).ToLowerInvariant())
            {
                case "archive":
                case "image":
                case "audio":
                case "script":
                    return type.ToLowerInvariant();
                default:
                    return "other";
            }
        }
    }
}
