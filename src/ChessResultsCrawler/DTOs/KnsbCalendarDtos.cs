namespace ChessResultsCrawler.DTOs;

using ChessResultsCrawler.Services;

/// <summary>
/// Ein Turnier aus dem Kalender des niederlaendischen Verbands (KNSB). Enddatum, Ort, Anschrift,
/// Postleitzahl, Koordinaten, Rundenzahl und Teilnehmerzahl fehlen bewusst — die Liste dieser
/// Quelle liefert sie strukturell nicht (siehe <see cref="KnsbCalendarService"/>).
/// </summary>
public class KnsbEventResponse
{
    /// <summary>Der WordPress-Slug — deterministische Kennung dieser Quelle.</summary>
    public string Slug { get; set; } = "";

    public string Name { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>"Normaalschaak"/"Rapidschaak"/"Snelschaak" oder <c>null</c>.</summary>
    public string? Speed { get; set; }

    public bool Online { get; set; }

    public static KnsbEventResponse FromParsed(ParsedKnsbEvent e) => new()
    {
        Slug = e.Slug,
        Name = e.Name,
        StartDate = e.StartDate.ToString("yyyy-MM-dd"),
        Url = e.Url,
        Speed = e.Speed,
        Online = e.Online,
    };
}
