using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samba.ApiServer.Modern.Data.Migrations
{
    /// <inheritdoc />
    public partial class TerminalQueueEventPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TerminalQueueEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReplayOutcome = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConflictReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplayedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminalQueueEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TerminalQueueEvents_CreatedAtUtc",
                table: "TerminalQueueEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalQueueEvents_TerminalId_CorrelationId",
                table: "TerminalQueueEvents",
                columns: new[] { "TerminalId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TerminalQueueEvents_TerminalId_Status",
                table: "TerminalQueueEvents",
                columns: new[] { "TerminalId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerminalQueueEvents");
        }
    }
}
