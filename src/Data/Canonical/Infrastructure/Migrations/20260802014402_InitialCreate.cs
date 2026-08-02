using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Data.Canonical.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "canonical");

        migrationBuilder.CreateTable(
            name: "Customers",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                StateOrRegion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ProductCategories",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductCategories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProductType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "QuarantineEntries",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SyncRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                FailedRule = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuarantineEntries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SalesInvoiceItems",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SalesInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                GrossAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesInvoiceItems", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SalesInvoices",
            schema: "canonical",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Series = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                NetAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                CanceledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                SchemaVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesInvoices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId",
            schema: "canonical",
            table: "Customers",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "canonical",
            table: "Customers",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_TenantId",
            schema: "canonical",
            table: "ProductCategories",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "canonical",
            table: "ProductCategories",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Products_CategoryId",
            schema: "canonical",
            table: "Products",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId",
            schema: "canonical",
            table: "Products",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "canonical",
            table: "Products",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_QuarantineEntries_SyncRunId",
            schema: "canonical",
            table: "QuarantineEntries",
            column: "SyncRunId");

        migrationBuilder.CreateIndex(
            name: "IX_QuarantineEntries_TenantId",
            schema: "canonical",
            table: "QuarantineEntries",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoiceItems_ProductId",
            schema: "canonical",
            table: "SalesInvoiceItems",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoiceItems_SalesInvoiceId",
            schema: "canonical",
            table: "SalesInvoiceItems",
            column: "SalesInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoiceItems_TenantId",
            schema: "canonical",
            table: "SalesInvoiceItems",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoiceItems_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "canonical",
            table: "SalesInvoiceItems",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_CustomerId",
            schema: "canonical",
            table: "SalesInvoices",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_TenantId",
            schema: "canonical",
            table: "SalesInvoices",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "canonical",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        // RLS obrigatória (ADR-007): toda tabela com TenantId nasce protegida, na mesma migration que
        // a cria — mesmo padrão de tenant/connector/identity. Todas as 6 tabelas deste schema
        // (incluindo QuarantineEntries, que não é um CanonicalEntity mas ainda carrega TenantId)
        // entram na mesma policy.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION canonical.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY canonical.CanonicalAccessPolicy
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Customers,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Customers AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Customers AFTER UPDATE,
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.ProductCategories,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.ProductCategories AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.ProductCategories AFTER UPDATE,
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Products,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Products AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.Products AFTER UPDATE,
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoices,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoices AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoices AFTER UPDATE,
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoiceItems,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoiceItems AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.SalesInvoiceItems AFTER UPDATE,
            ADD FILTER PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.QuarantineEntries,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.QuarantineEntries AFTER INSERT,
            ADD BLOCK PREDICATE canonical.fn_TenantAccessPredicate(TenantId) ON canonical.QuarantineEntries AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A policy/função precisam ser removidas antes das tabelas que protegem.
        migrationBuilder.Sql("DROP SECURITY POLICY canonical.CanonicalAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION canonical.fn_TenantAccessPredicate;");

        migrationBuilder.DropTable(
            name: "Customers",
            schema: "canonical");

        migrationBuilder.DropTable(
            name: "ProductCategories",
            schema: "canonical");

        migrationBuilder.DropTable(
            name: "Products",
            schema: "canonical");

        migrationBuilder.DropTable(
            name: "QuarantineEntries",
            schema: "canonical");

        migrationBuilder.DropTable(
            name: "SalesInvoiceItems",
            schema: "canonical");

        migrationBuilder.DropTable(
            name: "SalesInvoices",
            schema: "canonical");
    }
}
