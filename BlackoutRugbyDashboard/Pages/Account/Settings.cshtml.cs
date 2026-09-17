using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BlackoutRugbyDashboard.Pages.Account;

/// <summary>
/// Settings (D3) — every recovery hatch in one place: link status, the re-link
/// form (a rotated Member Key never locks the User out), unlink (wipes member
/// fields + key; the account and the entire Match Cache survive), D1's manual
/// per-table Match Cache reset, and sign-out (a POST handler — no logout route).
/// </summary>
public class SettingsModel(
    ClubLinkService clubLinks,
    MatchCacheService matchCache,
    SignInManager<BlackoutRugbyDashboard.Data.DashboardUser> signInManager,
    ILogger<SettingsModel> logger) : PageModel
{
    public ClubLinkState? Link { get; private set; }

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    [BindProperty]
    public LinkInputModel LinkInput { get; set; } = new();

    [BindProperty]
    public string? ResetTable { get; set; }

    public class LinkInputModel
    {
        [Required(ErrorMessage = "Enter your Member ID.")]
        [Range(1, int.MaxValue, ErrorMessage = "Enter a valid Member ID (a positive number).")]
        [Display(Name = "Member ID")]
        public int? MemberId { get; set; }

        [Required(ErrorMessage = "Enter your Member Key.")]
        [DataType(DataType.Password)]
        [Display(Name = "Member Key")]
        public string? MemberKey { get; set; }
    }

    public void OnGet()
    {
        Link = clubLinks.GetLinkState();
    }

    public async Task<IActionResult> OnPostLinkAsync()
    {
        if (!ModelState.IsValid)
        {
            Link = clubLinks.GetLinkState();
            return Page();
        }

        var result = await clubLinks.LinkAsync(LinkInput.MemberId!.Value, LinkInput.MemberKey);
        Link = clubLinks.GetLinkState();
        if (result.Success)
        {
            StatusMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUnlinkAsync()
    {
        await clubLinks.UnlinkAsync();
        Link = clubLinks.GetLinkState();
        StatusMessage = "Club unlinked — your account and the entire Match Cache are untouched.";
        return Page();
    }

    public async Task<IActionResult> OnPostResetCacheAsync()
    {
        Link = clubLinks.GetLinkState();
        if (string.IsNullOrWhiteSpace(ResetTable) || !Enum.TryParse<CacheTable>(ResetTable, out var table) || !Enum.IsDefined(table))
        {
            ErrorMessage = "Pick a Match Cache table to reset.";
            return Page();
        }

        try
        {
            var deleted = await matchCache.ResetTableAsync(table);
            StatusMessage = $"Match Cache reset — {table}: {deleted} row(s) deleted. The cache re-fills append-only as Fixtures are viewed again.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Match Cache reset failed for table {Table}", table);
            ErrorMessage = $"Reset failed: {exception.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSignOutAsync()
    {
        await signInManager.SignOutAsync();
        return Redirect("/");
    }
}