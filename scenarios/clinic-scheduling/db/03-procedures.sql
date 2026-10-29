-- Southridge Clinic Systems: stored procedures (version 6.4)
USE SouthridgeClinic;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-------------------------------------------------------------------------------
-- Numbers, settings, audit, jobs
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_NextNumber') IS NOT NULL DROP PROCEDURE dbo.usp_NextNumber;
GO
CREATE PROCEDURE dbo.usp_NextNumber
    @Name varchar(30),
    @Count int = 1,
    @FirstValue bigint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @t TABLE (NextValue bigint);
    UPDATE dbo.NumberSequence SET NextValue = NextValue + @Count
    OUTPUT deleted.NextValue INTO @t
    WHERE Name = @Name;
    IF @@ROWCOUNT = 0
    BEGIN
        RAISERROR('Number sequence %s does not exist.', 16, 1, @Name);
        RETURN;
    END
    SELECT @FirstValue = NextValue FROM @t;
END
GO

IF OBJECT_ID('dbo.fn_ConfirmationCode') IS NOT NULL DROP FUNCTION dbo.fn_ConfirmationCode;
GO
-- Eight characters without 0/O/1/I so the code can be read over the phone.
CREATE FUNCTION dbo.fn_ConfirmationCode (@random varbinary(8))
RETURNS char(8)
AS
BEGIN
    DECLARE @alphabet char(32) = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    DECLARE @code varchar(8) = '', @i int = 1;
    WHILE @i <= 8
    BEGIN
        SET @code = @code + SUBSTRING(@alphabet, (CAST(SUBSTRING(@random, @i, 1) AS int) % 32) + 1, 1);
        SET @i = @i + 1;
    END
    RETURN @code;
END
GO

IF OBJECT_ID('dbo.usp_AppSetting_List') IS NOT NULL DROP PROCEDURE dbo.usp_AppSetting_List;
GO
CREATE PROCEDURE dbo.usp_AppSetting_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Name, Value, Description, ModifiedBy, ModifiedOn FROM dbo.AppSetting ORDER BY Name;
END
GO

IF OBJECT_ID('dbo.usp_AppSetting_Save') IS NOT NULL DROP PROCEDURE dbo.usp_AppSetting_Save;
GO
CREATE PROCEDURE dbo.usp_AppSetting_Save
    @Name varchar(100), @Value nvarchar(400), @UserName nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AppSetting SET Value = @Value, ModifiedBy = @UserName, ModifiedOn = GETDATE() WHERE Name = @Name;
    IF @@ROWCOUNT = 0
        INSERT dbo.AppSetting (Name, Value, ModifiedBy, ModifiedOn) VALUES (@Name, @Value, @UserName, GETDATE());
END
GO

