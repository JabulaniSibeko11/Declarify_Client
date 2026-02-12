using Declarify.Models;

namespace Declarify.Services
{
    public interface IDoiPdfService
    {
        Task<(string FileName, string FullPath)> GenerateAndSaveAsync(FormSubmission submission, CancellationToken ct);

    }
}
