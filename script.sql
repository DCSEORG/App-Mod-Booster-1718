-- script.sql
-- Configures managed identity database access for the Expense Management System
-- The MANAGED-IDENTITY-NAME placeholder is replaced by deploy.sh before running

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'mid-AppModAssist-020317')
BEGIN
    DROP USER [mid-AppModAssist-020317];
END

CREATE USER [mid-AppModAssist-020317] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [mid-AppModAssist-020317];
ALTER ROLE db_datawriter ADD MEMBER [mid-AppModAssist-020317];
GRANT EXECUTE TO [mid-AppModAssist-020317];
