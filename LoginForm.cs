using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // formularz logowania - pierwsze okno ktore widzi uzytkownik
    // po poprawnym zalogowaniu ustawia LoggedInUser i zamyka sie z DialogResult.OK
    public class LoginForm : Form
    {
        // publiczne pole - po zamknieciu formularza mozna stad odczytac zalogowanego usera
        // moze byc null jesli logowanie sie nie powiodlo lub user zamknal okno
        public User? LoggedInUser { get; private set; }

        // wszystkie kontrolki jako pola klasy zeby miec do nich dostep z roznych metod
        private TableLayoutPanel _layout = null!;
        private Label _lblTitle = null!;
        private Label _lblUser = null!;
        private TextBox _txtUser = null!;
        private Label _lblPass = null!;
        private TextBox _txtPass = null!;
        private Button _btnLogin = null!;
        private Button _btnRegister = null!;
        private Label _lblStatus = null!;
        private Panel _mainPanel = null!;

        public LoginForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Kurs/NIP – Logowanie";
            MinimumSize = new Size(520, 500);
            Size = new Size(560, 520);
            StartPosition = FormStartPosition.CenterScreen;   // pojawia sie na srodku ekranu
            BackColor = Color.White;
            Font = new Font("Segoe UI", 13f);
            FormBorderStyle = FormBorderStyle.Sizable;
            KeyPreview = true;   // formularz przechwytuje klawisze przed kontrolkami

            // zewnetrzny layout zeby centralnie umiesci panel logowania
            // 3 rzedy: pusty gorny | formularz | pusty dolny
            // dzieki temu formularz jest zawsze na srodku pionowo
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.White
            };
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 10f));    // gorny odstep
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // sam formularz
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 10f));    // dolny odstep
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(outer);

            // panel zawierajacy faktyczny formularz
            _mainPanel = new Panel
            {
                Anchor = AnchorStyles.None,   // Anchor.None + AutoSize = centrowanie w komorce
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(30)
            };
            outer.Controls.Add(_mainPanel, 0, 1);

            // layout z 2 kolumnami: etykieta | pole tekstowe
            _layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                BackColor = Color.White,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));
            _mainPanel.Controls.Add(_layout);

            // tytul aplikacji - zajmuje obie kolumny
            _lblTitle = MakeLabel("Kurs / NIP", true);
            _lblTitle.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
            _lblTitle.Margin = new Padding(0, 0, 0, 30);
            _layout.SetColumnSpan(_lblTitle, 2);
            _layout.Controls.Add(_lblTitle);

            // rzad: uzytkownik
            _lblUser = MakeLabel("Użytkownik:");
            _layout.Controls.Add(_lblUser);
            _txtUser = MakeTextBox();
            _layout.Controls.Add(_txtUser);

            // rzad: haslo - PasswordChar ustawia maskowanie gwiazdkami
            _lblPass = MakeLabel("Hasło:");
            _layout.Controls.Add(_lblPass);
            _txtPass = MakeTextBox();
            _txtPass.PasswordChar = '●';   // okragle kropy zamiast standardowych *
            _layout.Controls.Add(_txtPass);

            // etykieta na komunikaty o bledach - poczatkowo pusta
            _lblStatus = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(160, 0, 0),   // ciemnoczerwony kolor bledow
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                Margin = new Padding(0, 10, 0, 10)
            };
            _layout.SetColumnSpan(_lblStatus, 2);
            _layout.Controls.Add(_lblStatus);

            // panel z przyciskami obok siebie
            var btnPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 10, 0, 0)
            };
            _layout.SetColumnSpan(btnPanel, 2);
            _layout.Controls.Add(btnPanel);

            _btnLogin = MakeButton("Zaloguj");
            _btnLogin.Click += BtnLogin_Click;
            btnPanel.Controls.Add(_btnLogin);

            // przycisk rejestracji otwiera nowe okno
            _btnRegister = MakeButton("Rejestracja", secondary: true);
            _btnRegister.Click += (s, e) =>
            {
                using var rf = new RegisterForm();
                rf.ShowDialog(this);   // ShowDialog = modalne okno (blokuje rodzica)
            };
            btnPanel.Controls.Add(_btnRegister);

            // podpowiedz z domyslnymi danymi (przydatna przy pierwszym uruchomieniu)
            var hint = new Label
            {
                Text = "Domyślne konto:  admin / admin123",
                ForeColor = Color.Gray,
                AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                Margin = new Padding(0, 18, 0, 0)
            };
            _layout.SetColumnSpan(hint, 2);
            _layout.Controls.Add(hint);

            // obsluga klawiatury - Enter przechodzi dalej lub loguje
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) BtnLogin_Click(s, e); };
            _txtUser.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) _txtPass.Focus(); };
            _txtPass.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) BtnLogin_Click(s, e); };

            AcceptButton = _btnLogin;   // Enter w formularzu = klikniecie Zaloguj
        }

        // glowna logika logowania
        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            _lblStatus.Text = "";
            var username = _txtUser.Text.Trim();
            var password = _txtPass.Text;

            // podstawowa walidacja - nie wysylamy pustych pol
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _lblStatus.Text = "Wprowadź nazwę użytkownika i hasło.";
                return;
            }

            // szukamy uzytkownika w bazie i porownujemy hash hasla
            var user = Database.GetUser(username);
            if (user == null || Database.HashPassword(password) != user.PasswordHash)
            {
                // celowo ten sam komunikat dla obu przypadkow - nie zdradzamy czy login istnieje
                _lblStatus.Text = "Nieprawidłowa nazwa użytkownika lub hasło.";
                return;
            }

            // jesli uzytkownik ma wlaczona weryfikacje dwuetapowa, obsluza ja osobna metoda
            if (user.TwoFactorEnabled)
            {
                Handle2FA(user);
                return;
            }

            // logowanie bez 2FA - ustawiamy usera i zamykamy formularz z sukcesem
            LoggedInUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }

        // obsluga logowania z kodem 2FA
        // jest async wewnatrz Task.Run zeby nie blokowac UI podczas wysylania maila
        private void Handle2FA(User user)
        {
            // generujemy kod i zapisujemy go w bazie
            var code = Database.SaveTwoFactorCode(user.Id);

            _btnLogin.Enabled = false;
            _lblStatus.ForeColor = Color.DimGray;
            _lblStatus.Text = "Wysyłanie kodu weryfikacyjnego...";

            // wysylamy maila w tle zeby UI sie nie zamrozilo
            Task.Run(async () =>
            {
                var (ok, err) = await EmailService.SendCodeAsync(user.Email, code);

                // Invoke jest wymagane bo UI mozna dotykac tylko z watku glownego
                Invoke(() =>
                {
                    _btnLogin.Enabled = true;

                    if (!ok)
                    {
                        // nie udalo sie wyslac maila - informujemy i NIE pokazujemy okna kodu
                        _lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
                        _lblStatus.Text =
                            $"Nie można wysłać kodu 2FA na adres {user.Email}.\n" +
                            $"Sprawdź konfigurację SMTP w Ustawieniach.\n" +
                            $"Szczegóły: {err}";
                        return;
                    }

                    // mail sie wyslal - informujemy i pokazujemy okno do wpisania kodu
                    _lblStatus.ForeColor = Color.DimGray;
                    _lblStatus.Text = "";

                    MessageBox.Show(
                        $"Kod weryfikacyjny został wysłany na adres:\n{user.Email}\n\nKod ważny 10 minut.",
                        "Weryfikacja dwuetapowa",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // otwieramy okno weryfikacji kodu
                    using var twoFaForm = new TwoFactorForm(user.Id, user.TwoFactorMethod);
                    if (twoFaForm.ShowDialog(this) == DialogResult.OK)
                    {
                        // kod poprawny - logujemy uzytkownika
                        LoggedInUser = user;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        // user anulował lub wpisał bledny kod
                        _lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
                        _lblStatus.Text = "Weryfikacja dwuetapowa nieudana.";
                    }
                });
            });
        }

        // metody pomocnicze do tworzenia kontrolek z gotowym stylem

        private static Label MakeLabel(string text, bool span = false) => new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 13f),
            Margin = new Padding(0, 10, 20, 10)
        };

        private static TextBox MakeTextBox() => new TextBox
        {
            Font = new Font("Segoe UI", 13f),
            Width = 290,
            Height = 36,
            Margin = new Padding(0, 8, 0, 8),
            BorderStyle = BorderStyle.FixedSingle
        };

        // secondary=true to bialy przycisk (np. Rejestracja), false to czarny (Zaloguj)
        private static Button MakeButton(string text, bool secondary = false) => new Button
        {
            Text = text,
            Width = 160,
            Height = 48,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            BackColor = secondary ? Color.White : Color.Black,
            ForeColor = secondary ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 16, 0),
            Cursor = Cursors.Hand
        };
    }
}
