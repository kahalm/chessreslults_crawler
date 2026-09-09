namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>Ein Eintrag aus dem Saisonkalender der Welsh Chess Union.</summary>
public class WcuEventResponse
{
    /// <summary>
    /// Es gibt keine Nummer und keinen Slug bei dieser Quelle — der Wert wird aus Termin und
    /// Anschrift gebildet (siehe <see cref="WcuCalendarService.EventKeyOf"/>), bewusst OHNE den
    /// Namen.
    /// </summary>
    public string EventId { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string? Place { get; set; }
    public string Url { get; set; } = "";

    public static WcuEventResponse FromParsed(ParsedWcuEvent e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        EndDate = e.EndDate.ToString("yyyy-MM-dd"),
        Place = e.Place,
        Url = e.Url,
    };
}
