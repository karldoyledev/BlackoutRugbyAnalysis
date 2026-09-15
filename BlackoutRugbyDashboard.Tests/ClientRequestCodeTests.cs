using System.Net;
using BlackoutRugby.Api;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Pins the wire contract of the corrected request codes (R1/R2/R5): the client
/// must send r=lu for lineups, r=ms for match summaries, and r=t for teams.
/// </summary>
public class ClientRequestCodeTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<blackoutrugby_api_response />")
            });
        }
    }

    [Fact]
    public async Task GetLineupsAsync_SendsTheLiveRequestCode()
    {
        var handler = new RecordingHandler();
        var client = new BlackoutRugbyApiClient(new HttpClient(handler), "http://example.test");

        await client.GetLineupsAsync(45047, fixtureId: 21416928);

        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("r=lu", uri);
        Assert.Contains("teamid=45047", uri);
        Assert.Contains("fixtureid=21416928", uri);
    }

    [Fact]
    public async Task GetMatchSummaryAsync_SendsTheLiveRequestCode_WithBatchSupport()
    {
        var handler = new RecordingHandler();
        var client = new BlackoutRugbyApiClient(new HttpClient(handler), "http://example.test");

        await client.GetMatchSummaryAsync(fixtureIds: "21416925,21416928");

        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("r=ms", uri);
        Assert.Contains("fixtureids=21416925", uri);
    }

    [Fact]
    public async Task GetTeamsAsync_SendsTheTeamsRequestCode_WithBatchSupport()
    {
        var handler = new RecordingHandler();
        var client = new BlackoutRugbyApiClient(new HttpClient(handler), "http://example.test");

        await client.GetTeamsAsync(teamIds: "45037,44520");

        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("r=t", uri);
        Assert.Contains("teamids=45037", uri);
    }

    [Fact]
    public async Task GetTeamsAsync_SendsTheTeamsRequestCode_ForASingleTeam()
    {
        var handler = new RecordingHandler();
        var client = new BlackoutRugbyApiClient(new HttpClient(handler), "http://example.test");

        await client.GetTeamsAsync(teamId: 45047);

        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("r=t", uri);
        Assert.Contains("teamid=45047", uri);
    }

    [Fact]
    public async Task GetMemberAsync_SendsTheVerifiedMemberProbe()
    {
        var handler = new RecordingHandler();
        var client = new BlackoutRugbyApiClient(new HttpClient(handler), "http://example.test");

        await client.GetMemberAsync(1333);

        var uri = Assert.Single(handler.RequestUris);
        Assert.Contains("r=m", uri);
        Assert.Contains("memberid=1333", uri);
    }
}
