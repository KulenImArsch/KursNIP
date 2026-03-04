using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KursNIP
{
    // ta klasa odpowiada za pobieranie kursow walut z NBP (Narodowy Bank Polski)
    // uzywam HttpClient zeby wysylac zapytania do ich darmowego API
    public static class NbpService
    {
        // jeden wspolny klient HTTP dla calej klasy, nie tworze nowego za kazdym razem
        // ustawiam timeout na 15 sekund bo jak dluzej nie odpowiada to cos nie tak
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ta metoda zwraca liste wszystkich walut dostepnych w NBP (tabela A)
        // wynik to lista obiektow CurrencyInfo z kodem i nazwa waluty
        public static async Task<List<CurrencyInfo>> GetAvailableCurrenciesAsync()
        {
            // adres API NBP do pobrania calej tabeli A
            var url = "https://api.nbp.pl/api/exchangerates/tables/A/?format=json";

            // pobieramy odpowiedz jako tekst i parsujemy JSON
            var resp = await _http.GetStringAsync(url);
            var doc = JsonNode.Parse(resp);

            // w JSONie pod kluczem "rates" jest tablica z walutami
            // [0] bo odpowiedz to tablica i bierzemy pierwszy element
            var rates = doc![0]!["rates"]!.AsArray();

            var list = new List<CurrencyInfo>();

            // przechodzimy przez kazda walute i dodajemy do listy
            foreach (var r in rates)
            {
                list.Add(new CurrencyInfo
                {
                    Code = r!["code"]!.GetValue<string>(),       // np. "USD"
                    Name = r!["currency"]!.GetValue<string>()    // np. "dolar amerykanski"
                });
            }
            return list;
        }

        // pobiera aktualny kurs dla jednej konkretnej waluty (np. "USD")
        // zwraca obiekt z kursem, nazwa i data obowiazywania
        public static async Task<ExchangeRateResult> GetRateAsync(string currencyCode)
        {
            // buduje URL z kodem waluty, np. .../rates/A/USD/?format=json
            var url = $"https://api.nbp.pl/api/exchangerates/rates/A/{currencyCode}/?format=json";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonNode.Parse(resp);

            // kurs jest w tablicy "rates", bierzemy pierwszy (i jedyny) element
            var rate = doc!["rates"]![0]!;

            return new ExchangeRateResult
            {
                CurrencyCode = currencyCode.ToUpper(),
                CurrencyName = doc["currency"]!.GetValue<string>(),
                Rate = rate["mid"]!.GetValue<double>(),              // "mid" to kurs sredni NBP
                EffectiveDate = rate["effectiveDate"]!.GetValue<string>()
            };
        }

        // pobiera ostatnie N kursow dla danej waluty (domyslnie 10)
        // przydatne zeby zobaczyc jak kurs sie zmienial w czasie
        public static async Task<List<ExchangeRateResult>> GetLastRatesAsync(string currencyCode, int count = 10)
        {
            // /last/10/ na koncu URLa mowi API ile rekordow chcemy
            var url = $"https://api.nbp.pl/api/exchangerates/rates/A/{currencyCode}/last/{count}/?format=json";
            var resp = await _http.GetStringAsync(url);
            var doc = JsonNode.Parse(resp);

            // nazwa waluty jest raz na gorze, nie w kazdym kursie osobno
            var name = doc!["currency"]!.GetValue<string>();

            var list = new List<ExchangeRateResult>();

            // iterujemy przez wszystkie kursy i dodajemy do listy
            foreach (var r in doc["rates"]!.AsArray())
            {
                list.Add(new ExchangeRateResult
                {
                    CurrencyCode = currencyCode.ToUpper(),
                    CurrencyName = name,
                    Rate = r!["mid"]!.GetValue<double>(),
                    EffectiveDate = r["effectiveDate"]!.GetValue<string>()
                });
            }
            return list;
        }
    }

    // prosty model danych przechowujacy informacje o jednej walucie
    public class CurrencyInfo
    {
        public string Code { get; set; } = "";   // kod ISO np. "USD", "EUR"
        public string Name { get; set; } = "";   // pelna nazwa waluty po polsku

        // nadpisuje ToString() zeby w ComboBoxie ladnie wygladalo
        public override string ToString() => $"{Code}  –  {Name}";
    }

    // model wynikowy dla jednego kursu waluty
    public class ExchangeRateResult
    {
        public string CurrencyCode { get; set; } = "";
        public string CurrencyName { get; set; } = "";
        public double Rate { get; set; }           // kurs w zlotowkach
        public string EffectiveDate { get; set; } = "";  // data od kiedy kurs obowiazuje
    }

    // ta klasa sluzy do weryfikacji numerow NIP przez internet
    // uzywa darmowego API Ministerstwa Finansow (Biala Lista VAT)
    public static class NipService
    {
        // osobny klient HTTP, timeout troche dluzszy bo MF czasem wolno odpowiada
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        // glowna metoda weryfikujaca NIP
        // zwraca NipResult z informacjami o firmie albo bledem
        public static async Task<NipResult> VerifyNipAsync(string nip)
        {
            // najpierw czyscimy NIP z kresek i spacji bo user moze wpisac roznie
            nip = nip.Replace("-", "").Replace(" ", "");

            // sprawdzamy czy ma dokladnie 10 cyfr
            if (nip.Length != 10 || !nip.All(char.IsDigit))
                return new NipResult { Nip = nip, Valid = false, Status = "Nieprawidłowy format NIP (wymagane 10 cyfr)" };

            // sprawdzamy sume kontrolna (matematyczna weryfikacja czy NIP w ogole moze istniec)
            if (!ValidateNipChecksum(nip))
                return new NipResult { Nip = nip, Valid = false, Status = "Nieprawidłowa suma kontrolna NIP" };

            // jesli NIP jest poprawny formalnie, pytamy API Ministerstwa Finansow
            // uzywam try/catch bo polaczenie moze nie dzialac
            try
            {
                // potrzebna dzisiejsza data w formacie YYYY-MM-DD
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                var url = $"https://wl-api.mf.gov.pl/api/search/nip/{nip}?date={today}";
                var resp = await _http.GetStringAsync(url);
                var doc = JsonNode.Parse(resp);

                // dane firmy sa w result -> subject
                var subject = doc!["result"]?["subject"];
                if (subject != null)
                {
                    // wyciagamy wszystkie dane ktore sa dostepne
                    var name = subject["name"]?.GetValue<string>() ?? "";
                    var statusVat = subject["statusVat"]?.GetValue<string>() ?? "";
                    var regon = subject["regon"]?.GetValue<string>() ?? "";
                    var address = subject["workingAddress"]?.GetValue<string>() ?? "";

                    return new NipResult
                    {
                        Nip = nip,
                        Valid = true,
                        CompanyName = name,
                        Status = TranslateVatStatus(statusVat),   // tlumaczenie na polski
                        StatusRaw = statusVat,                    // oryginalny status z API
                        Regon = regon,
                        Address = address,
                        Source = "Ministerstwo Finansów – Biała Lista"
                    };
                }
            }
            catch { /* jesli API nie odpowiada to leci nizej i zwracamy fallback */ }

            // jesli API nie zadziala, przynajmniej informujemy ze NIP jest arytmetycznie poprawny
            return new NipResult
            {
                Nip = nip,
                Valid = true,
                CompanyName = "(brak danych)",
                Status = "NIP poprawny – brak danych z rejestru (sprawdź połączenie)",
                Source = "lokalna weryfikacja"
            };
        }

        // zamienia angielskie statusy VAT z API na polskie opisy
        private static string TranslateVatStatus(string s) => s switch
        {
            "Czynny" => "Czynny podatnik VAT",
            "Zwolniony" => "Zwolniony z VAT",
            "Niezarejestrowany" => "Nie zarejestrowany jako podatnik VAT",
            _ => s   // jesli nieznany status, zostawiamy jak jest
        };

        // weryfikacja sumy kontrolnej NIP - algorytm matematyczny
        // wagi to specjalne liczby zdefiniowane przez prawo polskie
        // suma iloczynow cyfr przez wagi musi byc rowna ostatniej cyfrze (mod 11)
        public static bool ValidateNipChecksum(string nip)
        {
            int[] weights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += (nip[i] - '0') * weights[i];   // nip[i] - '0' zamienia znak cyfry na int
            return (sum % 11) == (nip[9] - '0');       // ostatnia cyfra to cyfra kontrolna
        }
    }

    // model wynikowy dla weryfikacji NIP
    public class NipResult
    {
        public string Nip { get; set; } = "";
        public bool Valid { get; set; }            // czy NIP jest prawidlowy
        public string CompanyName { get; set; } = "";
        public string Status { get; set; } = "";   // status VAT po polsku
        public string StatusRaw { get; set; } = "";  // oryginalny status z API
        public string Regon { get; set; } = "";
        public string Address { get; set; } = "";
        public string Source { get; set; } = "";   // skad wzielismy dane
    }
}
