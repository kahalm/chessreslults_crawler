namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>
/// Ein Termin aus dem Aktivitaeten-Feed des norwegischen Verbands. Bewusst NUR das, was der Feed
/// wirklich traegt — er hat kein Ortsfeld, und ein leeres <c>place</c> mitzuliefern taeuschte
/// vor, es koennte gefuellt sein.
/// </summary>
public class SjakkEventResponse
{
    /// <summary>Der Adressbestandteil der Detailseite („horten-bgp-høst-2027").</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string Url { get; set; } = "";

    public static SjakkEventResponse FromParsed(ParsedSjakkEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Url = e.Url,
    };
}

/// <summary>
/// Die Detailseite EINES Termins — Spielort, ausrichtender Verein, Bedenkzeit, Rundenzahl,
/// System. Ein Abruf je Turnier, deshalb eine eigene Antwort.
/// </summary>
public class SjakkDetailResponse
{
    /// <summary>Spielort aus „Spillsted" — nur 17 von 80 kuenftigen Terminen haben einen.</summary>
    public string? Venue { get; set; }

    /// <summary>
    /// Ausrichtender Verein aus „Arrangør" (52 von 80). Kein Spielort, aber der beste Hinweis
    /// darauf, den diese Quelle hat: ein norwegischer Vereinsname traegt meist seinen Ort.
    /// </summary>
    public string? Organizer { get; set; }

    public string? TimeControl { get; set; }
    public int? Rounds { get; set; }

    /// <summary>„swiss" oder „roundRobin".</summary>
    public string? System { get; set; }

    public string? Website { get; set; }

    public static SjakkDetailResponse FromParsed(ParsedSjakkDetail d) => new()
    {
        Venue = d.Venue,
        Organizer = d.Organizer,
        TimeControl = d.TimeControl,
        Rounds = d.Rounds,
        System = d.System,
        Website = d.Website,
    };
}
