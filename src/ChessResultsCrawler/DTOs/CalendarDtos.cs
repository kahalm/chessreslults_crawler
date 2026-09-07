using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>
/// Ein Eintrag des chess-results-ANKUENDIGUNGSkalenders. Flach und ohne Persistenz — der Crawler
/// reicht ihn durch.
/// </summary>
public class CalendarEntryResponse
{
    public string Name { get; set; } = "";

    /// <summary>Dreibuchstabiger Foederationscode.</summary>
    public string? Federation { get; set; }

    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    /// <summary>
    /// Nummer des Kalendereintrags aus dem Ausschreibungs-Verweis — die einzige stabile Kennung,
    /// die der Kalender hergibt, und bei rund einem Fuenftel der Eintraege nicht vorhanden.
    /// </summary>
    public string? CalendarId { get; set; }

    /// <summary>Verweis des Veranstalters.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// Turniernummer, WENN der Verweis auf eine chess-results-Turnierseite zeigt — die Ausnahme,
    /// nicht die Regel.
    /// </summary>
    public string? ChessResultsId { get; set; }

    public static CalendarEntryResponse FromParsed(ParsedCalendarEntry e) => new()
    {
        Name = e.Name,
        Federation = e.Federation,
        StartDate = e.StartDate?.ToString("yyyy-MM-dd") ?? "",
        EndDate = e.EndDate?.ToString("yyyy-MM-dd") ?? "",
        CalendarId = e.CalendarId,
        Url = e.Url,
        ChessResultsId = e.ChessResultsId,
    };
}
