using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BlackoutRugbyDashboard.Pages.Account;

/// <summary>
/// Link your club (D3): one r=m probe validates the Member ID + Member Key pair
/// on submit — never per keystroke (rate limits). Success binds the Club Link
/// and lands on Home; every failure class carries its pinned copy. Skippable:
/// an Unlinked user can still reach Home and the API Playground. Linked users
/// belong on Settings (the re-link hatch lives there).
/// </summary>
public class LinkClubModel(ClubLinkService clubLinks) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public class InputModel
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

    public IActionResult OnGet()
    {
        if (clubLinks.IsLinked)
        {
            return RedirectToPage("Settings");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (clubLinks.IsLinked)
        {
            return RedirectToPage("Settings");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await clubLinks.LinkAsync(Input.MemberId!.Value, Input.MemberKey);
        if (result.Success)
        {
            return Redirect("/");
        }

        ErrorMessage = result.Message;
        return Page();
    }
}