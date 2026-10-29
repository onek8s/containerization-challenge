USE CityPermits;
GO
IF OBJECT_ID(N'permits.CaseDocuments') IS NULL
    CREATE TABLE permits.CaseDocuments AS FILETABLE
    WITH (FILETABLE_DIRECTORY = N'CaseDocuments', FILETABLE_COLLATE_FILENAME = database_default);
GO
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'PermitsDocuments')
    CREATE FULLTEXT CATALOG PermitsDocuments;
GO
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'permits.CaseDocuments'))
BEGIN
    DECLARE @pk sysname = (SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID(N'permits.CaseDocuments') AND is_primary_key = 1);
    EXEC (N'CREATE FULLTEXT INDEX ON permits.CaseDocuments (file_stream TYPE COLUMN file_type LANGUAGE 2057)
          KEY INDEX ' + @pk + N' ON PermitsDocuments WITH CHANGE_TRACKING AUTO');
END
GO
EXEC sp_fulltext_service 'load_os_resources', 1;
EXEC sp_fulltext_service 'verify_signature', 0;
GO
