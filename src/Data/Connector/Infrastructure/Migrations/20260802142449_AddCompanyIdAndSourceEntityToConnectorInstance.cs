using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Data.Connector.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCompanyIdAndSourceEntityToConnectorInstance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CompanyId",
            schema: "connector",
            table: "ConnectorInstances",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "SourceEntity",
            schema: "connector",
            table: "ConnectorInstances",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CompanyId",
            schema: "connector",
            table: "ConnectorInstances");

        migrationBuilder.DropColumn(
            name: "SourceEntity",
            schema: "connector",
            table: "ConnectorInstances");
    }
}
