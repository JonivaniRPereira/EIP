using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Intelligence.AI.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ProtectedProviderCredentials : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProtectedCredentials",
            schema: "ai",
            table: "ProviderSettings",
            type: "nvarchar(max)",
            maxLength: 131072,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProtectedCredentials",
            schema: "ai",
            table: "ProviderSettings");
    }
}
