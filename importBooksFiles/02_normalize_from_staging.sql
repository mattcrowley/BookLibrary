-- ============================================================
-- 1. Normalized target tables
-- ============================================================

CREATE TABLE dbo.Works (
    Id                INT IDENTITY PRIMARY KEY,
    OLKey             VARCHAR(50)     NOT NULL,   -- e.g. /works/OL999903W
    Title             NVARCHAR(MAX)  NULL,
    Subtitle          NVARCHAR(1000)  NULL,
    FirstPublishDate  NVARCHAR(50)    NULL,
    Revision          INT             NULL,
    LastModified      DATETIME2       NULL,
    RawJson           NVARCHAR(MAX)   NOT NULL,   -- kept so any field you didn't
                                                   -- model yet is still queryable
                                                   -- via OPENJSON/JSON_VALUE later
    CONSTRAINT UQ_Works_OLKey UNIQUE (OLKey)
);

-- Authors here just record OpenLibrary's author key; actual author
-- names/bios come from OpenLibrary's separate authors dump, if you load that too.
CREATE TABLE dbo.Authors (
    Id     INT IDENTITY PRIMARY KEY,
    OLKey  VARCHAR(50) NOT NULL,
    CONSTRAINT UQ_Authors_OLKey UNIQUE (OLKey)
);

CREATE TABLE dbo.WorkAuthors (
    WorkId   INT NOT NULL REFERENCES dbo.Works(Id),
    AuthorId INT NOT NULL REFERENCES dbo.Authors(Id),
    PRIMARY KEY (WorkId, AuthorId)
);

CREATE TABLE dbo.Subjects (
    Id   INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(500) NOT NULL,
    CONSTRAINT UQ_Subjects_Name UNIQUE (Name)
);

CREATE TABLE dbo.WorkSubjects (
    WorkId    INT NOT NULL REFERENCES dbo.Works(Id),
    SubjectId INT NOT NULL REFERENCES dbo.Subjects(Id),
    PRIMARY KEY (WorkId, SubjectId)
);

GO

-- ============================================================
-- 2. Batched extraction from staging
--    Run this repeatedly (or wrap in a loop / scheduled job) until
--    it reports 0 rows processed. Batching avoids one 40M-row
--    transaction and lets you stop/resume safely.
-- ============================================================

DECLARE @BatchSize INT = 100000;
DECLARE @LastId BIGINT = 0;         -- persist this between runs if you stop partway
DECLARE @RowsThisBatch INT = 1;

WHILE @RowsThisBatch > 0
BEGIN
    ;WITH Batch AS (
        SELECT TOP (@BatchSize) *
        FROM dbo.OL_Works_Staging
        WHERE StagingId > @LastId
        ORDER BY StagingId
    )
    -- 2a. Works (only rows with a title; adjust if you want to keep titleless rows)
    INSERT INTO dbo.Works (OLKey, Title, Subtitle, FirstPublishDate, Revision, LastModified, RawJson)
    SELECT
        b.RecordKey,
        JSON_VALUE(b.RawJson, '$.title'),
        JSON_VALUE(b.RawJson, '$.subtitle'),
        JSON_VALUE(b.RawJson, '$.first_publish_date'),
        b.Revision,
        b.LastModified,
        b.RawJson
    FROM Batch b
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Works w WHERE w.OLKey = b.RecordKey);

    SET @RowsThisBatch = @@ROWCOUNT;

    -- 2b. Authors + WorkAuthors (nested array: authors[].author.key)
    INSERT INTO dbo.Authors (OLKey)
    SELECT DISTINCT a.AuthorKey
    FROM dbo.OL_Works_Staging b
    CROSS APPLY OPENJSON(b.RawJson, '$.authors') WITH (
        AuthorKey NVARCHAR(100) '$.author.key'
    ) a
    WHERE b.StagingId > @LastId AND b.StagingId <= @LastId + @BatchSize
      AND a.AuthorKey IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Authors ex WHERE ex.OLKey = a.AuthorKey);

    INSERT INTO dbo.WorkAuthors (WorkId, AuthorId)
    SELECT DISTINCT w.Id, au.Id
    FROM dbo.OL_Works_Staging b
    JOIN dbo.Works w ON w.OLKey = b.RecordKey
    CROSS APPLY OPENJSON(b.RawJson, '$.authors') WITH (
        AuthorKey NVARCHAR(100) '$.author.key'
    ) a
    JOIN dbo.Authors au ON au.OLKey = a.AuthorKey
    WHERE b.StagingId > @LastId AND b.StagingId <= @LastId + @BatchSize
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkAuthors ex WHERE ex.WorkId = w.Id AND ex.AuthorId = au.Id);

    -- 2c. Subjects + WorkSubjects (flat string array)
    INSERT INTO dbo.Subjects (Name)
    SELECT DISTINCT s.[value]
    FROM dbo.OL_Works_Staging b
    CROSS APPLY OPENJSON(b.RawJson, '$.subjects') s
    WHERE b.StagingId > @LastId AND b.StagingId <= @LastId + @BatchSize
      AND NOT EXISTS (SELECT 1 FROM dbo.Subjects ex WHERE ex.Name = s.[value]);

    INSERT INTO dbo.WorkSubjects (WorkId, SubjectId)
    SELECT DISTINCT w.Id, sub.Id
    FROM dbo.OL_Works_Staging b
    JOIN dbo.Works w ON w.OLKey = b.RecordKey
    CROSS APPLY OPENJSON(b.RawJson, '$.subjects') s
    JOIN dbo.Subjects sub ON sub.Name = s.[value]
    WHERE b.StagingId > @LastId AND b.StagingId <= @LastId + @BatchSize
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkSubjects ex WHERE ex.WorkId = w.Id AND ex.SubjectId = sub.Id);

    SELECT @LastId = MAX(StagingId) FROM dbo.OL_Works_Staging WHERE StagingId <= @LastId + @BatchSize;

    PRINT CONCAT('Processed through StagingId ', @LastId, ' - Works inserted this batch: ', @RowsThisBatch);
END
