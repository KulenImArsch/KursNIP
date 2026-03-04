using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // ═══════════════════════════════════════════════════════════════════
    //  REGISTER FORM
    // ═══════════════════════════════════════════════════════════════════
    public class RegisterForm : Form
    {
        public RegisterForm()
        {
            Text = "Kurs/NIP – Rejestracja nowego użytkownika";
            MinimumSize = new Size(560, 660);
            Size = new Size(580, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 13f);

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1,
                BackColor = Color.White,
                AutoScroll = true
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(outer);

            var panel = new Panel
            {
                Anchor = AnchorStyles.None,
                AutoSize = true,
                Padding = new Padding(36),
                BackColor = Color.White
            };
            outer.Controls.Add(panel, 0, 0);

            var tbl = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Color.White
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));
            panel.Controls.Add(tbl);

            // ── Tytuł ────────────────────────────────────────────────────
            var title = new Label
            {
                Text = "Nowe konto",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 24)
            };
            tbl.SetColumnSpan(title, 2);
            tbl.Controls.Add(title);

            // ── Dane konta ───────────────────────────────────────────────
            var txtUser  = AddRow(tbl, "Użytkownik:");
            var txtPass  = AddRow(tbl, "Hasło:");         txtPass.PasswordChar  = '●';
            var txtPass2 = AddRow(tbl, "Powtórz hasło:"); txtPass2.PasswordChar = '●';
            var txtEmail = AddRow(tbl, "Adres e-mail:");

            // ── 2FA toggle ───────────────────────────────────────────────
            tbl.Controls.Add(MakeLabel("Weryfikacja 2FA:"));
            var chk2fa = new CheckBox
            {
                Text = "Włącz",
                Font = new Font("Segoe UI", 13f),
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 12)
            };
            tbl.Controls.Add(chk2fa);

            tbl.Controls.Add(MakeLabel("Metoda 2FA:"));
            var cmbMethod = new ComboBox
            {
                Font = new Font("Segoe UI", 13f),
                Width = 270,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 6, 0, 6),
                Enabled = false
            };
            cmbMethod.Items.Add("E-mail");
            cmbMethod.SelectedIndex = 0;
            tbl.Controls.Add(cmbMethod);

            // ── Sekcja SMTP – każda kontrolka tworzona z jawną referencją ─
            var smtpSeparator = new Label
            {
                Text = "Konfiguracja SMTP (wymagana do 2FA)",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.Black,
                Margin = new Padding(0, 20, 0, 10),
                Visible = false
            };
            tbl.SetColumnSpan(smtpSeparator, 2);
            tbl.Controls.Add(smtpSeparator);

            var cfg = AppConfig.Load();

            // Serwer SMTP
            var lblSmtpHost = MakeLabel("Serwer SMTP:"); lblSmtpHost.Visible = false;
            var txtSmtpHost = MakeTextBox(cfg.SmtpHost);  txtSmtpHost.Visible = false;
            tbl.Controls.Add(lblSmtpHost);
            tbl.Controls.Add(txtSmtpHost);

            // Port
            var lblSmtpPort = MakeLabel("Port:"); lblSmtpPort.Visible = false;
            var txtSmtpPort = MakeTextBox(cfg.SmtpPort.ToString()); txtSmtpPort.Visible = false;
            tbl.Controls.Add(lblSmtpPort);
            tbl.Controls.Add(txtSmtpPort);

            // Login
            var lblSmtpUser = MakeLabel("Login (e-mail):"); lblSmtpUser.Visible = false;
            var txtSmtpUser = MakeTextBox(cfg.SmtpUser);    txtSmtpUser.Visible = false;
            tbl.Controls.Add(lblSmtpUser);
            tbl.Controls.Add(txtSmtpUser);

            // Hasło SMTP
            var lblSmtpPass = MakeLabel("Hasło SMTP:"); lblSmtpPass.Visible = false;
            var txtSmtpPass = MakeTextBox(cfg.SmtpPassword);
            txtSmtpPass.PasswordChar = '●';
            txtSmtpPass.Visible = false;
            tbl.Controls.Add(lblSmtpPass);
            tbl.Controls.Add(txtSmtpPass);

            // SSL
            var lblSsl = MakeLabel("Szyfrowanie:"); lblSsl.Visible = false;
            var chkSsl = new CheckBox
            {
                Text = "SSL/TLS",
                AutoSize = true,
                Font = new Font("Segoe UI", 13f),
                Margin = new Padding(0, 10, 0, 10),
                Checked = cfg.SmtpSsl,
                Visible = false
            };
            tbl.Controls.Add(lblSsl);
            tbl.Controls.Add(chkSsl);

            // Pokaż/ukryj sekcję SMTP przy zmianie checkboxa 2FA
            chk2fa.CheckedChanged += (s, e) =>
            {
                bool on = chk2fa.Checked;
                cmbMethod.Enabled     = on;
                smtpSeparator.Visible = on;
                lblSmtpHost.Visible   = on; txtSmtpHost.Visible = on;
                lblSmtpPort.Visible   = on; txtSmtpPort.Visible = on;
                lblSmtpUser.Visible   = on; txtSmtpUser.Visible = on;
                lblSmtpPass.Visible   = on; txtSmtpPass.Visible = on;
                lblSsl.Visible        = on; chkSsl.Visible      = on;
                Height = on ? 920 : 680;
            };

            // ── Status + przyciski ───────────────────────────────────────
            var lblStatus = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(160, 0, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                Margin = new Padding(0, 10, 0, 8)
            };
            tbl.SetColumnSpan(lblStatus, 2);
            tbl.Controls.Add(lblStatus);

            var btnFlow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            tbl.SetColumnSpan(btnFlow, 2);
            tbl.Controls.Add(btnFlow);

            var btnOk     = MakeButton("Utwórz konto");
            var btnCancel = MakeButton("Anuluj", secondary: true);
            btnCancel.Click += (s, e) => Close();
            btnFlow.Controls.Add(btnOk);
            btnFlow.Controls.Add(btnCancel);

            // ── Logika tworzenia konta ────────────────────────────────────
            btnOk.Click += async (s, e) =>
            {
                lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
                lblStatus.Text = "";

                var u      = txtUser.Text.Trim();
                var p      = txtPass.Text;
                var p2     = txtPass2.Text;
                var email  = txtEmail.Text.Trim();
                bool twoFA = chk2fa.Checked;

                if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
                { lblStatus.Text = "Nazwa użytkownika i hasło są wymagane."; return; }
                if (p != p2)
                { lblStatus.Text = "Hasła nie są identyczne."; return; }
                if (p.Length < 6)
                { lblStatus.Text = "Hasło musi mieć co najmniej 6 znaków."; return; }
                if (twoFA && string.IsNullOrEmpty(email))
                { lblStatus.Text = "Adres e-mail jest wymagany przy włączonym 2FA."; return; }

                if (twoFA)
                {
                    if (string.IsNullOrWhiteSpace(txtSmtpUser.Text) || string.IsNullOrWhiteSpace(txtSmtpPass.Text))
                    { lblStatus.Text = "Uzupełnij dane SMTP (login i hasło) aby móc wysyłać kody 2FA."; return; }

                    if (!int.TryParse(txtSmtpPort.Text, out var port) || port <= 0)
                        port = 587;

                    lblStatus.ForeColor = Color.DimGray;
                    lblStatus.Text = "Sprawdzanie konfiguracji SMTP...";
                    btnOk.Enabled = false;

                    EmailService.SaveSettings(
                        txtSmtpHost.Text.Trim(), port,
                        txtSmtpUser.Text.Trim(), txtSmtpPass.Text,
                        chkSsl.Checked);

                    var testCode = "TEST-" + new Random().Next(100000, 999999);
                    var (ok, err) = await EmailService.SendCodeAsync(email, testCode);

                    btnOk.Enabled = true;

                    if (!ok)
                    {
                        lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
                        lblStatus.Text = $"Błąd SMTP – nie można wysłać e-maila:\n{err}\nSprawdź dane serwera.";
                        return;
                    }

                    lblStatus.ForeColor = Color.FromArgb(0, 120, 0);
                    lblStatus.Text = "Testowy e-mail wysłany pomyślnie. Tworzenie konta...";
                }

                var created = Database.CreateUser(u, p, email, twoFA, "email");
                if (created)
                {
                    MessageBox.Show(
                        $"Konto '{u}' zostało utworzone.\nMożna się teraz zalogować.",
                        "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                else
                {
                    lblStatus.ForeColor = Color.FromArgb(160, 0, 0);
                    lblStatus.Text = "Użytkownik o tej nazwie już istnieje.";
                }
            };
        }

        // Dodaje wiersz Label + TextBox do tabeli; zwraca TextBox
        private static TextBox AddRow(TableLayoutPanel tbl, string label)
        {
            tbl.Controls.Add(MakeLabel(label));
            var tb = MakeTextBox();
            tbl.Controls.Add(tb);
            return tb;
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 10, 20, 10),
            Font = new Font("Segoe UI", 13f)
        };

        private static TextBox MakeTextBox(string value = "") => new TextBox
        {
            Font = new Font("Segoe UI", 13f),
            Width = 290,
            Text = value,
            Margin = new Padding(0, 8, 0, 8),
            BorderStyle = BorderStyle.FixedSingle
        };

        private static Button MakeButton(string text, bool secondary = false) => new Button
        {
            Text = text,
            Width = 160,
            Height = 48,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            BackColor = secondary ? Color.White : Color.Black,
            ForeColor = secondary ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 14, 0),
            Cursor = Cursors.Hand
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TWO-FACTOR VERIFICATION FORM
    // ═══════════════════════════════════════════════════════════════════
    public class TwoFactorForm : Form
    {
        private readonly int _userId;
        private readonly string _method;

        public TwoFactorForm(int userId, string method)
        {
            _userId = userId;
            _method = method;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Kurs/NIP – Weryfikacja dwuetapowa";
            Size = new Size(480, 380);
            MinimumSize = new Size(460, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 13f);

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1,
                BackColor = Color.White,
                Padding = new Padding(40)
            };
            Controls.Add(tbl);

            var inner = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, BackColor = Color.White };
            tbl.Controls.Add(inner, 0, 0);

            inner.Controls.Add(new Label
            {
                Text = "Weryfikacja dwuetapowa",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            });

            var infoText = _method == "email"
                ? "Wprowadź 6-cyfrowy kod wysłany\nna Twój adres e-mail."
                : "Wprowadź 6-cyfrowy kod.";

            inner.Controls.Add(new Label
            {
                Text = infoText,
                AutoSize = true,
                Font = new Font("Segoe UI", 13f),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 20)
            });

            var txtCode = new TextBox
            {
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                MaxLength = 6,
                Width = 200,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(0, 0, 0, 20),
                BorderStyle = BorderStyle.FixedSingle
            };
            inner.Controls.Add(txtCode);

            var lblStatus = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(160, 0, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                Margin = new Padding(0, 0, 0, 12)
            };
            inner.Controls.Add(lblStatus);

            var btnFlow = new FlowLayoutPanel { AutoSize = true };
            inner.Controls.Add(btnFlow);

            var btnVerify = new Button
            {
                Text = "Weryfikuj",
                Width = 150,
                Height = 48,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                BackColor = Color.Black,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 14, 0),
                Cursor = Cursors.Hand
            };
            btnFlow.Controls.Add(btnVerify);

            var btnCancel = new Button
            {
                Text = "Anuluj",
                Width = 130,
                Height = 48,
                Font = new Font("Segoe UI", 13f),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnFlow.Controls.Add(btnCancel);

            btnVerify.Click += (s, e) =>
            {
                var code = txtCode.Text.Trim();
                if (code.Length != 6) { lblStatus.Text = "Wprowadź 6-cyfrowy kod."; return; }
                if (Database.VerifyTwoFactorCode(_userId, code))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    lblStatus.Text = "Kod nieprawidłowy lub wygasł.";
                    txtCode.Clear();
                    txtCode.Focus();
                }
            };

            txtCode.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) btnVerify.PerformClick(); };
            AcceptButton = btnVerify;
            ActiveControl = txtCode;
        }
    }
}
