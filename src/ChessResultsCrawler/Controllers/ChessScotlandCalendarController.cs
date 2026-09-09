using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender von Chess Scotland (chessscotland.com). Zustandslos wie die uebrigen
/// Routen — und ZWEISTUFIG wie chessarbiter/sjakk.no.
///
/// <para>43-44 kuenftige Turniere in EINEM Abruf, gegen 8 auf chess-results fuer SCO. Die Liste
/// nennt keinen Spielort; der steht bestenfalls auf der Detailseite, und die kostet einen Abruf
/// je Turnier (Trefferquote gemessen: 19 %). Deshalb zwei Routen: der Aufrufer entscheidet, fuer
/// welche Termine sich der zweite Abruf lohnt.</para>
/// </summary>
[ApiController]
[Route("api/chess-scotland-calendar")]
public class ChessScotlandCalendarController : ControllerBase
{
    private readonly ChessScotlandCalendarService _chessScotland;

    public ChessScotlandCalendarController(ChessScotlandCalendarService chessScotland)
    {
        _chessScotland = chessScotland;
    }

    /// <summary>Turniere, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ChessScotlandEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _chessScotland.FetchAsync(start, ct);
        return Ok(events.Select(ChessScotlandEventResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Detailseite EINES Turniers: der Spielort, wenn die Ausschreibung eine erkennbare
    /// britische Postleitzahl enthaelt. 404, wenn die Seite nicht lesbar ist — ihr Ausfall kostet
    /// den Spielort, nicht den Termin (den hat die Liste schon).
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<ChessScotlandDetailResponse>> Detail(
        [FromQuery] string slug, CancellationToken ct = default)
    {
        var detail = await _chessScotland.FetchDetailAsync(slug, ct);
        return detail is null ? NotFound() : Ok(ChessScotlandDetailResponse.FromParsed(detail));
    }
}