IF OBJECT_ID('dbo.usp_Audit_Write') IS NOT NULL DROP PROCEDURE dbo.usp_Audit_Write;
GO
CREATE PROCEDURE dbo.usp_Audit_Write
    @UserName nvarchar(256), @Action varchar(40), @EntityType varchar(40), @EntityId varchar(40),
    @PatientId int = NULL, @Details nvarchar(1000) = NULL, @ClientAddress varchar(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.AuditLog (UserName, Action, EntityType, EntityId, PatientId, Details, ClientAddress)
    VALUES (@UserName, @Action, @EntityType, @EntityId, @PatientId, @Details, @ClientAddress);
END
GO

IF OBJECT_ID('dbo.usp_Job_Start') IS NOT NULL DROP PROCEDURE dbo.usp_Job_Start;
GO
CREATE PROCEDURE dbo.usp_Job_Start
    @JobName varchar(40), @MachineName varchar(64), @JobRunId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.JobRun (JobName, StartedOn, MachineName) VALUES (@JobName, GETDATE(), @MachineName);
    SET @JobRunId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_Job_Finish') IS NOT NULL DROP PROCEDURE dbo.usp_Job_Finish;
GO
CREATE PROCEDURE dbo.usp_Job_Finish
    @JobRunId int, @Succeeded bit, @Summary nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.JobRun SET FinishedOn = GETDATE(), Succeeded = @Succeeded, Summary = @Summary WHERE JobRunId = @JobRunId;
END
GO

IF OBJECT_ID('dbo.usp_ApiClient_Validate') IS NOT NULL DROP PROCEDURE dbo.usp_ApiClient_Validate;
GO
CREATE PROCEDURE dbo.usp_ApiClient_Validate
    @ApiKeyHash char(64), @ClientType varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ApiClient SET LastUsedOn = GETDATE()
    OUTPUT inserted.ApiClientId, inserted.Name, inserted.ClientType, inserted.SiteId
    WHERE ApiKeyHash = @ApiKeyHash AND IsActive = 1 AND (@ClientType IS NULL OR ClientType = @ClientType);
END
GO

-------------------------------------------------------------------------------
-- Patients
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Patient_Search') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_Search;
GO
CREATE PROCEDURE dbo.usp_Patient_Search
    @LastName nvarchar(50) = NULL,
    @FirstName nvarchar(50) = NULL,
    @DateOfBirth date = NULL,
    @Mrn char(9) = NULL,
    @Phone varchar(20) = NULL,
    @SortExpression varchar(40) = NULL,
    @StartRowIndex int = 0,
    @MaximumRows int = 25,
    @TotalRows int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @TotalRows = COUNT(*)
    FROM dbo.Patient p
    WHERE p.IsActive = 1
      AND (@LastName IS NULL OR p.LastName LIKE @LastName + '%')
      AND (@FirstName IS NULL OR p.FirstName LIKE @FirstName + '%')
      AND (@DateOfBirth IS NULL OR p.DateOfBirth = @DateOfBirth)
      AND (@Mrn IS NULL OR p.Mrn = @Mrn)
      AND (@Phone IS NULL OR p.HomePhone = @Phone OR p.MobilePhone = @Phone);

    WITH rows AS (
        SELECT p.PatientId, p.Mrn, p.FirstName, p.LastName, p.DateOfBirth, p.Sex, p.HomePhone, p.MobilePhone, p.City, p.Balance,
               s.Name AS PreferredSite,
               (SELECT MAX(a.StartsAt) FROM dbo.Appointment a WHERE a.PatientId = p.PatientId AND a.Status = 4) AS LastVisit,
               (SELECT MIN(a.StartsAt) FROM dbo.Appointment a WHERE a.PatientId = p.PatientId AND a.Status IN (0,1) AND a.StartsAt >= GETDATE()) AS NextAppointment,
               ROW_NUMBER() OVER (ORDER BY
                    CASE WHEN @SortExpression = 'Mrn' THEN p.Mrn END,
                    CASE WHEN @SortExpression = 'DateOfBirth' THEN p.DateOfBirth END,
                    CASE WHEN @SortExpression = 'Balance DESC' THEN p.Balance END DESC,
                    p.LastName, p.FirstName, p.DateOfBirth) AS RowNumber
        FROM dbo.Patient p
        LEFT JOIN dbo.Site s ON s.SiteId = p.PreferredSiteId
        WHERE p.IsActive = 1
          AND (@LastName IS NULL OR p.LastName LIKE @LastName + '%')
          AND (@FirstName IS NULL OR p.FirstName LIKE @FirstName + '%')
          AND (@DateOfBirth IS NULL OR p.DateOfBirth = @DateOfBirth)
          AND (@Mrn IS NULL OR p.Mrn = @Mrn)
          AND (@Phone IS NULL OR p.HomePhone = @Phone OR p.MobilePhone = @Phone)
    )
    SELECT PatientId, Mrn, FirstName, LastName, DateOfBirth, Sex, HomePhone, MobilePhone, City, Balance, PreferredSite, LastVisit, NextAppointment
    FROM rows
    WHERE RowNumber > @StartRowIndex AND RowNumber <= @StartRowIndex + @MaximumRows
    ORDER BY RowNumber;
END
GO

IF OBJECT_ID('dbo.usp_Patient_Create') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_Create;
GO
CREATE PROCEDURE dbo.usp_Patient_Create
    @FirstName nvarchar(50), @MiddleName nvarchar(50) = NULL, @LastName nvarchar(50), @DateOfBirth date, @Sex tinyint,
    @Address1 nvarchar(100) = NULL, @Address2 nvarchar(100) = NULL, @City nvarchar(50) = NULL, @State char(2) = NULL, @Zip varchar(10) = NULL,
    @HomePhone varchar(20) = NULL, @MobilePhone varchar(20) = NULL, @Email nvarchar(256) = NULL,
    @PreferredSiteId int = NULL, @HospitalId varchar(20) = NULL, @PortalUserName nvarchar(256) = NULL,
    @CreatedBy nvarchar(256),
    @PatientId int OUTPUT, @Mrn char(9) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @seq bigint;
    EXEC dbo.usp_NextNumber 'MRN', 1, @seq OUTPUT;

    -- MRN = 7 digits + '-' + Luhn check digit computed over the 8 digit zero-padded number.
    DECLARE @body char(8) = RIGHT('00000000' + CAST(@seq AS varchar(8)), 8);
    DECLARE @sum int = 0, @i int = 8, @d int, @dbl bit = 1;
    WHILE @i >= 1
    BEGIN
        SET @d = CAST(SUBSTRING(@body, @i, 1) AS int);
        IF @dbl = 1 BEGIN SET @d = @d * 2; IF @d > 9 SET @d = @d - 9; END
        SET @sum = @sum + @d;
        SET @dbl = 1 - @dbl;
        SET @i = @i - 1;
    END
    SET @Mrn = SUBSTRING(@body, 2, 7) + '-' + CAST((10 - (@sum % 10)) % 10 AS char(1));

    INSERT dbo.Patient (Mrn, FirstName, MiddleName, LastName, DateOfBirth, Sex, Address1, Address2, City, State, Zip,
                        HomePhone, MobilePhone, Email, PreferredSiteId, HospitalId, PortalUserName, CreatedBy)
    VALUES (@Mrn, @FirstName, @MiddleName, @LastName, @DateOfBirth, @Sex, @Address1, @Address2, @City, @State, @Zip,
            @HomePhone, @MobilePhone, @Email, @PreferredSiteId, @HospitalId, @PortalUserName, @CreatedBy);
    SET @PatientId = SCOPE_IDENTITY();
    COMMIT;
END
GO

IF OBJECT_ID('dbo.usp_Patient_FindDuplicates') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_FindDuplicates;
GO
CREATE PROCEDURE dbo.usp_Patient_FindDuplicates
    @LastName nvarchar(50), @DateOfBirth date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PatientId, Mrn, FirstName, LastName, DateOfBirth, HomePhone, MobilePhone, PortalUserName, HospitalId
    FROM dbo.Patient
    WHERE MatchKey = UPPER(@LastName) + '|' + CONVERT(char(8), @DateOfBirth, 112) AND IsActive = 1;
END
GO

IF OBJECT_ID('dbo.usp_Patient_LinkPortalAccount') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_LinkPortalAccount;
GO
CREATE PROCEDURE dbo.usp_Patient_LinkPortalAccount
    @Mrn char(9), @LastName nvarchar(50), @DateOfBirth date, @PortalUserName nvarchar(256), @PatientId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @PatientId = NULL;
    UPDATE dbo.Patient SET PortalUserName = @PortalUserName, @PatientId = PatientId, ModifiedBy = @PortalUserName, ModifiedOn = GETDATE()
    WHERE Mrn = @Mrn AND UPPER(LastName) = UPPER(@LastName) AND DateOfBirth = @DateOfBirth AND PortalUserName IS NULL AND IsActive = 1;
END
GO

IF OBJECT_ID('dbo.usp_Hl7_UpsertPatient') IS NOT NULL DROP PROCEDURE dbo.usp_Hl7_UpsertPatient;
GO
-- Patient demographics from the hospital ADT feed: match on our MRN, the hospital number, then name and date of birth.
CREATE PROCEDURE dbo.usp_Hl7_UpsertPatient
    @Mrn char(9) = NULL, @HospitalId varchar(20) = NULL,
    @FirstName nvarchar(50), @MiddleName nvarchar(50) = NULL, @LastName nvarchar(50), @DateOfBirth date, @Sex tinyint,
    @Address1 nvarchar(100) = NULL, @Address2 nvarchar(100) = NULL, @City nvarchar(50) = NULL, @State char(2) = NULL, @Zip varchar(10) = NULL,
    @HomePhone varchar(20) = NULL, @CreateIfMissing bit = 1,
    @PatientId int OUTPUT, @Action varchar(10) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @PatientId = NULL;
    IF @Mrn IS NOT NULL SELECT @PatientId = PatientId FROM dbo.Patient WHERE Mrn = @Mrn;
    IF @PatientId IS NULL AND @HospitalId IS NOT NULL SELECT TOP 1 @PatientId = PatientId FROM dbo.Patient WHERE HospitalId = @HospitalId ORDER BY PatientId;
    IF @PatientId IS NULL
        SELECT TOP 1 @PatientId = PatientId FROM dbo.Patient
        WHERE MatchKey = UPPER(@LastName) + '|' + CONVERT(char(8), @DateOfBirth, 112) AND LEFT(UPPER(FirstName), 1) = LEFT(UPPER(@FirstName), 1)
        ORDER BY PatientId;

    IF @PatientId IS NOT NULL
    BEGIN
        UPDATE dbo.Patient SET
            FirstName = @FirstName, MiddleName = ISNULL(@MiddleName, MiddleName), LastName = @LastName, Sex = CASE WHEN @Sex = 0 THEN Sex ELSE @Sex END,
            Address1 = ISNULL(@Address1, Address1), Address2 = ISNULL(@Address2, Address2), City = ISNULL(@City, City),
            State = ISNULL(@State, State), Zip = ISNULL(@Zip, Zip), HomePhone = ISNULL(@HomePhone, HomePhone),
            HospitalId = ISNULL(@HospitalId, HospitalId), ModifiedBy = 'interface', ModifiedOn = GETDATE()
        WHERE PatientId = @PatientId;
        SET @Action = 'Updated';
        RETURN;
    END

    IF @CreateIfMissing = 0
    BEGIN
        SET @Action = 'NotFound';
        RETURN;
    END

    DECLARE @newMrn char(9);
    EXEC dbo.usp_Patient_Create @FirstName = @FirstName, @MiddleName = @MiddleName, @LastName = @LastName, @DateOfBirth = @DateOfBirth, @Sex = @Sex,
        @Address1 = @Address1, @Address2 = @Address2, @City = @City, @State = @State, @Zip = @Zip, @HomePhone = @HomePhone,
        @HospitalId = @HospitalId, @CreatedBy = 'interface', @PatientId = @PatientId OUTPUT, @Mrn = @newMrn OUTPUT;
    SET @Action = 'Created';
END
GO

IF OBJECT_ID('dbo.usp_Patient_Statement') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_Statement;
GO
CREATE PROCEDURE dbo.usp_Patient_Statement
    @PatientId int, @FromDate date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PatientId, p.Mrn, p.FirstName, p.LastName, p.Address1, p.Address2, p.City, p.State, p.Zip, p.Balance,
           ISNULL((SELECT TOP 1 s.AmountDue FROM dbo.Statement s WHERE s.PatientId = p.PatientId ORDER BY s.StatementDate DESC), 0) AS PreviousAmountDue,
           ISNULL((SELECT TOP 1 s.DunningLevel FROM dbo.Statement s WHERE s.PatientId = p.PatientId ORDER BY s.StatementDate DESC), 0) AS PreviousDunningLevel
    FROM dbo.Patient p WHERE p.PatientId = @PatientId;

    SELECT e.DateOfService, c.CptCode, pc.Description, c.Units, c.Amount, c.PaidAmount, c.AdjustmentAmount, c.PatientAmount,
           pr.LastName AS ProviderLastName, st.Name AS SiteName
    FROM dbo.Charge c
    JOIN dbo.Encounter e ON e.EncounterId = c.EncounterId
    JOIN dbo.ProcedureCode pc ON pc.CptCode = c.CptCode
    JOIN dbo.Provider pr ON pr.ProviderId = e.ProviderId
    JOIN dbo.Site st ON st.SiteId = e.SiteId
    WHERE e.PatientId = @PatientId AND c.Status <> 5 AND (c.PatientAmount > 0 OR e.DateOfService >= @FromDate)
    ORDER BY e.DateOfService, c.LineNumber;

    SELECT p.PostedOn, p.Source, p.Method, p.Amount, p.Reference
    FROM dbo.Payment p
    WHERE p.PatientId = @PatientId AND p.PostedOn >= @FromDate AND p.Source IN ('Patient', 'Copay')
    ORDER BY p.PostedOn;
END
GO

-------------------------------------------------------------------------------
-- Scheduling
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Appointment_Book') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_Book;
GO
-- Books an appointment. The provider/day application lock serialises concurrent bookings from the staff app,
-- the booking site and the interfaces; double booking is refused unless the template allows overbooking.
CREATE PROCEDURE dbo.usp_Appointment_Book
    @PatientId int, @ProviderId int, @SiteId int, @AppointmentTypeId int, @StartsAt datetime, @DurationMinutes int = NULL,
    @Source tinyint, @Reason nvarchar(200) = NULL, @AllowOverbook bit = 0, @ReferralId int = NULL, @RescheduledFromId int = NULL,
    @BookedBy nvarchar(256),
    @AppointmentId int OUTPUT, @ConfirmationCode char(8) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @DurationMinutes IS NULL SELECT @DurationMinutes = DurationMinutes FROM dbo.AppointmentType WHERE AppointmentTypeId = @AppointmentTypeId;
    DECLARE @EndsAt datetime = DATEADD(minute, @DurationMinutes, @StartsAt);
    DECLARE @resource nvarchar(64) = N'appt:' + CAST(@ProviderId AS nvarchar(10)) + N':' + CONVERT(nvarchar(8), @StartsAt, 112);
    DECLARE @overbook bit = 0;

    BEGIN TRANSACTION;
    EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;

    IF EXISTS (SELECT 1 FROM dbo.ScheduleBlock b
               WHERE (b.ProviderId = @ProviderId OR (b.ProviderId IS NULL AND b.SiteId = @SiteId))
                 AND b.StartsAt < @EndsAt AND b.EndsAt > @StartsAt)
    BEGIN
        ROLLBACK;
        RAISERROR('The provider is not available at this time.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.Appointment a
               WHERE a.ProviderId = @ProviderId AND a.Status IN (0,1,2,3)
                 AND a.StartsAt < @EndsAt AND a.EndsAt > @StartsAt)
    BEGIN
        DECLARE @max int = 0, @existing int;
        SELECT TOP 1 @max = t.MaxOverbook FROM dbo.ScheduleTemplate t
        WHERE t.ProviderId = @ProviderId AND t.SiteId = @SiteId AND t.DayOfWeek = (DATEPART(weekday, @StartsAt) + @@DATEFIRST - 1) % 7
          AND t.StartTime <= CAST(@StartsAt AS time) AND t.EndTime > CAST(@StartsAt AS time)
          AND t.EffectiveFrom <= CAST(@StartsAt AS date) AND (t.EffectiveTo IS NULL OR t.EffectiveTo >= CAST(@StartsAt AS date));
        SELECT @existing = COUNT(*) FROM dbo.Appointment a
        WHERE a.ProviderId = @ProviderId AND a.Status IN (0,1,2,3) AND a.IsOverbook = 1 AND CAST(a.StartsAt AS date) = CAST(@StartsAt AS date);
        IF @AllowOverbook = 0 OR @existing >= @max
        BEGIN
            ROLLBACK;
            RAISERROR('The time is already booked.', 16, 1);
            RETURN;
        END
        SET @overbook = 1;
    END

    DECLARE @tries int = 0;
    WHILE 1 = 1
    BEGIN
        SET @ConfirmationCode = dbo.fn_ConfirmationCode(CRYPT_GEN_RANDOM(8));
        IF NOT EXISTS (SELECT 1 FROM dbo.Appointment WHERE ConfirmationCode = @ConfirmationCode) BREAK;
        SET @tries = @tries + 1;
        IF @tries > 10 BEGIN ROLLBACK; RAISERROR('Could not allocate a confirmation code.', 16, 1); RETURN; END
    END

    INSERT dbo.Appointment (PatientId, ProviderId, SiteId, AppointmentTypeId, StartsAt, EndsAt, Status, Source, Reason, IsOverbook,
                            ReferralId, RescheduledFromId, ConfirmationCode, BookedBy)
    VALUES (@PatientId, @ProviderId, @SiteId, @AppointmentTypeId, @StartsAt, @EndsAt, 0, @Source, @Reason, @overbook,
            @ReferralId, @RescheduledFromId, @ConfirmationCode, @BookedBy);
    SET @AppointmentId = SCOPE_IDENTITY();

    IF @RescheduledFromId IS NOT NULL
        UPDATE dbo.Appointment SET Status = 7, CancelledOn = GETDATE(), CancelledBy = @BookedBy, CancelReason = 'Rescheduled'
        WHERE AppointmentId = @RescheduledFromId;

    UPDATE dbo.WaitlistEntry SET ResolvedOn = GETDATE(), AppointmentId = @AppointmentId
    WHERE PatientId = @PatientId AND ResolvedOn IS NULL AND AppointmentTypeId = @AppointmentTypeId;

    COMMIT;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_SetStatus') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_SetStatus;
GO
CREATE PROCEDURE dbo.usp_Appointment_SetStatus
    @AppointmentId int, @Status tinyint, @UserName nvarchar(256), @Reason nvarchar(200) = NULL, @CopayCollected money = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now datetime = GETDATE();
    UPDATE dbo.Appointment SET
        Status = @Status,
        ConfirmedOn = CASE WHEN @Status = 1 THEN @now ELSE ConfirmedOn END,
        CheckedInOn = CASE WHEN @Status = 2 THEN @now ELSE CheckedInOn END,
        RoomedOn = CASE WHEN @Status = 3 THEN @now ELSE RoomedOn END,
        CompletedOn = CASE WHEN @Status = 4 THEN @now ELSE CompletedOn END,
        CancelledOn = CASE WHEN @Status = 5 THEN @now ELSE CancelledOn END,
        CancelledBy = CASE WHEN @Status = 5 THEN @UserName ELSE CancelledBy END,
        CancelReason = CASE WHEN @Status = 5 THEN @Reason ELSE CancelReason END,
        LateCancel = CASE WHEN @Status = 5 AND DATEDIFF(hour, @now, StartsAt) < 24 THEN 1 ELSE LateCancel END,
        CopayCollected = ISNULL(@CopayCollected, CopayCollected)
    WHERE AppointmentId = @AppointmentId;

    -- Checking in opens the encounter for the visit.
    IF @Status = 2 AND NOT EXISTS (SELECT 1 FROM dbo.Encounter WHERE AppointmentId = @AppointmentId)
        INSERT dbo.Encounter (AppointmentId, PatientId, ProviderId, SiteId, DateOfService, ChiefComplaint)
        SELECT AppointmentId, PatientId, ProviderId, SiteId, CAST(StartsAt AS date), Reason FROM dbo.Appointment WHERE AppointmentId = @AppointmentId;

    IF @CopayCollected IS NOT NULL AND @CopayCollected > 0
        INSERT dbo.Payment (PatientId, Source, Method, Amount, Reference, PostedBy)
        SELECT PatientId, 'Copay', 'Card', @CopayCollected, ConfirmationCode, @UserName FROM dbo.Appointment WHERE AppointmentId = @AppointmentId;

    EXEC dbo.usp_Audit_Write @UserName, 'AppointmentStatus', 'Appointment', @AppointmentId, NULL, @Reason;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_ListByDay') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_ListByDay;
GO
CREATE PROCEDURE dbo.usp_Appointment_ListByDay
    @SiteId int, @Date date, @ProviderId int = NULL, @IncludeCancelled bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AppointmentId, a.StartsAt, a.EndsAt, a.Status, a.Source, a.Reason, a.IsOverbook, a.ConfirmationCode, a.CheckedInOn,
           p.PatientId, p.Mrn, p.FirstName, p.LastName, p.DateOfBirth, p.MobilePhone, p.Balance,
           pr.ProviderId, pr.LastName AS ProviderLastName, pr.FirstName AS ProviderFirstName, pr.Credentials,
           t.Code AS AppointmentTypeCode, t.Name AS AppointmentTypeName, t.Color,
           (SELECT TOP 1 py.Name FROM dbo.PatientInsurance i JOIN dbo.Payer py ON py.PayerId = i.PayerId
            WHERE i.PatientId = p.PatientId AND i.Sequence = 1 AND i.EffectiveFrom <= @Date AND (i.EffectiveTo IS NULL OR i.EffectiveTo >= @Date)) AS PrimaryPayer,
           (SELECT TOP 1 i.Copay FROM dbo.PatientInsurance i
            WHERE i.PatientId = p.PatientId AND i.Sequence = 1 AND i.EffectiveFrom <= @Date AND (i.EffectiveTo IS NULL OR i.EffectiveTo >= @Date)) AS Copay,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PatientNote n WHERE n.PatientId = p.PatientId AND n.IsAlert = 1) THEN 1 ELSE 0 END AS bit) AS HasAlert
    FROM dbo.Appointment a
    JOIN dbo.Patient p ON p.PatientId = a.PatientId
    JOIN dbo.Provider pr ON pr.ProviderId = a.ProviderId
    JOIN dbo.AppointmentType t ON t.AppointmentTypeId = a.AppointmentTypeId
    WHERE a.SiteId = @SiteId AND a.StartsAt >= @Date AND a.StartsAt < DATEADD(day, 1, CAST(@Date AS datetime))
      AND (@ProviderId IS NULL OR a.ProviderId = @ProviderId)
      AND (@IncludeCancelled = 1 OR a.Status NOT IN (5, 7))
    ORDER BY a.StartsAt, pr.LastName;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_FindForCheckIn') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_FindForCheckIn;
