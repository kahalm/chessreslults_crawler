using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Aktivitaeten-Feed des norwegischen Schachverbands (sjakk.no). Zustandslos wie die uebrigen
/// Routen — und ZWEISTUFIG wie chessarbiter.
///
/// <para>chess-results fuehrt fuer NOR null kuenftige Turniere; alles hier ist Zugewinn. Der Feed
/// bringt sie in EINEM Abruf, nennt aber weder Ort noch Verein — das steht nur auf der
/// Detailseite, und die kostet einen Abruf je Turnier. Deshalb zwei Routen: der Aufrufer
/// entscheidet, fuer welche Termine sich der zweite Abruf lohnt.</para>
/// </summary>
[ApiController]
[Route("api/sjakk-calendar")]
public class SjakkCalendarController : ControllerBase
{
    private readonly SjakkCalendarService _sjakk;

    public SjakkCalendarController(SjakkCalendarService sjakk)
    {
        _sjakk = sjakk;
    }

    /// <summary>Termine, die am <paramref name="from"/> (Vorgabe: heute) noch laufen oder spaeter beginnen.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SjakkEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _sjakk.FetchAsync(start, ct);
        return Ok(events.Select(SjakkEventResponse.FromParsed).ToList());
    }

    /// <summary>
    /// Die Detailseite EINES Termins: Spielort, ausrichtender Verein, Bedenkzeit, Rundenzahl,
    /// System. 404, wenn die Seite nicht lesbar ist — ihr Ausfall kostet die Zusatzangaben, nicht
    /// den Termin (den hat der Feed schon).
    /// </summary>
    [HttpGet("detail")]
    public async Task<ActionResult<SjakkDetailResponse>> Detail(
        [FromQuery] string slug, CancellationToken ct = default)
    {
        var detail = await _sjakk.FetchDetailAsync(slug, ct);
        return detail is null ? NotFound() : Ok(SjakkDetailResponse.FromParsed(detail));
    }
}
