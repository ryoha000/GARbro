using System;
using System.Collections.Generic;

namespace GARbro.Cli
{
    public class CliOptions
    {
        public string Command { get; set; }
        public string Input { get; set; }
        public bool Json { get; set; }
        public bool Pretty { get; set; }
        public string Hints { get; set; }
        public string SafeRoot { get; set; }

        public string Filter { get; set; }
        public string Type { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; } = 0;
        public string Sort { get; set; }
        public bool Recursive { get; set; }
        public bool Tree { get; set; }

        public string Entry { get; set; }
        public List<string> Entries { get; set; } = new List<string>();
        public string EntryFile { get; set; }
        public string OutDir { get; set; }
        public string TargetFormat { get; set; }
        public bool Convert { get; set; }
        public string Overwrite { get; set; } = "skip";
        public bool Flatten { get; set; }
        public bool DryRun { get; set; }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            if (args.Length == 0) return options;

            options.Command = args[0].ToLowerInvariant();

            int i = 1;
            while (i < args.Length)
            {
                var arg = args[i];
                if (arg.StartsWith("--"))
                {
                    var opt = arg.Substring(2).ToLowerInvariant();
                    switch (opt)
                    {
                        case "json": options.Json = true; break;
                        case "pretty": options.Pretty = true; break;
                        case "recursive": options.Recursive = true; break;
                        case "tree": options.Tree = true; break;
                        case "convert": options.Convert = true; break;
                        case "flatten": options.Flatten = true; break;
                        case "dry-run": options.DryRun = true; break;

                        case "input": options.Input = GetNextArg(args, ref i); break;
                        case "hints": options.Hints = GetNextArg(args, ref i); break;
                        case "filter": options.Filter = GetNextArg(args, ref i); break;
                        case "type": options.Type = GetNextArg(args, ref i); break;
                        case "sort": options.Sort = GetNextArg(args, ref i); break;
                        case "entry":
                            var entryValue = GetNextArg(args, ref i);
                            options.Entry = entryValue;
                            if (options.Command == "extract")
                                options.Entries.Add(entryValue);
                            break;
                        case "entry-file": options.EntryFile = GetNextArg(args, ref i); break;
                        case "out": options.OutDir = GetNextArg(args, ref i); break;
                        case "to": options.TargetFormat = GetNextArg(args, ref i); break;
                        case "overwrite": options.Overwrite = GetNextArg(args, ref i); break;
                        case "safe-root": options.SafeRoot = GetNextArg(args, ref i); break;

                        case "limit": 
                            if (int.TryParse(GetNextArg(args, ref i), out int limit)) options.Limit = limit;
                            break;
                        case "offset": 
                            if (int.TryParse(GetNextArg(args, ref i), out int offset)) options.Offset = offset;
                            break;
                    }
                }
                i++;
            }
            return options;
        }

        private static string GetNextArg(string[] args, ref int index)
        {
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--"))
            {
                index++;
                return args[index];
            }
            return null;
        }
    }
}
