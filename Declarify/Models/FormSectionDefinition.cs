namespace Declarify.Models
{
    public class FormSectionDefinition
    {
        /// <summary>
        /// Unique identifier for the section (used by frontend)
        /// </summary>
        public string SectionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Section title shown to users
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Optional description/instructions for this section
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Order of the section in the form
        /// </summary>
        public int Order { get; set; }

    }
}
