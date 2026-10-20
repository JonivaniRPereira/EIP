SET NOCOUNT ON;
SET XACT_ABORT OFF;
EXEC sys.sp_set_session_context @key=N'TenantId', @value=NULL;
IF EXISTS (SELECT 1 FROM tenant.Companies) THROW 51010,'RLS sem contexto falhou.',1;
EXEC sys.sp_set_session_context @key=N'TenantId', @value='11111111-1111-1111-1111-111111111111';
IF (SELECT COUNT(*) FROM tenant.Companies)<>1
    OR EXISTS (SELECT 1 FROM tenant.Companies WHERE TenantId<>'11111111-1111-1111-1111-111111111111')
    THROW 51011,'Isolamento entre tenants falhou.',1;
DECLARE @blocked bit=0;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT tenant.Companies (Id,TenantId,Name,DefaultCurrency,Status,CreatedAt,UpdatedAt)
    VALUES (NEWID(),'22222222-2222-2222-2222-222222222222',N'Forbidden',N'BRL',N'Active',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
END TRY
BEGIN CATCH
    IF ERROR_NUMBER()<>33504 BEGIN ROLLBACK TRANSACTION; THROW; END;
    SET @blocked=1;
END CATCH;
ROLLBACK TRANSACTION;
IF @blocked=0 THROW 51012,'INSERT cruzado nao foi bloqueado.',1;
IF EXISTS (SELECT 1 FROM sys.security_policies WHERE is_enabled=0)
    THROW 51013,'Politica RLS desativada.',1;
SELECT 'RLS_ROLLBACK_OK';
