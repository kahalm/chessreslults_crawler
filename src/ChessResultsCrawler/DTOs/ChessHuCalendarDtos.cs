namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des ungarischen Verbands (chess.hu).</summary>
public class ChessHuEventResponse
{
    /// <summary>Die <c>id</c> der Quelle — ihre Identitaet.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd) — die Quelle liefert Jahr und <c>MM.DD</c> getrennt.</summary>
    public string StartDate { get; set; } = "";

    public string EndDate { get; set; } = "";

    public string? Place { get; set; }

    /// <summary>Ob <see cref="Place"/> ein Spielort ist — „Online" und „Helyszín később" sind keiner.</summary>
    public bool HasVenue { get; set; }

    public bool FideRated { get; set; }

    public static ChessHuEventResponse FromParsed(ParsedChessHuEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        HasVenue = e.HasVenue,
        FideRated = e.FideRated,
    };
}
