using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAuditEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEvents",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_OccurredAt",
            schema: "identity",
            table: "AuditEvents",
            column: "OccurredAt");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_UserId",
            schema: "identity",
            table: "AuditEvents",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditEvents",
            schema: "identity");
    }
}
