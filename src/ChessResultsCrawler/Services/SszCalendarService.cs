using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des slowakischen Verbands (SŠZ).</summary>
public class ParsedSszEvent
{
    /// <summary>Die <c>tournamentId</c> der Quelle — ihre Identitaet, 78 von 78 tragen sie.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Ort als Freitext („Bratislava - mestská časť Rača"). Ohne Postleitzahl.</summary>
    public string? Place { get; set; }

    /// <summary>Dreibuchstabiger Laendercode — der Kalender fuehrt vereinzelt Auslandstermine.</summary>
    public string? Country { get; set; }

    /// <summary>
    /// Die chess-results-Turniernummer, sofern der Eintrag dorthin verlinkt.
    ///
    /// <para><b>Der praktische Gewinn dieser Quelle.</b> 25 von 78 Eintraegen liefern sie mit —
    /// ein EXAKTER Zuordnungsschluessel gegen den bestehenden Bestand, statt ueber Namen und
    /// Termin zu raten. Bei den uebrigen 53 gibt es sie nicht, weil es die Turniere auf
    /// chess-results (noch) nicht gibt.</para>
    /// </summary>
    public string? ChessResultsId { get; set; }

    /// <summary>Seite des Veranstalters.</summary>
    public string? Website { get; set; }

    /// <summary>Verweis auf die Kalenderseite des Verbands.</summary>
    public string? Url { get; set; }
}

/// <summary>
/// Der Turnierkalender des slowakischen Verbands (Slovenský šachový zväz).
///
/// <para><b>Die einzige Quelle, die ausdruecklich zur Nutzung einlaedt.</b> Es gibt eine
/// dokumentierte REST-Schnittstelle mit OpenAPI-Beschreibung, im Footer als „Free api
/// specification" verlinkt, mit <c>Access-Control-Allow-Origin: *</c>, und die Beschreibung sagt
/// woertlich: „If you need more, just ask for it - sekretariat@chess.sk".</para>
///
/// <para><b>Ertrag.</b> 78 kuenftige Turniere, 20 Monate Vorlauf (bis 2028-05). 53 von 78 stehen
/// nicht auf chess-results — im Nahbereich rund 45 %, ab 2027 sogar 85 %. Dauerhafter Ueberschuss
/// sind die nicht FIDE-gewerteten Klub- und Barturniere, die dort nie landen.</para>
///
/// <para><b>Zwei Dinge, die der Aufrufer wissen muss.</b> Der Kalender fuehrt auch
/// NICHT-Turniere — Schiedsrichterschulungen, Trainerseminare, Ferienlager — die ueber den Namen
/// gefiltert werden muessen. Und er nennt keine Postleitzahl, keine Bedenkzeit und keine
/// Rundenzahl; die stehen nur auf der Detailseite (ein Abruf je Turnier) und werden hier bewusst
/// nicht geholt.</para>
///
/// <para>Rechtslage (2026-09-07 geprueft): <c>User-agent: * / Allow: /</c>, danach 622 namentlich
/// gesperrte Bots aus einer Blockliste von 2021 — ClaudeBot und GPTBot kommen darin nicht vor.
/// Keine Nutzungsbedingungen, kein TDM-Vorbehalt.</para>
/// </summary>
public class SszCalendarService
{
    internal const string AllowedHost = "www.chess.sk";

    private const string TournamentsUrl = "https://www.chess.sk/api/turnaje.php/v1/tournaments";

    private readonly HttpClient _http;
    private readonly ILogger<SszCalendarService> _log;

    public SszCalendarService(HttpClient http, ILogger<SszCalendarService> log)
    {
        _http = http;
        _log = log;
    }

    /// <summary>
    /// Alle kuenftigen Turniere. Die Schnittstelle liefert von sich aus nur Kuenftiges
    /// („Get tournaments which will start in future" laut Beschreibung) — es gibt hier also
    /// nichts zu filtern und keine Vorjahres-Falle.
    /// </summary>
    public async Task<List<ParsedSszEvent>> FetchAsync(CancellationToken ct = default)
    {
        var target = new Uri(TournamentsUrl);
        EnsureAllowedTarget(target);

        using var response = await _http.GetAsync(target, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var events = Parse(json);
        _log.LogInformation("SSZ-Kalender: {Count} Turniere", events.Count);
        return events;
    }

    /// <summary>
    /// Das JSON auseinandernehmen. Alle Werte kommen als ZEICHENKETTE — auch die Nummer
    /// (<c>"tournamentId": "5909"</c>); wer eine Zahl erwartet, bekommt einen Deserialisierungs-
    /// Fehler.
    /// </summary>
    internal static List<ParsedSszEvent> Parse(string json)
    {
        var rows = JsonSerializer.Deserialize<List<SszRow>>(json, Options) ?? [];
        var results = new List<ParsedSszEvent>();

        foreach (var row in rows)
        {
            if (row.TournamentId is not { Length: > 0 } id) continue;
            if (row.Nazov is not { Length: > 0 } name) continue;

            var start = ParseDate(row.TerminOd);
            if (start is null) continue;

            results.Add(new ParsedSszEvent
            {
                EventId = id,
                Name = name.Trim(),
                StartDate = start.Value,
                // Ein fehlendes Ende heisst „eintaegig", nicht „unbekannt".
                EndDate = ParseDate(row.TerminDo) ?? start.Value,
                Place = Trim(row.Mesto),
                Country = Trim(row.Stat),
                ChessResultsId = TournamentIdFrom(row.SwissManager),
                Website = Trim(row.Webstranka),
                Url = Trim(row.Url),
            });
        }
        return results;
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private sealed record SszRow(
        [property: JsonPropertyName("tournamentId")] string? TournamentId,
        [property: JsonPropertyName("termin_od")] string? TerminOd,
        [property: JsonPropertyName("termin_do")] string? TerminDo,
        [property: JsonPropertyName("nazov")] string? Nazov,
        [property: JsonPropertyName("mesto")] string? Mesto,
        [property: JsonPropertyName("stat")] string? Stat,
        [property: JsonPropertyName("s_m")] string? SwissManager,
        [property: JsonPropertyName("webstranka")] string? Webstranka,
        [property: JsonPropertyName("url")] string? Url);

    private static readonly Regex TnrPattern =
        new(@"chess-results\.com/[Tt]nr(\d+)\.aspx", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Die Turniernummer aus dem Swiss-Manager-Verweis. Der Wirt ist eine der
    /// Server-Unterdomaenen (<c>s1</c>…<c>s3</c>), deshalb wird nicht auf den Anfang geprueft.
    /// </summary>
    internal static string? TournamentIdFrom(string? url)
    {
        if (url is not { Length: > 0 }) return null;
        var m = TnrPattern.Match(url);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? Trim(string? value) =>
        value is { Length: > 0 } && value.Trim() is { Length: > 0 } t ? t : null;

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact((text ?? "").Trim(), "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;

    internal static void EnsureAllowedTarget(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"Refusing non-https target: {url}");

        if (!url.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing unexpected host: {url.Host}");
    }
}
