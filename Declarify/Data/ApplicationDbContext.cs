using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>

    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Core business entities (from PRD Section 6)
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<FormTask> DOITasks { get; set; }
        public DbSet<FormSubmission> DOIFormSubmissions { get; set; }
        public DbSet<Credit> Credits { get; set; }
        public DbSet<License> Licenses { get; set; }
        public DbSet<VerificationResult> VerificationResults { get; set; }
        public DbSet<OrganizationalDomain> OrganizationalDomains { get; set; }

        public DbSet<VerificationAttachment> VerificationAttachments { get; set; }
        // Supporting entities


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>()
       .HasOne(e => e.ApplicationUser)
       .WithOne(u => u.Employee)
       .HasForeignKey<ApplicationUser>(u => u.EmployeeId); // <-- Explicit FK


            // Employee hierarchy
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeId);
                entity.HasIndex(e => e.Email_Address);

                entity.HasOne(e => e.Manager)
        .WithMany(m => m.Subordinates)
        .HasForeignKey(e => e.ManagerId)
        .OnDelete(DeleteBehavior.NoAction);

            });

            // Template
            modelBuilder.Entity<Template>(entity =>
            {
                entity.HasKey(t => t.TemplateId);
                entity.Property(t => t.TemplateConfig)
                      .IsRequired()
                      .HasColumnType("nvarchar(max)"); // JSON string
            });

            // DOITask
            modelBuilder.Entity<FormTask>(entity =>
            {
                entity.HasKey(t => t.TaskId);
                entity.Property(t => t.Status).HasDefaultValue("Outstanding");

                entity.HasOne(t => t.Employee)
                      .WithMany(e => e.DOITasks)
                      .HasForeignKey(t => t.EmployeeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Template)
                      .WithMany()
                      .HasForeignKey(t => t.TemplateId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // DOIFormSubmission
            modelBuilder.Entity<FormSubmission>(entity =>
            {
                entity.HasKey(s => s.SubmissionId);

                entity.Property(s => s.FormData)
                      .IsRequired()
                      .HasColumnType("nvarchar(max)"); // JSON string

                entity.HasOne(s => s.Task)
                      .WithOne()
                      .HasForeignKey<FormSubmission>(s => s.FormTaskId)
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Self-reference (Amendment)
                entity.HasOne(s => s.AmendsSubmission)
                      .WithMany()
                      .HasForeignKey(s => s.AmendmentOfSubmissionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Credit
            modelBuilder.Entity<Credit>(entity =>
            {
                entity.HasKey(c => c.CreditId);
                entity.HasIndex(c => c.LoadDate);
                entity.HasIndex(c => c.ExpiryDate);
            });

            // VerificationResult
            modelBuilder.Entity<VerificationResult>(entity =>
            {
                entity.HasKey(v => v.VerificationId);
                entity.Property(v => v.ResultData).HasColumnType("nvarchar(max)");

                entity.HasOne(v => v.Submission)
                      .WithMany(s => s.VerificationResults)
                      .HasForeignKey(v => v.SubmissionId)
                      .OnDelete(DeleteBehavior.Cascade);

            });

            // OrganizationalDomain
            modelBuilder.Entity<OrganizationalDomain>(entity =>
            {
                entity.HasIndex(d => d.DomainName).IsUnique();
            });

            modelBuilder.Entity<VerificationAttachment>(entity =>
            {
                entity.HasKey(v => v.VerificationId);

                entity.Property(v => v.Type)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(v => v.ResultJson)
                      .IsRequired();

                entity.Property(v => v.VerifiedDate)
                      .HasDefaultValueSql("GETUTCDATE()"); // Use GETUTCDATE() for SQL Server

                // Main relationship: Submission → Attachments (keep CASCADE — safe)
                entity.HasOne(v => v.Submission)
                      .WithMany(s => s.VerificationAttachments)
                      .HasForeignKey(v => v.SubmissionId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Problematic FK: InitiatedBy → Employee
                // Change to NO ACTION to avoid multiple cascade paths
                entity.HasOne(v => v.InitiatedBy)
                      .WithMany() // No navigation collection needed back
                      .HasForeignKey(v => v.InitiatedByEmployeeId)
                      .OnDelete(DeleteBehavior.NoAction); // ← THIS FIXES THE ERROR
            });

            // Ensure SubmissionId is indexed for performance
            modelBuilder.Entity<VerificationAttachment>()
                .HasIndex(v => v.SubmissionId);

            

        }
    }
}
