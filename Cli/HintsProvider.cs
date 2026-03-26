using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using GameRes;
using GameRes.Formats.KiriKiri;

namespace GARbro.Cli
{
    public static class HintsProvider
    {
        public static void ApplyHints(string hintsFile, string archivePath)
        {
            if (string.IsNullOrEmpty(hintsFile) || !File.Exists(hintsFile))
            {
                DisableInteractiveXp3Queries();
                return;
            }

            try
            {
                var json = File.ReadAllText(hintsFile);
                var serializer = new JavaScriptSerializer();
                var hints = serializer.Deserialize<Dictionary<string, object>>(json);

                string title = null;
                if (hints != null && hints.ContainsKey("title") && hints["title"] != null)
                    title = hints["title"].ToString();

                if (!string.IsNullOrEmpty(title))
                    FormatCatalog.Instance.RegisterGameTitle(archivePath, title);

                // CLI is non-interactive. Keep XP3 from prompting for a scheme.
                DisableInteractiveXp3Queries();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Warning: Failed to parse hints file: " + ex.Message);
                DisableInteractiveXp3Queries();
            }
        }

        private static void DisableInteractiveXp3Queries()
        {
            foreach (var format in FormatCatalog.Instance.ArcFormats)
            {
                var xp3 = format as Xp3Opener;
                if (xp3 != null)
                    xp3.ForceEncryptionQuery = false;
            }
        }
    }
}
