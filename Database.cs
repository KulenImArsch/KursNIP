using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace KursNIP
{
    // tutaj jest cala logika bazy danych
    // uzywam SQLite bo nie trzeba instalowac zadnego serwera, baza to po prostu jeden plik
    public static class Database
    {
        // sciezka do pliku bazy - laduję go do tego samego folderu co program
        private static string DbPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "kursnip.db");

        // connection string to po prostu info jak sie polaczyc z baza
        public static string ConnectionString => $"Data Source={DbPath}";

        // ta metoda tworzy tabele jesli jeszcze nie istnieja
        // wywoluje sie raz na starcie programu
        public static void Initialize()
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // CREATE TABLE IF NOT EXISTS - bezpieczne, nie wysypie sie jesli tabela juz jest
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Email TEXT,
                    TwoFactorEnabled INTEGER NOT NULL DEFAULT 0,
                    TwoFactorMethod TEXT DEFAULT 'email',
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS TwoFactorCodes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Code TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    Used INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS ExchangeRateHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Currency TEXT NOT NULL,
                    Rate REAL NOT NULL,
                    EffectiveDate TEXT NOT NULL,
                    QueryDate TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS NipHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Nip TEXT NOT NULL,
                    CompanyName TEXT,
                    Status TEXT,
                    QueryDate TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );
            ";
            cmd.ExecuteNonQuery();

            // jesli baza jest pusta (nowi uzytkownicy nie istnieja), tworzymy domyslne konto admin
            // dzieki temu program dziala od razu po instalacji
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            var count = (long)(cmd.ExecuteScalar() ?? 0);
            if (count == 0)
            {
                var hash = HashPassword("admin123");
                cmd.CommandText = @"INSERT INTO Users (Username, PasswordHash, Email, TwoFactorEnabled, TwoFactorMethod)
                                    VALUES ('admin', @hash, 'admin@example.com', 0, 'email')";
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.ExecuteNonQuery();
            }
        }

        // hashowanie hasla za pomoca SHA256
        // NIGDY nie przechowujemy hasel w czystym tekscie!
        // dodaje sol (staly ciag znakow) zeby te same hasla mialy rozne hashe
        public static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + "KursNIP_SALT_2024"));
            return Convert.ToHexString(bytes);   // zwraca hash jako tekst hex
        }

        // szuka uzytkownika w bazie po nazwie
        // zwraca null jesli nie znaleziony
        public static User? GetUser(string username)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // uzycie parametrow (@u) zamiast wklejania stringa bezposrednio chroni przed SQL injection
            cmd.CommandText = "SELECT Id, Username, PasswordHash, Email, TwoFactorEnabled, TwoFactorMethod FROM Users WHERE Username = @u";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Email = reader.IsDBNull(3) ? "" : reader.GetString(3),       // email moze byc pusty w bazie
                    TwoFactorEnabled = reader.GetInt32(4) == 1,                  // SQLite nie ma bool, uzywa 0/1
                    TwoFactorMethod = reader.IsDBNull(5) ? "email" : reader.GetString(5)
                };
            }
            return null;
        }

        // tworzy nowego uzytkownika w bazie
        // zwraca false jesli sie nie udalo (np. nazwa juz zajeta - UNIQUE constraint)
        public static bool CreateUser(string username, string password, string email, bool twoFactor, string method)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Users (Username, PasswordHash, Email, TwoFactorEnabled, TwoFactorMethod)
                                    VALUES (@u, @p, @e, @tf, @m)";
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", HashPassword(password));   // hashujemy haslo przed zapisem!
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@tf", twoFactor ? 1 : 0);      // bool -> 0/1 dla SQLite
                cmd.Parameters.AddWithValue("@m", method);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch { return false; }   // catch lapie np. naruszenie UNIQUE na nazwie uzytkownika
        }

        // aktualizuje dane uzytkownika (email, 2FA, opcjonalnie haslo)
        // newPasswordHash jest null jesli user nie zmienia hasla
        public static bool UpdateUser(int userId, string email, bool twoFactor, string method, string? newPasswordHash = null)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // jesli podano nowe haslo to dolaczamy je do UPDATE, jesli nie - pomijamy
            if (newPasswordHash != null)
            {
                cmd.CommandText = @"UPDATE Users SET Email=@e, TwoFactorEnabled=@tf, TwoFactorMethod=@m, PasswordHash=@ph WHERE Id=@id";
                cmd.Parameters.AddWithValue("@ph", newPasswordHash);
            }
            else
            {
                cmd.CommandText = @"UPDATE Users SET Email=@e, TwoFactorEnabled=@tf, TwoFactorMethod=@m WHERE Id=@id";
            }
            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@tf", twoFactor ? 1 : 0);
            cmd.Parameters.AddWithValue("@m", method);
            cmd.Parameters.AddWithValue("@id", userId);

            // ExecuteNonQuery zwraca ile wierszy zmienilo, jesli 0 to cos nie tak
            return cmd.ExecuteNonQuery() > 0;
        }

        // generuje 6-cyfrowy kod 2FA, zapisuje go w bazie i zwraca
        // stare kody tego uzytkownika oznaczamy jako uzyte zeby nie mozna bylo uzyc starego
        public static string SaveTwoFactorCode(int userId)
        {
            // losowy 6-cyfrowy kod np. "482917"
            var code = new Random().Next(100000, 999999).ToString();

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // najpierw uniewaznij wszystkie stare kody tego uzytkownika
            cmd.CommandText = "UPDATE TwoFactorCodes SET Used=1 WHERE UserId=@uid AND Used=0";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.ExecuteNonQuery();

            // wstaw nowy kod, wazny 10 minut od teraz
            cmd.CommandText = @"INSERT INTO TwoFactorCodes (UserId, Code, ExpiresAt)
                                VALUES (@uid, @code, datetime('now', '+10 minutes'))";
            cmd.Parameters.Clear();   // wyczyszczenie parametrow przed nowym zapytaniem
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.ExecuteNonQuery();

            return code;
        }

        // sprawdza czy podany kod 2FA jest poprawny dla danego uzytkownika
        // kod musi byc nieuzywany i nie moze byc przedawniony (10 minut)
        public static bool VerifyTwoFactorCode(int userId, string code)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // szukamy kodu ktory: pasuje do usera, jest nieuzywany, i jeszcze nie wygasl
            cmd.CommandText = @"SELECT Id FROM TwoFactorCodes
                                WHERE UserId=@uid AND Code=@code AND Used=0
                                AND datetime('now') < ExpiresAt";
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@code", code);
            var id = cmd.ExecuteScalar();

            // jesli nie znaleziono - kod niepoprawny lub wygasl
            if (id == null) return false;

            // oznaczamy kod jako uzywany zeby nie mozna bylo go uzyc ponownie
            cmd.CommandText = "UPDATE TwoFactorCodes SET Used=1 WHERE Id=@id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return true;
        }

        // zapisuje wynik pobrania kursu waluty do historii uzytkownika
        public static void SaveExchangeRate(int userId, string currency, double rate, string effectiveDate)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ExchangeRateHistory (UserId, Currency, Rate, EffectiveDate)
                                VALUES (@u, @c, @r, @e)";
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@c", currency);
            cmd.Parameters.AddWithValue("@r", rate);
            cmd.Parameters.AddWithValue("@e", effectiveDate);
            cmd.ExecuteNonQuery();
        }

        // pobiera ostatnie wpisy z historii kursow dla danego uzytkownika
        // domyslnie ostatnie 20, posortowane od najnowszego
        public static List<ExchangeRateRecord> GetExchangeHistory(int userId, int limit = 20)
        {
            var list = new List<ExchangeRateRecord>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();

            // ORDER BY Id DESC - od najnowszego do najstarszego
            // LIMIT - nie chcemy wczytywac tysiecy rekordow
            cmd.CommandText = @"SELECT Currency, Rate, EffectiveDate, QueryDate FROM ExchangeRateHistory
                                WHERE UserId=@u ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@l", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new ExchangeRateRecord
                {
                    Currency = r.GetString(0),
                    Rate = r.GetDouble(1),
                    EffectiveDate = r.GetString(2),
                    QueryDate = r.GetString(3)
                });
            return list;
        }

        // zapisuje wynik weryfikacji NIP do historii
        public static void SaveNipQuery(int userId, string nip, string companyName, string status)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO NipHistory (UserId, Nip, CompanyName, Status)
                                VALUES (@u, @n, @c, @s)";
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@n", nip);
            cmd.Parameters.AddWithValue("@c", companyName);
            cmd.Parameters.AddWithValue("@s", status);
            cmd.ExecuteNonQuery();
        }

        // pobiera ostatnie zapytania NIP dla danego uzytkownika
        public static List<NipRecord> GetNipHistory(int userId, int limit = 20)
        {
            var list = new List<NipRecord>();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT Nip, CompanyName, Status, QueryDate FROM NipHistory
                                WHERE UserId=@u ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@l", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new NipRecord
                {
                    Nip = r.GetString(0),
                    CompanyName = r.IsDBNull(1) ? "" : r.GetString(1),   // moze byc null w bazie
                    Status = r.IsDBNull(2) ? "" : r.GetString(2),
                    QueryDate = r.GetString(3)
                });
            return list;
        }
    }

    // model uzytkownika - mapowanie wiersza z tabeli Users na obiekt C#
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";   // haslo w formie hashu, nie jako tekst!
        public string Email { get; set; } = "";
        public bool TwoFactorEnabled { get; set; }
        public string TwoFactorMethod { get; set; } = "email";   // na razie tylko email
    }

    // model jednego wpisu historii kursow
    public class ExchangeRateRecord
    {
        public string Currency { get; set; } = "";
        public double Rate { get; set; }
        public string EffectiveDate { get; set; } = "";   // data z tabel NBP
        public string QueryDate { get; set; } = "";       // kiedy my zapytalismy
    }

    // model jednego wpisu historii NIP
    public class NipRecord
    {
        public string Nip { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Status { get; set; } = "";
        public string QueryDate { get; set; } = "";
    }
}
