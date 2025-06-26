using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitoringSystem.Data;

namespace MonitoringSystem.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // Untuk menampilkan error jika email tidak ditemukan
        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "New password is required.")]
            [StringLength(100, ErrorMessage = "The new password must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet()
        {
            // Kosong, hanya untuk render form
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Email tidak ditemukan, tampilkan error
                ModelState.AddModelError(string.Empty, "Email not found.");
                return Page();
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetPassResult = await _userManager.ResetPasswordAsync(user, resetToken, Input.NewPassword);

            if (resetPassResult.Succeeded)
            {
                // Redirect ke halaman login setelah berhasil reset password
                return RedirectToPage("/Account/Login", new { area = "Identity", message = "Password has been reset successfully. Please log in with your new password." });
            }
            else
            {
                foreach (var error in resetPassResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }
    }
}
