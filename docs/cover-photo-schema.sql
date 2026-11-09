-- Apply before deploying an API version that reads Profiles.CoverImageUrl.
-- Idempotent SQL Server schema update; the Development API applies the same change on startup.
IF COL_LENGTH(N'dbo.Profiles', N'CoverImageUrl') IS NULL
    ALTER TABLE [dbo].[Profiles] ADD [CoverImageUrl] nvarchar(1000) NULL;
GO
