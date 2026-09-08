using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des polnischen Verbands (chessarbiter.com). Zustandslos wie die uebrigen
/// Routen — und ZWEISTUFIG, wie der chess-results-Sweep selbst.
///
/// <para>Die Liste bringt in EINEM Abruf 611 kuenftige Turniere; die Detailseite kostet einen
/// Abruf je Turnier und wird deshalb einzeln angefragt — der Aufrufer entscheidet, fuer welche
/// (bei uns: fuer die neuen).</para>
/// </summary>
[ApiController]
[Route("api/chess-arbiter-calendar")]
public class ChessArbiterCalendarController : ControllerBase
{
    private readonly ChessArbiterCalendarService _chessArbiter;

    public ChessArbiterCalendarController(ChessArbiterCalendarService chessArbiter)
    {
        _chessArbiter = chessArbiter;
    }

    /// <summary>
    /// Die geplanten Turniere. <paramref name="today"/> setzt den Stichtag, ab dem die
    /// Jahresableitung rechnet (Vorgabe: heute) — die Liste selbst nennt nur Tag und Monat.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ChessArbiterEventResponse>>> Calendar(
        [FromQuery] DateOnly? today = null, CancellationToken ct = default)
    {
        var start = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _chessArbiter.FetchListAsync(start, ct);
        return Ok(events.Select(ChessArbiterEventResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Detailseite EINES Turniers: Enddatum, Bedenkzeit, Rundenzahl, System — und die
    /// Teilnehmerzahl, die es sonst fuer kuenftige Turniere nirgends gibt. 404, wenn die Seite
    /// nicht lesbar ist.
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<ChessArbiterDetailResponse>> Detail(
        [FromQuery] string year, [FromQuery] string id, CancellationToken ct = default)
    {
        var detail = await _chessArbiter.FetchDetailAsync(year, id, ct);
        return detail is null
            ? NotFound()
            : Ok(ChessArbiterDetailResponse.FromParsed(detail));
    }
}
