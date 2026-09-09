using System.Net;
using ChessResultsCrawler.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace ChessResultsCrawler.Tests.Services;

/// <summary>
/// Der Wiederholversuch ueber einen anderen VPN-Ausgang. Die Unterscheidung, WAS wiederholt wird,
/// ist der ganze Wert dieser Klasse: eine Antwort der Quelle — auch 403 oder 500 — ist eine
/// Auskunft und darf keinen Ausgangswechsel ausloesen, ein nicht zustande gekommener
/// Verbindungsaufbau schon.
/// </summary>
public class RotateOnConnectFailureHandlerTests
{
    [Fact]
    public void EinNichtZustandeGekommenerVerbindungsaufbau_wirdWiederholt()
    {
        Assert.True(RotateOnConnectFailureHandler.IsConnectFailure(
            new HttpRequestException("Resource temporarily unavailable"), CancellationToken.None));
    }

    /// <summary>
    /// Ein Timeout des Verbindungsaufbaus (`SocketsHttpHandler.ConnectTimeout`) kommt als
    /// TaskCanceledException, OHNE dass der Aufrufer abgebrochen haette — genau der Fall, den
    /// Italien und Rumaenien am 2026-09-09 lieferten.
    /// </summary>
    [Fact]
    public void EinVerbindungsTimeout_wirdWiederholt()
    {
        Assert.True(RotateOnConnectFailureHandler.IsConnectFailure(
            new TaskCanceledException("The operation was canceled."), CancellationToken.None));
    }

    /// <summary>
    /// Bricht der AUFRUFER ab, wird nicht wiederholt. Ohne diese Unterscheidung loeste jeder
    /// abgebrochene Request vier Ausgangswechsel aus — und jeder legt den Tunnel kurz still.
    /// </summary>
    [Fact]
    public void EinAbbruchDurchDenAufrufer_wirdNichtWiederholt()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(RotateOnConnectFailureHandler.IsConnectFailure(
            new TaskCanceledException("cancelled"), cts.Token));
    }

    [Fact]
    public void EinAndererFehler_wirdNichtWiederholt()
    {
        Assert.False(RotateOnConnectFailureHandler.IsConnectFailure(
            new InvalidOperationException("Parserfehler"), CancellationToken.None));
    }

    [Fact]
    public async Task EineAntwortDerQuelle_wirdDurchgereicht_ohneAusgangswechsel()
    {
        var gate = Gate();
        var handler = new RotateOnConnectFailureHandler(gate, NullLogger<RotateOnConnectFailureHandler>.Instance)
        {
            InnerHandler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)),
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://quelle.test/kalender");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NachEinemFehlschlag_wirdEinZweitesMalVersucht_undGeliefert()
    {
        var inner = new SequenceHandler(
            new HttpRequestException("keine Verbindung"),
            new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new RotateOnConnectFailureHandler(Gate(), NullLogger<RotateOnConnectFailureHandler>.Instance)
        {
            InnerHandler = inner,
        };

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://quelle.test/kalender");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    /// <summary>
    /// Bleibt der Host von JEDEM Ausgang unerreichbar, gibt der Handler nach
    /// <see cref="RotateOnConnectFailureHandler.MaxAttempts"/> Versuchen auf und laesst den Fehler
    /// durch — sonst liefe ein gesperrter Host in eine Endlosschleife von Tunnel-Neustarts.
    /// </summary>
    [Fact]
    public async Task EinDauerhaftGesperrterHost_gibtNachFuenfVersuchenAuf()
    {
        var inner = new SequenceHandler(Enumerable.Repeat(
            (object)new HttpRequestException("keine Verbindung"), 10).ToArray());
        var handler = new RotateOnConnectFailureHandler(Gate(), NullLogger<RotateOnConnectFailureHandler>.Instance)
        {
            InnerHandler = inner,
        };

        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://gesperrt.test/"));

        Assert.Equal(RotateOnConnectFailureHandler.MaxAttempts, inner.Calls);
    }

    /// <summary>Ein Gate, dessen Rotation ins Leere laeuft (kein gluetun im Test) — der Handler
    /// darf davon nicht abhaengen, er wiederholt trotzdem.</summary>
    private static VpnReadinessGate Gate()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new AlwaysFailsHandler()) { Timeout = TimeSpan.FromMilliseconds(50) });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Crawler:VpnRestartPauseMs"] = "0",
        }).Build();
        return new VpnReadinessGate(factory.Object, config, NullLogger<VpnReadinessGate>.Instance);
    }

    private sealed class AlwaysFailsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new HttpRequestException("kein gluetun im Test");
    }

    /// <summary>Antwortet der Reihe nach mit dem, was ihm gegeben wurde (Antwort oder Ausnahme).</summary>
    private sealed class SequenceHandler(params object[] steps) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var step = steps[Math.Min(Calls, steps.Length - 1)];
            Calls++;
            return step is Exception ex ? Task.FromException<HttpResponseMessage>(ex)
                                        : Task.FromResult((HttpResponseMessage)step);
        }
    }
}
