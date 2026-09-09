using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>
/// Ein Turnier aus der Monatsliste des franzoesischen Verbands. Flach und ohne Persistenz — der
/// Crawler reicht es durch.
/// </summary>
public class FfeEventResponse
{
    /// <summary>Die FFE-Nummer (<c>FicheTournoi.aspx?Ref=</c>); die Identitaet des Eintrags.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd). Nur der Beginn — das Ende steht auf der Turnierseite.</summary>
    public string StartDate { get; set; } = "";

    /// <summary>Spielort in der Versalschrift der Quelle („SAINT BRISSON").</summary>
    public string City { get; set; } = "";

    /// <summary>Departements-Nummer („58") — Anfang jeder Postleitzahl des Departements.</summary>
    public string Department { get; set; } = "";

    /// <summary>„FFE" oder ein Ligue-Kuerzel („EST", „NAQ", „IDF") — WER homologiert, nicht WO.</summary>
    public string? HomologatedBy { get; set; }

    /// <summary>Adresse der Turnierseite.</summary>
    public string Url { get; set; } = "";

    public static FfeEventResponse FromParsed(ParsedFfeEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        City = e.City,
        Department = e.Department,
        HomologatedBy = e.HomologatedBy,
        Url = e.Url,
    };
}

/// <summary>
/// Die Turnierseite EINES franzoesischen Turniers: Enddatum, Anschrift mit Postleitzahl,
/// Bedenkzeit, Rundenzahl, Paarungsverfahren.
/// </summary>
public class FfeDetailResponse
{
    /// <summary>ISO-Datum oder <c>null</c>, wenn die Seite den Termin nicht nennt.</summary>
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }

    public string? City { get; set; }
    public string? Department { get; set; }

    /// <summary>Anschrift als Freitext — traegt in 7 von 8 gemessenen Faellen die Postleitzahl.</summary>
    public string? Address { get; set; }

    public string? TimeControl { get; set; }
    public int? Rounds { get; set; }

    /// <summary>Paarungsverfahren im Wortlaut der Quelle („Suisse", „S.A.D.", „Haley").</summary>
    public string? PairingSystem { get; set; }

    public static FfeDetailResponse FromParsed(ParsedFfeDetail d) => new()
    {
        StartDate = d.StartDate?.ToString("yyyy-MM-dd"),
        EndDate = d.EndDate?.ToString("yyyy-MM-dd"),
        City = d.City,
        Department = d.Department,
        Address = d.Address,
        TimeControl = d.TimeControl,
        Rounds = d.Rounds,
        PairingSystem = d.PairingSystem,
    };
}