GO
-- Kiosk check-in: today's appointments at the kiosk's site for a last name and date of birth (or confirmation code).
CREATE PROCEDURE dbo.usp_Appointment_FindForCheckIn
    @SiteId int, @LastName nvarchar(50) = NULL, @DateOfBirth date = NULL, @ConfirmationCode char(8) = NULL, @Today date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AppointmentId, a.StartsAt, a.Status, a.ConfirmationCode, p.PatientId, p.FirstName, p.LastName, p.Balance,
           pr.LastName AS ProviderLastName, pr.Credentials,
           ISNULL((SELECT TOP 1 i.Copay FROM dbo.PatientInsurance i WHERE i.PatientId = p.PatientId AND i.Sequence = 1
                   AND i.EffectiveFrom <= @Today AND (i.EffectiveTo IS NULL OR i.EffectiveTo >= @Today)), 0) AS Copay,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.IntakeForm f WHERE f.PatientId = p.PatientId AND f.SubmittedOn >= DATEADD(year, -1, @Today)) THEN 0 ELSE 1 END AS bit) AS IntakeFormDue
    FROM dbo.Appointment a
    JOIN dbo.Patient p ON p.PatientId = a.PatientId
    JOIN dbo.Provider pr ON pr.ProviderId = a.ProviderId
    WHERE a.SiteId = @SiteId AND a.StartsAt >= @Today AND a.StartsAt < DATEADD(day, 1, CAST(@Today AS datetime))
      AND a.Status IN (0, 1)
      AND ((@ConfirmationCode IS NOT NULL AND a.ConfirmationCode = @ConfirmationCode)
        OR (@ConfirmationCode IS NULL AND UPPER(p.LastName) = UPPER(@LastName) AND p.DateOfBirth = @DateOfBirth))
    ORDER BY a.StartsAt;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_DueForReminder') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_DueForReminder;
