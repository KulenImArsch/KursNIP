using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace KursNIP
{
    // ta klasa wysyla maile przez SMTP
    // uzywam biblioteki MailKit bo wbudowany SmtpClient w .NET jest przestarzaly
    public static class EmailService
    {
        // ustawienia SMTP - sa publiczne zeby inne klasy mogly je zmienic
        public static string SmtpHost     { get; set; } = "smtp.poczta.onet.pl";   // domyslnie Onet
        public static int    SmtpPort     { get; set; } = 465;                     // port dla SSL
        public static string SmtpUser     { get; set; } = "";
        public static string SmtpPassword { get; set; } = "";
        public static bool   UsesSsl      { get; set; } = true;

        // ile sekund czekamy na odpowiedz serwera zanim sie poddamy
        private const int TimeoutSeconds = 15;

        // sprawdza czy uzytkownik w ogole wpisaл dane SMTP
        // jesli nie, to nie ma sensu probowac wysylac
        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SmtpUser) && !string.IsNullOrWhiteSpace(SmtpPassword);

        // wysyla kod weryfikacyjny na podany adres email
        // zwraca (true, "") jak sie udalo albo (false, "opis bledu") jak nie
        public static async Task<(bool Success, string Error)> SendCodeAsync(string toEmail, string code)
        {
            // jesli SMTP nie jest skonfigurowany to od razu zwracamy blad
            if (!IsConfigured)
                return (false, "SMTP nie skonfigurowany. Przejdź do Ustawień → Konfiguracja e-mail.");

            // CancellationTokenSource pozwala przerwac operacje po uplywie czasu
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            try
            {
                // budowanie wiadomosci email za pomoca MimeKit
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Kurs/NIP App", SmtpUser));   // nadawca
                message.To.Add(MailboxAddress.Parse(toEmail));                     // odbiorca
                message.Subject = "Kod weryfikacyjny – Kurs/NIP";

                // tresc maila jako zwykly tekst
                message.Body = new TextPart("plain")
                {
                    Text = $@"Twój jednorazowy kod weryfikacyjny:

  {code}

Kod jest ważny przez 10 minut.
Jeśli to nie Ty próbujesz się zalogować, zignoruj tę wiadomość."
                };

                // wybieramy tryb SSL odpowiedni do portu
                var sslMode = DetermineSecureSocketOptions();

                using var client = new MailKit.Net.Smtp.SmtpClient();
                client.Timeout = TimeoutSeconds * 1000;   // MailKit chce timeout w milisekundach

                // laczymy sie z serwerem, logujemy i wysylamy
                await client.ConnectAsync(SmtpHost, SmtpPort, sslMode, cts.Token);
                await client.AuthenticateAsync(SmtpUser, SmtpPassword, cts.Token);
                await client.SendAsync(message, cts.Token);
                await client.DisconnectAsync(true, cts.Token);   // true = rozlacz elegancko (QUIT)

                return (true, "");
            }
            catch (OperationCanceledException)
            {
                // to znaczy ze minal timeout - serwer nie odpowiedzial w 15 sekund
                return (false,
                    $"Przekroczono limit czasu ({TimeoutSeconds}s).\n\n" +
                    "Sprawdź czy:\n" +
                    "  • adres serwera i port są prawidłowe\n" +
                    "  • port nie jest blokowany przez firewall lub antywirusa\n" +
                    "  • dla portu 465 wymagane jest SSL=tak\n" +
                    "  • dla portu 587 wymagane jest SSL=tak (STARTTLS)");
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                // bledne haslo albo login - osobny catch zeby dac bardziej konkretny komunikat
                return (false,
                    $"Błąd uwierzytelnienia: {ex.Message}\n\n" +
                    "Sprawdź login (pełny adres e-mail) i hasło.");
            }
            catch (Exception ex)
            {
                // jakikolwiek inny blad - zwracamy jego tresc
                return (false, ex.Message);
            }
        }

        // dobiera tryb SSL na podstawie numeru portu
        // port 465 to stary SSL (implicit), 587 to nowszy STARTTLS, reszta wg flagi
        private static SecureSocketOptions DetermineSecureSocketOptions()
        {
            if (SmtpPort == 465)
                return SecureSocketOptions.SslOnConnect;   // Onet, Gmail (alt port)
            if (SmtpPort == 587)
                return SecureSocketOptions.StartTls;       // Gmail, Outlook, Yahoo standardowy
            return UsesSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        }

        // laduje ustawienia SMTP z pliku konfiguracyjnego do tej klasy
        // wywoluje sie raz na starcie programu
        public static void LoadSettings()
        {
            var cfg      = AppConfig.Load();
            SmtpHost     = cfg.SmtpHost;
            SmtpPort     = cfg.SmtpPort;
            SmtpUser     = cfg.SmtpUser;
            SmtpPassword = cfg.SmtpPassword;
            UsesSsl      = cfg.SmtpSsl;
        }

        // zapisuje nowe ustawienia SMTP - aktualizuje zarowno pamiec jak i plik
        public static void SaveSettings(string host, int port, string user, string pass, bool ssl)
        {
            // aktualizacja pol statycznych w pamieci (dziala od razu)
            SmtpHost     = host;
            SmtpPort     = port;
            SmtpUser     = user;
            SmtpPassword = pass;
            UsesSsl      = ssl;

            // zapis do pliku zeby ustawienia przetrwaly restart programu
            var cfg      = AppConfig.Load();
            cfg.SmtpHost     = host;
            cfg.SmtpPort     = port;
            cfg.SmtpUser     = user;
            cfg.SmtpPassword = pass;
            cfg.SmtpSsl      = ssl;
            cfg.Save();
        }
    }

    // prosta klasa do zapisu i odczytu konfiguracji z pliku tekstowego
    // format pliku: klucz=wartosc, po jednym na linii
    public class AppConfig
    {
        // plik konfiguracyjny w tym samym folderze co program
        private static string ConfigPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "kursnip.cfg");

        // wartosci domyslne na wypadek gdyby plik nie istnial
        public string SmtpHost     { get; set; } = "smtp.poczta.onet.pl";
        public int    SmtpPort     { get; set; } = 465;
        public string SmtpUser     { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public bool   SmtpSsl      { get; set; } = true;

        // wczytuje konfiguracje z pliku
        // jesli plik nie istnieje zwraca obiekt z wartosciami domyslnymi
        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            if (!File.Exists(ConfigPath)) return cfg;

            // czytamy linia po linii i parsujemy format klucz=wartosc
            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var parts = line.Split('=', 2);   // Split z limitem 2 zeby wartosc mogla zawierac '='
                if (parts.Length < 2) continue;   // pomijamy linie bez '='
                var key = parts[0].Trim();
                var val = parts[1].Trim();
                switch (key)
                {
                    case "SmtpHost":     cfg.SmtpHost     = val; break;
                    case "SmtpPort":     if (int.TryParse(val, out var p) && p > 0) cfg.SmtpPort = p; break;   // walidujemy port
                    case "SmtpUser":     cfg.SmtpUser     = val; break;
                    case "SmtpPassword": cfg.SmtpPassword = val; break;
                    case "SmtpSsl":      cfg.SmtpSsl      = val == "1"; break;   // "1" to true, wszystko inne to false
                }
            }
            return cfg;
        }

        // zapisuje konfiguracje do pliku
        // WriteAllLines nadpisuje caly plik od nowa
        public void Save()
        {
            File.WriteAllLines(ConfigPath,
            [
                $"SmtpHost={SmtpHost}",
                $"SmtpPort={SmtpPort}",
                $"SmtpUser={SmtpUser}",
                $"SmtpPassword={SmtpPassword}",
                $"SmtpSsl={(SmtpSsl ? "1" : "0")}"   // bool zapisujemy jako 0/1
            ]);
        }
    }
}
