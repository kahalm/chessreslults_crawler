using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender der Irish Chess Union (icu.ie). Zustandslos wie die uebrigen Routen — und
/// ZWEISTUFIG wie chessarbiter: die Liste bringt fast alles, die Detailseite kostet einen Abruf
/// je Turnier und wird deshalb einzeln angefragt.
///
/// <para>81 kuenftige Turniere, rund 94 % davon nicht auf chess-results. Fuenf Abrufe (20 Zeilen
/// je Seite) mit 5 s Wartezeit dazwischen — ein Durchgang der Liste dauert also gut zwanzig
/// Sekunden.</para>
/// </summary>
[ApiController]
[Route("api/icu-calendar")]
public class IcuCalendarController : ControllerBase
{
    private readonly IcuCalendarService _icu;

    public IcuCalendarController(IcuCalendarService icu)
    {
        _icu = icu;
    }

    /// <summary>
    /// Turniere, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter
    /// beginnen. Der Stichtag setzt zugleich das Jahr, mit dem die jahreslosen Datumsangaben der
    /// Quelle gelesen werden („12 Sep").
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<IcuEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _icu.FetchAsync(start, ct);
        return Ok(events.Select(IcuEventResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Detailseite EINES Turniers: die chess-results-Nummer (der einzige exakte
    /// Zuordnungsschluessel dieser Quelle) und die Zahl der bereits Gemeldeten. 404, wenn die
    /// Seite nicht lesbar ist.
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<IcuDetailResponse>> Detail(
        [FromQuery] string id, CancellationToken ct = default)
    {
        var detail = await _icu.FetchDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(IcuDetailResponse.FromParsed(detail));
    }
}
