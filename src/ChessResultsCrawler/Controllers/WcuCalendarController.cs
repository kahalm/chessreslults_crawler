using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Saisonkalender der Welsh Chess Union. Zustandslos wie die uebrigen Routen.
///
/// <para>EIN Abruf ohne Formular, ohne Detailseiten, ohne Paginierung — die ganze Saison steht in
/// einer einzigen Tabelle auf <c>/calendar/</c>. Gemessen 38 Turniere, keines davon eine Sitzung
/// oder ein Lehrgang.</para>
/// </summary>
[ApiController]
[Route("api/wcu-calendar")]
public class WcuCalendarController : ControllerBase
{
    private readonly WcuCalendarService _wcu;

    public WcuCalendarController(WcuCalendarService wcu)
    {
        _wcu = wcu;
    }

    /// <summary>Termine, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WcuEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _wcu.FetchAsync(start, ct);
        return Ok(events.Select(WcuEventResponse.FromParsed).ToList());
    }
}
