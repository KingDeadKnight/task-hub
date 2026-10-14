using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Project = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RawStatus = table.Column<string>(type: "TEXT", nullable: false),
                    NativePriority = table.Column<int>(type: "INTEGER", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    InactiveSince = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_Source_StartedAt",
                table: "SyncRuns",
                columns: new[] { "Source", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Source_IsActive",
                table: "WorkItems",
                columns: new[] { "Source", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncRuns");

            migrationBuilder.DropTable(
                name: "WorkItems");
        }
    }
}
