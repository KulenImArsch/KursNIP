using System.Windows.Forms;

namespace KursNIP
{
    // glowna klasa startowa - tu program sie zaczyna
    internal static class Program
    {
       
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();                      // wlacza nowoczesny wyglad kontrolek
            Application.SetCompatibleTextRenderingDefault(false);  // lepsze renderowanie czcionek

            // inicjalizacja bazy - tworzy tabele jesli nie istnieja
            Database.Initialize();

            // wczytanie ustawien SMTP z pliku konfiguracyjnego
            EmailService.LoadSettings();

            // petla logowania - pokazuje ekran logowania, a po wylogowaniu pokazuje go znowu
            // wychodzi z petli gdy user zamknie okno logowania (lub po wylogowaniu przez Application.Restart)
            while (true)
            {
                using var loginForm = new LoginForm();

                // ShowDialog czeka az user zamknie okno
                // jesli zamknal bez logowania (X) lub LoggedInUser jest null - wychodzimy z programu
                if (loginForm.ShowDialog() != DialogResult.OK || loginForm.LoggedInUser == null)
                    break;

                // logowanie sie powiodlo - otwieramy glowne okno
                using var mainForm = new MainForm(loginForm.LoggedInUser);
                Application.Run(mainForm);   // Application.Run blokuje az mainForm sie zamknie

                // po Application.Restart() ta linia sie nie wykona bo program sie zrestartuje
                // przy normalnym zamknieciu mainForm wracamy na poczatek petli i pokazujemy logowanie
                break;
            }
        }
    }
}
