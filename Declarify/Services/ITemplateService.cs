using Declarify.Models;
using Declarify.Models.ViewModels;

namespace Declarify.Services
{
    public interface ITemplateService
    {
        Task<IEnumerable<Template>> GetAllAsync();
        Task<Template> GetByIdAsync(int id);
        Task<Template> CreateAsync(TemplateDefinition definition);
        Task<Template> UpdateAsync(int id, TemplateDefinition definition);
        Task DeleteAsync(int id);
        Task<TemplateDefinition> GetDefinitionAsync(int id);
        Task SeedStandardTemplateAsync();

        Task<int> GetNonCompliantCountAsync();
        Task<int> GetCompliantCountAsync();
        Task<IEnumerable<Template>>GetActiveTemplatesAsync();
        Task<int> GetCompliancePercentageAsync();
        Task<Template> PublishTemplateAsync(int id);

        Task<Template> ArchiveTemplateAsync(int id);
    }
}
