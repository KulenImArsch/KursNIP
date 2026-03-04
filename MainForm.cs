using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // glowne okno programu - wyswietla sie po zalogowaniu
    // zawiera naglowek z nazwa i przycisk wylogowania oraz zakladki z funkcjami
    public class MainForm : Form
    {
        // zalogowany uzytkownik - potrzebny zeby przekazac do paneli (np. historia jest per uzytkownik)
        private readonly User _user;

        private TabControl _tabControl = null!;
        private Label _lblUser = null!;
        private Button _btnLogout = null!;

        public MainForm(User user)
        {
            _user = user;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Kurs / NIP";
            MinimumSize = new Size(800, 600);
            Size = new Size(1100, 750);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 13f);

            // glowny layout: 2 rzedy - czarny pasek naglowkowy i zakladki
            var outerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White
            };
            outerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));    // naglowek - stala wysokosc
            outerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));    // zakladki - reszta miejsca
            outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(outerTable);

            // czarny pasek na gorze z nazwa aplikacji i przyciskiem wylogowania
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Height = 80
            };
            outerTable.Controls.Add(header, 0, 0);

            // nazwa aplikacji po lewej stronie naglowka
            var lblAppName = new Label
            {
                Text = "Kurs / NIP",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Left = 20,
                Top = 22
            };
            header.Controls.Add(lblAppName);

            // przycisk wylogowania w prawym gornym rogu
            _btnLogout = new Button
            {
                Text = "Wyloguj",
                Width = 120,
                Height = 40,
                Font = new Font("Segoe UI", 12f),
                BackColor = Color.FromArgb(60, 60, 60),   // ciemno szary - odroznia od czarnego tla
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Top = 20
            };
            _btnLogout.FlatAppearance.BorderColor = Color.Gray;
            _btnLogout.Left = header.Width - _btnLogout.Width - 20;   // ustawienie od prawej krawedzi
            _btnLogout.Anchor = AnchorStyles.Top | AnchorStyles.Right;   // przylega do prawej przy zmianie rozmiaru
            header.Controls.Add(_btnLogout);

            // etykieta "Zalogowany: nazwa" po lewej od przycisku wylogowania
            _lblUser = new Label
            {
                Text = $"Zalogowany:  {_user.Username}",
                ForeColor = Color.Silver,
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                Top = 28
            };
            _lblUser.Left = header.Width - _btnLogout.Width - _lblUser.PreferredWidth - 50;
            _lblUser.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(_lblUser);

            // wylogowanie restartuje caly program - najprostrze rozwiazanie
            // Application.Restart() zamknie i ponownie otworzy program od nowa
            _btnLogout.Click += (s, e) =>
            {
                if (MessageBox.Show("Czy na pewno chcesz się wylogować?", "Wylogowanie",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Application.Restart();
                }
            };

            // TabControl - kontener na zakladki
            // ItemSize i SizeMode.Fixed = wszystkie zakladki maja ta sama szerokosc
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14f),
                ItemSize = new Size(200, 48),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(10, 6),
                Margin = new Padding(0)
            };
            outerTable.Controls.Add(_tabControl, 0, 1);

            // zakladka 1: kursy walut
            var tabRate = new TabPage("  Kurs walut")
            {
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            tabRate.Controls.Add(new ExchangeRatePanel(_user));   // caly panel z logiką walut
            _tabControl.TabPages.Add(tabRate);

            // zakladka 2: weryfikacja NIP
            var tabNip = new TabPage("  Weryfikacja NIP")
            {
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            tabNip.Controls.Add(new NipPanel(_user));
            _tabControl.TabPages.Add(tabNip);

            // zakladka 3: ustawienia (konto, SMTP itd.)
            var tabSettings = new TabPage("  Ustawienia")
            {
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            tabSettings.Controls.Add(new SettingsPanel(_user));
            _tabControl.TabPages.Add(tabSettings);

            // wlaczamy wlasne rysowanie zakladek (OwnerDraw) zeby moc je ladnie pokolorowac
            _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabControl.DrawItem += TabControl_DrawItem;

            // przy zmianie zakladki odrysowujemy wszystkie zeby zaktualizowac kolory
            _tabControl.SelectedIndexChanged += (s, e) =>
            {
                _tabControl.Invalidate();
            };
        }

        // metoda rysujaca pojedyncza zakladke
        // aktywna zakladka ma czarne tlo z bialym tekstem, nieaktywna jasno szara z czarnym
        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = _tabControl.TabPages[e.Index];
            var isSelected = (e.State & DrawItemState.Selected) != 0;   // bitowa flaga czy zakladka aktywna

            // tlo zakladki
            var bgBrush = isSelected
                ? new SolidBrush(Color.Black)
                : new SolidBrush(Color.FromArgb(240, 240, 240));

            // kolor tekstu
            var fgBrush = isSelected
                ? Brushes.White
                : Brushes.Black;

            // rysujemy tlo
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // rysujemy tekst wycentrowany w zakladce
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(tab.Text, new Font("Segoe UI", 13f, isSelected ? FontStyle.Bold : FontStyle.Regular),
                fgBrush, e.Bounds, sf);

            bgBrush.Dispose();   // zwalniamy pamiec po brushu (IDisposable)
        }
    }
}
