using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // panel ustawien - zakladka "Ustawienia" w glownym oknie
    // pozwala zmienic email, haslo, 2FA i konfiguracje SMTP
    public class SettingsPanel : UserControl
    {
        private readonly User _user;

        public SettingsPanel(User user)
        {
            _user = user;
            BuildUI();
        }

        private void BuildUI()
        {
            BackColor = Color.White;
            Dock = DockStyle.Fill;
            Padding = new Padding(30);

            // scroll pozwala przewijac jesli ustawien jest za duzo
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            Controls.Add(scroll);

            // glowna tabela z 2 kolumnami: nazwa opcji | kontrolka
            var tbl = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));  // kolumna z etykietami
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340f));  // kolumna z polami
            scroll.Controls.Add(tbl);

            // sekcja: dane konta uzytkownika
            AddSection(tbl, "Konto użytkownika");

            // nazwa uzytkownika - tylko do odczytu, nie mozna zmienic
            tbl.Controls.Add(MakeLabel("Użytkownik:"));
            tbl.Controls.Add(MakeValue(_user.Username));

            // pole email - uzytkownik moze edytowac
            var txtEmail = AddEditRow(tbl, "Adres e-mail:", _user.Email);

            // checkbox do wlaczania/wylaczania 2FA
            var chk2FA = new CheckBox { Text = "Aktywna", AutoSize = true, Font = new Font("Segoe UI", 13f), Margin = new Padding(0, 10, 0, 10), Checked = _user.TwoFactorEnabled };
            tbl.Controls.Add(MakeLabel("Weryfikacja 2FA:"));
            tbl.Controls.Add(chk2FA);

            // metoda 2FA - na razie tylko email wiec dropdown z jedną opcja
            tbl.Controls.Add(MakeLabel("Metoda 2FA:"));
            var cmbMethod = new ComboBox { Font = new Font("Segoe UI", 13f), DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 8, 0, 8) };
            cmbMethod.Items.Add("E-mail");
            cmbMethod.SelectedIndex = 0;
            tbl.Controls.Add(cmbMethod);

            // sekcja: zmiana hasla
            AddSection(tbl, "Zmiana hasła");
            var txtOldPass = AddEditRow(tbl, "Stare hasło:", ""); txtOldPass.PasswordChar = '●';
            var txtNewPass = AddEditRow(tbl, "Nowe hasło:", ""); txtNewPass.PasswordChar = '●';
            var txtNewPass2 = AddEditRow(tbl, "Powtórz hasło:", ""); txtNewPass2.PasswordChar = '●';

            // sekcja: ustawienia SMTP do wysylania maili z kodami 2FA
            AddSection(tbl, "Konfiguracja poczty (SMTP)");

            // wczytujemy aktualne ustawienia z pliku konfiguracyjnego
            var cfg = AppConfig.Load();
            var txtHost = AddEditRow(tbl, "Serwer SMTP:", cfg.SmtpHost);
            var txtPort = AddEditRow(tbl, "Port:", cfg.SmtpPort.ToString());
            var txtSmtpUser = AddEditRow(tbl, "Login (e-mail):", cfg.SmtpUser);
            var txtSmtpPass = AddEditRow(tbl, "Hasło SMTP:", cfg.SmtpPassword); txtSmtpPass.PasswordChar = '●';
            var chkSsl = new CheckBox { Text = "SSL/TLS", AutoSize = true, Font = new Font("Segoe UI", 13f), Margin = new Padding(0, 10, 0, 10), Checked = cfg.SmtpSsl };
            tbl.Controls.Add(MakeLabel("Szyfrowanie:"));
            tbl.Controls.Add(chkSsl);

            // odstep przed przyciskami
            var spacer = new Label { Height = 20, AutoSize = false };
            tbl.SetColumnSpan(spacer, 2);
            tbl.Controls.Add(spacer);

            // etykieta statusu - pokazuje czy zapisano czy jest blad
            var lblStatus = new Label { Text = "", ForeColor = Color.FromArgb(0, 100, 0), AutoSize = true, Font = new Font("Segoe UI", 12f), Margin = new Padding(0, 6, 0, 10) };
            tbl.SetColumnSpan(lblStatus, 2);
            tbl.Controls.Add(lblStatus);

            // przyciski akcji
            var btnFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            tbl.SetColumnSpan(btnFlow, 2);
            tbl.Controls.Add(btnFlow);

            var btnSave = MakeButton("Zapisz ustawienia");
            btnFlow.Controls.Add(btnSave);

            var btnTestSmtp = MakeButton("Testuj SMTP", secondary: true);
            btnFlow.Controls.Add(btnTestSmtp);

            // logika zapisu ustawien
            btnSave.Click += (s, e) =>
            {
                lblStatus.Text = "";

                // sprawdzamy czy user chce zmienic haslo (jesli pola hasla sa wypelnione)
                string? newHash = null;
                if (!string.IsNullOrEmpty(txtNewPass.Text))
                {
                    // weryfikujemy stare haslo
                    if (Database.HashPassword(txtOldPass.Text) != _user.PasswordHash)
                    { lblStatus.ForeColor = Color.FromArgb(160, 0, 0); lblStatus.Text = "Stare hasło jest nieprawidłowe."; return; }

                    // sprawdzamy czy oba nowe hasla sa identyczne
                    if (txtNewPass.Text != txtNewPass2.Text)
                    { lblStatus.ForeColor = Color.FromArgb(160, 0, 0); lblStatus.Text = "Nowe hasła nie są identyczne."; return; }

                    // minimalna dlugosc hasla
                    if (txtNewPass.Text.Length < 6)
                    { lblStatus.ForeColor = Color.FromArgb(160, 0, 0); lblStatus.Text = "Hasło musi mieć co najmniej 6 znaków."; return; }

                    newHash = Database.HashPassword(txtNewPass.Text);
                    _user.PasswordHash = newHash;   // aktualizujemy tez obiekt w pamieci
                }

                // aktualizujemy dane konta w obiekcie uzytkownika
                _user.Email = txtEmail.Text.Trim();
                _user.TwoFactorEnabled = chk2FA.Checked;
                _user.TwoFactorMethod = "email";   // na razie tylko email

                // zapisujemy zmiany konta do bazy
                Database.UpdateUser(_user.Id, _user.Email, _user.TwoFactorEnabled, _user.TwoFactorMethod, newHash);

                // parsujemy port - jesli niepoprawny to ustawiamy 587 jako bezpieczny default
                if (int.TryParse(txtPort.Text, out var port) && port > 0) { }
                else port = 587;

                // zapisujemy ustawienia SMTP do pliku konfiguracyjnego
                EmailService.SaveSettings(txtHost.Text.Trim(), port, txtSmtpUser.Text.Trim(), txtSmtpPass.Text, chkSsl.Checked);

                lblStatus.ForeColor = Color.FromArgb(0, 100, 0);   // zielony = sukces
                lblStatus.Text = "Ustawienia zapisane.";
                txtOldPass.Clear(); txtNewPass.Clear(); txtNewPass2.Clear();   // czyscimy pola hasel
            };

            // przycisk do testowania SMTP - wysyla maila testowego na adres usera
            btnTestSmtp.Click += async (s, e) =>
            {
                lblStatus.ForeColor = Color.DimGray;
                lblStatus.Text = "Wysyłanie testowego e-maila...";
                btnTestSmtp.Enabled = false;

                // wysylamy maila z fikcyjnym kodem TEST-123456
                var (ok, err) = await EmailService.SendCodeAsync(_user.Email, "TEST-123456");

                // zielony jesli sukces, czerwony jesli blad
                lblStatus.ForeColor = ok ? Color.FromArgb(0, 100, 0) : Color.FromArgb(160, 0, 0);
                lblStatus.Text = ok ? $"E-mail testowy wysłany na {_user.Email}." : $"Błąd: {err}";
                btnTestSmtp.Enabled = true;
            };
        }

        // dodaje naglowek sekcji (grubsza etykieta rozdzielajaca sekcje ustawien)
        private static void AddSection(TableLayoutPanel tbl, string title)
        {
            var sep = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 24, 0, 12),
                ForeColor = Color.Black
            };
            tbl.SetColumnSpan(sep, 2);   // zajmuje cala szerokosc
            tbl.Controls.Add(sep);
        }

        // dodaje rzad z etykieta i polem edytowalnym, zwraca pole tekstowe
        private static TextBox AddEditRow(TableLayoutPanel tbl, string label, string value)
        {
            tbl.Controls.Add(MakeLabel(label));
            var tb = new TextBox { Font = new Font("Segoe UI", 13f), Width = 320, Text = value, Margin = new Padding(0, 8, 0, 8), BorderStyle = BorderStyle.FixedSingle };
            tbl.Controls.Add(tb);
            return tb;
        }

        // etykieta opisu pola
        private static Label MakeLabel(string text) => new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 13f), Margin = new Padding(0, 10, 20, 10) };

        // etykieta wartosci (pogrubiona) - uzywana dla pol tylko do odczytu jak nazwa uzytkownika
        private static Label MakeValue(string text) => new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Margin = new Padding(0, 10, 0, 10) };

        // przycisk ze spojnym stylem - czarny lub bialy w zaleznosci od secondary
        private static Button MakeButton(string text, bool secondary = false) => new Button
        {
            Text = text,
            Width = 200,
            Height = 46,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            BackColor = secondary ? Color.White : Color.Black,
            ForeColor = secondary ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 14, 0),
            Cursor = Cursors.Hand
        };
    }
}
