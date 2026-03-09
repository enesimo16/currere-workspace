using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Currere_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStudentEmailVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentEmail",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStudentEmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StudentEmail",
                table: "Users");
        }
    }
}
