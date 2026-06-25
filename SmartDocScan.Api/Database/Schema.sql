IF OBJECT_ID('dbo.audit_log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_log (
        audit_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_audit_log PRIMARY KEY,
        action nvarchar(80) NOT NULL,
        actor nvarchar(100) NULL,
        comp_id int NULL,
        target_type nvarchar(80) NULL,
        target_id nvarchar(160) NULL,
        outcome nvarchar(30) NOT NULL,
        ip_address nvarchar(64) NULL,
        details nvarchar(1000) NULL,
        created_on datetime2 NOT NULL CONSTRAINT DF_audit_log_created_on DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_audit_log_created_on ON dbo.audit_log(created_on DESC);
    CREATE INDEX IX_audit_log_actor ON dbo.audit_log(actor, created_on DESC);
    CREATE INDEX IX_audit_log_company ON dbo.audit_log(comp_id, created_on DESC);
END;

IF OBJECT_ID('dbo.app_setting', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.app_setting (
        setting_key nvarchar(160) NOT NULL CONSTRAINT PK_app_setting PRIMARY KEY,
        setting_value nvarchar(max) NULL,
        updated_on datetime2 NOT NULL CONSTRAINT DF_app_setting_updated_on DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.auth_login_attempt', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.auth_login_attempt (
        username varchar(50) NOT NULL CONSTRAINT PK_auth_login_attempt PRIMARY KEY,
        failed_count int NOT NULL,
        first_failed_on datetime2 NOT NULL,
        last_failed_on datetime2 NOT NULL,
        locked_until datetime2 NULL
    );
END;

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

IF COL_LENGTH('dbo.documents', 'date_of_service') IS NULL
BEGIN
    ALTER TABLE dbo.documents ADD date_of_service date NULL;
END;
