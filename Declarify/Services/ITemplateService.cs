using Declarify.Models;
using Declarify.Models.ViewModels;

namespace Declarify.Services
{
    public interface ITemplateService
    {
        // Get all templates ordered by creation date
        Task<IEnumerable<Template>> GetAllAsync();

        // Get active templates only
        Task<IEnumerable<Template>> GetActiveTemplatesAsync();

        // Get template by ID
        Task<Template> GetByIdAsync(int id);

        // Create new template from definition
        Task<Template> CreateAsync(TemplateDefinition definition);

        // Update existing template
        Task<Template> UpdateAsync(int id, TemplateDefinition definition);

        // Delete template
        Task DeleteAsync(int id);

        // Publish template
        Task<Template> PublishTemplateAsync(int id);

        // Archive template
        Task<Template> ArchiveTemplateAsync(int id);

        // Get template configuration as object
        Task<TemplateDefinition> GetDefinitionAsync(int id);

        // Seed standard template
        Task SeedStandardTemplateAsync();

        // Get non-compliant count
        Task<int> GetNonCompliantCountAsync();

        // Get compliant count
        Task<int> GetCompliantCountAsync();

        // Get compliance percentage
        Task<int> GetCompliancePercentageAsync();
    }
}
