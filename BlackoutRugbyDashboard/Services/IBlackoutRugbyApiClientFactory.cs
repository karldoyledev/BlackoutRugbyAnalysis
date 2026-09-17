using BlackoutRugby.Api;
using BlackoutRugbyDashboard.Models;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Builds API clients over the one HTTP-boundary seam (spec Testing Decisions §4).
/// Every client carries the developer credentials (required on every read, R2 F2);
/// member credentials ride per purpose — none for credential-free reads, candidate
/// credentials for the one Club Link probe, and the signed-in User's linked
/// credentials for the per-request default client registered in Program.cs.
/// </summary>
public interface IBlackoutRugbyApiClientFactory
{
    IBlackoutRugbyApiClient CreateDeveloperOnly();

    IBlackoutRugbyApiClient CreateForMember(int memberId, string memberKey);

    /// <summary>
    /// The full 17-endpoint concrete client for the API Playground (D2:
    /// auth-only, not link-required). Carries developer credentials plus the
    /// given member credentials when the signed-in User is linked.
    /// </summary>
    BlackoutRugbyApiClient CreateFullSurface(int? memberId = null, string? memberKey = null);
}

public sealed class BlackoutRugbyApiClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<DashboardDefaultsOptions> defaults,
    IOptions<DeveloperOptions> developer) : IBlackoutRugbyApiClientFactory
{
    public IBlackoutRugbyApiClient CreateDeveloperOnly() => Build(null, null);

    public IBlackoutRugbyApiClient CreateForMember(int memberId, string memberKey) => Build(memberId, memberKey);

    public BlackoutRugbyApiClient CreateFullSurface(int? memberId = null, string? memberKey = null) => Build(memberId, memberKey);

    private BlackoutRugbyApiClient Build(int? memberId, string? memberKey)
    {
        var endpoint = string.IsNullOrWhiteSpace(defaults.Value.BaseEndpoint)
            ? "http://classic-api.blackoutrugby.com"
            : defaults.Value.BaseEndpoint;

        return new BlackoutRugbyApiClient(
            httpClientFactory.CreateClient(),
            endpoint,
            new BlackoutRugbyApiCredentials(memberId, memberKey)
            {
                DeveloperId = developer.Value.DeveloperId,
                DeveloperKey = developer.Value.DeveloperKey,
                DeveloperIV = developer.Value.DeveloperIV
            });
    }
}