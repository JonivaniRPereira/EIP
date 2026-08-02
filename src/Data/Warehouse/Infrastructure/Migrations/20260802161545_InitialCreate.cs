using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EIP.Data.Warehouse.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "warehouse");

        migrationBuilder.CreateTable(
            name: "DimCompany",
            schema: "warehouse",
            columns: table => new
            {
                CompanyKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                LoadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimCompany", x => x.CompanyKey);
            });

        migrationBuilder.CreateTable(
            name: "DimCurrency",
            schema: "warehouse",
            columns: table => new
            {
                CurrencyKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimCurrency", x => x.CurrencyKey);
            });

        migrationBuilder.CreateTable(
            name: "DimCustomer",
            schema: "warehouse",
            columns: table => new
            {
                CustomerKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                StateOrRegion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimCustomer", x => x.CustomerKey);
            });

        migrationBuilder.CreateTable(
            name: "DimDate",
            schema: "warehouse",
            columns: table => new
            {
                DateKey = table.Column<int>(type: "int", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Quarter = table.Column<int>(type: "int", nullable: false),
                Month = table.Column<int>(type: "int", nullable: false),
                Day = table.Column<int>(type: "int", nullable: false),
                DayOfWeek = table.Column<int>(type: "int", nullable: false),
                IsWeekend = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimDate", x => x.DateKey);
            });

        migrationBuilder.CreateTable(
            name: "DimProduct",
            schema: "warehouse",
            columns: table => new
            {
                ProductKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ProductType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CategoryKey = table.Column<int>(type: "int", nullable: true),
                UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsCurrent = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimProduct", x => x.ProductKey);
            });

        migrationBuilder.CreateTable(
            name: "DimProductCategory",
            schema: "warehouse",
            columns: table => new
            {
                ProductCategoryKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimProductCategory", x => x.ProductCategoryKey);
            });

        migrationBuilder.CreateTable(
            name: "DimTenant",
            schema: "warehouse",
            columns: table => new
            {
                TenantKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                LoadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DimTenant", x => x.TenantKey);
            });

        migrationBuilder.CreateTable(
            name: "FactSalesInvoiceItem",
            schema: "warehouse",
            columns: table => new
            {
                FactSalesInvoiceItemKey = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantKey = table.Column<int>(type: "int", nullable: false),
                CompanyKey = table.Column<int>(type: "int", nullable: false),
                DateKey = table.Column<int>(type: "int", nullable: false),
                CustomerKey = table.Column<int>(type: "int", nullable: false),
                ProductKey = table.Column<int>(type: "int", nullable: false),
                CurrencyKey = table.Column<int>(type: "int", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SalesInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SalesInvoiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RawObjectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                LoadBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                LineNumber = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                NetAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                LoadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FactSalesInvoiceItem", x => x.FactSalesInvoiceItemKey);
            });

        migrationBuilder.CreateTable(
            name: "LoadBatches",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ItemsConsideredCount = table.Column<int>(type: "int", nullable: true),
                FactRowsUpsertedCount = table.Column<int>(type: "int", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoadBatches", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DimCompany_TenantId",
            schema: "warehouse",
            table: "DimCompany",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DimCompany_TenantId_CompanyId",
            schema: "warehouse",
            table: "DimCompany",
            columns: new[] { "TenantId", "CompanyId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DimCurrency_Code",
            schema: "warehouse",
            table: "DimCurrency",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DimCustomer_TenantId",
            schema: "warehouse",
            table: "DimCustomer",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DimCustomer_TenantId_CustomerId_CurrentOnly",
            schema: "warehouse",
            table: "DimCustomer",
            columns: new[] { "TenantId", "CustomerId" },
            unique: true,
            filter: "[IsCurrent] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_DimDate_Date",
            schema: "warehouse",
            table: "DimDate",
            column: "Date",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DimProduct_CategoryKey",
            schema: "warehouse",
            table: "DimProduct",
            column: "CategoryKey");

        migrationBuilder.CreateIndex(
            name: "IX_DimProduct_TenantId",
            schema: "warehouse",
            table: "DimProduct",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DimProduct_TenantId_ProductId_CurrentOnly",
            schema: "warehouse",
            table: "DimProduct",
            columns: new[] { "TenantId", "ProductId" },
            unique: true,
            filter: "[IsCurrent] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_DimProductCategory_TenantId",
            schema: "warehouse",
            table: "DimProductCategory",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DimProductCategory_TenantId_CategoryId",
            schema: "warehouse",
            table: "DimProductCategory",
            columns: new[] { "TenantId", "CategoryId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DimTenant_TenantId",
            schema: "warehouse",
            table: "DimTenant",
            column: "TenantId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_CustomerKey",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            column: "CustomerKey");

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_DateKey",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            column: "DateKey");

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_LoadBatchId",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            column: "LoadBatchId");

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_ProductKey",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            column: "ProductKey");

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_TenantId",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_FactSalesInvoiceItem_TenantId_SourceSystemId_SourceEntity_SourceRecordId",
            schema: "warehouse",
            table: "FactSalesInvoiceItem",
            columns: new[] { "TenantId", "SourceSystemId", "SourceEntity", "SourceRecordId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LoadBatches_SourceSystemId",
            schema: "warehouse",
            table: "LoadBatches",
            column: "SourceSystemId");

        migrationBuilder.CreateIndex(
            name: "IX_LoadBatches_TenantId",
            schema: "warehouse",
            table: "LoadBatches",
            column: "TenantId");

        // RLS obrigatória (ADR-007): toda tabela com TenantId nasce protegida, na mesma
        // migration que a cria — mesmo padrão de tenant/connector/canonical/identity. DimDate e
        // DimCurrency ficam de fora: são dado de referência compartilhado, sem TenantId
        // (docs/09-Data-Warehouse.md §5.1/§5.2).
        migrationBuilder.Sql(
            """
            CREATE FUNCTION warehouse.fn_TenantAccessPredicate(@TenantId uniqueidentifier)
            RETURNS TABLE
            WITH SCHEMABINDING
            AS
            RETURN SELECT 1 AS fn_accesspredicate_result
            WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS uniqueidentifier);
            """);

        migrationBuilder.Sql(
            """
            CREATE SECURITY POLICY warehouse.WarehouseAccessPolicy
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimTenant,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimTenant AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimTenant AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCompany,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCompany AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCompany AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCustomer,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCustomer AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimCustomer AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProduct,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProduct AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProduct AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProductCategory,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProductCategory AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.DimProductCategory AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.FactSalesInvoiceItem,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.FactSalesInvoiceItem AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.FactSalesInvoiceItem AFTER UPDATE,
            ADD FILTER PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.LoadBatches,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.LoadBatches AFTER INSERT,
            ADD BLOCK PREDICATE warehouse.fn_TenantAccessPredicate(TenantId) ON warehouse.LoadBatches AFTER UPDATE
            WITH (STATE = ON);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A policy/função precisam ser removidas antes das tabelas que protegem.
        migrationBuilder.Sql("DROP SECURITY POLICY warehouse.WarehouseAccessPolicy;");
        migrationBuilder.Sql("DROP FUNCTION warehouse.fn_TenantAccessPredicate;");

        migrationBuilder.DropTable(
            name: "DimCompany",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimCurrency",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimCustomer",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimDate",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimProduct",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimProductCategory",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "DimTenant",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "FactSalesInvoiceItem",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "LoadBatches",
            schema: "warehouse");
    }
}
