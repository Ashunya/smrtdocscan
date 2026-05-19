IF OBJECT_ID('dbo.auth_otp_challenge', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.auth_otp_challenge (
        challenge_id uniqueidentifier NOT NULL CONSTRAINT PK_auth_otp_challenge PRIMARY KEY,
        username varchar(50) NOT NULL,
        code_hash nvarchar(255) NOT NULL,
        purpose varchar(30) NOT NULL,
        expires_on datetime2 NOT NULL,
        consumed_on datetime2 NULL,
        created_on datetime2 NOT NULL CONSTRAINT DF_auth_otp_challenge_created_on DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_auth_otp_challenge_username
        ON dbo.auth_otp_challenge(username, purpose, expires_on);
END;

IF OBJECT_ID('dbo.company_identity_tenant', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.company_identity_tenant (
        comp_id int NOT NULL,
        provider varchar(30) NOT NULL,
        tenant_id nvarchar(80) NOT NULL,
        tenant_name nvarchar(200) NULL,
        enabled bit NOT NULL CONSTRAINT DF_company_identity_tenant_enabled DEFAULT 1,
        created_on datetime2 NOT NULL CONSTRAINT DF_company_identity_tenant_created_on DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_company_identity_tenant PRIMARY KEY (comp_id, provider, tenant_id)
    );
END;

IF OBJECT_ID('dbo.user_external_login', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.user_external_login (
        username varchar(50) NOT NULL,
        provider varchar(30) NOT NULL,
        tenant_id nvarchar(80) NOT NULL,
        subject_id nvarchar(120) NOT NULL,
        email nvarchar(255) NULL,
        created_on datetime2 NOT NULL CONSTRAINT DF_user_external_login_created_on DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_user_external_login PRIMARY KEY (provider, tenant_id, subject_id)
    );

    CREATE INDEX IX_user_external_login_username
        ON dbo.user_external_login(username);
END;

-- Add one row per customer Microsoft tenant.
-- INSERT INTO dbo.company_identity_tenant (comp_id, provider, tenant_id, tenant_name, enabled)
-- VALUES (7, 'microsoft', '<customer-tenant-guid>', 'Arcadia Surgery Center', 1);
