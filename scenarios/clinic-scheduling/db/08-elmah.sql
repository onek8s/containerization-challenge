-- ELMAH 1.2 error log (SqlErrorLog) used by the staff web application.
USE SouthridgeClinic;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID('dbo.ELMAH_Error') IS NULL
BEGIN
    CREATE TABLE dbo.ELMAH_Error (
        ErrorId     uniqueidentifier NOT NULL CONSTRAINT DF_ELMAH_Error_ErrorId DEFAULT (NEWID()),
        Application nvarchar(60)  NOT NULL,
        Host        nvarchar(50)  NOT NULL,
        Type        nvarchar(100) NOT NULL,
        Source      nvarchar(60)  NOT NULL,
        Message     nvarchar(500) NOT NULL,
        [User]      nvarchar(50)  NOT NULL,
        StatusCode  int           NOT NULL,
        TimeUtc     datetime      NOT NULL,
        Sequence    int IDENTITY(1,1) NOT NULL,
        AllXml      ntext         NOT NULL,
        CONSTRAINT PK_ELMAH_Error PRIMARY KEY NONCLUSTERED (ErrorId)
    );
    CREATE NONCLUSTERED INDEX IX_ELMAH_Error_App_Time_Seq ON dbo.ELMAH_Error (Application, TimeUtc DESC, Sequence DESC);
END
GO

IF OBJECT_ID('dbo.ELMAH_GetErrorXml') IS NOT NULL DROP PROCEDURE dbo.ELMAH_GetErrorXml;
GO
CREATE PROCEDURE dbo.ELMAH_GetErrorXml
    @Application nvarchar(60),
    @ErrorId uniqueidentifier
AS
    SET NOCOUNT ON;
    SELECT AllXml FROM dbo.ELMAH_Error WHERE ErrorId = @ErrorId AND Application = @Application;
GO

IF OBJECT_ID('dbo.ELMAH_GetErrorsXml') IS NOT NULL DROP PROCEDURE dbo.ELMAH_GetErrorsXml;
GO
CREATE PROCEDURE dbo.ELMAH_GetErrorsXml
    @Application nvarchar(60),
    @PageIndex int = 0,
    @PageSize int = 15,
    @TotalCount int OUTPUT
AS
    SET NOCOUNT ON;
    DECLARE @FirstTimeUTC datetime, @FirstSequence int, @StartRow int, @StartRowIndex int;
    SELECT @TotalCount = COUNT(1) FROM dbo.ELMAH_Error WHERE Application = @Application;
    SET @StartRowIndex = @PageIndex * @PageSize + 1;
    IF @StartRowIndex <= @TotalCount
    BEGIN
        SET ROWCOUNT @StartRowIndex;
        SELECT @FirstTimeUTC = TimeUtc, @FirstSequence = Sequence FROM dbo.ELMAH_Error
        WHERE Application = @Application ORDER BY TimeUtc DESC, Sequence DESC;
    END
    ELSE
    BEGIN
        SET @PageSize = 0;
    END
    SET ROWCOUNT @PageSize;
    SELECT errorId = ErrorId, application = Application, host = Host, type = Type, source = Source, message = Message,
           [user] = [User], statusCode = StatusCode, time = CONVERT(varchar(50), TimeUtc, 126) + 'Z'
    FROM dbo.ELMAH_Error error
    WHERE Application = @Application AND TimeUtc <= @FirstTimeUTC AND Sequence <= @FirstSequence
    ORDER BY TimeUtc DESC, Sequence DESC
    FOR XML AUTO;
GO

IF OBJECT_ID('dbo.ELMAH_LogError') IS NOT NULL DROP PROCEDURE dbo.ELMAH_LogError;
GO
CREATE PROCEDURE dbo.ELMAH_LogError
    @ErrorId uniqueidentifier, @Application nvarchar(60), @Host nvarchar(30), @Type nvarchar(100), @Source nvarchar(60),
    @Message nvarchar(500), @User nvarchar(50), @AllXml ntext, @StatusCode int, @TimeUtc datetime
AS
    SET NOCOUNT ON;
    INSERT INTO dbo.ELMAH_Error (ErrorId, Application, Host, Type, Source, Message, [User], AllXml, StatusCode, TimeUtc)
    VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @AllXml, @StatusCode, @TimeUtc);
GO
