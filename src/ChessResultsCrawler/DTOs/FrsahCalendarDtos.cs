namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des rumaenischen Verbands (FRSah).</summary>
public class FrsahEventResponse
{
    /// <summary>Numerische Beitrags-Nummer — eine echte, stabile Kennung.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>Spielstaette samt Anschrift, zu einer Zeile zusammengezogen.</summary>
    public string? Place { get; set; }

    public string? City { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Land als NAME („Romania").</summary>
    public string? Country { get; set; }

    public static FrsahEventResponse FromParsed(ParsedFrsahEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Url = e.Url,
        Place = e.Place,
        City = e.City,
        PostalCode = e.PostalCode,
        Country = e.Country,
    };
}
