using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace ChessResultsCrawler.Services;

/// <summary>Ein Ereignis aus dem FIDE-Kalender.</summary>
public class ParsedFideEvent
{
    /// <summary>FIDE-Ereignis-Nummer (aus <c>calendar.php?id=</c>) — der Schluessel dort.</summary>
    public string EventId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    /// <summary>Stadt, wie FIDE sie schreibt. Leer bei Online-Ereignissen.</summary>
    public string? City { get; set; }
    /// <summary>Laenderkuerzel in FIDE-Schreibweise (drei Buchstaben); „ONL" = online.</summary>
    public string? Country { get; set; }
}

/// <summary>
/// Der FIDE-Kalender als ZWEITE Turnierquelle.
///
/// <para><b>Warum ueberhaupt.</b> Das Verzeichnis lebt aus der chess-results-Turniersuche, und die
/// grossen internationalen Turniere stehen dort nicht — jedenfalls nicht Monate vorher. Am
/// 2026-09-07 gemessen: von 139 FIDE-Ereignissen des Jahres 2026 fanden sich <b>132 nicht</b> im
/// eigenen Bestand, und darunter Tata Steel, Rilton Cup, Prague Masters, Aeroflot Open, das
/// Frauen-Kandidatenturnier und die Freestyle-WM. Das ist der Zugewinn.</para>
///
/// <para><b>Der richtige Endpunkt ist nicht der naheliegende.</b> `calendar.php` mit
/// <c>show=table</c> bzw. <c>show=apilist</c> liefert saubere JSON-Zeilen — aber aus einer
/// Tabelle, die bei 2025 stehen geblieben ist (661 Ereignisse, keines in der Zukunft). Gepflegt
/// wird die JAHRESansicht: <c>show=showYear</c> mit <c>page=&lt;Jahr&gt;</c>, dieselbe
/// `calendar_server.php`. Sie liefert HTML statt JSON, dafuer aktuelle Daten: 2026 → 143
/// Ereignisse, 2027 → 15.</para>
///
/// <para><b>Ein eigener HttpClient und ein eigener Host-Schutz.</b> Der CrawlerService prueft
/// jeden Hop gegen <c>chess-results.com</c> — richtig fuer ihn, hier waere es falsch. Deshalb ein
/// getrennter Dienst mit derselben Regel fuer <c>calendar.fide.com</c>: kein automatisches
/// Redirect-Folgen, https erzwungen, exakter Hostvergleich.</para>
/// </summary>
public class FideCalendarService
{
    /// <summary>Der einzige zulaessige Host. Exakt verglichen — „calendar.fide.com.attacker.tld" faellt durch.</summary>
    internal const string AllowedHost = "calendar.fide.com";

    private const string ServerUrl = "https://calendar.fide.com/calendar_server.php";

    /// <summary>
    /// Die Jahresansicht traegt nur Tag und Monat („27 Dec - 05 Jan"); das Jahr kommt aus der
    /// Anfrage. Fuer eine Spanne ueber den Jahreswechsel siehe <see cref="ResolveYears"/>.
    /// </summary>
    private static readonly Regex EventPattern = new(
        """href="calendar\.php\?id=(?<id>\d+)"[^>]*>(?<name>[^<]+)</a>""",
        RegexOptions.Compiled);

    private static readonly Regex WhenPattern = new(
        @"^(?<d1>\d{1,2})\s+(?<m1>\w{3})\s*-\s*(?<d2>\d{1,2})\s+(?<m2>\w{3})\s*(?:/\s*(?<place>.*))?$",
        RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<FideCalendarService> _log;

    public FideCalendarService(HttpClient http, ILogger<FideCalendarService> log)
    {
        _http = http;
        _log = log;
    }

    /// <summary>
    /// Die Ereignisse EINES Jahres. Ein Seitenabruf, nichts wird gespeichert — wie alles in
    /// diesem Dienst.
    /// </summary>
    public async Task<List<ParsedFideEvent>> FetchYearAsync(int year, CancellationToken ct = default)
    {
        var target = new Uri(ServerUrl);
        EnsureAllowedTarget(target);

        // Genau die Felder, die die Seite selbst schickt (js/tabs.js, loadYear). `cat_filter` und
        // `cat_cont` bleiben WEG: leer mitgeschickt antwortet der Server mit 500.
        var form = new Dictionary<string, string>
        {
            ["country"] = "",
            ["name_filter"] = "",
            ["event_type"] = "all",
            ["time_control"] = "all",
            ["page"] = year.ToString(CultureInfo.InvariantCulture),
            ["show"] = "showYear",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = new FormUrlEncodedContent(form),
        };
        // Ohne diesen Kopf antwortet der Server die Rahmenseite statt des Ausschnitts.
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Referrer = new Uri("https://calendar.fide.com/majorcalendar.php");

        using var response = await _http.SendAsync(request, ct);
        var html = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var events = await ParseYearAsync(html, year);
        _log.LogInformation("FIDE-Kalender {Year}: {Count} Ereignisse", year, events.Count);
        return events;
    }

    /// <summary>
    /// Die Jahresansicht auseinandernehmen. Je Ereignis ein Link auf <c>calendar.php?id=</c> mit
    /// dem Namen und daneben ein <c>span.session-time</c> mit „01 May - 07 May / Malmo (SWE)".
    /// </summary>
    internal static async Task<List<ParsedFideEvent>> ParseYearAsync(string html, int year)
    {
        var results = new List<ParsedFideEvent>();
        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in document.QuerySelectorAll("a[href*='calendar.php?id=']"))
        {
            var href = link.GetAttribute("href") ?? "";
            var idMatch = Regex.Match(href, @"id=(\d+)");
            if (!idMatch.Success) continue;

            var eventId = idMatch.Groups[1].Value;
            // Dasselbe Ereignis kann in der Jahresansicht mehrfach verlinkt sein (Bild + Titel).
            if (!seen.Add(eventId)) continue;

            var name = link.TextContent.Trim();
            if (name.Length == 0) continue;

            var when = FindSessionTime(link);
            if (when is null) continue;

            var parsed = ParseWhen(when, year);
            if (parsed is null) continue;

            results.Add(new ParsedFideEvent
            {
                EventId = eventId,
                Name = name,
                StartDate = parsed.Value.Start,
                EndDate = parsed.Value.End,
                City = parsed.Value.City,
                Country = parsed.Value.Country,
            });
        }
        return results;
    }

