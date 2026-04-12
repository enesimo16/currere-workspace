using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Currere_backend.Migrations
{
    /// <inheritdoc />
    public partial class EnsureWorkspaceIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [ExperimentLogs];");

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceId",
                table: "ExperimentLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentLogs_WorkspaceId",
                table: "ExperimentLogs",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExperimentLogs_Workspaces_WorkspaceId",
                table: "ExperimentLogs",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExperimentLogs_Workspaces_WorkspaceId",
                table: "ExperimentLogs");

            migrationBuilder.DropIndex(
                name: "IX_ExperimentLogs_WorkspaceId",
                table: "ExperimentLogs");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "ExperimentLogs");
        }
    }
}
