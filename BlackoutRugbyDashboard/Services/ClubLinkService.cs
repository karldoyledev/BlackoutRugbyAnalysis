using System.Security.Claims;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

/// <summary>The failure/success classes of a Club Link attempt (D3, pinned error copy).</summary>
public enum ClubLinkStatus
{
    Linked,
    WrongMemberKey,
    UnknownMemberId,
    MemberAlreadyLinked,
    DeveloperCredentialsMissing,
    ApiUnreachable,
    ApiRejected,
    BlankInput,
    NotSignedIn
}

/// <summary>Outcome of one Club Link attempt: the class, its pinned copy, and what the probe found.</summary>
public sealed record ClubLinkResult(ClubLinkStatus Status, string Message, int? TeamId = null, string? Username = null)
{
    public bool Success => Status == ClubLinkStatus.Linked;
}

/// <summary>The signed-in User's decrypted member credentials for API reads, when linked.</summary>
public sealed record MemberCredentials(int MemberId, string MemberKey, int TeamId);

/// <summary>The link status shown on Settings: Member, Team, linked-at, and the Team's cached name.</summary>
public sealed record ClubLinkState(int MemberId, int TeamId, DateTime LinkedAt, string? TeamName);

/// <summary>
/// The Club Link state machine (D3): two states only, Unlinked → Linked. Linking
/// live-validates on submit with one r=m probe (+ developer credentials), binds
/// MemberId + TeamId, and encrypts the Member Key at rest. Unlinking keeps the
/// account and the entire Match Cache. A broken Member Key is a runtime condition
/// on a Linked user (decision 2's hard-error panel), never a persisted state.
/// </summary>
public class ClubLinkService(
    DashboardDbContext db,
    IBlackoutRugbyApiClientFactory apiFactory,
    IMemberKeyProtector keyProtector,
    IHttpContextAccessor httpContextAccessor,
    IOptions<DeveloperOptions> developer,
    BlackoutRugbyResponseAdapter adapter)
{
    private DashboardUser? _currentUser;
    private bool _currentUserResolved;

    /// <summary>True when the signed-in User is Linked. Anonymous → false.</summary>
    public bool IsLinked => GetLinkState() is not null;

    /// <summary>
    /// The link status of the signed-in User, or null when anonymous/unlinked.
    /// The Team name comes from the Match Cache's TeamFacts when seen — zero API calls.
    /// </summary>
    public ClubLinkState? GetLinkState()
    {
        var user = ResolveCurrentUser();
        if (user?.MemberId is not int memberId)
        {
            return null;
        }

        var teamId = user.TeamId ?? 0;
        var teamName = db.TeamFacts
            .Where(fact => fact.TeamId == teamId)
            .OrderByDescending(fact => fact.CapturedAt)
            .Select(fact => (string?)fact.Name)
            .FirstOrDefault();

        return new ClubLinkState(memberId, teamId, user.LinkedAt ?? DateTime.UtcNow, teamName);
    }

    /// <summary>
    /// The signed-in User's decrypted member credentials, or null when
    /// anonymous/unlinked/unrecoverable. Program.cs builds the per-request
    /// default client from this.
    /// </summary>
    public MemberCredentials? ResolveCurrentMemberCredentials()
    {
        var user = ResolveCurrentUser();
        if (user?.MemberId is not int memberId || user.TeamId is not int teamId)
        {
            return null;
        }

        var memberKey = keyProtector.Unprotect(user.EncryptedMemberKey);
        return memberKey is null ? null : new MemberCredentials(memberId, memberKey, teamId);
    }

    /// <summary>
    /// Validates a Member ID + Member Key pair live (one r=m probe) and, on
    /// success, binds the Club Link. Blank input never reaches the wire; a Member
    /// already bound to another User is refused by copy (and again by the store's
    /// unique index); missing developer credentials are refused before the probe.
    /// Re-linking the same User (rotated key) is always allowed.
    /// </summary>
    public async Task<ClubLinkResult> LinkAsync(int memberId, string? memberKey, CancellationToken cancellationToken = default)
    {
        var user = ResolveCurrentUser();
        if (user is null)
        {
            return new ClubLinkResult(ClubLinkStatus.NotSignedIn, "Sign in first — the Club Link belongs to your dashboard account.");
        }

        if (memberId <= 0 || string.IsNullOrWhiteSpace(memberKey))
        {
            return new ClubLinkResult(ClubLinkStatus.BlankInput, "Enter both a Member ID and a Member Key.");
        }

        var memberTaken = await db.Users
            .AnyAsync(other => other.MemberId == memberId && other.Id != user.Id, cancellationToken);
        if (memberTaken)
        {
            return new ClubLinkResult(ClubLinkStatus.MemberAlreadyLinked, "That Member is already linked to another dashboard account.");
        }

        var dev = developer.Value;
        if (dev.DeveloperId <= 0 || string.IsNullOrWhiteSpace(dev.DeveloperKey) || string.IsNullOrWhiteSpace(dev.DeveloperIV))
        {
            return new ClubLinkResult(
                ClubLinkStatus.DeveloperCredentialsMissing,
                "Developer credentials are missing on this machine. Set Developer:DeveloperId, Developer:DeveloperKey and Developer:DeveloperIV (user-secrets) and try again.");
        }

        string xml;
        try
        {
            var client = apiFactory.CreateForMember(memberId, memberKey);
            xml = await client.GetMemberAsync(memberId);
        }
        catch (HttpRequestException)
        {
            return new ClubLinkResult(ClubLinkStatus.ApiUnreachable, "The game API could not be reached. Try again in a moment.");
        }
        catch (TaskCanceledException)
        {
            return new ClubLinkResult(ClubLinkStatus.ApiUnreachable, "The game API could not be reached. Try again in a moment.");
        }

        var adapterError = adapter.ExtractResponseError(xml);
        if (adapterError is not null)
        {
            return ClassifyApiError(adapterError);
        }

        var member = adapter.ParseMember(xml);
        if (member is null || member.Id != memberId)
        {
            return new ClubLinkResult(ClubLinkStatus.UnknownMemberId, "No Member with that ID was found. Check the Member ID in the game UI and try again.");
        }

        user.MemberId = member.Id;
        user.TeamId = member.TeamId;
        user.EncryptedMemberKey = keyProtector.Protect(memberKey);
        user.LinkedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new ClubLinkResult(
            ClubLinkStatus.Linked,
            $"Club linked — Member {member.Id} ({member.Username}) bound to Team {member.TeamId}.",
            member.TeamId,
            member.Username);
    }

    /// <summary>
    /// Removes the Club Link (Settings): wipes the member fields and the encrypted
    /// key; the account and the entire Match Cache survive (D3).
    /// </summary>
    public async Task UnlinkAsync(CancellationToken cancellationToken = default)
    {
        var user = ResolveCurrentUser();
        if (user is null)
        {
            return;
        }

        user.MemberId = null;
        user.TeamId = null;
        user.EncryptedMemberKey = null;
        user.LinkedAt = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps an API error to the pinned failure classes (D3). The exact upstream
    /// error strings for a wrong key vs. an unknown Member ID are not live-verified
    /// yet (probes are rate-limit-gated), so classification keys on the error text
    /// with a defensive fallback that carries the raw text; 'No data requested' is
    /// the recorded transient global API state (R5 F5) and maps to retry guidance.
    /// </summary>
    private ClubLinkResult ClassifyApiError(string errorText)
    {
        var text = errorText.Trim();
        var normalized = text.ToLowerInvariant();

        if (normalized.Contains("no data requested"))
        {
            return new ClubLinkResult(ClubLinkStatus.ApiUnreachable, "The game API answered 'No data requested' — a recorded transient state. Wait a moment and try again.");
        }

        if (normalized.Contains("key") || normalized.Contains("credential") || normalized.Contains("password"))
        {
            return new ClubLinkResult(ClubLinkStatus.WrongMemberKey, "That Member Key was rejected. Check it in the game UI — it changes whenever the in-game password changes — and try again.");
        }

        if (normalized.Contains("member") || normalized.Contains("user"))
        {
            return new ClubLinkResult(ClubLinkStatus.UnknownMemberId, "No Member with that ID was found. Check the Member ID in the game UI and try again.");
        }

        return new ClubLinkResult(ClubLinkStatus.ApiRejected, $"The API rejected the link: {text}");
    }

    private DashboardUser? ResolveCurrentUser()
    {
        if (_currentUserResolved)
        {
            return _currentUser;
        }

        _currentUserResolved = true;
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _currentUser = userId is null ? null : db.Users.FirstOrDefault(row => row.Id == userId);
        return _currentUser;
    }
}