    /// <summary>
    /// Der Termin steht in einem Geschwister-Element des Titels, nicht in dessen Elternknoten —
    /// gesucht wird deshalb im umgebenden Block aufwaerts, bis einer ein
    /// <c>span.session-time</c> enthaelt.
    /// </summary>
    private static string? FindSessionTime(IElement link)
    {
        for (var node = link.ParentElement; node is not null; node = node.ParentElement)
        {
            var span = node.QuerySelector("span.session-time");
            if (span is not null) return Regex.Replace(span.TextContent, @"\s+", " ").Trim();
            // Nicht beliebig weit hoch: sonst findet ein Ereignis den Termin des naechsten.
            if (node.ClassList.Contains("session")) break;
        }
        return null;
    }

    internal static (DateOnly Start, DateOnly End, string? City, string? Country)? ParseWhen(
        string when, int year)
    {
        var match = WhenPattern.Match(when);
        if (!match.Success) return null;

        var m1 = Month(match.Groups["m1"].Value);
        var m2 = Month(match.Groups["m2"].Value);
        if (m1 is null || m2 is null) return null;

        var (startYear, endYear) = ResolveYears(m1.Value, m2.Value, year);

        if (!TryDate(startYear, m1.Value, match.Groups["d1"].Value, out var start)) return null;
        if (!TryDate(endYear, m2.Value, match.Groups["d2"].Value, out var end)) return null;

        var (city, country) = SplitPlace(match.Groups["place"].Value);
        return (start, end, city, country);
    }

    /// <summary>
    /// Welches Jahr gehoert zu Anfang und Ende?
    ///
    /// <para>Die Jahresansicht nennt nur Tag und Monat. Liegt der Endmonat VOR dem Startmonat,
    /// laeuft das Ereignis ueber den Jahreswechsel („27 Dec - 05 Jan", Rilton Cup) — und
    /// erscheint dann in ZWEI Jahresansichten mit derselben Zeichenkette. Aufgeloest wird es
    /// ueber den Startmonat: in der zweiten Jahreshaelfte beginnt es im abgefragten Jahr und
    /// endet im naechsten, sonst umgekehrt. Wer die Jahre AUFSTEIGEND abfragt und den ersten
    /// Treffer behaelt, bekommt damit die richtigen Daten. Von 139 Ereignissen des Jahres 2026
    /// war genau eines betroffen.</para>
    /// </summary>
    internal static (int Start, int End) ResolveYears(int startMonth, int endMonth, int year) =>
        endMonth >= startMonth ? (year, year)
            : startMonth >= 7 ? (year, year + 1)
            : (year - 1, year);

    private static bool TryDate(int year, int month, string day, out DateOnly date)
    {
        date = default;
        if (!int.TryParse(day, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return false;
        if (d < 1 || d > DateTime.DaysInMonth(year, month)) return false;

        date = new DateOnly(year, month, d);
        return true;
    }

    private static int? Month(string abbreviation)
    {
        var index = Array.FindIndex(Months,
            m => m.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? null : index + 1;
    }

    private static readonly string[] Months =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>
    /// „Malmo (SWE)" → Stadt + Land. Online-Ereignisse haben keine Stadt („ (ONL)"), und ein
    /// Stadtname darf Klammern enthalten — deshalb wird die LETZTE Klammer genommen.
    /// </summary>
    internal static (string? City, string? Country) SplitPlace(string? place)
    {
        var text = (place ?? "").Trim();
        if (text.Length == 0) return (null, null);

        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close < open) return (text, null);

        var city = text[..open].Trim();
        var country = text[(open + 1)..close].Trim();
        return (city.Length == 0 ? null : city, country.Length == 0 ? null : country);
    }

    /// <summary>
    /// Derselbe Schutz wie im CrawlerService, nur fuer den anderen Host: https und ein exakter
    /// Hostvergleich. Redirects folgt dieser Client nicht automatisch (siehe Program.cs) — ein
    /// 3xx kaeme also als Antwort zurueck und scheiterte an EnsureSuccessStatusCode, statt blind
    /// irgendwohin zu laufen.
    /// </summary>
    internal static void EnsureAllowedTarget(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"Refusing non-https target: {url}");

        if (!url.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing unexpected host: {url.Host}");
    }
}
