using System.Globalization;
using System.Text.RegularExpressions;
using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Turnierverzeichnis-Abfrage: eine Foederation, ein Zeitfenster, eine Trefferliste. Zustandslos -
/// nichts wird hier gespeichert, das Verzeichnis lebt in RookHub. Laeuft wie alle anderen Routen
/// hinter der ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/tournament-search")]
public class TournamentSearchController : ControllerBase
{
    // Die Suche filtert auf das End-Datum. Ein Fenster ueber ~3 Jahre bringt keine zusaetzlichen
    // Treffer mehr (chess-results kappt bei 2000 Zeilen), kostet aber Antwortzeit.
    private const int MaxWindowDays = 3 * 366;
    private const int MaxRowsCap = 2000;

    private static readonly Regex FederationPattern = new(@"^[A-Za-z]{3}$", RegexOptions.Compiled);

    /// <summary>Turnierart „alle" — der Vorgabewert der Suchmaske.</summary>
    public const string AllTournamentTypes = "5";

    /// <summary>
    /// Die Turnierarten der Suchmaske: 0 Schweizer System, 1 Rundenturnier, 2 Rundenturnier fuer
    /// Mannschaften, 3 Schweizer System fuer Mannschaften, 5 alle.
    /// </summary>
    private static readonly string[] AllowedTypes = ["0", "1", "2", "3", AllTournamentTypes];

    /// <summary>Die chess-results-Turniernummer ist rein numerisch — nichts anderes wird geholt.</summary>
    private static readonly Regex TournamentIdPattern = new(@"^\d{1,10}$", RegexOptions.Compiled);

    private readonly CrawlerService _crawlerService;