GO
CREATE PROCEDURE dbo.usp_Appointment_DueForReminder
    @From datetime, @To datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AppointmentId, a.StartsAt, a.ConfirmationCode, p.PatientId, p.FirstName, p.LastName, p.Email, p.MobilePhone, p.PreferredContact,
           pr.FirstName AS ProviderFirstName, pr.LastName AS ProviderLastName, pr.Credentials,
           s.Name AS SiteName, s.Address1 AS SiteAddress, s.City AS SiteCity, s.Phone AS SitePhone, t.Name AS AppointmentTypeName
    FROM dbo.Appointment a
    JOIN dbo.Patient p ON p.PatientId = a.PatientId
    JOIN dbo.Provider pr ON pr.ProviderId = a.ProviderId
    JOIN dbo.Site s ON s.SiteId = a.SiteId
    JOIN dbo.AppointmentType t ON t.AppointmentTypeId = a.AppointmentTypeId
    WHERE a.StartsAt >= @From AND a.StartsAt < @To AND a.Status IN (0, 1) AND a.ReminderSentOn IS NULL AND p.ReminderOptIn = 1
    ORDER BY a.StartsAt;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_ReminderSent') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_ReminderSent;
GO
CREATE PROCEDURE dbo.usp_Appointment_ReminderSent
    @AppointmentId int, @Channel varchar(10), @Recipient nvarchar(256), @Succeeded bit, @Detail nvarchar(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.ReminderLog (AppointmentId, Channel, Recipient, Succeeded, Detail) VALUES (@AppointmentId, @Channel, @Recipient, @Succeeded, @Detail);
    IF @Succeeded = 1 UPDATE dbo.Appointment SET ReminderSentOn = GETDATE() WHERE AppointmentId = @AppointmentId;
END
GO

IF OBJECT_ID('dbo.usp_Appointment_MarkNoShows') IS NOT NULL DROP PROCEDURE dbo.usp_Appointment_MarkNoShows;
GO
CREATE PROCEDURE dbo.usp_Appointment_MarkNoShows
    @AsOf datetime = NULL, @GraceMinutes int = 30
AS
BEGIN
    SET NOCOUNT ON;
    IF @AsOf IS NULL SET @AsOf = GETDATE();
    UPDATE dbo.Appointment SET Status = 6
    OUTPUT inserted.AppointmentId, inserted.PatientId, inserted.ProviderId, inserted.SiteId, inserted.StartsAt
    WHERE Status IN (0, 1) AND StartsAt < DATEADD(minute, -@GraceMinutes, @AsOf) AND StartsAt >= DATEADD(day, -7, @AsOf);
END
GO

IF OBJECT_ID('dbo.usp_Waitlist_Candidates') IS NOT NULL DROP PROCEDURE dbo.usp_Waitlist_Candidates;
GO
CREATE PROCEDURE dbo.usp_Waitlist_Candidates
    @ProviderId int, @SiteId int, @Date date, @AppointmentTypeId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.WaitlistEntryId, w.PatientId, p.FirstName, p.LastName, p.MobilePhone, p.Email, w.AppointmentTypeId, w.Priority, w.EarliestDate, w.LatestDate, w.CreatedOn
    FROM dbo.WaitlistEntry w
    JOIN dbo.Patient p ON p.PatientId = w.PatientId
    WHERE w.ResolvedOn IS NULL
      AND (w.ProviderId IS NULL OR w.ProviderId = @ProviderId)
      AND (w.SiteId IS NULL OR w.SiteId = @SiteId)
      AND (@AppointmentTypeId IS NULL OR w.AppointmentTypeId = @AppointmentTypeId)
      AND w.EarliestDate <= @Date AND (w.LatestDate IS NULL OR w.LatestDate >= @Date)
    ORDER BY w.Priority DESC, w.CreatedOn;
END
GO

-------------------------------------------------------------------------------
-- Interface messages (HL7)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Interface_Enqueue') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Enqueue;
GO
CREATE PROCEDURE dbo.usp_Interface_Enqueue
    @Partner varchar(10), @MessageType varchar(10), @ControlId varchar(30), @PatientId int = NULL, @Payload nvarchar(max), @MessageId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.InterfaceMessage (Direction, Partner, MessageType, ControlId, Status, PatientId, Payload, NextAttemptOn)
    VALUES (1, @Partner, @MessageType, @ControlId, 0, @PatientId, @Payload, GETDATE());
    SET @MessageId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_Interface_Dequeue') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Dequeue;
GO
-- Takes the next pending outbound messages for a partner in creation order. The lease (NextAttemptOn) keeps
-- a second engine instance from sending the same message while the first one waits for the ACK.
CREATE PROCEDURE dbo.usp_Interface_Dequeue
    @Partner varchar(10), @BatchSize int = 10, @LeaseSeconds int = 120
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE m SET Attempts = m.Attempts + 1, NextAttemptOn = DATEADD(second, @LeaseSeconds, GETDATE())
    OUTPUT inserted.MessageId, inserted.Attempts, inserted.MessageType, inserted.ControlId, inserted.Payload
    FROM dbo.InterfaceMessage m WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE m.MessageId IN (SELECT TOP (@BatchSize) q.MessageId FROM dbo.InterfaceMessage q WITH (UPDLOCK, READPAST, ROWLOCK)
                          WHERE q.Direction = 1 AND q.Partner = @Partner AND q.Status = 0 AND q.NextAttemptOn <= GETDATE()
                          ORDER BY q.MessageId);
END
GO

IF OBJECT_ID('dbo.usp_Interface_Complete') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Complete;
GO
CREATE PROCEDURE dbo.usp_Interface_Complete
    @MessageId int, @Status tinyint, @AckPayload nvarchar(max) = NULL, @Error nvarchar(1000) = NULL, @RetryMinutes int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.InterfaceMessage SET
        Status = CASE WHEN @RetryMinutes IS NOT NULL THEN 0 ELSE @Status END,
        AckPayload = ISNULL(@AckPayload, AckPayload),
        Error = @Error,
        NextAttemptOn = CASE WHEN @RetryMinutes IS NOT NULL THEN DATEADD(minute, @RetryMinutes, GETDATE()) ELSE NULL END,
        ProcessedOn = CASE WHEN @RetryMinutes IS NULL THEN GETDATE() ELSE NULL END
    WHERE MessageId = @MessageId;
END
GO

IF OBJECT_ID('dbo.usp_Interface_LogInbound') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_LogInbound;
GO
CREATE PROCEDURE dbo.usp_Interface_LogInbound
    @Partner varchar(10), @MessageType varchar(10), @ControlId varchar(30), @Payload nvarchar(max), @Status tinyint,
    @PatientId int = NULL, @AckPayload nvarchar(max) = NULL, @Error nvarchar(1000) = NULL, @MessageId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.InterfaceMessage (Direction, Partner, MessageType, ControlId, Status, PatientId, Payload, AckPayload, Attempts, Error, ProcessedOn)
    VALUES (0, @Partner, @MessageType, @ControlId, @Status, @PatientId, @Payload, @AckPayload, 1, @Error, GETDATE());
    SET @MessageId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_Interface_IsDuplicate') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_IsDuplicate;
GO
CREATE PROCEDURE dbo.usp_Interface_IsDuplicate
    @Partner varchar(10), @ControlId varchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM dbo.InterfaceMessage WHERE Direction = 0 AND Partner = @Partner AND ControlId = @ControlId AND Status = 3;
END
GO

IF OBJECT_ID('dbo.usp_Interface_Search') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Search;
GO
CREATE PROCEDURE dbo.usp_Interface_Search
    @Partner varchar(10) = NULL, @Status tinyint = NULL, @From datetime, @To datetime, @MaximumRows int = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@MaximumRows) m.MessageId, m.Direction, m.Partner, m.MessageType, m.ControlId, m.Status, m.Attempts, m.Error,
           m.CreatedOn, m.ProcessedOn, m.PatientId, p.Mrn, p.LastName
    FROM dbo.InterfaceMessage m
    LEFT JOIN dbo.Patient p ON p.PatientId = m.PatientId
    WHERE m.CreatedOn >= @From AND m.CreatedOn < @To
      AND (@Partner IS NULL OR m.Partner = @Partner) AND (@Status IS NULL OR m.Status = @Status)
    ORDER BY m.MessageId DESC;
