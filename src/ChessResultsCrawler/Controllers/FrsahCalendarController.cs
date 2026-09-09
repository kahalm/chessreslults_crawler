using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des rumaenischen Verbands (FRSah). Zustandslos wie die uebrigen Routen.
///
/// <para>31 kuenftige Turniere, rund ein Drittel davon nicht auf chess-results — darunter die
/// kompletten nationalen Mannschaftsligen. Anders als beim englischen Verband steht die
/// Spielstaette hier schon vollstaendig im Termin selbst; ein Durchgang holt deshalb nur EINEN
/// Endpunkt (heute eine Seite) und dauert nur Sekunden.</para>
/// </summary>
[ApiController]
[Route("api/frsah-calendar")]
public class FrsahCalendarController : ControllerBase
{
    private readonly FrsahCalendarService _frsah;

    public FrsahCalendarController(FrsahCalendarService frsah)
    {
        _frsah = frsah;
    }

    /// <summary>Turniere, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<FrsahEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _frsah.FetchAsync(start, ct);
        return Ok(events.Select(FrsahEventResponse.FromParsed).ToList());
    }
}
