using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der FIDE-Kalender als zweite Turnierquelle. Zustandslos wie die chess-results-Routen — nichts
/// wird hier gespeichert, das Verzeichnis lebt in RookHub. Laeuft hinter derselben
/// ApiKeyMiddleware.
///
/// <para>Gebraucht, weil die grossen internationalen Turniere in der chess-results-Turniersuche
/// nicht (oder erst spaet) stehen: von 139 FIDE-Ereignissen des Jahres 2026 fanden sich 132 nicht
/// im eigenen Bestand — Tata Steel, Rilton Cup, Prague Masters, Aeroflot Open, das
/// Frauen-Kandidatenturnier. Siehe <see cref="FideCalendarService"/>.</para>
/// </summary>
[ApiController]
[Route("api/fide-calendar")]
public class FideCalendarController : ControllerBase
{
    /// <summary>
    /// Sinnvolle Jahresgrenzen. Der Kalender fuehrt Vergangenes bis ~2010 und Kuenftiges bis
    /// hoechstens zwei Jahre voraus; alles darueber ist ein Tippfehler, kein Wunsch.
    /// </summary>
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    private readonly FideCalendarService _fide;

    public FideCalendarController(FideCalendarService fide)
    {
        _fide = fide;
    }

    /// <summary>
    /// Die Ereignisse EINES Jahres.
    ///
    /// <para><b>Vertrag fuer den Aufrufer.</b> Die Jahresansicht nennt nur Tag und Monat; das Jahr
    /// kommt aus dieser Anfrage. Ein Ereignis ueber den Jahreswechsel („27 Dec - 05 Jan", Rilton
    /// Cup) erscheint deshalb in ZWEI Jahresansichten mit derselben Zeichenkette, und nur EINE
    /// Lesart kann stimmen. Diese Antwort legt es als „beginnt im abgefragten Jahr" aus. Wer die
    /// Jahre also <b>aufsteigend</b> abfragt und je Ereignis-Nummer den ERSTEN Treffer behaelt,
    /// bekommt die richtigen Daten; wer nur ein spaeteres Jahr abfragt, liest so ein Ereignis um
    /// ein Jahr zu spaet. Von 139 Ereignissen des Jahres 2026 war genau eines betroffen (siehe
    /// <c>FideCalendarService.ResolveYears</c>).</para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FideEventResponse>>> Year(
        [FromQuery] int year, CancellationToken ct = default)
    {
        if (year is < MinYear or > MaxYear)
            return BadRequest(new { message = $"year must be between {MinYear} and {MaxYear}." });

        var events = await _fide.FetchYearAsync(year, ct);
        return Ok(events.Select(FideEventResponse.FromParsed).ToList());
    }
}
