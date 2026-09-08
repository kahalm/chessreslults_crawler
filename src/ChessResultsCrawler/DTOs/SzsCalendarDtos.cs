using ChessResultsCrawler.Services;

namespace ChessResultsCrawler.DTOs;

/// <summary>Ein Turnier aus dem Kalender des slowenischen Verbands. Flach, ohne Persistenz.</summary>
public class SzsEventResponse
{
    /// <summary>TID der Quelle — ihre Identitaet.</summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>ISO-Datum (yyyy-MM-dd).</summary>
    public string Date { get; set; } = "";

    /// <summary>Vierstellige Postleitzahl — steht schon in der Trefferliste.</summary>
    public string? PostalCode { get; set; }

    public string? Place { get; set; }

    /// <summary>Im NAMEN als abgesagt oder verlegt gekennzeichnet; ein Statusfeld gibt es nicht.</summary>
    public bool Cancelled { get; set; }

    public static SzsEventResponse FromParsed(ParsedSzsEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        Date = e.Date.ToString("yyyy-MM-dd"),
        PostalCode = e.PostalCode,
        Place = e.Place,
        Cancelled = e.Cancelled,
    };
}
