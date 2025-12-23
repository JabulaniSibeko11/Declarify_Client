using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Declarify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "VerificationResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "VerificationResults",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedDate",
                table: "DOIFormSubmissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerNotes",
                table: "DOIFormSubmissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewerSignature",
                table: "DOIFormSubmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerificationAttachments",
                columns: table => new
                {
                    VerificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InitiatedByEmployeeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationAttachments", x => x.VerificationId);
                    table.ForeignKey(
                        name: "FK_VerificationAttachments_DOIFormSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "DOIFormSubmissions",
                        principalColumn: "SubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VerificationAttachments_Employees_InitiatedByEmployeeId",
                        column: x => x.InitiatedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificationAttachments_InitiatedByEmployeeId",
                table: "VerificationAttachments",
                column: "InitiatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationAttachments_SubmissionId",
                table: "VerificationAttachments",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerificationAttachments");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "VerificationResults");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "VerificationResults");

            migrationBuilder.DropColumn(
                name: "ReviewedDate",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewerNotes",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewerSignature",
                table: "DOIFormSubmissions");
        }
    }
}
