namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Turnier aus dem Kalender des slowakischen Verbands (chess.sk).</summary>
public class ChessSkEventResponse
{
    /// <summary>Die <c>tournamentId</c> der Quelle — ihre Identitaet.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string StartDate { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd); bei eintaegigen Turnieren gleich <see cref="StartDate"/>.</summary>
    public string EndDate { get; set; } = "";

    public string? City { get; set; }

    /// <summary>Anschrift des Spielorts von der Detailseite — traegt oft eine Postleitzahl.</summary>
    public string? Address { get; set; }

    /// <summary>ISO-3-Staat der Quelle (SVK, vereinzelt CZE).</summary>
    public string? Country { get; set; }

    /// <summary>Die chess-results-Nummer aus dem Feld „Swiss manager URL" — ein EXAKTER Dedup-Schluessel.</summary>
    public string? ChessResultsId { get; set; }

    public string? Website { get; set; }
    public string? Url { get; set; }

    /// <summary>Das Freitextfeld „System" im Rohzustand.</summary>
    public string? SystemText { get; set; }

    /// <summary>Nur der Bedenkzeit-Teil daraus.</summary>
    public string? TimeControl { get; set; }

    /// <summary>„swiss", „roundRobin" oder <c>null</c>.</summary>
    public string? System { get; set; }

    public int? Rounds { get; set; }

    /// <summary>„Standard", „Rapid", „Blitz", „Online" — die Angabe der Quelle.</summary>
    public string? Type { get; set; }

    /// <summary>Nach dem Namen kein Turnier, sondern Schulung/Seminar/Trainingslager.</summary>
    public bool NonTournament { get; set; }

    public static ChessSkEventResponse FromParsed(ParsedChessSkEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        City = e.City,
        Address = e.Address,
        Country = e.Country,
        ChessResultsId = e.ChessResultsId,
        Website = e.Website,
        Url = e.Url,
        SystemText = e.SystemText,
        TimeControl = e.TimeControl,
        System = e.System,
        Rounds = e.Rounds,
        Type = e.Type,
        NonTournament = e.NonTournament,
    };
}
