using ChessResultsCrawler.Controllers;
using ChessResultsCrawler.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChessResultsCrawler.Tests.Controllers;

/// <summary>
/// Eingabepruefung des FIDE-Endpunkts. Der Wert geht unveraendert in eine fremde Anfrage — ein
/// absurdes Jahr laesst FIDE arbeiten, nicht uns.
/// </summary>
public class FideCalendarControllerTests
{
    private static FideCalendarController CreateController() =>
        new(new FideCalendarService(new HttpClient(new UnusedHandler()),
            NullLogger<FideCalendarService>.Instance));

    [Theory]
    [InlineData(0)]
    [InlineData(1999)]
    [InlineData(2101)]
    [InlineData(-2026)]
    public async Task Year_OutOfRange_ReturnsBadRequest(int year)
    {
        var result = await CreateController().Year(year);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>Wird in diesen Tests nie benutzt — ein Aufruf ist ein Fehler, kein Zufall.</summary>
    private sealed class UnusedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Diese Tests erwarten keinen HTTP-Aufruf.");
    }
}
