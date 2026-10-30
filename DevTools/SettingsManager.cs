
using DevTools.Services;
using System;
using System.IO;
using System.Text.Json;

namespace DevTools
{
    public static class SettingsManager
    {

        private static readonly string ConfigDirectory =
    AppPaths.ConfigDirectory;

        private static readonly string ConfigFilePath =
            Path.Combine(ConfigDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(ConfigFilePath);

                return JsonSerializer.Deserialize<AppSettings>(json)
                       ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(ConfigDirectory);

            string json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(ConfigFilePath, json);
        }
    }
}
