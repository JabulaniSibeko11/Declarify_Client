using Declarify.Models;

namespace Declarify.Services
{
    public interface IUserService
    {
       
        Task<Employee?> GetCurrentEmployeeAsync();
        Task<ApplicationUser?> GetCurrentUserAsync();
        Task<string> GetCurrentUserRoleAsync();
        Task<bool> IsFirstLoginAsync(string email);
        Task CompleteFirstTimeSetupAsync(
            string email,
            string password,
            string? signatureBase64 = null); // For Reviewer/Admin signature upload
        Task CreateOrUpdateAuthUserAsync(
            string email,
            string? password = null,
            string role = "Employee",
            string? signatureBase64 = null);
        Task UpdateSignatureAsync(string email, string signatureBase64);
        Task<string?> GetSignatureAsync(string email);
        Task<bool> IsCurrentUserAdminAsync();
        Task<bool> IsCurrentUserReviewerAsync();
    }
}
