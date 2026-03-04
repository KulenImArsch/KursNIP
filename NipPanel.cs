using System.Drawing;
using System.Windows.Forms;

namespace KursNIP
{
    // panel do weryfikacji NIPow - zakladka "Weryfikacja NIP" w glownym oknie
    public class NipPanel : UserControl
    {
        private readonly User _user;

        // kontrolki formularza
        private TextBox _txtNip = null!;
        private Button _btnVerify = null!;
        private Button _btnClear = null!;
        private Panel _resultBox = null!;
        private Label _lblStatus = null!;
        private DataGridView _grid = null!;

        // etykiety w panelu wynikowym - oddzielne pola zeby latwo je aktualizowac
        private Label _lblName = null!;
        private Label _lblNipVal = null!;
        private Label _lblVatStatus = null!;
        private Label _lblRegon = null!;
        private Label _lblAddress = null!;
        private Label _lblSource = null!;

        public NipPanel(User user)
        {
            _user = user;
            BuildUI();
        }

        private void BuildUI()
        {
            BackColor = Color.White;
            Dock = DockStyle.Fill;
            Padding = new Padding(24);

            // layout: pole do wpisania | wynik | status | historia
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.White
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(outer);

            // rzad z polem do wpisania NIPu i przyciskami
            var inputFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 20),
                WrapContents = false   // nie lamie do nastepnej linii
            };
            outer.Controls.Add(inputFlow, 0, 0);

            inputFlow.Controls.Add(new Label
            {
                Text = "Numer NIP:",
                AutoSize = true,
                Font = new Font("Segoe UI", 14f),
                Padding = new Padding(0, 10, 12, 0)
            });

