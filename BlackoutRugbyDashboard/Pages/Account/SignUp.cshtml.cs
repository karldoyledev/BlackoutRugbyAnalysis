using BlackoutRugbyDashboard.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BlackoutRugbyDashboard.Pages.Account;

/// <summary>
/// Local sign-up (D3): email + password creates the account — no email
/// confirmation, no game credentials. Sign-up lands on LinkClub, which is
/// skippable. Authenticated users have no business here → Home.
/// </summary>
public class SignUpModel(SignInManager<DashboardUser> signInManager, UserManager<DashboardUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Enter an email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a password.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "The password must be at least 8 characters (letters and numbers are enough).")]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new DashboardUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            LockoutEnabled = true
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("LinkClub");
    }
}