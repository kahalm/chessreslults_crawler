using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des ungarischen Verbands (chess.hu). Zustandslos wie die uebrigen Routen.
///
/// <para>Ein Abruf fuer den ganzen Kalender — aber ein <b>POST</b>: ein GET auf denselben
/// Endpunkt antwortet 404. Und ein langsamer: 75 Sekunden fuer 31 kB, gemessen am 2026-09-08.</para>
/// </summary>
[ApiController]
[Route("api/chess-hu-calendar")]
public class ChessHuCalendarController : ControllerBase
{
    private readonly ChessHuCalendarService _chessHu;

    public ChessHuCalendarController(ChessHuCalendarService chessHu)
    {
        _chessHu = chessHu;
    }

    /// <summary>Turniere, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ChessHuEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _chessHu.FetchAsync(start, ct);
        return Ok(events.Select(ChessHuEventResponse.FromParsed).ToList());
    }
}
