using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class TemplateService: ITemplateService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<TemplateService> _logger;
        public TemplateService(ApplicationDbContext context, ILogger<TemplateService> logger)
        {
            _db = context;
            _logger = logger;
        }
        //Get all templates ordered by creation date

        public async Task<IEnumerable<Template>> GetAllAsync()
        {
            return await _db.Templates
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();
        }
        // Get active templates only (for bulk request selection)
        public async Task<IEnumerable<Template>> GetActiveTemplatesAsync()
        {
            return await _db.Templates
                .Where(t => t.Status == "Active")
                .OrderBy(t => t.TemplateName)
                .ToListAsync();
        }
        // Get template by ID
        public async Task<Template> GetByIdAsync(int id)
        {
            var template = await _db.Templates.FindAsync(id);
            if (template == null)
            {
                throw new NotFoundException($"Template with ID {id} not found");
            }
            return template;
        }
        // Create new template from definition (FR 4.2.1)
        public async Task<Template> CreateAsync(TemplateDefinition definition)
        {
            var template = new Template
            {
                TemplateName = definition.TemplateName,
                Description = definition.Description,
                TemplateConfig = System.Text.Json.JsonSerializer.Serialize(definition.Config),
                Status = "Draft",
                CreatedDate = DateTime.UtcNow
            };

            _db.Templates.Add(template);
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Created new template: {template.TemplateName} (ID: {template.TemplateId})");
            return template;
        }

        // Update existing template (FR 4.2.1)
        public async Task<Template> UpdateAsync(int id, TemplateDefinition definition)
        {
            var template = await GetByIdAsync(id);

            template.TemplateName = definition.TemplateName;
            template.Description = definition.Description;
            template.TemplateConfig = System.Text.Json.JsonSerializer.Serialize(definition.Config);

            await _db.SaveChangesAsync();
            _logger.LogInformation($"Updated template: {template.TemplateName} (ID: {template.TemplateId})");

            return template;
        }
        // Delete template (if not in use)
        public async Task DeleteAsync(int id)
        {
            var template = await GetByIdAsync(id);

            // Check if template is being used in any tasks
            var isInUse = await _db.DOITasks.AnyAsync(t => t.TemplateId == id);

            if (isInUse)
            {
                throw new InvalidOperationException($"Cannot delete template {id} - it is being used in DOI tasks");
            }

            _db.Templates.Remove(template);
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Deleted template: {template.TemplateName} (ID: {template.TemplateId})");
        }
        // Publish template (change status from Draft to Active)
        public async Task<Template> PublishTemplateAsync(int id)
        {
            var template = await GetByIdAsync(id);
            template.Status = "Active";

            await _db.SaveChangesAsync();
            _logger.LogInformation($"Published template: {template.TemplateName} (ID: {template.TemplateId})");

            return template;
        }
        // Archive template (change status to Archived)
        public async Task<Template> ArchiveTemplateAsync(int id)
        {
            var template = await GetByIdAsync(id);
            template.Status = "Archived";

            await _db.SaveChangesAsync();
            _logger.LogInformation($"Archived template: {template.TemplateName} (ID: {template.TemplateId})");

            return template;
        }
        // Get template configuration as object (FR 4.2.1)
        public async Task<TemplateDefinition> GetDefinitionAsync(int id)
        {
            var template = await GetByIdAsync(id);

            var config = System.Text.Json.JsonSerializer.Deserialize<TemplateConfig>(template.TemplateConfig ?? "{}");

            return new TemplateDefinition
            {
                TemplateName = template.TemplateName ?? "",
                Description = template.Description,
                Config = config ?? new TemplateConfig()
            };
        }
        // Seed standard City of Johannesburg template on first run (FR 4.2.2)
        // CRITICAL: This must be called during application startup
        public async Task SeedStandardTemplateAsync()
        {
            var existingStandard = await _db.Templates
                .FirstOrDefaultAsync(t => t.TemplateName == "Standard DOI - City of Johannesburg");

            if (existingStandard != null)
            {
                _logger.LogInformation("Standard template already exists, skipping seed");
                return;
            }

            var standardConfig = new TemplateConfig
            {
                Sections = new List<TemplateSection>
                {
                    new TemplateSection
                    {
                        SectionId = "personal_info",
                        SectionTitle = "Personal Information",
                        SectionOrder = 1,
                        Fields = new List<TemplateField>
                        {
                            new TemplateField { FieldId = "full_name", FieldLabel = "Full Name", FieldType = "text", Required = true, Order = 1 },
                            new TemplateField { FieldId = "employee_number", FieldLabel = "Employee Number", FieldType = "text", Required = true, Order = 2 },
                            new TemplateField { FieldId = "position", FieldLabel = "Position", FieldType = "text", Required = true, Order = 3 },
                            new TemplateField { FieldId = "department", FieldLabel = "Department", FieldType = "text", Required = true, Order = 4 }
                        }
                    },
                    new TemplateSection
                    {
                        SectionId = "shares_securities",
                        SectionTitle = "Shares and Other Financial Interests",
                        SectionOrder = 2,
                        Disclaimer = "Declare all shares and securities held directly or indirectly",
                        Fields = new List<TemplateField>
                        {
                            new TemplateField { FieldId = "has_shares", FieldLabel = "Do you hold any shares or securities?", FieldType = "boolean", Required = true, Order = 1 },
                            new TemplateField { FieldId = "shares_details", FieldLabel = "Details of shares/securities", FieldType = "textarea", Required = false, Order = 2, ConditionalOn = "has_shares" }
                        }
                    },
                    new TemplateSection
                    {
                        SectionId = "directorships",
                        SectionTitle = "Directorships and Partnerships",
                        SectionOrder = 3,
                        Fields = new List<TemplateField>
                        {
                            new TemplateField { FieldId = "has_directorships", FieldLabel = "Are you a director of any company?", FieldType = "boolean", Required = true, Order = 1 },
                            new TemplateField { FieldId = "directorship_details", FieldLabel = "Company name and registration details", FieldType = "textarea", Required = false, Order = 2, ConditionalOn = "has_directorships" }
                        }
                    },
                    new TemplateSection
                    {
                        SectionId = "gifts_hospitality",
                        SectionTitle = "Gifts and Hospitality",
                        SectionOrder = 4,
                        Disclaimer = "Declare gifts or hospitality exceeding R500 in value",
                        Fields = new List<TemplateField>
                        {
                            new TemplateField { FieldId = "received_gifts", FieldLabel = "Have you received any gifts or hospitality?", FieldType = "boolean", Required = true, Order = 1 },
                            new TemplateField { FieldId = "gift_details", FieldLabel = "Details of gifts/hospitality", FieldType = "textarea", Required = false, Order = 2, ConditionalOn = "received_gifts" }
                        }
                    },
                    new TemplateSection
                    {
                        SectionId = "declaration",
                        SectionTitle = "Declaration and Attestation",
                        SectionOrder = 5,
                        Fields = new List<TemplateField>
                        {
                            new TemplateField { FieldId = "attestation", FieldLabel = "I declare that the information provided is true and complete to the best of my knowledge", FieldType = "checkbox", Required = true, Order = 1 }
                        }
                    }
                }
            };

            var standardTemplate = new Template
            {
                TemplateName = "Standard DOI - City of Johannesburg",
                Description = "Standard Declaration of Interest form based on City of Johannesburg requirements",
                TemplateConfig = System.Text.Json.JsonSerializer.Serialize(standardConfig),
                Status = "Active",
                CreatedDate = DateTime.UtcNow
            };

            _db.Templates.Add(standardTemplate);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded standard DOI template");
        }
        // Get non-compliant count - placeholder for interface requirement
        public async Task<int> GetNonCompliantCountAsync()
        {
            // This should be implemented in FormTaskService instead
            return await _db.DOITasks
                .Where(t => t.Status == "Outstanding" || t.Status == "Overdue")
                .CountAsync();
        }
        //Get compliant count - placeholder for interface requirement
        public async Task<int> GetCompliantCountAsync()
        {
            return await _db.DOITasks
                .Where(t => t.Status == "Submitted" || t.Status == "Reviewed")
                .CountAsync();
        }
        // Get compliance percentage - placeholder for interface requirement
        public async Task<int> GetCompliancePercentageAsync()
        {
            var total = await _db.DOITasks.CountAsync();
            if (total == 0) return 100;

            var compliant = await GetCompliantCountAsync();
            return (int)((double)compliant / total * 100);
        }
    }
}