END
GO

IF OBJECT_ID('dbo.usp_Interface_Resend') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Resend;
GO
CREATE PROCEDURE dbo.usp_Interface_Resend
    @MessageId int, @UserName nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.InterfaceMessage SET Status = 0, NextAttemptOn = GETDATE(), Error = NULL WHERE MessageId = @MessageId AND Direction = 1;
    EXEC dbo.usp_Audit_Write @UserName, 'Resend', 'InterfaceMessage', @MessageId;
END
GO

IF OBJECT_ID('dbo.usp_Interface_Purge') IS NOT NULL DROP PROCEDURE dbo.usp_Interface_Purge;
GO
CREATE PROCEDURE dbo.usp_Interface_Purge
    @RetentionDays int = 180
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cutoff datetime = DATEADD(day, -@RetentionDays, GETDATE()), @total int = 0;
    WHILE 1 = 1
    BEGIN
        DELETE TOP (5000) FROM dbo.InterfaceMessage WHERE CreatedOn < @cutoff AND Status IN (2, 3, 5);
        IF @@ROWCOUNT = 0 BREAK;
        SET @total = @total + @@ROWCOUNT;
    END
    SELECT @total AS Purged;
END
GO

IF OBJECT_ID('dbo.usp_LabResult_Insert') IS NOT NULL DROP PROCEDURE dbo.usp_LabResult_Insert;
GO
CREATE PROCEDURE dbo.usp_LabResult_Insert
    @PatientId int, @OrderNumber varchar(20) = NULL, @FillerOrderNumber varchar(30) = NULL, @TestCode varchar(20),
    @ObservationCode varchar(20), @ObservationName nvarchar(100), @Value nvarchar(200) = NULL, @Units varchar(20) = NULL,
    @ReferenceRange varchar(40) = NULL, @AbnormalFlag varchar(4) = NULL, @ResultStatus char(1) = 'F', @ObservedOn datetime = NULL, @MessageId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @orderId int;
    SELECT @orderId = LabOrderId FROM dbo.LabOrder WHERE OrderNumber = @OrderNumber;

    -- Corrected results (status C) replace the earlier value of the same observation.
    IF @ResultStatus = 'C'
        DELETE FROM dbo.LabResult WHERE PatientId = @PatientId AND OrderNumber = @OrderNumber AND ObservationCode = @ObservationCode;

    INSERT dbo.LabResult (PatientId, LabOrderId, OrderNumber, FillerOrderNumber, TestCode, ObservationCode, ObservationName, Value, Units,
                          ReferenceRange, AbnormalFlag, ResultStatus, ObservedOn, MessageId)
    VALUES (@PatientId, @orderId, @OrderNumber, @FillerOrderNumber, @TestCode, @ObservationCode, @ObservationName, @Value, @Units,
            @ReferenceRange, NULLIF(@AbnormalFlag, ''), ISNULL(NULLIF(@ResultStatus, ''), 'F'), @ObservedOn, @MessageId);

    IF @orderId IS NOT NULL
        UPDATE dbo.LabOrder SET Status = CASE WHEN @ResultStatus = 'P' THEN 'Partial' ELSE 'Resulted' END, ResultedOn = GETDATE() WHERE LabOrderId = @orderId;
END
GO

