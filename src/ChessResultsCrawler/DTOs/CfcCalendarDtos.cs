using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

public class CfcEventResponse
{
    /// <summary>
    /// Diese Quelle hat keine stabile Kennung — der Wert entsteht aus Termin, Ort und Name
    /// (siehe <see cref="CfcCalendarService.EventKeyOf"/>). Er ist LAENGER als die 60 Zeichen von
    /// <c>TournamentDirectorySource.ExternalId</c>; der Aufrufer vermerkt deshalb einen
    /// Kurzschluessel, wie es Wales und Deutschland ebenfalls tun.
    /// </summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    /// <summary>Ort ohne Anschrift — die Quelle fuehrt keine Strasse und keine Postleitzahl.</summary>
    public string? Place { get; set; }

    /// <summary>Provinz-Kuerzel (ON, BC, QC …).</summary>
    public string? Province { get; set; }

    /// <summary>Meist eine FREMDE Seite (Verein, Formular, PDF) — kein eigenes Permalink.</summary>
    public string? Url { get; set; }

    public static CfcEventResponse FromParsed(ParsedCfcEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        Province = e.Province,
        Url = e.Url,
    };
}
