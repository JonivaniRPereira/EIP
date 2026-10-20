SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Executado somente em cópia descartável: a transação permite ler todos os tenants
-- para calcular hashes e desfaz a desativação temporária de políticas no final.
DECLARE @policies nvarchar(max) = (
    SELECT SCHEMA_NAME(p.schema_id) AS [schema], p.name, p.is_enabled,
        JSON_QUERY((SELECT sp.predicate_definition, sp.predicate_type_desc, sp.operation_desc,
            OBJECT_SCHEMA_NAME(sp.target_object_id) AS targetSchema,
            OBJECT_NAME(sp.target_object_id) AS targetTable
            FROM sys.security_predicates sp WHERE sp.object_id = p.object_id
            ORDER BY sp.security_predicate_id FOR JSON PATH, INCLUDE_NULL_VALUES)) AS predicates
    FROM sys.security_policies p ORDER BY SCHEMA_NAME(p.schema_id), p.name
    FOR JSON PATH, INCLUDE_NULL_VALUES);
CREATE TABLE #Manifest (SchemaName sysname, TableName sysname, [RowCount] bigint, DataHash varchar(64));
BEGIN TRANSACTION;
DECLARE @schema sysname, @name sysname, @sql nvarchar(max), @id int;
DECLARE policies CURSOR LOCAL FAST_FORWARD FOR
    SELECT SCHEMA_NAME(schema_id), name FROM sys.security_policies WHERE is_enabled = 1;
OPEN policies;
FETCH NEXT FROM policies INTO @schema, @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER SECURITY POLICY ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@name) + N' WITH (STATE = OFF);';
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM policies INTO @schema, @name;
END;
CLOSE policies;
DEALLOCATE policies;

DECLARE tables CURSOR LOCAL FAST_FORWARD FOR
    SELECT SCHEMA_NAME(schema_id), name, object_id FROM sys.tables WHERE is_ms_shipped = 0
    ORDER BY SCHEMA_NAME(schema_id), name;
OPEN tables;
FETCH NEXT FROM tables INTO @schema, @name, @id;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @order nvarchar(max), @payload nvarchar(max), @rows bigint;
    SET @order = NULL;
    SELECT @order = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N',')
        WITHIN GROUP (ORDER BY ic.key_ordinal)
    FROM sys.indexes i
    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = @id AND i.is_primary_key = 1 AND ic.key_ordinal > 0;
    IF @order IS NULL THROW 51000, 'Tabela sem chave primaria: manifesto deterministico indisponivel.', 1;
    SET @sql = N'SELECT @rows=COUNT_BIG(*) FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@name)
        + N'; SELECT @payload=(SELECT * FROM ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@name)
        + N' ORDER BY ' + @order + N' FOR JSON PATH, INCLUDE_NULL_VALUES);';
    EXEC sys.sp_executesql @sql, N'@rows bigint OUTPUT, @payload nvarchar(max) OUTPUT', @rows OUTPUT, @payload OUTPUT;
    INSERT #Manifest VALUES (@schema, @name, @rows, CONVERT(varchar(64), HASHBYTES('SHA2_256', @payload), 2));
    FETCH NEXT FROM tables INTO @schema, @name, @id;
END;
CLOSE tables;
DEALLOCATE tables;

DECLARE @manifest nvarchar(max) = (
    SELECT JSON_QUERY(@policies) AS policies,
        JSON_QUERY((SELECT * FROM #Manifest ORDER BY SchemaName, TableName FOR JSON PATH)) AS tables
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
ROLLBACK TRANSACTION;
SELECT @manifest;