IF OBJECT_ID('dbo.usp_LabOrder_Create') IS NOT NULL DROP PROCEDURE dbo.usp_LabOrder_Create;
GO
CREATE PROCEDURE dbo.usp_LabOrder_Create
    @EncounterId int = NULL, @PatientId int, @ProviderId int, @TestCode varchar(20), @Priority char(1) = 'R', @ClinicalInfo nvarchar(200) = NULL,
    @LabOrderId int OUTPUT, @OrderNumber varchar(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq bigint;
    EXEC dbo.usp_NextNumber 'LabOrder', 1, @seq OUTPUT;
    SET @OrderNumber = 'SMG' + RIGHT('0000000' + CAST(@seq AS varchar(10)), 7);
    INSERT dbo.LabOrder (OrderNumber, EncounterId, PatientId, ProviderId, TestCode, Priority, ClinicalInfo)
    VALUES (@OrderNumber, @EncounterId, @PatientId, @ProviderId, @TestCode, @Priority, @ClinicalInfo);
    SET @LabOrderId = SCOPE_IDENTITY();
END
GO

-------------------------------------------------------------------------------
-- Billing
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Billing_ReadyEncounters') IS NOT NULL DROP PROCEDURE dbo.usp_Billing_ReadyEncounters;
GO
-- Encounters whose charges were entered at least @LagDays ago and that have an active primary insurance.
CREATE PROCEDURE dbo.usp_Billing_ReadyEncounters
    @AsOf datetime, @LagDays int = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.EncounterId, e.DateOfService, e.PatientId, p.Mrn, p.LastName, p.FirstName,
           i.PatientInsuranceId, i.PayerId, py.Code AS PayerCode, py.PayerType,
           SUM(c.Amount * c.Units) AS TotalCharge, COUNT(*) AS ChargeCount
    FROM dbo.Encounter e
    JOIN dbo.Patient p ON p.PatientId = e.PatientId
    JOIN dbo.Charge c ON c.EncounterId = e.EncounterId AND c.Status IN (0, 1) AND c.ClaimId IS NULL
    LEFT JOIN dbo.PatientInsurance i ON i.PatientId = e.PatientId AND i.Sequence = 1
         AND i.EffectiveFrom <= e.DateOfService AND (i.EffectiveTo IS NULL OR i.EffectiveTo >= e.DateOfService)
    LEFT JOIN dbo.Payer py ON py.PayerId = i.PayerId
    WHERE e.Status = 1 AND c.EnteredOn <= DATEADD(day, -@LagDays, @AsOf)
    GROUP BY e.EncounterId, e.DateOfService, e.PatientId, p.Mrn, p.LastName, p.FirstName, i.PatientInsuranceId, i.PayerId, py.Code, py.PayerType
    ORDER BY e.DateOfService, e.EncounterId;
END
GO

IF OBJECT_ID('dbo.usp_Claim_BuildData') IS NOT NULL DROP PROCEDURE dbo.usp_Claim_BuildData;
GO
-- Everything the 837P writer needs for one claim, as four result sets.
CREATE PROCEDURE dbo.usp_Claim_BuildData
    @ClaimId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ClaimId, c.ClaimNumber, c.DateOfService, c.TotalCharge, c.FrequencyCode, c.OriginalClaimNumber, c.PayerClaimNumber,
           p.Mrn, p.FirstName, p.MiddleName, p.LastName, p.DateOfBirth, p.Sex, p.Address1, p.Address2, p.City, p.State, p.Zip,
           i.MemberId, i.GroupNumber, i.Relationship, i.SubscriberFirstName, i.SubscriberLastName, i.SubscriberDateOfBirth,
           py.Name AS PayerName, py.ClaimPayerId, py.FilingIndicator, py.PayerType, py.Address1 AS PayerAddress1, py.City AS PayerCity, py.State AS PayerState, py.Zip AS PayerZip,
           pr.Npi AS RenderingNpi, pr.FirstName AS RenderingFirstName, pr.LastName AS RenderingLastName, sp.TaxonomyCode,
           s.Name AS SiteName, s.Address1 AS SiteAddress1, s.City AS SiteCity, s.State AS SiteState, s.Zip AS SiteZip, s.PlaceOfService, s.TaxId, s.GroupNpi,
           r.AuthorizationNumber
    FROM dbo.Claim c
    JOIN dbo.Patient p ON p.PatientId = c.PatientId
    JOIN dbo.PatientInsurance i ON i.PatientInsuranceId = c.PatientInsuranceId
    JOIN dbo.Payer py ON py.PayerId = c.PayerId
    JOIN dbo.Provider pr ON pr.ProviderId = c.ProviderId
    JOIN dbo.Specialty sp ON sp.SpecialtyCode = pr.SpecialtyCode
    JOIN dbo.Site s ON s.SiteId = c.SiteId
    JOIN dbo.Encounter e ON e.EncounterId = c.EncounterId
    LEFT JOIN dbo.Appointment a ON a.AppointmentId = e.AppointmentId
    LEFT JOIN dbo.Referral r ON r.ReferralId = a.ReferralId
    WHERE c.ClaimId = @ClaimId;

    SELECT d.Sequence, d.Code, d.CodeSet
    FROM dbo.EncounterDiagnosis d JOIN dbo.Claim c ON c.EncounterId = d.EncounterId
    WHERE c.ClaimId = @ClaimId ORDER BY d.Sequence;

    SELECT ch.ChargeId, ch.LineNumber, ch.CptCode, ch.Modifier1, ch.Modifier2, ch.Units, ch.Amount, ch.DiagnosisPointers
    FROM dbo.Charge ch WHERE ch.ClaimId = @ClaimId AND ch.Status <> 5 ORDER BY ch.LineNumber;
END
GO

IF OBJECT_ID('dbo.usp_Claim_Create') IS NOT NULL DROP PROCEDURE dbo.usp_Claim_Create;
GO
CREATE PROCEDURE dbo.usp_Claim_Create
    @EncounterId int, @PatientInsuranceId int, @ClaimId int OUTPUT, @ClaimNumber varchar(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @seq bigint;
    EXEC dbo.usp_NextNumber 'Claim', 1, @seq OUTPUT;
    SET @ClaimNumber = 'C' + RIGHT('000000000' + CAST(@seq AS varchar(10)), 9);

    INSERT dbo.Claim (ClaimNumber, EncounterId, PatientId, PayerId, PatientInsuranceId, ProviderId, SiteId, DateOfService, TotalCharge)
    SELECT @ClaimNumber, e.EncounterId, e.PatientId, i.PayerId, i.PatientInsuranceId, e.ProviderId, e.SiteId, e.DateOfService,
           (SELECT SUM(c.Amount * c.Units) FROM dbo.Charge c WHERE c.EncounterId = e.EncounterId AND c.Status IN (0, 1) AND c.ClaimId IS NULL)
    FROM dbo.Encounter e JOIN dbo.PatientInsurance i ON i.PatientInsuranceId = @PatientInsuranceId
    WHERE e.EncounterId = @EncounterId;
    SET @ClaimId = SCOPE_IDENTITY();

    UPDATE dbo.Charge SET ClaimId = @ClaimId, Status = 2 WHERE EncounterId = @EncounterId AND Status IN (0, 1) AND ClaimId IS NULL;
    UPDATE dbo.Encounter SET Status = 2 WHERE EncounterId = @EncounterId;
    COMMIT;
END
GO

IF OBJECT_ID('dbo.usp_Claim_Reject') IS NOT NULL DROP PROCEDURE dbo.usp_Claim_Reject;
GO
CREATE PROCEDURE dbo.usp_Claim_Reject
    @ClaimId int, @Reason nvarchar(400)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Claim SET Status = 3, RejectReason = @Reason, AdjudicatedOn = GETDATE() WHERE ClaimId = @ClaimId;
END
GO

IF OBJECT_ID('dbo.usp_ClaimBatch_Create') IS NOT NULL DROP PROCEDURE dbo.usp_ClaimBatch_Create;
GO
CREATE PROCEDURE dbo.usp_ClaimBatch_Create
    @FileName varchar(100), @ClaimIds varchar(max), @ClaimBatchId int OUTPUT, @InterchangeControlNumber int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @seq bigint;
    EXEC dbo.usp_NextNumber 'Interchange', 1, @seq OUTPUT;
    SET @InterchangeControlNumber = CAST(@seq AS int);

    DECLARE @ids TABLE (ClaimId int PRIMARY KEY);
    DECLARE @xml xml = CAST('<i>' + REPLACE(@ClaimIds, ',', '</i><i>') + '</i>' AS xml);
    INSERT @ids SELECT DISTINCT x.value('.', 'int') FROM @xml.nodes('/i') AS t(x);

    INSERT dbo.ClaimBatch (InterchangeControlNumber, FileName, ClaimCount, TotalCharge)
    SELECT @InterchangeControlNumber, @FileName, COUNT(*), ISNULL(SUM(c.TotalCharge), 0)
    FROM dbo.Claim c JOIN @ids i ON i.ClaimId = c.ClaimId;
    SET @ClaimBatchId = SCOPE_IDENTITY();

    UPDATE c SET ClaimBatchId = @ClaimBatchId FROM dbo.Claim c JOIN @ids i ON i.ClaimId = c.ClaimId;
    COMMIT;
END
GO

IF OBJECT_ID('dbo.usp_ClaimBatch_MarkSent') IS NOT NULL DROP PROCEDURE dbo.usp_ClaimBatch_MarkSent;
GO
CREATE PROCEDURE dbo.usp_ClaimBatch_MarkSent
    @ClaimBatchId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ClaimBatch SET Status = 'Sent', SentOn = GETDATE() WHERE ClaimBatchId = @ClaimBatchId;
    UPDATE dbo.Claim SET Status = 1, SubmittedOn = GETDATE() WHERE ClaimBatchId = @ClaimBatchId AND Status = 0;
END
GO

IF OBJECT_ID('dbo.usp_ClaimBatch_Acknowledge') IS NOT NULL DROP PROCEDURE dbo.usp_ClaimBatch_Acknowledge;
GO
-- 999 implementation acknowledgment: A accepted, E accepted with errors, R rejected (whole batch goes back to Ready).
CREATE PROCEDURE dbo.usp_ClaimBatch_Acknowledge
    @InterchangeControlNumber int, @AckStatus char(1), @AckFileName varchar(100), @Updated int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @batch int;
    SELECT @batch = ClaimBatchId FROM dbo.ClaimBatch WHERE InterchangeControlNumber = @InterchangeControlNumber;
    SET @Updated = 0;
    IF @batch IS NULL RETURN;
    UPDATE dbo.ClaimBatch SET AckStatus = @AckStatus, AckFileName = @AckFileName, AckOn = GETDATE(),
        Status = CASE WHEN @AckStatus = 'R' THEN 'Rejected' ELSE 'Accepted' END
    WHERE ClaimBatchId = @batch;
    UPDATE dbo.Claim SET Status = CASE WHEN @AckStatus = 'R' THEN 3 ELSE 2 END,
        RejectReason = CASE WHEN @AckStatus = 'R' THEN 'Batch rejected by clearinghouse (999)' ELSE NULL END
    WHERE ClaimBatchId = @batch AND Status = 1;
    SET @Updated = @@ROWCOUNT;
END
GO

IF OBJECT_ID('dbo.usp_Remittance_Insert') IS NOT NULL DROP PROCEDURE dbo.usp_Remittance_Insert;
GO
CREATE PROCEDURE dbo.usp_Remittance_Insert
    @FileName varchar(100), @PayerName nvarchar(100), @PayerIdentifier varchar(20) = NULL, @TraceNumber varchar(30), @PaymentMethod char(3),
    @PaymentDate date, @TotalPaid money, @ClaimCount int, @RemittanceId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.Remittance WHERE FileName = @FileName)
    BEGIN
        RAISERROR('Remittance file %s has already been posted.', 16, 1, @FileName);
        RETURN;
    END
    INSERT dbo.Remittance (FileName, PayerId, PayerName, TraceNumber, PaymentMethod, PaymentDate, TotalPaid, ClaimCount)
    VALUES (@FileName, (SELECT TOP 1 PayerId FROM dbo.Payer WHERE ClaimPayerId = @PayerIdentifier), @PayerName, @TraceNumber, @PaymentMethod, @PaymentDate, @TotalPaid, @ClaimCount);
    SET @RemittanceId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_Payment_PostClaim') IS NOT NULL DROP PROCEDURE dbo.usp_Payment_PostClaim;
GO
-- Posts one CLP loop of an 835: claim payment, contractual adjustment and patient responsibility.
CREATE PROCEDURE dbo.usp_Payment_PostClaim
    @RemittanceId int, @ClaimNumber varchar(20), @ClaimStatusCode varchar(2), @ChargeAmount money, @PaidAmount money,
    @PatientResponsibility money, @AdjustmentAmount money, @PayerClaimNumber varchar(30) = NULL, @Reference varchar(40) = NULL,
    @Matched bit OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @claimId int, @patientId int;
    SELECT @claimId = ClaimId, @patientId = PatientId FROM dbo.Claim WHERE ClaimNumber = @ClaimNumber;
    IF @claimId IS NULL
    BEGIN
        SET @Matched = 0;
        UPDATE dbo.Remittance SET UnmatchedCount = UnmatchedCount + 1 WHERE RemittanceId = @RemittanceId;
        RETURN;
    END

    SET @Matched = 1;
    BEGIN TRANSACTION;
    UPDATE dbo.Claim SET
        PaidAmount = PaidAmount + @PaidAmount,
        AdjustmentAmount = AdjustmentAmount + @AdjustmentAmount,
        PatientResponsibility = @PatientResponsibility,
        PayerClaimNumber = ISNULL(@PayerClaimNumber, PayerClaimNumber),
        Status = CASE @ClaimStatusCode WHEN '4' THEN 6 WHEN '22' THEN 7
                     ELSE CASE WHEN @PaidAmount + @AdjustmentAmount + @PatientResponsibility >= TotalCharge THEN 4 ELSE 5 END END,
        AdjudicatedOn = GETDATE()
    WHERE ClaimId = @claimId;

    INSERT dbo.Payment (PatientId, ClaimId, RemittanceId, Source, Method, Amount, AdjustmentAmount, AdjustmentReason, Reference, PostedBy)
    VALUES (@patientId, @claimId, @RemittanceId, 'Payer', 'EFT', @PaidAmount, @AdjustmentAmount, 'CO-45', @Reference, 'billing-batch');

    -- Spread the patient share over the claim lines in proportion to the line charge.
    UPDATE ch SET
        PatientAmount = ROUND(@PatientResponsibility * (ch.Amount * ch.Units) / NULLIF(c.TotalCharge, 0), 2),
        PaidAmount = ROUND(@PaidAmount * (ch.Amount * ch.Units) / NULLIF(c.TotalCharge, 0), 2),
        AdjustmentAmount = ROUND(@AdjustmentAmount * (ch.Amount * ch.Units) / NULLIF(c.TotalCharge, 0), 2),
        Status = 3
    FROM dbo.Charge ch JOIN dbo.Claim c ON c.ClaimId = ch.ClaimId
    WHERE ch.ClaimId = @claimId AND ch.Status <> 5;

    UPDATE dbo.Patient SET Balance = Balance + @PatientResponsibility WHERE PatientId = @patientId;
    COMMIT;
END
GO

IF OBJECT_ID('dbo.usp_Payment_PostPatient') IS NOT NULL DROP PROCEDURE dbo.usp_Payment_PostPatient;
GO
CREATE PROCEDURE dbo.usp_Payment_PostPatient
    @PatientId int, @Amount money, @Method varchar(10), @Reference varchar(40) = NULL, @PostedBy nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    INSERT dbo.Payment (PatientId, Source, Method, Amount, Reference, PostedBy) VALUES (@PatientId, 'Patient', @Method, @Amount, @Reference, @PostedBy);
    UPDATE dbo.Patient SET Balance = Balance - @Amount WHERE PatientId = @PatientId;
    COMMIT;
    EXEC dbo.usp_Audit_Write @PostedBy, 'PatientPayment', 'Patient', @PatientId, @PatientId, @Reference;
END
GO

IF OBJECT_ID('dbo.usp_Statement_Candidates') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_Candidates;
GO
-- Patients with a balance who have not had a statement in the last 28 days.
CREATE PROCEDURE dbo.usp_Statement_Candidates
    @StatementDate date, @MinimumBalance money = 5
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PatientId, p.Mrn, p.LastName, p.FirstName, p.Balance
    FROM dbo.Patient p
    WHERE p.Balance >= @MinimumBalance AND p.IsActive = 1 AND p.Address1 IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Statement s WHERE s.PatientId = p.PatientId AND s.StatementDate > DATEADD(day, -28, @StatementDate))
    ORDER BY p.Zip, p.LastName;
