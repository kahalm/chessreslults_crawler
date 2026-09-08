using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des italienischen Verbands (FSI) als weitere Quelle. Zustandslos wie die
/// uebrigen Routen — nichts wird hier gespeichert, das Verzeichnis lebt in RookHub. Laeuft hinter
/// derselben ApiKeyMiddleware.
///
/// <para><b>Warum diese Quelle.</b> Italien faehrt sein Turnierwesen auf Vega/vesus, nicht auf
/// chess-results: von 285 Eintraegen verlinkt KEIN EINZIGER dorthin, und eine Namensstichprobe
/// von 15 fand nur 3 auf chess-results. Rund vier Fuenftel der italienischen Turniere fehlen dort
/// also — und das ist keine Momentaufnahme, sondern die Entscheidung eines ganzen Verbands.</para>
/// </summary>
[ApiController]
[Route("api/fsi-calendar")]
public class FsiCalendarController : ControllerBase
{
    /// <summary>
    /// Der Kalender fuehrt rund acht Monate voraus; mehr als drei Jahre ist ein Tippfehler.
    /// </summary>
    private const int MaxSpanDays = 1100;

    private readonly FsiCalendarService _fsi;

    public FsiCalendarController(FsiCalendarService fsi)
    {
        _fsi = fsi;
    }

    /// <summary>
    /// Turniere im Zeitfenster. Ohne Angaben: von heute an 18 Monate — dasselbe Fenster, das das
    /// Verzeichnis fuehrt.
    ///
    /// <para><b>Ein Abruf liefert alles.</b> Name, Termin, Region, Provinz, Ort, Bedenkzeit und
    /// Rundenzahl stehen inline in der Trefferliste; es gibt keine Detailseiten und keine
    /// Paginierung. Der Preis ist die Groesse — 283 Turniere sind rund 1,25 MB HTML.</para>
    ///
    /// <para>Was NICHT drinsteht: Postleitzahl (0 von 283), Turniersystem, Einzel/Mannschaft. Die
    /// Provinz ist dafuer immer da und loest italienische Namensgleichheit auf.</para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FsiEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? start.AddMonths(18);

        if (end < start)
            return BadRequest(new { message = "to must not be before from." });
        if (end.DayNumber - start.DayNumber > MaxSpanDays)
            return BadRequest(new { message = $"Span must not exceed {MaxSpanDays} days." });

        var events = await _fsi.FetchAsync(start, end, ct);
        return Ok(events.Select(FsiEventResponse.FromParsed).ToList());
    }
}
