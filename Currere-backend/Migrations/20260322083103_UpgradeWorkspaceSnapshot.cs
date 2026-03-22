using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Currere_backend.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeWorkspaceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CodeContent",
                table: "WorkspaceSnapshots",
                newName: "ZipFilePath");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "WorkspaceSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "WorkspaceSnapshots");

            migrationBuilder.RenameColumn(
                name: "ZipFilePath",
                table: "WorkspaceSnapshots",
                newName: "CodeContent");
        }
    }
}
