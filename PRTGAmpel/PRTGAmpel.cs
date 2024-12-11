using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Management;

namespace PRTGAmpel
{
    public partial class AmpelService : ServiceBase
    {
        // Speichert vorherige Statusinformationen der Sensoren
        private static Dictionary<string, string> previousSensorStatuses = new Dictionary<string, string>();

        // Speichert die zuletzt eingelesenen Sensordaten für jeden Server
        private static Dictionary<string, string> previousSensorData = new Dictionary<string, string>();

        // Liste der Servernamen, die in der Konfiguration geladen werden
        private static List<string> serverNames = new List<string>();

        private Timer timer;  // Timer für die regelmäßige Serverüberwachung
        private Thread usbWatcherThread;  // Thread, der den USB-Watcher ausführt
        private static uint aktuellerbinar = 0b_0000_1111_1111_1111;  // Binärer Zustand der Ampel
        private bool cancelUsbWatcher = false;  // Flag, um den USB-Watcher sauber zu stoppen

        public class Config
        {
            // Liste der IP-Adressen der Server, die in der Konfiguration angegeben sind
            public List<string> ServerIPs { get; set; }
        }

        public AmpelService()
        {
            InitializeComponent();  // Initialisiert den Dienst
            this.ServiceName = "AmpelService";  // Setzt den Dienstnamen
        }

        protected override void OnStart(string[] args)
        {
            // Löscht bestehende Logdateien beim Start des Dienstes
            ClearLogFiles();

            Log("Service gestartet - Initialisierung beginnt.");

            // Befehl zum Starten der Ampel mit Animationen
            AmpelSteuerung("Animation gruen");

            Thread.Sleep(150);

            AmpelSteuerung("Animation gelb");

            Thread.Sleep(150);

            AmpelSteuerung("Animation rot");

            Thread.Sleep(150);


            // Konfiguration der Server laden
            var config = ServerNamen();
            if (config == null || config.ServerIPs == null || config.ServerIPs.Count == 0)
            {
                Log("Fehler: Keine Server in der Konfiguration gefunden.");
                return;
            }

            serverNames = config.ServerIPs;  // Setze die geladenen Servernamen
            Log($"Geladene Server: {string.Join(", ", serverNames)}");

            Log("Starte regelmäßige Überwachung der Server.");

            // Timer für regelmäßige Serverüberwachung starten
            timer = new Timer(ÜberwachungServer, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            // USB-Watcher im Hintergrund starten
            usbWatcherThread = new Thread(StartUsbWatcher);
            usbWatcherThread.Start();

            Log("Service gestartet - USB-Watcher läuft im Hintergrund.");
        }

        private void StartUsbWatcher()
        {
            try
            {
                // Erstelle einen ManagementEventWatcher, der auf USB-Ereignisse wartet
                ManagementEventWatcher watcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));

                // Ereignisbehandlung definieren
                watcher.EventArrived += new EventArrivedEventHandler(UsbEventArrived);
                watcher.Start();  // Starte das Überwachen der Ereignisse

                Log("USB-Watcher gestartet. Wartet auf USB-Geräteereignisse...");

                // Wartet, bis der Thread gestoppt wird
                while (!cancelUsbWatcher)  // Kontrollflag, um den Thread bei Bedarf zu stoppen
                {
                    Thread.Sleep(1000);  // Sleep für 1 Sekunde, damit der Thread aktiv bleibt
                }

                watcher.Stop();  // Stoppe den Watcher, wenn das Flag gesetzt ist
                Log("USB-Watcher gestoppt.");
            }
            catch (Exception ex)
            {
                Log($"Fehler im USB-Watcher: {ex.Message}");
            }
        }

