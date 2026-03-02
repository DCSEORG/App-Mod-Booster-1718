-- script.sql
-- Configures managed identity database access for the Expense Management System
-- IMPORTANT: The placeholder 'MANAGED-IDENTITY-NAME' below is replaced by deploy.sh
-- before this script is executed. Do not run this file directly without replacing
-- the placeholder first.

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'MANAGED-IDENTITY-NAME')
BEGIN
    DROP USER [MANAGED-IDENTITY-NAME];
END

CREATE USER [MANAGED-IDENTITY-NAME] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [MANAGED-IDENTITY-NAME];
ALTER ROLE db_datawriter ADD MEMBER [MANAGED-IDENTITY-NAME];
GRANT EXECUTE TO [MANAGED-IDENTITY-NAME];
