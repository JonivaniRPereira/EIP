using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Data.Connector.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddWatermarkToConnectorInstance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastWatermark",
            schema: "connector",
            table: "ConnectorInstances",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastWatermark",
            schema: "connector",
            table: "ConnectorInstances");
    }
}
