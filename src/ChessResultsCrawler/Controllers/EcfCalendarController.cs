using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des englischen Verbands (ECF). Zustandslos wie die uebrigen Routen.
///
/// <para>256 kuenftige Turniere bis Juli 2027, 86 % davon nicht auf chess-results — und als
/// einzige Quelle des Projekts liefert sie KOORDINATEN mit, fuer zwei Drittel der Turniere.</para>
///
/// <para>Ein Durchgang holt zwei Endpunkte (Termine und Spielstaetten, je geblaettert) mit der
/// Wartezeit aus der robots.txt der Quelle und dauert deshalb rund drei Minuten.</para>
/// </summary>
[ApiController]
[Route("api/ecf-calendar")]
public class EcfCalendarController : ControllerBase
{
    private readonly EcfCalendarService _ecf;

    public EcfCalendarController(EcfCalendarService ecf)
    {
        _ecf = ecf;
    }

    /// <summary>Turniere, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<EcfEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _ecf.FetchAsync(start, ct);
        return Ok(events.Select(EcfEventResponse.FromParsed).ToList());
    }
}
