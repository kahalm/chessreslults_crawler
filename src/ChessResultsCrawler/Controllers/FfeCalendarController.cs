using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des franzoesischen Verbands (FFE). Zustandslos wie die uebrigen Routen —
/// nichts wird hier gespeichert, das Verzeichnis lebt in RookHub. Laeuft hinter derselben
/// ApiKeyMiddleware.
///
/// <para><b>Warum diese Quelle.</b> Der groesste Einzel-Zugewinn der ganzen Quellenrunde: von 40
/// gegengeprueften FFE-Turnieren stehen ZWEI auf chess-results (5 %). Die FFE fuehrt die
/// nicht-FIDE-gewerteten Vereins- und Ligue-Turniere, die chess-results praktisch gar nicht
/// kennt.</para>
///
/// <para><b>Zweistufig wie chessarbiter.</b> Die Monatsliste kostet einen Abruf je Monat und
/// bringt Nummer, Name, Starttag, Ort und Departement. Enddatum, Anschrift mit Postleitzahl,
/// Bedenkzeit, Rundenzahl und Paarungsverfahren stehen auf der Turnierseite — ein Abruf je
/// Turnier, deshalb einzeln angefragt: der Aufrufer entscheidet, fuer welche.</para>
/// </summary>
[ApiController]
[Route("api/ffe-calendar")]
public class FfeCalendarController : ControllerBase
{
    /// <summary>
    /// Wie viele Monatslisten hoechstens gelesen werden. Der Kalender ist gemessen nach vier
    /// Monaten fast leer (September 105, Oktober 43, November 12, Dezember 10, danach einzelne);
    /// zwei Jahre sind also reichlich, und mehr waere ein Tippfehler.
    /// </summary>
    private const int MaxMonths = 24;

    /// <summary>Zwoelf Monate kosten gemessen 12 GET + 3 Postbacks — das ist der Regelfall.</summary>
    private const int DefaultMonths = 12;

    private readonly FfeCalendarService _ffe;

    public FfeCalendarController(FfeCalendarService ffe)
    {
        _ffe = ffe;
    }

    /// <summary>
    /// Die Turniere ab <paramref name="from"/> (Vorgabe: heute) ueber <paramref name="months"/>
    /// Monatslisten. Alles vor dem Stichtag faellt weg — der laufende Monat enthaelt auch
    /// Vergangenes, und die Liste ist absteigend sortiert, sodass nur so weit geblaettert wird,
    /// bis eine Seite ganz davor liegt.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FfeEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, [FromQuery] int months = DefaultMonths,
        CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (months < 1)
            return BadRequest(new { message = "months must be at least 1." });
        if (months > MaxMonths)
            return BadRequest(new { message = $"months must not exceed {MaxMonths}." });

        var events = await _ffe.FetchAsync(start, months, ct);
        return Ok(events.Select(FfeEventResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Turnierseite EINES Turniers: Enddatum, Anschrift mit Postleitzahl, Bedenkzeit,
    /// Rundenzahl, Paarungsverfahren. 404, wenn die Seite nicht lesbar ist.
    ///
    /// <para>Das Enddatum ist der eigentliche Grund: die Monatsliste kennt nur den Starttag, und
    /// ohne Ende stuende ein dreitaegiges Open im Kalender nur an seinem ersten Tag.</para>
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<FfeDetailResponse>> Detail(
        [FromQuery] string id, CancellationToken ct = default)
    {
        var detail = await _ffe.FetchDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(FfeDetailResponse.FromParsed(detail));
    }
}
