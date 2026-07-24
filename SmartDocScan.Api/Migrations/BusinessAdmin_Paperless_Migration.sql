IF COL_LENGTH('dbo.business_documents', 'doc_type_id') IS NULL ALTER TABLE dbo.business_documents ADD doc_type_id INT NULL;
IF COL_LENGTH('dbo.business_documents', 'corresp_id') IS NULL ALTER TABLE dbo.business_documents ADD corresp_id INT NULL;
IF COL_LENGTH('dbo.business_documents', 'asn') IS NULL ALTER TABLE dbo.business_documents ADD asn INT NULL;
IF COL_LENGTH('dbo.business_documents', 'content') IS NULL ALTER TABLE dbo.business_documents ADD content NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.business_documents', 'title') IS NULL ALTER TABLE dbo.business_documents ADD title NVARCHAR(255) NULL;
GO

IF OBJECT_ID('dbo.business_tags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.business_tags (
        tag_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_tags PRIMARY KEY,
        comp_id INT NOT NULL,
        name NVARCHAR(255) NOT NULL,
        color NVARCHAR(50) NOT NULL CONSTRAINT DF_business_tags_color DEFAULT '#c1692a',
        match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_tags_match_alg DEFAULT 'any',
        match_pattern NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_business_tags_comp ON dbo.business_tags(comp_id);
END
GO

IF OBJECT_ID('dbo.business_document_tags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.business_document_tags (
        doc_id INT NOT NULL,
        tag_id INT NOT NULL,
        CONSTRAINT PK_business_document_tags PRIMARY KEY (doc_id, tag_id)
    );
    CREATE INDEX IX_business_document_tags_tag ON dbo.business_document_tags(tag_id);
END
GO

IF OBJECT_ID('dbo.business_correspondents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.business_correspondents (
        corresp_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_business_correspondents PRIMARY KEY,
        comp_id INT NOT NULL,
        name NVARCHAR(255) NOT NULL,
        match_algorithm NVARCHAR(50) NOT NULL CONSTRAINT DF_business_correspondents_match_alg DEFAULT 'any',
        match_pattern NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_business_correspondents_comp ON dbo.business_correspondents(comp_id);
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
