using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Platform.Tenant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "tenant",
                table: "Memberships",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                schema: "tenant",
                table: "Memberships");
        }
    }
}
