using Declarify.Data;
using Declarify.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _db;

        public UserService(
          UserManager<ApplicationUser> userManager,
          SignInManager<ApplicationUser> signInManager,
          IHttpContextAccessor httpContextAccessor,
          ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        // =========================
        // Current User Helpers
        // =========================

        public async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal == null) return null;

            return await _userManager.GetUserAsync(principal);
        }

        public async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return null;

            return await _db.Employees
                .FirstOrDefaultAsync(e => e.Email_Address == user.Email);
        }
        public async Task<string> GetCurrentUserRoleAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return string.Empty;

            var roles = await _userManager.GetRolesAsync(user);
            return roles.FirstOrDefault() ?? string.Empty;
        }
        public async Task<bool> IsCurrentUserAdminAsync()
        {
            var user = await GetCurrentUserAsync();
            return user != null && await _userManager.IsInRoleAsync(user, "Admin");
        }

        public async Task<bool> IsCurrentUserReviewerAsync()
        {
            var user = await GetCurrentUserAsync();
            return user != null && await _userManager.IsInRoleAsync(user, "Reviewer");
        }
        // =========================
        // First Login Logic (PRD FR 4.1.1)
        // =========================

        public async Task<bool> IsFirstLoginAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user != null && !user.EmailConfirmed;
        }

        public async Task CompleteFirstTimeSetupAsync(
            string email,
            string password,
            string? signatureBase64 = null)
        {
            var user = await _userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("User not found");

            user.EmailConfirmed = true;

            if (!string.IsNullOrWhiteSpace(signatureBase64))
            {
                user.Signature = signatureBase64;
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, password);

            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join(", ",
                    result.Errors.Select(e => e.Description)));

            await _userManager.UpdateAsync(user);
        }
        // =========================
        // User Creation / Sync (PRD bulk employee load)
        // =========================

        public async Task CreateOrUpdateAuthUserAsync(
            string email,
            string? password = null,
            string role = "Employee",
            string? signatureBase64 = null)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    Role = role,
                    EmailConfirmed = false,
                    Signature = signatureBase64
                };

                var createResult = password == null
                    ? await _userManager.CreateAsync(user)
                    : await _userManager.CreateAsync(user, password);

                if (!createResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ",
                        createResult.Errors.Select(e => e.Description)));
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }

        // =========================
        // Signature Management (Reviewer/Admin sign-off)
        // =========================

        public async Task UpdateSignatureAsync(string email, string signatureBase64)
        {
            var user = await _userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("User not found");

            user.Signature = signatureBase64;
            await _userManager.UpdateAsync(user);
        }

        public async Task<string?> GetSignatureAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user?.Signature;
        }

    }
}
