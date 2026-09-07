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

    private static bool TryParseIsoDate(string? text, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrWhiteSpace(text)
            && DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