    public TournamentSearchController(CrawlerService crawlerService)
    {
        _crawlerService = crawlerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DirectoryTournamentResponse>>> Search(
        [FromQuery] string fed,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] int maxRows = MaxRowsCap,
        [FromQuery] string? art = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fed) || !FederationPattern.IsMatch(fed.Trim()))
            return BadRequest(new { message = "fed must be a 3-letter federation code (e.g. AUT)." });

        if (!TryParseIsoDate(from, out var fromDate))
            return BadRequest(new { message = "from must be an ISO date (yyyy-MM-dd)." });

        if (!TryParseIsoDate(to, out var toDate))
            return BadRequest(new { message = "to must be an ISO date (yyyy-MM-dd)." });

        if (toDate < fromDate)
            return BadRequest(new { message = "to must not be before from." });

        if (toDate.DayNumber - fromDate.DayNumber > MaxWindowDays)
            return BadRequest(new { message = $"Date window must not exceed {MaxWindowDays} days." });

        maxRows = Math.Clamp(maxRows, 1, MaxRowsCap);

        // Die Turnierart ist ein Dropdown-INDEX der Suchmaske. Nur die tatsaechlich vorhandenen
        // Werte durchlassen: ein erfundener Index laesst ASP.NET die Auswahl verwerfen, die Suche
        // liefert dann stillschweigend etwas anderes als bestellt.
        var artValue = (art ?? "").Trim();
        if (artValue.Length == 0) artValue = AllTournamentTypes;
        if (!AllowedTypes.Contains(artValue))
            return BadRequest(new { message = $"art must be one of {string.Join(", ", AllowedTypes)}." });

        var results = await _crawlerService.SearchTournamentsAsync(
            fed.Trim().ToUpperInvariant(), fromDate, toDate, maxRows, artValue, ct);

        var now = DateTime.UtcNow;
        return Ok(results.Select(r => DirectoryTournamentResponse.FromParsed(r, now)).ToList());
    }

    /// <summary>
    /// Die Vereins-/Mannschaftsnamen eines Turniers. Zustandslos wie die Suche — ein Seitenabruf,
    /// nichts wird gespeichert. RookHub benutzt sie, um einen mehrdeutigen Spielort aufzuloesen
    /// („St.Veit" ist Tirol ODER Kaernten; „SV ASKOE St. Veit/Glan" sagt, welches).
    /// </summary>
    [HttpGet("teams")]
    public async Task<ActionResult<List<string>>> Teams([FromQuery] string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !TournamentIdPattern.IsMatch(id.Trim()))
            return BadRequest(new { message = "Invalid tournament ID." });

        return Ok(await _crawlerService.FetchTeamNamesAsync(id.Trim(), ct));
    }

    /// <summary>
    /// Die Turnierhistorie eines Spielers — EIN Seitenabruf, der alle Teilnahmen liefert
    /// (vergangene UND kuenftige), je Zeile mit Turnier-Id, Enddatum, Platz, Rundenzahl,
    /// Teilnehmerzahl und der STARTNUMMER (die nur im Link steht und die Spielerkarte adressiert).
    ///
    /// <para>Gesucht wird ueber den NAMEN, weil chess-results keine Suche ueber die Ident-Nummer
    /// anbietet. Die Zeilen tragen Ident-Nummer und Fide-ID mit, damit der Aufrufer bei
    /// Namensgleichheit die richtige Person auswaehlen kann — und weil bei Auslandsturnieren die
    /// Ident-Nummer „0" ist und nur die Fide-ID die Identitaet traegt.</para>
    /// </summary>
    [HttpGet("player-history")]
    public async Task<ActionResult<List<PlayerTournamentResponse>>> PlayerHistory(
        [FromQuery] string lastName, [FromQuery] string? firstName = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length < 2)
            return BadRequest(new { message = "lastName must have at least 2 characters." });

        var results = await _crawlerService.SearchPlayerTournamentsAsync(
            lastName.Trim(), firstName?.Trim(), ct);

        return Ok(results.Select(PlayerTournamentResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Kopfdaten EINES Turniers, ohne es zu importieren: Termin, Ort, Rundenzahl — und die
    /// BEDENKZEIT als Rohtext.
    ///
    /// <para>Die Spielersuche liefert Termin, Platz und Rundenzahl, aber keine Bedenkzeit. Ohne die
    /// stehen im Turnierverlauf Blitz- und Turnierschach-Ergebnisse in derselben Spalte, als
    /// waeren sie vergleichbar. Welche KLASSE daraus wird, entscheidet der Aufrufer — hier steht
    /// nur, was auf der Seite steht.</para>
    /// </summary>
    [HttpGet("tournament-info")]
    public async Task<ActionResult<TournamentInfoResponse>> TournamentInfo(
        [FromQuery] string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !TournamentIdPattern.IsMatch(id.Trim()))
            return BadRequest(new { message = "Invalid tournament ID." });

        var info = await _crawlerService.FetchTournamentInfoAsync(id.Trim(), ct);
        return Ok(TournamentInfoResponse.FromParsed(info));
    }

    /// <summary>
    /// Der Rundenplan eines Turniers: je Runde Nummer, Datum und Uhrzeit. Zustandslos, ein
    /// Seitenabruf.
    ///
    /// <para>Gebraucht, weil Start- und Enddatum einer LIGA nicht sagen, wann gespielt wird:
    /// elf Runden von September bis April liegen Wochen auseinander. Eine leere Liste ist der
    /// Normalfall bei Turnieren ohne hinterlegten Plan, kein Fehler.</para>
    /// </summary>
    [HttpGet("rounds")]
    public async Task<ActionResult<List<RoundDateResponse>>> Rounds(
        [FromQuery] string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !TournamentIdPattern.IsMatch(id.Trim()))
            return BadRequest(new { message = "Invalid tournament ID." });

        var rounds = await _crawlerService.FetchRoundPlanAsync(id.Trim(), ct);
        return Ok(rounds.Select(RoundDateResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Spielerkarte: Punkte, Platz, Performance-Rating und Elo-Aenderung eines Spielers in
    /// EINEM Turnier. Zustandslos, ein Seitenabruf.
    ///
    /// <para>204, wenn die Seite keinen Player-info-Block hat (falsche Startnummer). Ein
    /// KUENFTIGES Turnier antwortet dagegen mit 200 und <c>hasResult: false</c> — der
    /// Unterschied ist wesentlich: das eine ist ein Fehler, das andere der Normalfall.</para>
    /// </summary>
    [HttpGet("player-card")]
    public async Task<ActionResult<PlayerCardResponse>> PlayerCard(
        [FromQuery] string id, [FromQuery] int snr, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !TournamentIdPattern.IsMatch(id.Trim()))
            return BadRequest(new { message = "Invalid tournament ID." });

        if (snr is < 1 or > 10000)
            return BadRequest(new { message = "snr must be between 1 and 10000." });

        var card = await _crawlerService.FetchPlayerCardAsync(id.Trim(), snr, ct);
        return card is null ? NoContent() : Ok(PlayerCardResponse.FromParsed(card));
    }

    private static bool TryParseIsoDate(string? text, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrWhiteSpace(text)
            && DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
