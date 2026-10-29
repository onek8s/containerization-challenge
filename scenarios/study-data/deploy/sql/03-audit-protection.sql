-- 21 CFR Part 11: audit trail rows can never be changed or removed, also not by dbo.
USE [StudyData];
GO
CREATE OR ALTER TRIGGER [edc].[TR_Audit_Immutable] ON [edc].[Audit]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N'Audit trail entries cannot be changed or deleted.', 1;
END
GO
