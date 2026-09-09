using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>Ein Turnier aus dem slowakischen Verbandskalender. Flach, ohne Persistenz.</summary>
public class SszEventResponse
{
    /// <summary>Die tournamentId der Quelle — ihre Identitaet.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    public string? Place { get; set; }

    /// <summary>Dreibuchstabiger Laendercode; der Kalender fuehrt vereinzelt Auslandstermine.</summary>
    public string? Country { get; set; }

    /// <summary>
    /// chess-results-Turniernummer, sofern der Eintrag dorthin verlinkt — ein EXAKTER
    /// Zuordnungsschluessel gegen den bestehenden Bestand statt Namensraterei.
    /// </summary>
    public string? ChessResultsId { get; set; }

    public string? Website { get; set; }
    public string? Url { get; set; }

    public static SszEventResponse FromParsed(ParsedSszEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        Country = e.Country,
        ChessResultsId = e.ChessResultsId,
        Website = e.Website,
        Url = e.Url,
    };
}

/// <summary>
/// Der Turnierkalender des slowakischen Verbands (SSZ). Zustandslos wie die uebrigen Routen.
///
/// <para><b>Die einzige gepruefte Quelle, die ausdruecklich zur Nutzung einlaedt:</b>
/// dokumentierte REST-Schnittstelle mit OpenAPI-Beschreibung, im Footer als „Free api
/// specification" verlinkt, <c>Access-Control-Allow-Origin: *</c>, und in der Beschreibung steht
/// woertlich „If you need more, just ask for it - sekretariat@chess.sk".</para>
///
/// <para>78 kuenftige Turniere, 20 Monate Vorlauf, 53 davon nicht auf chess-results — im
/// Nahbereich rund 45 %, ab 2027 sogar 85 %. Dauerhafter Ueberschuss sind die nicht
/// FIDE-gewerteten Klub- und Barturniere.</para>
/// </summary>
[ApiController]
[Route("api/ssz-calendar")]
public class SszCalendarController : ControllerBase
{
    private readonly SszCalendarService _ssz;

    public SszCalendarController(SszCalendarService ssz)
    {
        _ssz = ssz;
    }

    /// <summary>
    /// Alle kuenftigen Turniere. Die Schnittstelle liefert von sich aus nur Kuenftiges — es gibt
    /// hier nichts zu filtern und keine Vorjahres-Falle.
    ///
    /// <para>Was NICHT drinsteht: Postleitzahl, Bedenkzeit, Rundenzahl. Die stehen nur auf der
    /// Detailseite (ein Abruf je Turnier) und werden hier bewusst nicht geholt. Und der Kalender
    /// fuehrt auch NICHT-Turniere (Schulungen, Ferienlager) — die muss der Aufrufer ueber den
    /// Namen filtern.</para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SszEventResponse>>> Calendar(CancellationToken ct = default)
    {
        var events = await _ssz.FetchAsync(ct);
        return Ok(events.Select(SszEventResponse.FromParsed).ToList());
    }
}
