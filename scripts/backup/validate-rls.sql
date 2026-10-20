SET NOCOUNT ON;
-- Gate estrutural: todas as tabelas de tenant precisam de política ativa.
IF EXISTS (
    SELECT 1 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id AND c.name=N'TenantId'
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.security_predicates sp
        JOIN sys.security_policies p ON p.object_id=sp.object_id AND p.is_enabled=1
        WHERE sp.target_object_id=t.object_id AND sp.predicate_type_desc=N'FILTER'))
    THROW 51001, 'Tabela com TenantId sem RLS ativa.', 1;

-- Duas empresas sintéticas, somente na cópia. Nenhuma alteração persiste.
SET XACT_ABORT OFF;
DECLARE @a uniqueidentifier=NEWID(), @b uniqueidentifier=NEWID(),
    @companyA uniqueidentifier=NEWID(), @companyB uniqueidentifier=NEWID(), @blocked bit=0;
BEGIN TRY
    BEGIN TRANSACTION;
    EXEC sys.sp_set_session_context @key=N'TenantId', @value=@a;
    INSERT tenant.Companies (Id,TenantId,Name,DefaultCurrency,Status,CreatedAt,UpdatedAt,CountryCode)
        VALUES (@companyA,@a,N'Recovery probe A',N'BRL',N'Active',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),N'BR');
    EXEC sys.sp_set_session_context @key=N'TenantId', @value=@b;
    INSERT tenant.Companies (Id,TenantId,Name,DefaultCurrency,Status,CreatedAt,UpdatedAt,CountryCode)
        VALUES (@companyB,@b,N'Recovery probe B',N'BRL',N'Active',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),N'BR');
    EXEC sys.sp_set_session_context @key=N'TenantId', @value=NULL;
    IF EXISTS (SELECT 1 FROM tenant.Companies WHERE Id IN (@companyA,@companyB))
        THROW 51002, 'Sem contexto foi possivel ler dados de tenant.', 1;
    EXEC sys.sp_set_session_context @key=N'TenantId', @value=@a;
    IF (SELECT COUNT(*) FROM tenant.Companies WHERE Id IN (@companyA,@companyB)) <> 1
        OR EXISTS (SELECT 1 FROM tenant.Companies WHERE Id=@companyB)
        THROW 51003, 'Isolamento de leitura entre tenants falhou.', 1;
    BEGIN TRY
        INSERT tenant.Companies (Id,TenantId,Name,DefaultCurrency,Status,CreatedAt,UpdatedAt,CountryCode)
            VALUES (NEWID(),@b,N'Forbidden',N'BRL',N'Active',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),N'BR');
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 33504 THROW;
        SET @blocked=1;
    END CATCH;
    IF @blocked=0 THROW 51004, 'RLS nao bloqueou INSERT de outro tenant.', 1;
    ROLLBACK TRANSACTION;
    EXEC sys.sp_set_session_context @key=N'TenantId', @value=NULL;
    SELECT 'RLS_PROBE_OK';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
