using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Ankuendigungskalender der Chess Federation of Canada. Zustandslos wie die uebrigen Routen.
///
/// <para>ZWEI Abrufe: die Ereignisseite nennt den aktuellen Dateinamen des Datensatzes (der Hash
/// wechselt bei jedem Site-Build des Verbands), die Datei selbst traegt dann alle Termine als
/// JSON. Gemessen 2026-09-10: 173 Eintraege, 171 kuenftig — gegen 68 kuenftige auf
/// chess-results. Siehe <see cref="CfcCalendarService"/>.</para>
/// </summary>
[ApiController]
[Route("api/cfc-calendar")]
public class CfcCalendarController : ControllerBase
{
    private readonly CfcCalendarService _cfc;

    public CfcCalendarController(CfcCalendarService cfc)
    {
        _cfc = cfc;
    }

    /// <summary>Termine, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CfcEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _cfc.FetchAsync(start, ct);
        return Ok(events.Select(CfcEventResponse.FromParsed).ToList());
    }
}
