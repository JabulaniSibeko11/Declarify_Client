using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Declarify.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixedAmendementColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DOIFormSubmission");

            migrationBuilder.AddColumn<int>(
                name: "AmendmentOfSubmissionId",
                table: "DOIFormSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNo",
                table: "DOIFormSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DOIFormSubmissions_AmendmentOfSubmissionId",
                table: "DOIFormSubmissions",
                column: "AmendmentOfSubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DOIFormSubmissions_DOIFormSubmissions_AmendmentOfSubmissionId",
                table: "DOIFormSubmissions",
                column: "AmendmentOfSubmissionId",
                principalTable: "DOIFormSubmissions",
                principalColumn: "SubmissionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DOIFormSubmissions_DOIFormSubmissions_AmendmentOfSubmissionId",
                table: "DOIFormSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_DOIFormSubmissions_AmendmentOfSubmissionId",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "AmendmentOfSubmissionId",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "VersionNo",
                table: "DOIFormSubmissions");

            migrationBuilder.CreateTable(
                name: "DOIFormSubmission",
                columns: table => new
                {
                    SubmissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmendsSubmissionId = table.Column<int>(type: "int", nullable: true),
                    AttestationSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false)
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
    }
}
