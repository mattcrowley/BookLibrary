-- Staging table: one row per line in the dump, essentially untouched.
-- No constraints/indexes here on purpose - they only slow down the bulk load.
-- Add them after loading if you need to query staging directly.

CREATE TABLE dbo.OL_Works_Staging (
    StagingId     BIGINT IDENTITY PRIMARY KEY,   -- lets us batch the normalize step later
    RecordType    VARCHAR(50)     NOT NULL,
    RecordKey     VARCHAR(100)    NOT NULL,
    Revision      INT             NULL,
    LastModified  DATETIME2       NULL,
    RawJson       NVARCHAR(MAX)   NOT NULL,
    SourceLine    BIGINT          NOT NULL,       -- line number in the original file, for troubleshooting
    LoadedAtUtc   DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Add after the bulk load completes (much faster than maintaining it during insert):
-- CREATE UNIQUE INDEX IX_OL_Works_Staging_RecordKey ON dbo.OL_Works_Staging (RecordKey);

-- Where rows that don't parse into 5 fields, or fail JSON validation, get logged
-- instead of aborting the whole load.
CREATE TABLE dbo.OL_Works_ImportErrors (
    Id           BIGINT IDENTITY PRIMARY KEY,
    SourceLine   BIGINT         NOT NULL,
    RawLine      NVARCHAR(MAX)  NOT NULL,
    ErrorMessage NVARCHAR(1000) NOT NULL,
    LoggedAtUtc  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
