namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des polnischen Verbands (chessarbiter.com).</summary>
public class ChessArbiterEventResponse
{
    /// <summary>Jahr und Nummer aus dem Adresspfad bilden zusammen die Kennung.</summary>
    public string Year { get; set; } = "";

    public string EventId { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>ISO-Datum — das Jahr stammt aus der REIHENFOLGE der Liste, nicht aus dem Pfad.</summary>
    public string StartDate { get; set; } = "";

    public string? Place { get; set; }

    /// <summary>Woiwodschaft, ausgeschrieben.</summary>
    public string? Region { get; set; }

    /// <summary>Bedenkzeit-Klasse der Quelle (klasyczne, szybkie, blitz, …).</summary>
    public string? SpeedText { get; set; }

    public string Url { get; set; } = "";

    public static ChessArbiterEventResponse FromParsed(ParsedChessArbiterEvent e) => new()
    {
        Year = e.Year,
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        Region = e.Region,
        SpeedText = e.SpeedText,
        Url = e.Url,
    };
}

/// <summary>Was die Detailseite EINES polnischen Turniers zusaetzlich hergibt.</summary>
public class ChessArbiterDetailResponse
{
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Place { get; set; }
    public string? TimeControl { get; set; }
    public int? Rounds { get; set; }

    /// <summary>„swiss" oder „roundRobin".</summary>
    public string? System { get; set; }

    /// <summary>Teilnehmerzahl — steht dort auch bei GEPLANTEN Turnieren.</summary>
    public int? PlayerCount { get; set; }

    public string? Organizer { get; set; }

    public static ChessArbiterDetailResponse FromParsed(ParsedChessArbiterDetail d) => new()
    {
        StartDate = d.StartDate?.ToString("yyyy-MM-dd"),
        EndDate = d.EndDate?.ToString("yyyy-MM-dd"),
        Place = d.Place,
        TimeControl = d.TimeControl,
        Rounds = d.Rounds,
        System = d.System,
        PlayerCount = d.PlayerCount,
        Organizer = d.Organizer,
    };
}
