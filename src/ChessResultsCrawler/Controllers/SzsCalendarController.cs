using ChessResultsCrawler.DTOs;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsCrawler.Controllers;

/// <summary>
/// Der Turnierkalender des slowenischen Verbands (SZS). Zustandslos wie die uebrigen Routen.
///
/// <para><b>Warum diese Quelle.</b> Das krasseste Verhaeltnis aller geprueften: 78 kuenftige
/// Turniere gegen 7 auf chess-results. Und nicht bloss Vorlauf — im Rueckblick auf einen
/// abgeschlossenen Monat erscheinen 36 von 88 Eintraegen dort NIE; ganze woechentliche Serien
/// fehlen strukturell.</para>
/// </summary>
[ApiController]
[Route("api/szs-calendar")]
public class SzsCalendarController : ControllerBase
{
    private readonly SzsCalendarService _szs;

    public SzsCalendarController(SzsCalendarService szs)
    {
        _szs = szs;
    }

    /// <summary>
    /// Turniere ab <paramref name="from"/> (Vorgabe: heute).
    ///
    /// <para>Die Trefferliste traegt die POSTLEITZAHL — die Verortung braucht damit keinen Abruf
    /// je Turnier. Bedenkzeit, Rundenzahl und Turniersystem stehen dagegen nur auf der
    /// Detailseite und werden hier bewusst nicht geholt.</para>
    ///
    /// <para>Gelesen wird seitenweise von vorn, weil die Quelle nach Datum ABSTEIGEND sortiert:
    /// kuenftige Turniere stehen auf den ersten Seiten. Abgebrochen wird, sobald eine Seite
    /// vollstaendig vor dem Stichtag liegt.</para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SzsEventResponse>>> Calendar(
        [FromQuery] DateOnly? from = null, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await _szs.FetchAsync(start, ct);
        return Ok(events.Select(SzsEventResponse.FromParsed).ToList());
    }
}
