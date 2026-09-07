using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>
/// Ein Ereignis des FIDE-Kalenders. Flach und ohne Persistenz — der Crawler reicht es durch.
/// </summary>
public class FideEventResponse
{
    /// <summary>FIDE-Ereignis-Nummer; dort der Schluessel (<c>calendar.php?id=</c>).</summary>
    public string EventId { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    /// <summary>Stadt, wie FIDE sie schreibt. <c>null</c> bei Online-Ereignissen.</summary>
    public string? City { get; set; }
    /// <summary>Dreibuchstabiges Laenderkuerzel; „ONL" = online.</summary>
    public string? Country { get; set; }

    public static FideEventResponse FromParsed(ParsedFideEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        City = e.City,
        Country = e.Country,
    };
}
