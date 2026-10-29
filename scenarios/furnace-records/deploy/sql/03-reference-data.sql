-- Furnaces, sensors and recipes of the Adatum Thermal Processing plant. Run once after 01-create-database.sql;
-- later changes are made on the portal (Furnaces, Recipes).
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO
USE [FurnaceRecords];
GO

IF NOT EXISTS (SELECT 1 FROM [fr].[Furnaces] WHERE [Code] = N'F01')
    INSERT [fr].[Furnaces] ([Code], [Name], [FurnaceClass], [Instrumentation], [Active], [UniformitySurveyDue], [AccuracyTestDue])
    VALUES (N'F01', N'Solution treatment furnace 1', 2, N'B', 1, DATEADD(MONTH, 3, CAST(GETDATE() AS date)), DATEADD(MONTH, 1, CAST(GETDATE() AS date)));
IF NOT EXISTS (SELECT 1 FROM [fr].[Furnaces] WHERE [Code] = N'F02')
    INSERT [fr].[Furnaces] ([Code], [Name], [FurnaceClass], [Instrumentation], [Active], [UniformitySurveyDue], [AccuracyTestDue])
    VALUES (N'F02', N'Ageing oven 2', 3, N'C', 1, DATEADD(MONTH, 3, CAST(GETDATE() AS date)), DATEADD(MONTH, 1, CAST(GETDATE() AS date)));
GO

DECLARE @channels TABLE ([Furnace] nvarchar(6), [Channel] int, [Name] nvarchar(50), [Role] nvarchar(20), [Type] nvarchar(2));
INSERT @channels VALUES
    (N'F01', 0, N'Control', N'Control', N'N'),
    (N'F01', 1, N'Over-temperature', N'OverTemperature', N'N'),
    (N'F01', 2, N'Recorder hot', N'Recorder', N'N'),
    (N'F01', 3, N'Recorder cold', N'Recorder', N'N'),
    (N'F01', 4, N'Load 1', N'Load', N'N'),
    (N'F01', 5, N'Load 2', N'Load', N'N'),
    (N'F01', 6, N'Load 3', N'Load', N'N'),
    (N'F01', 7, N'Load 4', N'Load', N'N'),
    (N'F01', 8, N'Survey front left', N'Survey', N'N'),
    (N'F01', 9, N'Survey front right', N'Survey', N'N'),
    (N'F01', 10, N'Survey rear left', N'Survey', N'N'),
    (N'F01', 11, N'Survey rear right', N'Survey', N'N'),
    (N'F01', 12, N'Survey centre', N'Survey', N'N'),
    (N'F01', 13, N'Survey top', N'Survey', N'N'),
    (N'F01', 14, N'Survey bottom', N'Survey', N'N'),
    (N'F01', 15, N'Survey door', N'Survey', N'N'),
    (N'F01', 16, N'Survey back wall', N'Survey', N'N'),
    (N'F02', 0, N'Control', N'Control', N'K'),
    (N'F02', 1, N'Over-temperature', N'OverTemperature', N'K'),
    (N'F02', 2, N'Recorder', N'Recorder', N'K'),
    (N'F02', 3, N'Load 1', N'Load', N'K'),
    (N'F02', 4, N'Load 2', N'Load', N'K');

INSERT [fr].[Channels] ([FurnaceId], [Channel], [Name], [Role], [Type], [SensorSerial], [Enabled])
SELECT f.[Id], c.[Channel], c.[Name], c.[Role], c.[Type], CONCAT(c.[Furnace], N'-TC', FORMAT(c.[Channel], N'00')), 1
FROM @channels c
JOIN [fr].[Furnaces] f ON f.[Code] = c.[Furnace]
WHERE NOT EXISTS (SELECT 1 FROM [fr].[Channels] x WHERE x.[FurnaceId] = f.[Id] AND x.[Channel] = c.[Channel]);
GO

DECLARE @recipes TABLE ([Code] nvarchar(20), [Name] nvarchar(100), [Specification] nvarchar(100), [Setpoint] float, [Minus] float, [Plus] float,
    [Soak] int, [MaxSoak] int NULL, [MaxRamp] float NULL, [MaxQuench] int NULL);
INSERT @recipes VALUES
    (N'ST-7075', N'Solution treat 7075', N'AMS 2770', 480, 6, 6, 50, 120, 15, 15),
    (N'ST-2024', N'Solution treat 2024', N'AMS 2770', 493, 6, 6, 40, 90, 15, 15),
    (N'AG-T6', N'Age T6', N'AMS 2770', 121, 8, 8, 60, NULL, NULL, NULL),
    (N'TUS-480', N'Uniformity survey 480 °C', N'AMS 2750', 480, 6, 6, 35, NULL, NULL, NULL),
    (N'TUS-177', N'Uniformity survey 177 °C', N'AMS 2750', 177, 8, 8, 35, NULL, NULL, NULL),
    (N'AG-T73', N'Age T73 second step', N'AMS 2770', 177, 8, 8, 45, NULL, 10, NULL);

INSERT [fr].[Recipes] ([Code], [Name], [Specification], [SetpointCelsius], [ToleranceMinusCelsius], [TolerancePlusCelsius],
    [SoakMinutes], [MaxSoakMinutes], [MaxRampCelsiusPerMinute], [MaxQuenchDelaySeconds], [Active])
SELECT r.[Code], r.[Name], r.[Specification], r.[Setpoint], r.[Minus], r.[Plus], r.[Soak], r.[MaxSoak], r.[MaxRamp], r.[MaxQuench], 1
FROM @recipes r
WHERE NOT EXISTS (SELECT 1 FROM [fr].[Recipes] x WHERE x.[Code] = r.[Code]);
GO
