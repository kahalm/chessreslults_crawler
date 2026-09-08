namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des englischen Verbands (ECF).</summary>
public class EcfEventResponse
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

    /// <summary>Land als NAME („United Kingdom", „France").</summary>
    public string? Country { get; set; }

    /// <summary>Koordinaten der Spielstaette — der Sonderfall dieser Quelle.</summary>
    public double? Lat { get; set; }
    public double? Lon { get; set; }

    /// <summary>Schlagworte der Quelle („FIDE Rated", „Juniors Only", „Online", „Meeting").</summary>
    public List<string> Categories { get; set; } = [];

    public string? Website { get; set; }

    public static EcfEventResponse FromParsed(ParsedEcfEvent e) => new()
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
        Lat = e.Lat,
        Lon = e.Lon,
        Categories = e.Categories,
        Website = e.Website,
    };
}
