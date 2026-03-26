using System;
using System.Text;
using System.IO;
using GameRes;

namespace GARbro.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var options = CliOptions.Parse(args);

            if (string.IsNullOrEmpty(options.Command))
            {
                Console.Error.WriteLine("Error: Missing command.");
                PrintUsage();
                Environment.Exit(1);
            }

            try
            {
                DeserializeGameData();
                HintsProvider.ApplyHints(options.Hints, options.Input);

                object result = null;
                switch (options.Command)
                {
                    case "identify":
                        result = IdentifyCommand.Execute(options);
                        break;
                    case "list":
                        result = ListCommand.Execute(options);
                        break;
                    case "extract":
                        result = ExtractCommand.Execute(options);
                        break;
                    default:
                        Console.Error.WriteLine($"Error: Unknown command '{options.Command}'");
                        PrintUsage();
                        Environment.Exit(1);
                        break;
                }

                if (result != null)
                {
                    if (options.Json)
                    {
                        Console.WriteLine(JsonFormatter.Serialize(result, options.Pretty));
                    }
                    else
                    {
                        // Fallback simple output
                        Console.WriteLine(JsonFormatter.Serialize(result, true));
                    }
                }
            }
            catch (Exception ex)
            {
                var err = ResultBuilder.Error(options.Command, options.Input, ErrorCodes.INTERNAL_ERROR, ex.Message);
                if (options.Json)
                {
                    Console.WriteLine(JsonFormatter.Serialize(err, options.Pretty));
                }
                else
                {
                    Console.Error.WriteLine("Internal Error: " + ex.Message);
                }
            }
        }

        static void DeserializeGameData()
        {
            string scheme_file = Path.Combine(FormatCatalog.Instance.DataDirectory, "Formats.dat");
            try
            {
                using (var file = File.OpenRead(scheme_file))
                    FormatCatalog.Instance.DeserializeScheme(file);
            }
            catch
            {
                // Ignore scheme deserialization errors
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("garbro-cli <command> [options]");
            Console.WriteLine("Commands: identify, list, extract");
        }
    }
}