        private void UsbEventArrived(object sender, EventArrivedEventArgs e)
        {
            // Diese Methode wird aufgerufen, wenn ein USB-Gerät angeschlossen wird
            Log("USB-Gerät angeschlossen. Aktueller binärer Zustand wird ausgeführt.");

            string binaerstring = Convert.ToString(aktuellerbinar, toBase: 2);

            uint dezimalBefehl = BinaerInDezimal(binaerstring);  // Konvertiere den binären Zustand in Dezimal

            // Führe den entsprechenden Befehl aus, um die Ampel zu steuern
            AusfuehrungCMD($"USBswitchCMD -b {dezimalBefehl}");
        }

        private void ÜberwachungServer(object state)
        {
            // Überwacht alle Server, die in der Konfiguration geladen wurden
            Log("Überwachung gestartet.");
            foreach (var server in serverNames)
            {
                Log($"Überwachung für Server: {server} wird gestartet.");
                ServerStausErmittlung(server);  // Status des Servers ermitteln
            }
        }

        protected override void OnStop()
        {
            // Wird aufgerufen, wenn der Dienst gestoppt wird
            Log("Service wird gestoppt.");
            Log("Ampelbefehl 'Ausschalten' wird gesendet.");

            // Befehl zum Stoppen der Ampel mit Animationen
            AmpelSteuerung("Animation rot");

            Thread.Sleep(150);

            AmpelSteuerung("Animation gelb");

            Thread.Sleep(150);

            AmpelSteuerung("Animation gruen");

            Thread.Sleep(150);

            AmpelSteuerung("Ausschalten abschließen");

            // Stoppe den USB-Watcher, indem das Flag gesetzt wird
            cancelUsbWatcher = true;  // Setzt das Flag, um den Watcher-Thread zu beenden
            usbWatcherThread?.Join();  // Warten bis der Watcher-Thread beendet ist

            // Timer stoppen
            timer?.Dispose();

            Log("Service erfolgreich gestoppt.");
        }

        // Methode zum Löschen der Logdateien
        static void ClearLogFiles()
        {
            string logDirectory = @"C:\ProgramData\PRTGAmpel\Logs";
            try
            {
                if (Directory.Exists(logDirectory))
                {
                    // Lösche alle Logdateien im Verzeichnis
                    var logFiles = Directory.GetFiles(logDirectory, "*.txt");
                    foreach (var file in logFiles)
                    {
                        File.Delete(file);  // Lösche die Logdateien
                        Log($"Logdatei gelöscht: {file}");
                    }
                }
                else
                {
                    Log("Log-Verzeichnis existiert nicht.");
                }
            }
            catch (Exception ex)
            {
                Log($"Fehler beim Löschen der Logdateien: {ex.Message}");
            }
        }

        static uint BinaerInDezimal(string binaerstringrechnung)
        {
            // Konvertiert einen Binärstring in eine Dezimalzahl
            Log($"Konvertierung von Binär zu Dezimal: {binaerstringrechnung}");
            uint dezimalBefehlrechnung = 0;
            uint stellenwertrechnung = 1;
            for (int i = binaerstringrechnung.Length - 1; i >= 0; i--)
            {
                uint ziffer = (uint)(binaerstringrechnung[i] - '0');
                dezimalBefehlrechnung += ziffer * stellenwertrechnung;
                stellenwertrechnung *= 2;
            }
            Log($"Konvertiertes Dezimal: {dezimalBefehlrechnung}");
            return dezimalBefehlrechnung;
        }

