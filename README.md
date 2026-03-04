# Kurs / NIP – Aplikacja WinForms

Aplikacja desktopowa (Windows) umożliwiająca:
- Pobieranie kursów walut z API NBP (Narodowy Bank Polski)
- Weryfikację przedsiębiorcy po numerze NIP (Biała Lista MF)
- Logowanie z opcjonalną weryfikacją dwuetapową (2FA via e-mail)
- Przechowywanie historii zapytań w lokalnej bazie SQLite

---

## Wymagania

- **Windows 10/11**
- **.NET 8 SDK** (lub nowszy): https://dotnet.microsoft.com/download
- Połączenie z internetem (API NBP i MF są publiczne, bez kluczy)

---

## Uruchomienie

### Szybki start (gotowy plik .exe)

Jeśli masz gotowy build:
1. Uruchom `KursNIP.exe`
2. Zaloguj się: `admin` / `admin123`

### Kompilacja ze źródeł

```bash
cd KursNIP
dotnet restore
dotnet build
dotnet run
```

Lub zbuduj samodzielny plik exe:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Plik `KursNIP.exe` znajdzie się w:
`bin\Release\net8.0-windows\win-x64\publish\`

---

## Struktura projektu

```
KursNIP/
├── Program.cs              # Punkt wejścia
├── Database.cs             # SQLite – baza danych (użytkownicy, historia)
├── ApiServices.cs          # Serwisy: NBP (waluty) + MF Biała Lista (NIP)
├── EmailService.cs         # Wysyłanie kodów 2FA via SMTP
├── LoginForm.cs            # Formularz logowania
├── RegisterAndTwoFAForms.cs # Rejestracja i weryfikacja 2FA
├── MainForm.cs             # Główne okno z zakładkami
├── ExchangeRatePanel.cs    # Zakładka: Kurs walut
├── NipPanel.cs             # Zakładka: Weryfikacja NIP
├── SettingsPanel.cs        # Zakładka: Ustawienia konta i SMTP
└── KursNIP.csproj          # Definicja projektu
```

Pliki tworzone w katalogu aplikacji:
- `kursnip.db`  – baza SQLite
- `kursnip.cfg` – konfiguracja SMTP

---

## Funkcjonalności

### Logowanie
- Uwierzytelnianie hasłem (SHA-256 + salt)
- Opcjonalna 2FA przez e-mail (kod 6-cyfrowy, ważny 10 minut)
- Jeśli SMTP nie jest skonfigurowany → kod wyświetlany w oknie dialogowym (tryb testowy)

### Kurs walut (NBP)
- Załaduj listę walut z tabeli A NBP
- Pobierz bieżący kurs wybranej waluty
- Pobierz 10 ostatnich notowań
- Historia zapytań zapisywana w bazie danych

### Weryfikacja NIP
- Walidacja sumy kontrolnej NIP
- Sprawdzenie w Białej Liście MF (API publiczne, bez klucza)
- Wyświetlenie nazwy firmy, statusu VAT, REGON, adresu
- Historia zapytań zapisywana w bazie danych

### Ustawienia
- Zmiana adresu e-mail i hasła
- Włączanie/wyłączanie 2FA
- Konfiguracja serwera SMTP (do wysyłania kodów 2FA)
- Przycisk "Testuj SMTP" – wysyła testowy e-mail

---

## Konfiguracja 2FA (e-mail)

1. Zaloguj się → zakładka **Ustawienia**
2. Wypełnij sekcję **Konfiguracja poczty (SMTP)**:
   - Dla Gmail: host `smtp.gmail.com`, port `587`, SSL: tak
   - Użyj hasła aplikacji Google (nie zwykłego hasła konta)
3. Kliknij **Testuj SMTP** – sprawdź czy e-mail dotarł
4. Zaznacz **Weryfikacja 2FA: Aktywna**
5. Zapisz ustawienia

> **Uwaga:** Bez konfiguracji SMTP kody 2FA wyświetlają się w oknie dialogowym (tryb testowy – tylko do celów demonstracyjnych).

---

## Domyślne konto

| Login | Hasło    |
|-------|----------|
| admin | admin123 |

---

## Użyte technologie

| Technologia | Zastosowanie |
|---|---|
| .NET 8 WinForms | Interfejs użytkownika |
| Microsoft.Data.Sqlite | Baza danych (lokalny plik .db) |
| Newtonsoft.Json | Parsowanie odpowiedzi API |
| API NBP | Kursy walut |
| API MF Biała Lista | Weryfikacja NIP / VAT |
| System.Net.Mail | Wysyłanie kodów 2FA |
