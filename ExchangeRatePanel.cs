using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // panel z kursami walut - to jest zakladka "Kurs walut" w glownym oknie
    // dziedziczy po UserControl zeby mozna go bylo wsadzic do TabPage
    public class ExchangeRatePanel : UserControl
    {
        // przechowuje zalogowanego uzytkownika (potrzebny do zapisu historii w bazie)
        private readonly User _user;

        // deklaracja wszystkich kontrolek jako pola klasy
        // null! mowi kompilatorowi "wiem ze tu moze byc null, zaufaj mi"
        // inicjalizuje je dopiero w BuildUI()
        private ComboBox _cmbCurrency = null!;
        private Button _btnFetch = null!;
        private Button _btnLast10 = null!;
        private Button _btnLoadCurrencies = null!;
        private Label _lblResultTitle = null!;
        private Label _lblCurrencyName = null!;
        private Label _lblRate = null!;
        private Label _lblDate = null!;
        private DataGridView _grid = null!;
        private Label _lblStatus = null!;
        private Panel _resultBox = null!;

        public ExchangeRatePanel(User user)
        {
            _user = user;
            BuildUI();   // cały interfejs budujemy w kodzie, bez designera
        }

        // tu tworzymy caly interfejs uzytkownika
        // uzycie TableLayoutPanel pozwala na automatyczne ukladanie elementow
        private void BuildUI()
        {
            BackColor = Color.White;
            Dock = DockStyle.Fill;       // wypelnia caly dostepny obszar
            Padding = new Padding(24);   // margines od krawedzi

            // glowny layout podzielony na 4 rzedy:
            // kontrolki -> wynik -> status -> tabela historii
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.White
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // rzad z przyciskami
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // rzad z wynikiem
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // rzad ze statusem
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));     // tabela bierze reszte miejsca
            Controls.Add(outer);

            // FlowLayoutPanel automatycznie uklada elementy w rzedzie
            var ctrlPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 20),
                WrapContents = true   // jesli sie nie miesci, przejdzie do nastepnej linii
            };
            outer.Controls.Add(ctrlPanel, 0, 0);

            var lblPick = new Label
            {
                Text = "Waluta:",
                AutoSize = true,
                Font = new Font("Segoe UI", 14f),
                Padding = new Padding(0, 10, 10, 0)
            };
            ctrlPanel.Controls.Add(lblPick);

            // dropdown z walutami - na poczatku pokazuje placeholder
            _cmbCurrency = new ComboBox
            {
                Width = 320,
                Height = 44,
                Font = new Font("Segoe UI", 13f),
                DropDownStyle = ComboBoxStyle.DropDownList,   // tylko wybor z listy, bez wpisywania
                Margin = new Padding(0, 4, 14, 4)
            };
            _cmbCurrency.Items.Add("(kliknij Załaduj waluty)");
            _cmbCurrency.SelectedIndex = 0;
            ctrlPanel.Controls.Add(_cmbCurrency);

            _btnLoadCurrencies = MakeButton("Załaduj waluty", 170);
            _btnLoadCurrencies.Click += BtnLoadCurrencies_Click;
            ctrlPanel.Controls.Add(_btnLoadCurrencies);

            _btnFetch = MakeButton("Pobierz kurs", 160);
            _btnFetch.Click += BtnFetch_Click;
            ctrlPanel.Controls.Add(_btnFetch);

            // secondary=true daje bialy przycisk z czarnym tekstem (odwrotny styl)
            _btnLast10 = MakeButton("Ostatnie 10", 150, secondary: true);
            _btnLast10.Click += BtnLast10_Click;
            ctrlPanel.Controls.Add(_btnLast10);

            // panel wynikowy - pokazuje sie dopiero po pobraniu kursu
            _resultBox = new Panel
            {
                BackColor = Color.FromArgb(245, 245, 245),   // jasno szary
                BorderStyle = BorderStyle.FixedSingle,
                Height = 130,
                Margin = new Padding(0, 0, 0, 16),
                Visible = false,   // poczatkowo ukryty
                Padding = new Padding(20, 14, 20, 14),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            // dopasowujemy szerokosc panelu wynikowego przy zmianie rozmiaru okna
            _resultBox.Resize += (s, e) => _resultBox.Width = outer.ClientSize.Width - 2;
            outer.Controls.Add(_resultBox, 0, 1);

            // wewnatrz panelu wynikowego - tabela 2-kolumnowa: etykieta | wartosc
            var resultLayout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Color.FromArgb(245, 245, 245),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            _resultBox.Controls.Add(resultLayout);

            // tytul zajmuje cala szerokosc (obie kolumny)
            _lblResultTitle = new Label { Text = "", Font = new Font("Segoe UI", 15f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            resultLayout.SetColumnSpan(_lblResultTitle, 2);
            resultLayout.Controls.Add(_lblResultTitle);

            // dodajemy rzedy: nazwa, kurs, data - metoda pomocnicza zeby nie powtarzac kodu
            AddResultRow(resultLayout, "Nazwa:", ref _lblCurrencyName!);
            AddResultRow(resultLayout, "Kurs (PLN):", ref _lblRate!);
            AddResultRow(resultLayout, "Data tabeli:", ref _lblDate!);

            // etykieta statusu - wyswietla komunikaty np. "Pobieranie..." lub bledy
            _lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 10)
            };
            outer.Controls.Add(_lblStatus, 0, 2);

            // kontener dla tabeli historii - naglowek + grid
            var gridContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White
            };
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));    // naglowek
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));    // grid
            gridContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.Controls.Add(gridContainer, 0, 3);

            gridContainer.Controls.Add(new Label
            {
                Text = "Historia zapytań:",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);

            // tabela z historia kursow
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,       // ukrycie numerow wierszy po lewej
                AllowUserToAddRows = false,      // uzytkownik nie moze dodawac wierszy
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,  // zaznaczanie calego wiersza
                Font = new Font("Segoe UI", 12f),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,  // kolumny wypelniaja calą szerokosc
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 36 }
            };
            // styl naglowkow - czarne tlo, bialy tekst
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.Black;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;   // to musi byc false zeby nasze kolory dzialaly
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);   // ciemno szary przy zaznaczeniu
            gridContainer.Controls.Add(_grid, 0, 1);

            // definiowanie kolumn grida
            _grid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Waluta", Name = "Currency", FillWeight = 15 },
                new DataGridViewTextBoxColumn { HeaderText = "Kurs (PLN)", Name = "Rate", FillWeight = 20 },
                new DataGridViewTextBoxColumn { HeaderText = "Data tabeli NBP", Name = "EffDate", FillWeight = 25 },
                new DataGridViewTextBoxColumn { HeaderText = "Data zapytania", Name = "QDate", FillWeight = 40 }
            );

            // wczytaj historie z bazy przy starcie
            RefreshHistory();
        }

        // metoda pomocnicza - dodaje jeden rzad do panelu wynikowego (etykieta + wartosc)
        // ref pozwala nam przekazac pole i przypisac do niego wartosc
        private static void AddResultRow(TableLayoutPanel tbl, string label, ref Label valueLabel)
        {
            tbl.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("Segoe UI", 13f), Margin = new Padding(0, 2, 20, 2) });
            valueLabel = new Label { Text = "", AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Margin = new Padding(0, 2, 0, 2) };
            tbl.Controls.Add(valueLabel);
        }

        // handler przycisku "Zaladuj waluty"
        // async void jest ok dla handlerow eventow (nie mozna uzywac async Task tutaj)
        private async void BtnLoadCurrencies_Click(object? sender, EventArgs e)
        {
            _lblStatus.Text = "Ładowanie listy walut...";
            _btnLoadCurrencies.Enabled = false;   // blokujemy przycisk zeby nie klikac dwa razy
            try
            {
                var list = await NbpService.GetAvailableCurrenciesAsync();
                _cmbCurrency.Items.Clear();
                foreach (var c in list)
                    _cmbCurrency.Items.Add(c);   // CurrencyInfo.ToString() decyduje co widac w dropdownie
                if (_cmbCurrency.Items.Count > 0) _cmbCurrency.SelectedIndex = 0;
                _lblStatus.Text = $"Załadowano {list.Count} walut z NBP.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Błąd: {ex.Message}";
            }
            finally { _btnLoadCurrencies.Enabled = true; }   // zawsze odblokowujemy przycisk
        }

        // handler przycisku "Pobierz kurs" - pobiera aktualny kurs wybranej waluty
        private async void BtnFetch_Click(object? sender, EventArgs e)
        {
            // sprawdzamy czy user wybral walute z listy (nie placeholder)
            if (_cmbCurrency.SelectedItem is not CurrencyInfo ci) { _lblStatus.Text = "Wybierz walutę z listy."; return; }
            _lblStatus.Text = "Pobieranie kursu...";
            _btnFetch.Enabled = false;
            try
            {
                var result = await NbpService.GetRateAsync(ci.Code);
                ShowResult(result);                                                         // pokazujemy wynik
                Database.SaveExchangeRate(_user.Id, result.CurrencyCode, result.Rate, result.EffectiveDate);   // zapisujemy do historii
                RefreshHistory();                                                           // odswiez tabele historii
                _lblStatus.Text = "Kurs pobrany pomyślnie.";
            }
            catch (Exception ex)
            {
                _resultBox.Visible = false;
                _lblStatus.Text = $"Błąd: {ex.Message}";
            }
            finally { _btnFetch.Enabled = true; }
        }

        // handler przycisku "Ostatnie 10" - pobiera ostatnie 10 notowan i wyswietla w tabeli
        private async void BtnLast10_Click(object? sender, EventArgs e)
        {
            if (_cmbCurrency.SelectedItem is not CurrencyInfo ci) { _lblStatus.Text = "Wybierz walutę z listy."; return; }
            _lblStatus.Text = "Pobieranie ostatnich 10 notowań...";
            _btnLast10.Enabled = false;
            try
            {
                var results = await NbpService.GetLastRatesAsync(ci.Code, 10);
                _grid.Rows.Clear();
                // wstawiamy bezposrednio do grida, nie do historii bazy
                foreach (var r in results)
                    _grid.Rows.Add(r.CurrencyCode, r.Rate.ToString("F4"), r.EffectiveDate, "(z NBP)");
                _resultBox.Visible = false;   // chowamy panel wynikowy bo teraz dane sa w tabeli
                _lblStatus.Text = $"Ostatnie 10 notowań dla {ci.Code}.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Błąd: {ex.Message}";
            }
            finally { _btnLast10.Enabled = true; }
        }

        // wypelnia panel wynikowy danymi po pobraniu kursu
        private void ShowResult(ExchangeRateResult r)
        {
            _lblResultTitle.Text = $"{r.CurrencyCode}  →  {r.Rate:F4} PLN";   // F4 = 4 miejsca po przecinku
            _lblCurrencyName.Text = r.CurrencyName;
            _lblRate.Text = $"{r.Rate:F4} PLN";
            _lblDate.Text = r.EffectiveDate;
            _resultBox.Visible = true;   // pokazujemy panel
        }

        // odswiezenie tabeli historii z bazy danych
        private void RefreshHistory()
        {
            var history = Database.GetExchangeHistory(_user.Id);
            _grid.Rows.Clear();
            foreach (var r in history)
                _grid.Rows.Add(r.Currency, r.Rate.ToString("F4"), r.EffectiveDate, r.QueryDate);
        }

        // metoda pomocnicza do tworzenia przycisku z gotowym stylem
        // secondary=true = bialy przycisk, secondary=false = czarny przycisk
        private static Button MakeButton(string text, int width = 150, bool secondary = false) => new Button
        {
            Text = text,
            Width = width,
            Height = 44,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            BackColor = secondary ? Color.White : Color.Black,
            ForeColor = secondary ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 4, 10, 4),
            Cursor = Cursors.Hand   // kursor "reka" jak na stronie internetowej
        };
    }
}
