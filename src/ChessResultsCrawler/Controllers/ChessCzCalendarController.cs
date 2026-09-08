using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Terminkalender des tschechischen Verbands (chess.cz). Zustandslos wie die uebrigen Routen.
///
/// <para>Ein Abruf ohne Formular und ohne Detailseiten. Sein Wert liegt weniger im Volumen (rund
/// 38 echte Turniere, 13 davon nicht auf chess-results) als in den 33 LIGARUNDEN: fertige
/// Spieltermine, fuer die sonst je Turnier eine eigene chess-results-Seite geholt wird.</para>
/// </summary>
[ApiController]
[Route("api/chess-cz-calendar")]
public class ChessCzCalendarController : ControllerBase
{
    private readonly ChessCzCalendarService _chessCz;

    public ChessCzCalendarController(ChessCzCalendarService chessCz)
    {
        _chessCz = chessCz;
    }

    /// <summary>Termine, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ChessCzEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _chessCz.FetchAsync(start, ct);
        return Ok(events.Select(ChessCzEventResponse.FromParsed).ToList());
    }
}
