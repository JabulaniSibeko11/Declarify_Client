using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Declarify.Data.Migrations
{
    /// <inheritdoc />
    public partial class addMoreColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmendmentReason",
                table: "DOITasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AmendmentRequestedAt",
                table: "DOITasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendmentRequestedBy",
                table: "DOITasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAmendmentRequired",
                table: "DOITasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PdfFileName",
                table: "DOIFormSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfFilePath",
                table: "DOIFormSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PdfGeneratedUtc",
                table: "DOIFormSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DOIFormSubmission",
                columns: table => new
                {
                    SubmissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    FormData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttestationSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    AmendsSubmissionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DOIFormSubmission", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_DOIFormSubmission_DOIFormSubmission_AmendsSubmissionId",
                        column: x => x.AmendsSubmissionId,
                        principalTable: "DOIFormSubmission",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DOIFormSubmission_AmendsSubmissionId",
                table: "DOIFormSubmission",
                column: "AmendsSubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DOIFormSubmission");

            migrationBuilder.DropColumn(
                name: "AmendmentReason",
                table: "DOITasks");

            migrationBuilder.DropColumn(
                name: "AmendmentRequestedAt",
                table: "DOITasks");

            migrationBuilder.DropColumn(
                name: "AmendmentRequestedBy",
                table: "DOITasks");

            migrationBuilder.DropColumn(
                name: "IsAmendmentRequired",
                table: "DOITasks");

            migrationBuilder.DropColumn(
                name: "PdfFileName",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "PdfFilePath",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "PdfGeneratedUtc",
                table: "DOIFormSubmissions");
        }
    }
}
