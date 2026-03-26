using System;
using System.Collections.Generic;

namespace GARbro.Cli
{
    public static class ErrorCodes
    {
        public const string INVALID_ARGUMENT = "INVALID_ARGUMENT";
        public const string INPUT_NOT_FOUND = "INPUT_NOT_FOUND";
        public const string INPUT_NOT_SUPPORTED = "INPUT_NOT_SUPPORTED";
        public const string ARCHIVE_OPEN_FAILED = "ARCHIVE_OPEN_FAILED";
        public const string ENTRY_NOT_FOUND = "ENTRY_NOT_FOUND";
        public const string EXTRACTION_FAILED = "EXTRACTION_FAILED";
        public const string CONVERSION_FAILED = "CONVERSION_FAILED";
        public const string ACCESS_DENIED = "ACCESS_DENIED";
        public const string OUTSIDE_SAFE_ROOT = "OUTSIDE_SAFE_ROOT";
        public const string TIMEOUT = "TIMEOUT";
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";
        public const string REQUIRES_ADDITIONAL_CONTEXT = "REQUIRES_ADDITIONAL_CONTEXT";
    }

    public static class ResultBuilder
    {
        public static Dictionary<string, object> Success(string command, string input)
        {
            return new Dictionary<string, object>
            {
                { "ok", true },
                { "command", command },
                { "input", input }
            };
        }

        public static Dictionary<string, object> Error(string command, string input, string code, string message, Dictionary<string, object> details = null)
        {
            var err = new Dictionary<string, object>
            {
                { "code", code },
                { "message", message }
            };
            if (details != null)
            {
                err["details"] = details;
            }

            var res = new Dictionary<string, object>
            {
                { "ok", false },
                { "command", command },
                { "error", err }
            };

            if (input != null)
            {
                res["input"] = input;
            }

            return res;
        }
    }
}
