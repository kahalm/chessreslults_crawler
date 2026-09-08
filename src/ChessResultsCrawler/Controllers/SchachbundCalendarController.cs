using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Die Turnierdatenbank des Deutschen Schachbunds. Zustandslos wie die uebrigen Routen.
///
/// <para>Sie ist ein reines MELDE-System — der Veranstalter traegt seinen Termin selbst ein, ohne
/// Swiss-Manager. Deshalb stehen hier Vereins-Abendturniere, Jugend-Cups, Fernschach,
/// Problemschach und Schach960: Kategorien, die chess-results praktisch nie fuehrt.</para>
///
/// <para>Zwei Abrufe je Region (Terminliste und Ausschreibungs-Feed) mit der Wartezeit, die die
/// robots.txt der Quelle nennt — ein Durchgang ueber alle 25 Regionen dauert deshalb einige
/// Minuten.</para>
/// </summary>
[ApiController]
[Route("api/schachbund-calendar")]
public class SchachbundCalendarController : ControllerBase
{
    private readonly SchachbundCalendarService _schachbund;

    public SchachbundCalendarController(SchachbundCalendarService schachbund)
    {
        _schachbund = schachbund;
    }

    /// <summary>Termine, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SchachbundEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _schachbund.FetchAsync(start, ct);
        return Ok(events.Select(SchachbundEventResponse.FromParsed).ToList());
    }
}
