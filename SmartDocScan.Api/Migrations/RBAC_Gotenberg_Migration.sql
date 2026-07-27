-- SmartDocScan Business Documents RBAC & OCR Migration
-- Run this script against your [dms] database to create the necessary tables and columns.

-- 0. Ensure target tables exist (these are typically auto-created by the API)
IF OBJECT_ID('dbo.company_location', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.company_location (
        location_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_company_location PRIMARY KEY,
        comp_id INT NOT NULL,
        location_name NVARCHAR(150) NOT NULL,
        location_code NVARCHAR(50) NULL,
        address NVARCHAR(250) NULL,
        phone NVARCHAR(50) NULL,
        inactive BIT NOT NULL CONSTRAINT DF_company_location_inactive DEFAULT 0,
        created_on DATETIME2 NOT NULL CONSTRAINT DF_company_location_created DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_company_location_comp ON dbo.company_location(comp_id);
END
GO

IF OBJECT_ID('dbo.business_document_types', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.business_document_types (
        doc_type_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_document_types PRIMARY KEY,
        comp_id INT NOT NULL,
        name NVARCHAR(255) NOT NULL,
        match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_document_types_match_alg DEFAULT 'any',
        match_pattern NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_business_document_types_comp ON dbo.business_document_types(comp_id);
END
GO

-- 1. Create user_locations mapping table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[user_locations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[user_locations](
        [username] [varchar](50) NOT NULL,
        [location_id] [int] NOT NULL,
        CONSTRAINT [PK_user_locations] PRIMARY KEY CLUSTERED 
        (
            [username] ASC,
            [location_id] ASC
        ),
        CONSTRAINT [FK_user_locations_location] FOREIGN KEY([location_id])
        REFERENCES [dbo].[company_location] ([location_id])
        ON DELETE CASCADE,
        CONSTRAINT [FK_user_locations_username] FOREIGN KEY([username])
        REFERENCES [dbo].[usersinfo] ([username])
        ON DELETE CASCADE
    )
END
GO

-- 2. Create user_document_types mapping table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[user_document_types]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[user_document_types](
        [username] [varchar](50) NOT NULL,
        [document_type_id] [int] NOT NULL,
        CONSTRAINT [PK_user_document_types] PRIMARY KEY CLUSTERED 
        (
            [username] ASC,
            [document_type_id] ASC
        ),
        CONSTRAINT [FK_user_doc_types_doctype] FOREIGN KEY([document_type_id])
        REFERENCES [dbo].[business_document_types] ([doc_type_id])
        ON DELETE CASCADE,
        CONSTRAINT [FK_user_doc_types_username] FOREIGN KEY([username])
        REFERENCES [dbo].[usersinfo] ([username])
        ON DELETE CASCADE
    )
END
GO

-- 3. Add extracted_text column to business_documents for full-text search
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[business_documents]') 
    AND name = 'extracted_text'
)
BEGIN
    ALTER TABLE [dbo].[business_documents] ADD [extracted_text] NVARCHAR(MAX) NULL
END
GO
