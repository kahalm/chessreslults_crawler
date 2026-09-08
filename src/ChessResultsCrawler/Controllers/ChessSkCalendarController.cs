using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des slowakischen Verbands (chess.sk). Zustandslos wie die uebrigen Routen.
///
/// <para><b>Die einzige Quelle mit einer angebotenen Schnittstelle.</b> Die Liste kommt als JSON
/// (<c>/api/turnaje.php/v1/tournaments</c>, im Fussteil der Seite als „Free api specification"
/// verlinkt). Bedenkzeit, Rundenzahl, Turniersystem und Anschrift stehen aber nur auf der
/// Detailseite — die ist HTML, und sie kostet einen Abruf je Turnier.</para>
/// </summary>
[ApiController]
[Route("api/chess-sk-calendar")]
public class ChessSkCalendarController : ControllerBase
{
    private readonly ChessSkCalendarService _chessSk;

    public ChessSkCalendarController(ChessSkCalendarService chessSk)
    {
        _chessSk = chessSk;
    }

    /// <summary>
    /// Turniere ab <paramref name="from"/> (Vorgabe: heute).
    ///
    /// <para><paramref name="details"/> steuert den teuren Teil: mit <c>true</c> (Vorgabe) wird je
    /// Turnier die Detailseite nachgeholt — Anschrift (mit Postleitzahl), Bedenkzeit, Rundenzahl,
    /// System und Bedenkzeit-Klasse. Mit <c>false</c> bleibt es bei dem EINEN Abruf der
    /// Schnittstelle; das ist der Weg, wenn nur Name, Termin und Ort gebraucht werden.</para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ChessSkEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, [FromQuery] bool details = true,
        CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _chessSk.FetchAsync(start, details, ct);
        return Ok(events.Select(ChessSkEventResponse.FromParsed).ToList());
    }
}
