using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddTenantIdToRefreshToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            schema: "identity",
            table: "RefreshTokens",
            type: "uniqueidentifier",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TenantId",
            schema: "identity",
            table: "RefreshTokens");
    }
}
