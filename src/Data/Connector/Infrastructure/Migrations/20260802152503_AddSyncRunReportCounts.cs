using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Data.Connector.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSyncRunReportCounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AcceptedCount",
            schema: "connector",
            table: "SyncRuns",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DeletedCount",
            schema: "connector",
            table: "SyncRuns",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RejectedCount",
            schema: "connector",
            table: "SyncRuns",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "UpdatedCount",
            schema: "connector",
            table: "SyncRuns",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptedCount",
            schema: "connector",
            table: "SyncRuns");

        migrationBuilder.DropColumn(
            name: "DeletedCount",
            schema: "connector",
            table: "SyncRuns");

        migrationBuilder.DropColumn(
            name: "RejectedCount",
            schema: "connector",
            table: "SyncRuns");

        migrationBuilder.DropColumn(
            name: "UpdatedCount",
            schema: "connector",
            table: "SyncRuns");
    }
}
