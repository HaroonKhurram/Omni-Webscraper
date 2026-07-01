-- ========== FILE: DatabaseSetup.sql ==========
-- ========================================================================
-- SQL SERVER DATABASE SETUP SCRIPT
-- WebScrape Cron: Background Dataset Builder
-- ========================================================================
-- Run this script in SQL Server Management Studio (SSMS) to create:
--   1. Database: WebScrapeCronDB
--   2. Tables: ScrapeTargets, GatheredData, FieldMappings
--   3. Indexes for efficient querying
--   NO seed data is inserted. Add targets through the application UI.
-- ========================================================================

USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'WebScrapeCronDB')
BEGIN
    ALTER DATABASE WebScrapeCronDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE WebScrapeCronDB;
END
GO

CREATE DATABASE WebScrapeCronDB;
GO

USE WebScrapeCronDB;
GO

-- ========================================================================
-- TABLE: ScrapeTargets
-- ========================================================================
CREATE TABLE ScrapeTargets
(
    Id                      INT IDENTITY(1,1) PRIMARY KEY,
    Url                     NVARCHAR(500) NOT NULL,
    HtmlNodePath            NVARCHAR(500) NULL,
    JsonPropertyPath        NVARCHAR(200) NULL,
    Label                   NVARCHAR(100) NOT NULL,
    Frequency               NVARCHAR(50) DEFAULT 'daily',
    IsActive                BIT DEFAULT 1,
    SourceType              NVARCHAR(20) DEFAULT 'html',
    PaginationNextLinkXPath NVARCHAR(500) NULL,
    MaxRecords              INT NULL,
    PortionType             NVARCHAR(20) DEFAULT 'scheduling',
    RowXPath                NVARCHAR(500) NULL,
    CreatedAt               DATETIME2 DEFAULT GETUTCDATE()
);
GO

-- ========================================================================
-- TABLE: GatheredData (staging table)
-- ========================================================================
CREATE TABLE GatheredData
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    TargetId        INT NOT NULL,
    ExtractedValue  NVARCHAR(MAX) NULL,
    DatePulled      DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status          NVARCHAR(50) DEFAULT 'success',
    ErrorMessage    NVARCHAR(MAX) NULL,
    IsProcessed     BIT DEFAULT 0,
    CONSTRAINT FK_GatheredData_ScrapeTargets
        FOREIGN KEY (TargetId) REFERENCES ScrapeTargets(Id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_GatheredData_Target_Date
    ON GatheredData (TargetId, DatePulled DESC);
GO

CREATE NONCLUSTERED INDEX IX_GatheredData_IsProcessed
    ON GatheredData (IsProcessed) WHERE IsProcessed = 0;
GO

-- ========================================================================
-- TABLE: FieldMappings
-- ========================================================================
CREATE TABLE FieldMappings
(
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    TargetId    INT NOT NULL,
    FieldName   NVARCHAR(200) NOT NULL,
    NodePath    NVARCHAR(500) NOT NULL,
    FieldOrder  INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_FieldMappings_ScrapeTargets
        FOREIGN KEY (TargetId) REFERENCES ScrapeTargets(Id) ON DELETE CASCADE
);
GO

-- ========================================================================
-- VERIFICATION
-- ========================================================================
SELECT 'ScrapeTargets' AS TableName, COUNT(*) AS [RowCount] FROM ScrapeTargets;
SELECT 'FieldMappings' AS TableName, COUNT(*) AS [RowCount] FROM FieldMappings;
SELECT 'GatheredData'  AS TableName, COUNT(*) AS [RowCount] FROM GatheredData;

PRINT 'Database setup completed successfully! No seed data inserted.';
PRINT 'Add targets through the application UI.';
GO



SELECT * FROM Clean_Sched_lahore_weather;

SELECT name FROM sys.objects WHERE type IN ('U', 'V');