            // pole tekstowe na NIP - ograniczamy do cyfr i kresek
            _txtNip = new TextBox
            {
                Font = new Font("Segoe UI", 16f),
                Width = 240,
                MaxLength = 13,   // NIP ma 10 cyfr, max 13 z kreskami
                Margin = new Padding(0, 4, 14, 4),
                BorderStyle = BorderStyle.FixedSingle
            };
            // blokujemy wpisywanie znakow innych niz cyfry, kreski i backspace
            _txtNip.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '-' && e.KeyChar != (char)8)
                    e.Handled = true;   // e.Handled = true anuluje wpisanie znaku
            };
            // Enter w polu NIP = klikniecie przycisku Weryfikuj
            _txtNip.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return) _btnVerify.PerformClick(); };
            inputFlow.Controls.Add(_txtNip);

            _btnVerify = MakeButton("Weryfikuj NIP", 180);
            _btnVerify.Click += BtnVerify_Click;
            inputFlow.Controls.Add(_btnVerify);

            // przycisk czyszczacy - resetuje formularz do stanu poczatkowego
            _btnClear = MakeButton("Wyczyść", 120, secondary: true);
            _btnClear.Click += (s, e) =>
            {
                _txtNip.Clear();
                _resultBox.Visible = false;
                _lblStatus.Text = "";
                _txtNip.Focus();   // przenies fokus z powrotem do pola NIP
            };
            inputFlow.Controls.Add(_btnClear);

            // panel z wynikami weryfikacji - poczatkowo ukryty
            _resultBox = new Panel
            {
                BackColor = Color.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Height = 220,
                Margin = new Padding(0, 0, 0, 16),
                Visible = false,
                Padding = new Padding(20, 14, 20, 14),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            // przy zmianie rozmiaru okna dopasowujemy szerokosc panelu
            _resultBox.Resize += (s, e) => _resultBox.Width = outer.ClientSize.Width - 2;
            outer.Controls.Add(_resultBox, 0, 1);

            // layout w panelu wynikowym - 2 kolumny: nazwa pola | wartosc
            var resultGrid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Color.FromArgb(245, 245, 245)
            };
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resultGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _resultBox.Controls.Add(resultGrid);

            // tytul panelu wynikowego
            var titleLabel = new Label { Text = "Wynik weryfikacji NIP", Font = new Font("Segoe UI", 15f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            resultGrid.SetColumnSpan(titleLabel, 2);
            resultGrid.Controls.Add(titleLabel);

            // rzedy z danymi firmy - AddRow to metoda pomocnicza
            AddRow(resultGrid, "NIP:", ref _lblNipVal!);
            AddRow(resultGrid, "Nazwa:", ref _lblName!);
            AddRow(resultGrid, "Status VAT:", ref _lblVatStatus!);
            AddRow(resultGrid, "REGON:", ref _lblRegon!);
            AddRow(resultGrid, "Adres:", ref _lblAddress!);
            AddRow(resultGrid, "Źródło:", ref _lblSource!);

            // etykieta statusu operacji
            _lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 10)
            };
            outer.Controls.Add(_lblStatus, 0, 2);

            // tabela historii zapytan NIP
            var gridContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White
            };
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            gridContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.Controls.Add(gridContainer, 0, 3);

            gridContainer.Controls.Add(new Label
            {
                Text = "Historia zapytań NIP:",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);

            // DataGridView z historia - tylko do odczytu
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 12f),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 36 }
            };
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.Black;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;   // wylaczamy domyslny styl zeby nasze kolory dzialaly
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 60);
            gridContainer.Controls.Add(_grid, 0, 1);

            _grid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "NIP", Name = "Nip", FillWeight = 20 },
                new DataGridViewTextBoxColumn { HeaderText = "Nazwa firmy", Name = "Company", FillWeight = 35 },
                new DataGridViewTextBoxColumn { HeaderText = "Status VAT", Name = "Status", FillWeight = 20 },
                new DataGridViewTextBoxColumn { HeaderText = "Data zapytania", Name = "QDate", FillWeight = 25 }
            );

            // wypelniamy tabele historia z bazy przy starcie
            RefreshHistory();
        }

        // metoda pomocnicza - dodaje rzad etykieta + wartosc do layoutu
        private static void AddRow(TableLayoutPanel tbl, string label, ref Label valueLabel)
        {
            tbl.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("Segoe UI", 12f), ForeColor = Color.DimGray, Margin = new Padding(0, 3, 20, 3) });
            valueLabel = new Label { Text = "", AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(0, 3, 0, 3) };
            tbl.Controls.Add(valueLabel);
        }

        // handler przycisku Weryfikuj
        private async void BtnVerify_Click(object? sender, EventArgs e)
        {
            var nip = _txtNip.Text.Trim();
            if (string.IsNullOrEmpty(nip)) { _lblStatus.Text = "Wprowadź numer NIP."; return; }

            // blokujemy przycisk i chowamy stary wynik podczas weryfikacji
            _btnVerify.Enabled = false;
            _lblStatus.Text = "Weryfikacja...";
            _resultBox.Visible = false;

            try
            {
                // wywolujemy API / walidacje w tle (await)
                var result = await NipService.VerifyNipAsync(nip);
                ShowResult(result);                                                            // pokazujemy wynik
                Database.SaveNipQuery(_user.Id, result.Nip, result.CompanyName, result.Status);  // zapisujemy do historii
                RefreshHistory();                                                              // odswiez tabele
                _lblStatus.Text = result.Valid ? "Weryfikacja zakończona." : "NIP nieprawidłowy.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Błąd: {ex.Message}";
            }
            finally { _btnVerify.Enabled = true; }   // zawsze odblokowujemy przycisk
        }

        // wypelnia panel wynikowy danymi z NipResult
        private void ShowResult(NipResult r)
        {
            _lblNipVal.Text = r.Nip;
            _lblName.Text = string.IsNullOrEmpty(r.CompanyName) ? "(brak danych)" : r.CompanyName;

            // jesli NIP niepoprawny - czerwony kolor i ikonka X, jesli poprawny - czarny
            _lblVatStatus.Text = r.Valid ? r.Status : "❌  " + r.Status;
            _lblVatStatus.ForeColor = r.Valid ? Color.Black : Color.FromArgb(160, 0, 0);

            _lblRegon.Text = string.IsNullOrEmpty(r.Regon) ? "-" : r.Regon;
            _lblAddress.Text = string.IsNullOrEmpty(r.Address) ? "-" : r.Address;
            _lblSource.Text = string.IsNullOrEmpty(r.Source) ? "-" : r.Source;
            _resultBox.Visible = true;
        }

        // wczytuje i wyswietla historie zapytan NIP z bazy
        private void RefreshHistory()
        {
            var history = Database.GetNipHistory(_user.Id);
            _grid.Rows.Clear();
            foreach (var r in history)
                _grid.Rows.Add(r.Nip, r.CompanyName, r.Status, r.QueryDate);
        }

        // pomocnicza fabryka przyciskow ze spojnym stylem
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
            Cursor = Cursors.Hand
        };
    }
}
