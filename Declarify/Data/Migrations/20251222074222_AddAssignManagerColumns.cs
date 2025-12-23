using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Declarify.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignManagerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedManagerId",
                table: "DOIFormSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedManagerName",
                table: "DOIFormSubmissions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedManagerId",
                table: "DOIFormSubmissions");

            migrationBuilder.DropColumn(
                name: "AssignedManagerName",
                table: "DOIFormSubmissions");
        }
    }
}
