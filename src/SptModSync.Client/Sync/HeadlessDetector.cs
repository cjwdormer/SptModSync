using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SptModSync.Client.Sync
{
    internal static class HeadlessDetector
    {
        public static bool IsHeadless(string pluginDirectory, out string reason)
        {
            try
            {
                if (Application.isBatchMode)
                {
                    reason = "Unity reports batch mode";
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var commandLine = Environment.CommandLine;
                if (commandLine.IndexOf("-batchmode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    commandLine.IndexOf("-nographics", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reason = "launched with -batchmode/-nographics";
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var pluginsRoot = Directory.GetParent(pluginDirectory)?.FullName;
                if (!string.IsNullOrEmpty(pluginsRoot) && Directory.Exists(pluginsRoot))
                {
                    var hasHeadlessPlugin = Directory
                        .EnumerateFiles(pluginsRoot, "*.dll", SearchOption.AllDirectories)
                        .Any(path => Path.GetFileName(path)
                            .IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (hasHeadlessPlugin)
                    {
                        reason = "Fika headless plugin is installed";
                        return true;
                    }
                }
            }
            catch
            {
            }

            reason = "";
            return false;
        }
    }
}
