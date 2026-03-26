using System;
using System.IO;
using System.Collections.Generic;
using GameRes;

namespace GARbro.Cli
{
    public static class IdentifyCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.Input))
            {
                return ResultBuilder.Error("identify", options.Input, ErrorCodes.INVALID_ARGUMENT, "--input is required");
            }

            if (!File.Exists(options.Input) && !Directory.Exists(options.Input))
            {
                return ResultBuilder.Error("identify", options.Input, ErrorCodes.INPUT_NOT_FOUND, "Input path not found");
            }

            var result = ResultBuilder.Success("identify", options.Input);
            
            ArcFile arc = null;
            try
            {
                arc = ArcFile.TryOpen(options.Input);
            }
            catch (Exception ex)
            {
                FormatCatalog.Instance.LastError = ex;
            }

            if (arc != null)
            {
                result["kind"] = "archive";
                result["format"] = new Dictionary<string, string> { 
                    { "tag", arc.Tag }, 
                    { "description", arc.Description } 
                };

                // Try to deduce engine from description or tag
                string engine = arc.Tag;
                if (!string.IsNullOrEmpty(arc.Description))
                {
                    string[] parts = arc.Description.Split(' ');
                    if (parts.Length > 0 && parts[0] != "resource")
                        engine = parts[0];
                }

                result["engine"] = engine;
                result["is_supported"] = true;
                result["requires_additional_context"] = false;
                result["notes"] = new List<string>();
                
                arc.Dispose();
            }
            else
            {
                var err = FormatCatalog.Instance.LastError;
                if (err != null && (err.Message.Contains("passphrase") || err.Message.Contains("key") || err.Message.Contains("scheme") || err.GetType().Name.Contains("Encryption")))
                {
                    result["kind"] = "archive";
                    result["is_supported"] = true;
                    result["requires_additional_context"] = true;
                    result["notes"] = new List<string> { "archive may require game title or decryption parameters" };
                    return ResultBuilder.Error("identify", options.Input, ErrorCodes.REQUIRES_ADDITIONAL_CONTEXT, err.Message);
                }

                // fallback check if it's an image or other resource type
                try
                {
                    var entry = VFS.FindFile(options.Input);
                    if (entry != null && entry.Type != "archive")
                    {
                        result["kind"] = entry.Type;
                        result["is_supported"] = true;
                        result["requires_additional_context"] = false;
                        return result;
                    }
                }
                catch { }

                return ResultBuilder.Error("identify", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "Unsupported format: " + (err?.Message ?? "Unknown error"));
            }

            return result;
        }
    }
}
