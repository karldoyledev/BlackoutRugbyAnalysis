using Microsoft.AspNetCore.Identity;

namespace BlackoutRugbyDashboard.Data;

/// <summary>
/// The local dashboard account (D3). Identity carries the email + password; the
/// Club Link rides alongside: the Member id (unique — one Member per User), the
/// Data-Protection-encrypted Member Key, the linked Team id, and when the link
/// was made. Two states only — Unlinked → Linked; a broken Member Key is a
/// runtime condition on a Linked user, never a persisted third state.
/// </summary>
public class DashboardUser : IdentityUser
{
    public int? MemberId { get; set; }

    public string? EncryptedMemberKey { get; set; }

    public int? TeamId { get; set; }

    public DateTime? LinkedAt { get; set; }
}