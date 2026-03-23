using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Currere_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperimentLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodeContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DatasetReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    OutputMetrics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperimentLogs");
        }
    }
}
