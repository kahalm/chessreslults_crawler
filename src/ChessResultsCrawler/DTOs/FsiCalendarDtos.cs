using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>
/// Ein Turnier aus dem Kalender des italienischen Verbands. Flach und ohne Persistenz — der
/// Crawler reicht es durch.
/// </summary>
public class FsiEventResponse
{
    /// <summary>Fortlaufende FSI-Nummer; die Identitaet des Eintrags (283 von 283 tragen sie).</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    public string? Region { get; set; }

    /// <summary>Provinz — loest italienische Namensgleichheit auf.</summary>
    public string? Province { get; set; }

    public string? Place { get; set; }
    public string? EventType { get; set; }
    public string? TimeControl { get; set; }
    public int? Rounds { get; set; }
    public string? Note { get; set; }

    public static FsiEventResponse FromParsed(ParsedFsiEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Region = e.Region,
        Province = e.Province,
        Place = e.Place,
        EventType = e.EventType,
        TimeControl = e.TimeControl,
        Rounds = e.Rounds,
        Note = e.Note,
    };
}
