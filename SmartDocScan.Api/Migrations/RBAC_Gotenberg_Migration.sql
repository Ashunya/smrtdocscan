-- SmartDocScan Business Documents RBAC & OCR Migration
-- Run this script against your [dms] database to create the necessary tables and columns.

-- 1. Create user_locations mapping table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[user_locations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[user_locations](
        [username] [varchar](320) NOT NULL,
        [location_id] [int] NOT NULL,
        CONSTRAINT [PK_user_locations] PRIMARY KEY CLUSTERED 
        (
            [username] ASC,
            [location_id] ASC
        )
    )
END
GO

-- 2. Create user_document_types mapping table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[user_document_types]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[user_document_types](
        [username] [varchar](320) NOT NULL,
        [document_type_id] [int] NOT NULL,
        CONSTRAINT [PK_user_document_types] PRIMARY KEY CLUSTERED 
        (
            [username] ASC,
            [document_type_id] ASC
        )
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
