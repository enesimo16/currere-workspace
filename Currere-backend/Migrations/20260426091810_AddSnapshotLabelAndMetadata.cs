using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Currere_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotLabelAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                table: "WorkspaceSnapshots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "WorkspaceSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "WorkspaceSnapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileCount",
                table: "WorkspaceSnapshots");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "WorkspaceSnapshots");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "WorkspaceSnapshots");
        }
    }
}
