using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Tenant.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCompanyCountryCodeAndTradeName : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CountryCode",
            schema: "tenant",
            table: "Companies",
            type: "nvarchar(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TradeName",
            schema: "tenant",
            table: "Companies",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CountryCode",
            schema: "tenant",
            table: "Companies");

        migrationBuilder.DropColumn(
            name: "TradeName",
            schema: "tenant",
            table: "Companies");
    }
}