END
GO

IF OBJECT_ID('dbo.usp_Statement_Insert') IS NOT NULL DROP PROCEDURE dbo.usp_Statement_Insert;
GO
CREATE PROCEDURE dbo.usp_Statement_Insert
    @PatientId int, @StatementDate date, @PreviousBalance money, @NewCharges money, @Payments money, @AmountDue money, @DueDate date,
    @FileName nvarchar(260), @DunningLevel tinyint, @StatementId int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.Statement (PatientId, StatementDate, PreviousBalance, NewCharges, Payments, AmountDue, DueDate, FileName, DunningLevel)
    VALUES (@PatientId, @StatementDate, @PreviousBalance, @NewCharges, @Payments, @AmountDue, @DueDate, @FileName, @DunningLevel);
    SET @StatementId = SCOPE_IDENTITY();
END
GO

-------------------------------------------------------------------------------
-- Reports (typed DataSet, RDLC and SSRS)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Report_DailySchedule') IS NOT NULL DROP PROCEDURE dbo.usp_Report_DailySchedule;
GO
CREATE PROCEDURE dbo.usp_Report_DailySchedule
    @SiteId int, @Date date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.Name AS SiteName, pr.LastName + ', ' + pr.FirstName + ISNULL(' ' + pr.Credentials, '') AS ProviderName,
           a.StartsAt, a.EndsAt, t.Name AS AppointmentType, p.Mrn, p.LastName + ', ' + p.FirstName AS PatientName, p.DateOfBirth,
           ISNULL(p.MobilePhone, p.HomePhone) AS Phone, a.Reason,
           CASE a.Status WHEN 0 THEN 'Scheduled' WHEN 1 THEN 'Confirmed' WHEN 2 THEN 'Checked in' WHEN 3 THEN 'In room'
                         WHEN 4 THEN 'Completed' WHEN 5 THEN 'Cancelled' WHEN 6 THEN 'No show' ELSE 'Rescheduled' END AS StatusText
    FROM dbo.Appointment a
    JOIN dbo.Site s ON s.SiteId = a.SiteId
    JOIN dbo.Provider pr ON pr.ProviderId = a.ProviderId
    JOIN dbo.Patient p ON p.PatientId = a.PatientId
    JOIN dbo.AppointmentType t ON t.AppointmentTypeId = a.AppointmentTypeId
    WHERE a.SiteId = @SiteId AND a.StartsAt >= @Date AND a.StartsAt < DATEADD(day, 1, CAST(@Date AS datetime)) AND a.Status NOT IN (5, 7)
    ORDER BY ProviderName, a.StartsAt;
