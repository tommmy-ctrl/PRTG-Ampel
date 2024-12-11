using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Configurator
{
    class Program
    {
        // Struktur der Appsettings.json
        public class ServerConfig
        {
            public string ServerIP { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Protocol { get; set; }
            public bool UseAlternatePort { get; set; }
            public string Port { get; set; }
            public string RefreshInterval { get; set; }
        }

        public class AppSettings
        {
            public List<ServerConfig> Servers { get; set; }
        }

        // Struktur der config.json, die wir speichern möchten
        public class Config
        {
            public List<string> ServerIPs { get; set; }
        }

        static void Main(string[] args)
        {
            // Pfad zur appsettings.json und config.json
            string appSettingsPath = @"C:\ProgramData\PRTGSensorStatus\Appsettings\appsettings.json";
            string configPath = @"C:\ProgramData\PRTGAmpel\Appsettings\config.json";

            try
            {
                // Überprüfen, ob die appsettings.json existiert
                if (!File.Exists(appSettingsPath))
                {
                    Console.WriteLine("Fehler: Die Datei appsettings.json wurde nicht gefunden.");
                    return;
                }

                // Datei einlesen
                string appSettingsJson = File.ReadAllText(appSettingsPath);

                // Deserialize JSON in AppSettings-Objekt
                AppSettings appSettings = JsonConvert.DeserializeObject<AppSettings>(appSettingsJson);

                if (appSettings?.Servers == null || appSettings.Servers.Count == 0)
                {
                    Console.WriteLine("Fehler: Keine Server in der appsettings.json gefunden.");
                    return;
                }

                // Servernamen extrahieren und in eine Liste speichern
                List<string> serverIPs = new List<string>();

                foreach (var server in appSettings.Servers)
                {
                    // Nur Servernamen (ServerIP) speichern
                    serverIPs.Add(server.ServerIP);
                }

                // Die config.json Struktur erstellen
                Config config = new Config
                {
                    ServerIPs = serverIPs
                };

                // Erstelle den JSON-String aus der Config
                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);

                // Speichern der config.json Datei
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));  // Sicherstellen, dass das Verzeichnis existiert
                File.WriteAllText(configPath, configJson);

                Console.WriteLine("config.json wurde erfolgreich erstellt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler: {ex.Message}");
            }
        }
    }
}