        static void AusfuehrungCMD(string cmd)
        {
            // Führt einen Befehl in der Kommandozeile aus
            Log($"Befehl ausführen: {cmd}");
            string programPath = @"C:\ProgramData\PRTGAmpel\Program";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {cmd}",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = programPath
                    }
                };
                process.Start();  // Startet den Prozess
                process.WaitForExit();  // Wartet auf das Ende des Prozesses
                Log("Befehl erfolgreich ausgeführt.");
            }
            catch (Exception ex)
            {
                Log($"Fehler beim Ausführen des Befehls: {ex.Message}");
            }
        }

        static Config ServerNamen()
        {
            // Lädt die Servernamen aus der Konfiguration
            string configPath = @"C:\ProgramData\PRTGAmpel\Appsettings\config.json";
            Log("Lade Konfiguration.");
            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                Log("Konfiguration erfolgreich geladen.");
                return config;
            }
            catch (Exception ex)
            {
                Log($"Fehler beim Lesen der Konfiguration: {ex.Message}");
                return null;
            }
        }

        static void ServerStausErmittlung(string ServerNameErmittlung)
        {
            // Ermittelt den Status eines Servers
            Log($"Beginne Statusermittlung für Server: {ServerNameErmittlung}");
            string sensorDataPath = @"C:\ProgramData\PRTGSensorStatus\SensorUpdates";
            string ServerDataname = $"SensorData_{ServerNameErmittlung}_*.json";
            var ServerSensorDaten = Directory.GetFiles(sensorDataPath, ServerDataname);

            if (ServerSensorDaten.Length > 0)
            {
                string latestSensorDataFile = GetLatestSensorDataFile(ServerSensorDaten);
                string sensorData = File.ReadAllText(latestSensorDataFile);
                Log($"Neueste Datei für {ServerNameErmittlung}: {latestSensorDataFile}");
                ProcessSensorData(sensorData, ServerNameErmittlung);
            }
            else
            {
                Log($"Keine Sensordaten für Server {ServerNameErmittlung} gefunden.");
            }
        }

        static string GetLatestSensorDataFile(string[] sensorDataFiles)
        {
            // Gibt die neueste Sensordatei zurück
            Log("Suche neueste Sensordatei.");
            return sensorDataFiles
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();
        }

        static void ProcessSensorData(string sensorData, string serverAddress)
        {
            // Verarbeitet die Sensordaten für einen bestimmten Server
            Log($"Verarbeite Sensordaten für {serverAddress}.");
            string serverName = serverAddress;
            string serverStatus;

            if (!previousSensorData.ContainsKey(serverAddress))
            {
                Log("Neue Sensordaten erkannt.");
                previousSensorData[serverAddress] = sensorData;
                serverStatus = DetermineStatus(sensorData);
                AmpelSteuerung($"{serverStatus}An{serverName}");
                previousSensorStatuses[serverAddress] = serverStatus;
                return;
            }

            if (previousSensorData[serverAddress] != sensorData)
            {
                Log("Änderung in den Sensordaten erkannt.");
                serverStatus = DetermineStatus(sensorData);
                previousSensorData[serverAddress] = sensorData;
                previousSensorStatuses[serverAddress] = serverStatus;
                AmpelSteuerung($"{serverStatus}An{serverName}");
            }
        }

        static string DetermineStatus(string sensorData)
        {
            // Bestimmt den Status der Sensordaten (OK, Warnung, Fehler)
            Log("Bestimme Status der Sensordaten.");
            try
            {
                JObject jsonData = JObject.Parse(sensorData);

                bool hasWarning = false;
                bool hasError = false;

                foreach (var sensor in jsonData["sensors"])
                {
                    string status = sensor["status"]?.ToString();

                    if (string.IsNullOrEmpty(status))
                    {
                        continue;
                    }

                    if (status == "Fehler")
                    {
                        hasError = true;
                    }
                    else if (status == "Warnung")
                    {
                        hasWarning = true;
                    }
                }

                if (hasError)
                {
                    Log("Status: Fehler");
                    return "Rot";
                }
                else if (hasWarning)
                {
                    Log("Status: Warnung");
                    return "Gelb";
                }
                else
                {
                    Log("Status: OK");
                    return "Gruen";
                }
            }
            catch (Exception ex)
            {
                Log($"Fehler beim Verarbeiten der Sensordaten: {ex.Message}");
                return "Unbekannt";
            }
        }

        static void AmpelSteuerung(string Befehl)
        {
            // Führt den Ampelbefehl aus (setzt den Zustand der Ampel entsprechend der Anweisung)
            Log($"Ampelbefehl empfangen: {Befehl}");
            Log($"Alter Zustand (Binär): {Convert.ToString(aktuellerbinar, 2).PadLeft(16, '0')}");

            string seiteO = "O";
            string seiteN = "N";
            string seiteW = "W";
            string seiteS = "S";
            int serveranzahl = serverNames.Count;

            // Logik zur Zuweisung von Seiten anhand der Serveranzahl
            if (serveranzahl == 1)
            {
                seiteO = serverNames[0];
                seiteN = serverNames[0];
                seiteW = serverNames[0];
                seiteS = serverNames[0];
            }
            if (serveranzahl == 2)
            {
                seiteO = serverNames[0];
                seiteN = serverNames[1];
                seiteW = serverNames[0];
                seiteS = serverNames[1];
            }
            if (serveranzahl == 3)
            {
                seiteO = serverNames[0];
                seiteN = serverNames[1];
                seiteW = serverNames[2];
                seiteS = "KeinServer";
            }
            if (serveranzahl == 4)
            {
                seiteO = serverNames[0];
                seiteN = serverNames[1];
                seiteW = serverNames[2];
                seiteS = serverNames[3];
            }

            Log($"Seiten zugewiesen: O = {seiteO}, N = {seiteN}, W = {seiteW}, S = {seiteS}");

            uint dezimalBefehl = 0;
            uint resetbinar = 0b_0;
            uint AmpelOGruen = 0b_0000_0001_0000_0000;
            uint AmpelNGruen = 0b_0000_0010_0000_0000;
            uint AmpelWGruen = 0b_0000_0100_0000_0000;
            uint AmpelSGruen = 0b_0000_1000_0000_0000;
            uint AmpelOGelb = 0b_0000_0000_0001_0000;
            uint AmpelNGelb = 0b_0000_0000_0010_0000;
            uint AmpelWGelb = 0b_0000_0000_0100_0000;
            uint AmpelSGelb = 0b_0000_0000_1000_0000;
            uint AmpelORot = 0b_0000_0000_0000_0001;
            uint AmpelNRot = 0b_0000_0000_0000_0010;
            uint AmpelWRot = 0b_0000_0000_0000_0100;
            uint AmpelSRot = 0b_0000_0000_0000_1000;
            uint Blinken = 0b_0100_0000_0000_0000;

            string binaerstring;

            // Beispiel für das Setzen und Löschen von Bits für die Ampelsteuerung
            if (Befehl == $"RotAn{seiteO}")
            {
                // Lösche das Grün und Gelb für AmpelO und setze Rot
                aktuellerbinar = (aktuellerbinar & ~AmpelOGruen & ~AmpelOGelb) | AmpelORot;
                Log("seiteO Rot an ausgeführt");
            }

            if (Befehl == $"RotAn{seiteN}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelNGruen & ~AmpelNGelb) | AmpelNRot;
                Log("seiteN Rot an ausgeführt");
            }

            if (Befehl == $"RotAn{seiteW}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelWGruen & ~AmpelWGelb) | AmpelWRot;
                Log("seiteW Rot an ausgeführt");
            }

            if (Befehl == $"RotAn{seiteS}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelSGruen & ~AmpelSGelb) | AmpelSRot;
                Log("seiteS Rot an ausgeführt");
            }

            if (Befehl == $"GelbAn{seiteO}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelORot & ~AmpelOGruen) | AmpelOGelb;
                Log("seiteO Gelb an ausgeführt");
            }

            if (Befehl == $"GelbAn{seiteN}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelNRot & ~AmpelNGruen) | AmpelNGelb;
                Log("seiteN Gelb an ausgeführt");
            }

            if (Befehl == $"GelbAn{seiteW}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelWRot & ~AmpelWGruen) | AmpelWGelb;
                Log("seiteW Gelb an ausgeführt");
            }

            if (Befehl == $"GelbAn{seiteS}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelSRot & ~AmpelSGruen) | AmpelSGelb;
                Log("seiteS Gelb an ausgeführt");
            }

            if (Befehl == $"GruenAn{seiteO}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelOGelb & ~AmpelORot) | AmpelOGruen;
                Log("seiteO Gruen an ausgeführt");
            }

            if (Befehl == $"GruenAn{seiteN}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelNGelb & ~AmpelNRot) | AmpelNGruen;
                Log("seiteN Gruen an ausgeführt");
            }

            if (Befehl == $"GruenAn{seiteW}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelWGelb & ~AmpelWRot) | AmpelWGruen;
                Log("seiteW Gruen an ausgeführt");
            }

            if (Befehl == $"GruenAn{seiteS}")
            {
                aktuellerbinar = (aktuellerbinar & ~AmpelSGelb & ~AmpelSRot) | AmpelSGruen;
                Log("seiteS Gruen an ausgeführt");
            }

            if (seiteS == "KeinServer")
            {
                // Wenn "KeinServer" für S, dann lösche alle zugehörigen Bits
                aktuellerbinar = (aktuellerbinar & ~AmpelSGruen & ~AmpelSGelb & ~AmpelSRot);
                Log("seiteS Ausgeschaltet");
            }

            if (Befehl == "Blinken an")
            {
                aktuellerbinar |= Blinken;
                Log("Befehl Blinken an ausgeführt");
            }

            if (Befehl == "Blinken aus")
            {
                aktuellerbinar &= ~Blinken;
                Log("Befehl Blinken aus ausgeführt");
            }

            if (Befehl == "Animation gruen")
            {
                // Setze alles zurück und schalte Rot auf allen Seiten
                aktuellerbinar = resetbinar;
                aktuellerbinar |= AmpelOGruen | AmpelNGruen | AmpelSGruen | AmpelWGruen;
                Log("Befehl Ausschalten einleiten ausgeführt");
            }

            if (Befehl == "Animation gelb")
            {
                // Setze alles zurück und schalte Rot auf allen Seiten
                aktuellerbinar = resetbinar;
                aktuellerbinar |= AmpelOGelb | AmpelNGelb | AmpelSGelb | AmpelWGelb;
                Log("Befehl Ausschalten einleiten ausgeführt");
            }

            if (Befehl == "Animation rot")
            {
                // Setze alles zurück und schalte Rot auf allen Seiten
                aktuellerbinar = resetbinar;
                aktuellerbinar |= AmpelORot | AmpelNRot | AmpelSRot | AmpelWRot;
                Log("Befehl Ausschalten einleiten ausgeführt");
            }

            if (Befehl == "Ausschalten abschließen")
            {
                // Setze alles zurück und schalte Rot auf allen Seiten
                aktuellerbinar = resetbinar;
                Log("Befehl Ausschalten abschließen ausgeführt");
            }

            binaerstring = Convert.ToString(aktuellerbinar, toBase: 2);

            dezimalBefehl = BinaerInDezimal(binaerstring);

            Log($"Neuer Zustand (Binär): {Convert.ToString(aktuellerbinar, 2).PadLeft(16, '0')}");
            AusfuehrungCMD($"USBswitchCMD -b {dezimalBefehl}");
            Log($"Ampelbefehl empfangen: {Befehl}");
        }

        static void Log(string message)
        {
            // Loggt Nachrichten in eine Datei im Log-Verzeichnis
            string logDirectory = @"C:\ProgramData\PRTGAmpel\Logs";
            string logFile = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.txt");

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);  // Erstelle das Log-Verzeichnis, falls es nicht existiert
                }

                // Füge das Log ein
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(logFile, logMessage);  // Füge die Nachricht zur Logdatei hinzu
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Schreiben der Logdatei: {ex.Message}");  // Fehlerbehandlung
            }
        }
    }
}