END
GO

IF OBJECT_ID('dbo.usp_Report_ArAging') IS NOT NULL DROP PROCEDURE dbo.usp_Report_ArAging;
GO
CREATE PROCEDURE dbo.usp_Report_ArAging
    @AsOf date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(py.Name, 'Patient') AS PayerName, c.ClaimNumber, p.Mrn, p.LastName + ', ' + p.FirstName AS PatientName, c.DateOfService,
           c.TotalCharge - c.PaidAmount - c.AdjustmentAmount AS Outstanding,
           DATEDIFF(day, c.DateOfService, @AsOf) AS AgeDays,
           CASE WHEN DATEDIFF(day, c.DateOfService, @AsOf) <= 30 THEN '0-30'
                WHEN DATEDIFF(day, c.DateOfService, @AsOf) <= 60 THEN '31-60'
                WHEN DATEDIFF(day, c.DateOfService, @AsOf) <= 90 THEN '61-90'
                WHEN DATEDIFF(day, c.DateOfService, @AsOf) <= 120 THEN '91-120'
                ELSE '120+' END AS Bucket,
           CASE c.Status WHEN 0 THEN 'Ready' WHEN 1 THEN 'Submitted' WHEN 2 THEN 'Accepted' WHEN 3 THEN 'Rejected' WHEN 4 THEN 'Paid'
                         WHEN 5 THEN 'Partially paid' WHEN 6 THEN 'Denied' ELSE 'Closed' END AS StatusText
    FROM dbo.Claim c
    JOIN dbo.Patient p ON p.PatientId = c.PatientId
    LEFT JOIN dbo.Payer py ON py.PayerId = c.PayerId
    WHERE c.Status NOT IN (4, 7) AND c.DateOfService <= @AsOf AND c.TotalCharge - c.PaidAmount - c.AdjustmentAmount > 0
    ORDER BY PayerName, AgeDays DESC;
END
GO

IF OBJECT_ID('dbo.usp_Report_ClaimSummary') IS NOT NULL DROP PROCEDURE dbo.usp_Report_ClaimSummary;
GO
CREATE PROCEDURE dbo.usp_Report_ClaimSummary
    @From date, @To date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT py.Name AS PayerName, COUNT(*) AS Claims, SUM(c.TotalCharge) AS Charged, SUM(c.PaidAmount) AS Paid,
           SUM(c.AdjustmentAmount) AS Adjusted, SUM(c.PatientResponsibility) AS PatientShare,
           SUM(CASE WHEN c.Status IN (3, 6) THEN 1 ELSE 0 END) AS RejectedOrDenied
    FROM dbo.Claim c JOIN dbo.Payer py ON py.PayerId = c.PayerId
    WHERE c.DateOfService >= @From AND c.DateOfService <= @To
    GROUP BY py.Name
    ORDER BY py.Name;
END
GO

IF OBJECT_ID('dbo.usp_Report_ProviderProductivity') IS NOT NULL DROP PROCEDURE dbo.usp_Report_ProviderProductivity;
GO
CREATE PROCEDURE dbo.usp_Report_ProviderProductivity
    @From date, @To date, @SiteId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pr.LastName + ', ' + pr.FirstName AS ProviderName, s.Name AS SiteName,
           SUM(CASE WHEN a.Status = 4 THEN 1 ELSE 0 END) AS Completed,
           SUM(CASE WHEN a.Status = 6 THEN 1 ELSE 0 END) AS NoShows,
           SUM(CASE WHEN a.Status = 5 AND a.LateCancel = 1 THEN 1 ELSE 0 END) AS LateCancels,
           SUM(CASE WHEN a.Source = 1 THEN 1 ELSE 0 END) AS BookedOnline,
           ISNULL((SELECT SUM(ch.Amount * ch.Units) FROM dbo.Charge ch JOIN dbo.Encounter e ON e.EncounterId = ch.EncounterId
                   WHERE e.ProviderId = pr.ProviderId AND e.SiteId = s.SiteId AND e.DateOfService BETWEEN @From AND @To AND ch.Status <> 5), 0) AS Charges
    FROM dbo.Appointment a
    JOIN dbo.Provider pr ON pr.ProviderId = a.ProviderId
    JOIN dbo.Site s ON s.SiteId = a.SiteId
    WHERE a.StartsAt >= @From AND a.StartsAt < DATEADD(day, 1, CAST(@To AS datetime)) AND (@SiteId IS NULL OR a.SiteId = @SiteId)
    GROUP BY pr.ProviderId, pr.LastName, pr.FirstName, s.SiteId, s.Name
    ORDER BY s.Name, ProviderName;
END
GO

-------------------------------------------------------------------------------
-- Housekeeping (SQL Agent jobs)
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Referral_ExpireOverdue') IS NOT NULL DROP PROCEDURE dbo.usp_Referral_ExpireOverdue;
GO
CREATE PROCEDURE dbo.usp_Referral_ExpireOverdue
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Referral SET Status = 5, Version = Version + 1
    WHERE Status = 2 AND ExpiresOn < CAST(GETDATE() AS date) AND AppointmentId IS NULL;
    SELECT @@ROWCOUNT AS Expired;
END
GO

IF OBJECT_ID('dbo.usp_Encounter_FlagUnbilled') IS NOT NULL DROP PROCEDURE dbo.usp_Encounter_FlagUnbilled;
GO
-- Completed visits without charges after 3 days are listed on the billing dashboard.
CREATE PROCEDURE dbo.usp_Encounter_FlagUnbilled
    @Days int = 3
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.EncounterId, e.DateOfService, p.Mrn, p.LastName, p.FirstName, pr.LastName AS ProviderLastName, s.Code AS SiteCode
    FROM dbo.Encounter e
    JOIN dbo.Patient p ON p.PatientId = e.PatientId
    JOIN dbo.Provider pr ON pr.ProviderId = e.ProviderId
    JOIN dbo.Site s ON s.SiteId = e.SiteId
    WHERE e.Status = 0 AND e.DateOfService <= DATEADD(day, -@Days, CAST(GETDATE() AS date))
      AND NOT EXISTS (SELECT 1 FROM dbo.Charge c WHERE c.EncounterId = e.EncounterId)
    ORDER BY e.DateOfService;
END
GO
