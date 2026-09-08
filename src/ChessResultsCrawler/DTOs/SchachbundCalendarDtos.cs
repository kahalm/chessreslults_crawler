namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Termin aus der Turnierdatenbank des Deutschen Schachbunds.</summary>
public class SchachbundEventResponse
{
    /// <summary>Der Adressbestandteil der Detailseite — die einzige Kennung dieser Quelle.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";

    /// <summary>Der Regions-Schluessel, unter dem gemeldet wurde („bayern", „oesterreich", „welt").</summary>
    public string Region { get; set; } = "";

    /// <summary>Anschrift aus der Terminliste — oft mit Postleitzahl.</summary>
    public string? Place { get; set; }

    /// <summary>Bedenkzeit im Rohtext, sofern die Ausschreibung sie beschriftet.</summary>
    public string? TimeControl { get; set; }

    public int? Rounds { get; set; }

    /// <summary>„swiss" oder „roundRobin".</summary>
    public string? System { get; set; }

    public string Url { get; set; } = "";

    public static SchachbundEventResponse FromParsed(ParsedSchachbundEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Region = e.Region,
        Place = e.Place,
        TimeControl = e.TimeControl,
        Rounds = e.Rounds,
        System = e.System,
        Url = e.Url,
    };
}
