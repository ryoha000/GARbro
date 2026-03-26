using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using GameRes;

namespace GARbro.Cli
{
    public static class HintsProvider
    {
        public static void ApplyHints(string hintsFile)
        {
            if (string.IsNullOrEmpty(hintsFile) || !File.Exists(hintsFile))
                return;

            try
            {
                var json = File.ReadAllText(hintsFile);
                var serializer = new JavaScriptSerializer();
                var hints = serializer.Deserialize<Dictionary<string, object>>(json);

                // Future implementation: inject passwords and game titles into GameRes FormatCatalog
                // or specific Format classes (e.g. KiriKiri XP3 password lists) depending on the engine.
                var passwords = new List<string>();
                if (hints.ContainsKey("passwords") && hints["passwords"] is System.Collections.ArrayList list)
                {
                    foreach (var item in list)
                    {
                        passwords.Add(item.ToString());
                    }
                }
                
                string title = hints.ContainsKey("title") ? hints["title"].ToString() : null;

                // Currently MVP: just load the file without crashing.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: Failed to parse hints file: " + ex.Message);
            }
        }
    }
}
