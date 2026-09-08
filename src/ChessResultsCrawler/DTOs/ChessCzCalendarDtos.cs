namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Eintrag aus dem Terminkalender des tschechischen Verbands (chess.cz).</summary>
public class ChessCzEventResponse
{
    /// <summary>Der Adress-Bestandteil der Detailseite — die einzige Kennung dieser Quelle.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string? Place { get; set; }

    /// <summary>Laenderfaehnchen der Zeile als ISO-2 („CZ", „SK").</summary>
    public string? Country { get; set; }

    public string? ChessResultsId { get; set; }

    /// <summary>Steht in der Lasche „Kalendář mládeže" — eine verlaessliche Jugend-Angabe.</summary>
    public bool Youth { get; set; }

    /// <summary>Schulung, Trainingslager, Sitzung — kein Turnier.</summary>
    public bool NonTournament { get; set; }

    /// <summary>Die Rundennummer, wenn der Eintrag eine Ligarunde ist.</summary>
    public int? RoundNumber { get; set; }

    /// <summary>Der Name der Meisterschaft ohne Rundenzusatz — er bindet die Runden zusammen.</summary>
    public string? SeriesName { get; set; }

    public static ChessCzEventResponse FromParsed(ParsedChessCzEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        Country = e.Country,
        ChessResultsId = e.ChessResultsId,
        Youth = e.Youth,
        NonTournament = e.NonTournament,
        RoundNumber = e.RoundNumber,
        SeriesName = e.SeriesName,
    };
}
