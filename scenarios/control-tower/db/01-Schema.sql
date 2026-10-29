-- Control Tower schema (ct). Generated from the EF Core model with
--   ControlTower.LabTools schema --out db\01-Schema.sql
-- followed by the hand-written objects at the end of the file. Run against the ControlTower database.

USE [ControlTower];
GO

IF SCHEMA_ID(N'ct') IS NULL EXEC(N'CREATE SCHEMA [ct];');
GO


CREATE TABLE [ct].[Alert] (
    [Id] bigint NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [RuleCode] varchar(20) NOT NULL,
    [Severity] int NOT NULL,
    [Message] nvarchar(400) NOT NULL,
    [RaisedAt] datetimeoffset NOT NULL,
    [LastSeenAt] datetimeoffset NOT NULL,
    [Occurrences] int NOT NULL,
    [AcknowledgedBy] nvarchar(100) NULL,
    [AcknowledgedAt] datetimeoffset NULL,
    [ResolvedAt] datetimeoffset NULL,
    [Resolution] nvarchar(400) NULL,
    CONSTRAINT [PK_Alert] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Carrier] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(10) NOT NULL,
    [Name] nvarchar(70) NOT NULL,
    [Mode] int NOT NULL,
    [Integration] int NOT NULL,
    [EdiPartnerId] varchar(35) NULL,
    [EdiQualifier] varchar(4) NULL,
    [ApiBaseUrl] nvarchar(200) NULL,
    [TrackingUrlTemplate] nvarchar(200) NULL,
    [Active] bit NOT NULL,
    [OnTimeTargetPercent] decimal(5,2) NOT NULL,
    CONSTRAINT [PK_Carrier] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Customer] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(10) NOT NULL,
    [Name] nvarchar(70) NOT NULL,
    [VatNumber] varchar(20) NULL,
    [Eori] varchar(17) NULL,
    [CountryCode] varchar(2) NOT NULL,
    [ContactEmail] nvarchar(254) NULL,
    [ContactPhone] nvarchar(30) NULL,
    [InvoiceCurrency] varchar(3) NOT NULL,
    [PortalEnabled] bit NOT NULL,
    [CustomsBrokerageAgreed] bit NOT NULL,
    [DomesticSlaDays] int NOT NULL,
    [DiscountPercent] decimal(5,2) NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Customer] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[DailyKpi] (
    [Id] int NOT NULL IDENTITY,
    [Day] date NOT NULL,
    [CustomerId] int NULL,
    [CarrierId] int NULL,
    [Mode] int NOT NULL,
    [ShipmentsBooked] int NOT NULL,
    [ShipmentsDelivered] int NOT NULL,
    [DeliveredOnTime] int NOT NULL,
    [PickedUpOnTime] int NOT NULL,
    [PickupsDue] int NOT NULL,
    [AlertsRaised] int NOT NULL,
    [CriticalAlerts] int NOT NULL,
    [AverageTransitHours] float NOT NULL,
    [Revenue] decimal(18,2) NOT NULL,
    [ChargeableKg] decimal(14,1) NOT NULL,
    [CalculatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_DailyKpi] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Document] (
    [Id] uniqueidentifier NOT NULL,
    [ShipmentId] int NULL,
    [Kind] int NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [Length] bigint NOT NULL,
    [Sha256] varchar(64) NOT NULL,
    [VisibleToCustomer] bit NOT NULL,
    [CreatedBy] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [DeleteAfter] datetimeoffset NULL,
    CONSTRAINT [PK_Document] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[EdiInterchange] (
    [Id] bigint NOT NULL IDENTITY,
    [CarrierId] int NOT NULL,
    [Direction] int NOT NULL,
    [MessageType] varchar(6) NOT NULL,
    [ControlReference] varchar(14) NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [MessageCount] int NOT NULL,
    [Status] int NOT NULL,
    [Error] nvarchar(2000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ProcessedAt] datetimeoffset NULL,
    [Payload] nvarchar(max) NULL,
    CONSTRAINT [PK_EdiInterchange] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Location] (
    [Id] int NOT NULL IDENTITY,
    [Code] varchar(10) NOT NULL,
    [Name] nvarchar(70) NOT NULL,
    [Kind] int NOT NULL,
    [Street] nvarchar(70) NOT NULL,
    [PostalCode] nvarchar(10) NOT NULL,
    [City] nvarchar(35) NOT NULL,
    [CountryCode] varchar(2) NOT NULL,
    [Position] geography NOT NULL,
    [GeofenceRadiusMeters] int NOT NULL,
    [TimeZoneId] varchar(50) NOT NULL,
    [CustomerId] int NULL,
    [OpensAt] time NOT NULL,
    [ClosesAt] time NOT NULL,
    [IsEuCustomsTerritory] bit NOT NULL,
    CONSTRAINT [PK_Location] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[RateCard] (
    [Id] int NOT NULL IDENTITY,
    [CarrierId] int NULL,
    [Mode] int NOT NULL,
    [OriginCountry] varchar(2) NOT NULL,
    [DestinationCountry] varchar(2) NOT NULL,
    [OriginPostalPrefix] varchar(5) NULL,
    [Currency] varchar(3) NOT NULL,
    [MinimumCharge] decimal(18,2) NOT NULL,
    [FuelSurchargePercent] decimal(5,2) NOT NULL,
    [TransitDays] int NOT NULL,
    [ValidFrom] date NOT NULL,
    [ValidTo] date NOT NULL,
    CONSTRAINT [PK_RateCard] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Scan] (
    [Id] bigint NOT NULL IDENTITY,
    [Sscc] varchar(18) NOT NULL,
    [PackageId] int NULL,
    [ShipmentId] int NULL,
    [Kind] int NOT NULL,
    [TerminalId] int NOT NULL,
    [DoorOrBay] nvarchar(10) NULL,
    [VehiclePlate] nvarchar(12) NULL,
    [DeviceId] varchar(40) NOT NULL,
    [UserName] nvarchar(100) NOT NULL,
    [ScannedAt] datetimeoffset NOT NULL,
    [ReceivedAt] datetimeoffset NOT NULL,
    [Remarks] nvarchar(200) NULL,
    [ClientScanId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Scan] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Vehicle] (
    [Id] int NOT NULL IDENTITY,
    [Plate] nvarchar(12) NOT NULL,
    [CarrierId] int NULL,
    [TelematicsUnitId] varchar(32) NULL,
    [HasReefer] bit NOT NULL,
    [AdrCertified] bit NOT NULL,
    [MaxPayloadKg] decimal(10,0) NOT NULL,
    [LoadingMeters] decimal(4,1) NOT NULL,
    [LastPositionAt] datetimeoffset NULL,
    [LastPosition] geography NULL,
    [LastTemperature] decimal(5,1) NULL,
    [LastSpeedKmh] int NULL,
    CONSTRAINT [PK_Vehicle] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[VehiclePosition] (
    [Id] bigint NOT NULL IDENTITY,
    [VehicleId] int NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [Position] geography NOT NULL,
    [SpeedKmh] int NOT NULL,
    [HeadingDegrees] int NOT NULL,
    [IgnitionOn] bit NOT NULL,
    [ReeferTemperature] decimal(5,1) NULL,
    [ReeferSetpoint] decimal(5,1) NULL,
    [DoorOpen] bit NULL,
    CONSTRAINT [PK_VehiclePosition] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ct].[Shipment] (
    [Id] int NOT NULL IDENTITY,
    [Number] varchar(12) NOT NULL,
    [CustomerId] int NOT NULL,
    [CustomerReference] nvarchar(35) NULL,
    [Mode] int NOT NULL,
    [IncotermCode] varchar(3) NOT NULL,
    [Status] int NOT NULL,
    [OriginId] int NOT NULL,
    [DestinationId] int NOT NULL,
    [ShipperName] nvarchar(70) NOT NULL,
    [ConsigneeName] nvarchar(70) NOT NULL,
    [ConsigneeEmail] nvarchar(254) NULL,
    [NotifyPhone] nvarchar(30) NULL,
    [PlannedPickup] datetimeoffset NOT NULL,
    [PlannedDelivery] datetimeoffset NOT NULL,
    [ActualPickup] datetimeoffset NULL,
    [ActualDelivery] datetimeoffset NULL,
    [Eta] datetimeoffset NULL,
    [EtaCalculatedAt] datetimeoffset NULL,
    [GoodsDescription] nvarchar(350) NOT NULL,
    [DeclaredValue] decimal(18,2) NULL,
    [DeclaredValueCurrency] varchar(3) NOT NULL,
    [TemperatureMin] decimal(5,1) NULL,
    [TemperatureMax] decimal(5,1) NULL,
    [PodSignedBy] nvarchar(70) NULL,
    [CancelReason] nvarchar(200) NULL,
    [CreatedBy] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_Shipment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shipment_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [ct].[Customer] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Shipment_Location_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [ct].[Location] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Shipment_Location_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [ct].[Location] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ct].[RateBreak] (
    [Id] int NOT NULL IDENTITY,
    [RateCardId] int NOT NULL,
    [FromKg] decimal(10,1) NOT NULL,
    [PricePer100Kg] decimal(10,2) NOT NULL,
    CONSTRAINT [PK_RateBreak] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RateBreak_RateCard_RateCardId] FOREIGN KEY ([RateCardId]) REFERENCES [ct].[RateCard] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ct].[Charge] (
    [Id] int NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [Code] varchar(3) NOT NULL,
    [Description] nvarchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] varchar(3) NOT NULL,
    [Invoiced] bit NOT NULL,
    [InvoiceNumber] nvarchar(20) NULL,
    CONSTRAINT [PK_Charge] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Charge_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [ct].[Shipment] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ct].[CustomsDeclaration] (
    [Id] int NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [Type] int NOT NULL,
    [Status] int NOT NULL,
    [Lrn] varchar(22) NOT NULL,
    [Mrn] varchar(18) NULL,
    [DeclarantEori] varchar(17) NOT NULL,
    [ConsignorEori] varchar(17) NULL,
    [ConsigneeEori] varchar(17) NULL,
    [OfficeOfDeclaration] varchar(8) NOT NULL,
    [OfficeOfExit] varchar(8) NULL,
    [CurrencyCode] varchar(3) NOT NULL,
    [RejectionReason] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [SubmittedAt] datetimeoffset NULL,
    [AcceptedAt] datetimeoffset NULL,
    [ReleasedAt] datetimeoffset NULL,
    [SubmitAttempts] int NOT NULL,
    CONSTRAINT [PK_CustomsDeclaration] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomsDeclaration_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [ct].[Shipment] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ct].[Leg] (
    [Id] int NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [Sequence] int NOT NULL,
    [Mode] int NOT NULL,
    [CarrierId] int NULL,
    [FromId] int NOT NULL,
    [ToId] int NOT NULL,
    [Status] int NOT NULL,
    [PlannedDeparture] datetimeoffset NOT NULL,
    [PlannedArrival] datetimeoffset NOT NULL,
    [ActualDeparture] datetimeoffset NULL,
    [ActualArrival] datetimeoffset NULL,
    [Eta] datetimeoffset NULL,
    [CarrierReference] nvarchar(35) NULL,
    [VesselOrFlight] nvarchar(35) NULL,
    [VehicleId] int NULL,
    [DistanceKm] float NULL,
    CONSTRAINT [PK_Leg] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leg_Carrier_CarrierId] FOREIGN KEY ([CarrierId]) REFERENCES [ct].[Carrier] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leg_Location_FromId] FOREIGN KEY ([FromId]) REFERENCES [ct].[Location] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leg_Location_ToId] FOREIGN KEY ([ToId]) REFERENCES [ct].[Location] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leg_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [ct].[Shipment] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Leg_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [ct].[Vehicle] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [ct].[Milestone] (
    [Id] bigint NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [Code] varchar(3) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [RecordedAt] datetimeoffset NOT NULL,
    [LocationId] int NULL,
    [LocationText] nvarchar(100) NULL,
    [Source] nvarchar(60) NOT NULL,
    [Remarks] nvarchar(350) NULL,
    [VisibleToCustomer] bit NOT NULL,
    CONSTRAINT [PK_Milestone] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Milestone_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [ct].[Shipment] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ct].[Package] (
    [Id] int NOT NULL IDENTITY,
    [ShipmentId] int NOT NULL,
    [Sscc] varchar(18) NOT NULL,
    [Kind] int NOT NULL,
    [Quantity] int NOT NULL,
    [WeightKg] decimal(10,2) NOT NULL,
    [LengthCm] int NOT NULL,
    [WidthCm] int NOT NULL,
    [HeightCm] int NOT NULL,
    [Stackable] bit NOT NULL,
    [Marks] nvarchar(140) NULL,
    [DgUnNumber] varchar(4) NULL,
    [DgShippingName] nvarchar(200) NULL,
    [DgClass] varchar(5) NULL,
    [DgPackingGroup] varchar(3) NULL,
    [DgNetQuantity] decimal(10,3) NULL,
    [DgNetQuantityUnit] nvarchar(3) NULL,
    [DgLimitedQuantity] bit NULL,
    [DgTunnelCode] nvarchar(5) NULL,
    [ContainerNumber] varchar(11) NULL,
    [SealNumber] nvarchar(20) NULL,
    CONSTRAINT [PK_Package] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Package_Shipment_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [ct].[Shipment] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ct].[DeclarationLine] (
    [Id] int NOT NULL IDENTITY,
    [CustomsDeclarationId] int NOT NULL,
    [LineNumber] int NOT NULL,
    [CommodityCode] varchar(10) NOT NULL,
    [Description] nvarchar(280) NOT NULL,
    [OriginCountry] varchar(2) NOT NULL,
    [NetMassKg] decimal(12,3) NOT NULL,
    [GrossMassKg] decimal(12,3) NOT NULL,
    [StatisticalValue] decimal(18,2) NOT NULL,
    [ProcedureCode] varchar(4) NOT NULL,
    [Packages] int NOT NULL,
    [PackageKindCode] varchar(2) NOT NULL,
    CONSTRAINT [PK_DeclarationLine] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeclarationLine_CustomsDeclaration_CustomsDeclarationId] FOREIGN KEY ([CustomsDeclarationId]) REFERENCES [ct].[CustomsDeclaration] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Alert_ShipmentId_RuleCode_ResolvedAt] ON [ct].[Alert] ([ShipmentId], [RuleCode], [ResolvedAt]);
GO


CREATE UNIQUE INDEX [IX_Carrier_Code] ON [ct].[Carrier] ([Code]);
GO


CREATE INDEX [IX_Charge_ShipmentId] ON [ct].[Charge] ([ShipmentId]);
GO


CREATE UNIQUE INDEX [IX_Customer_Code] ON [ct].[Customer] ([Code]);
GO


CREATE UNIQUE INDEX [IX_CustomsDeclaration_Lrn] ON [ct].[CustomsDeclaration] ([Lrn]);
GO


CREATE INDEX [IX_CustomsDeclaration_Mrn] ON [ct].[CustomsDeclaration] ([Mrn]);
GO


CREATE INDEX [IX_CustomsDeclaration_ShipmentId] ON [ct].[CustomsDeclaration] ([ShipmentId]);
GO


CREATE INDEX [IX_DailyKpi_Day_CustomerId_CarrierId_Mode] ON [ct].[DailyKpi] ([Day], [CustomerId], [CarrierId], [Mode]);
GO


CREATE INDEX [IX_DeclarationLine_CustomsDeclarationId] ON [ct].[DeclarationLine] ([CustomsDeclarationId]);
GO


CREATE INDEX [IX_Document_ShipmentId] ON [ct].[Document] ([ShipmentId]);
GO


CREATE INDEX [IX_EdiInterchange_CarrierId_Direction_ControlReference] ON [ct].[EdiInterchange] ([CarrierId], [Direction], [ControlReference]);
GO


CREATE INDEX [IX_Leg_CarrierId] ON [ct].[Leg] ([CarrierId]);
GO


CREATE INDEX [IX_Leg_CarrierReference] ON [ct].[Leg] ([CarrierReference]);
GO


CREATE INDEX [IX_Leg_FromId] ON [ct].[Leg] ([FromId]);
GO


CREATE UNIQUE INDEX [IX_Leg_ShipmentId_Sequence] ON [ct].[Leg] ([ShipmentId], [Sequence]);
GO


CREATE INDEX [IX_Leg_ToId] ON [ct].[Leg] ([ToId]);
GO


CREATE INDEX [IX_Leg_VehicleId] ON [ct].[Leg] ([VehicleId]);
GO


CREATE UNIQUE INDEX [IX_Location_Code] ON [ct].[Location] ([Code]);
GO


CREATE INDEX [IX_Milestone_ShipmentId_OccurredAt] ON [ct].[Milestone] ([ShipmentId], [OccurredAt]);
GO


CREATE INDEX [IX_Package_ShipmentId] ON [ct].[Package] ([ShipmentId]);
GO


CREATE UNIQUE INDEX [IX_Package_Sscc] ON [ct].[Package] ([Sscc]);
GO


CREATE INDEX [IX_RateBreak_RateCardId] ON [ct].[RateBreak] ([RateCardId]);
GO


CREATE UNIQUE INDEX [IX_Scan_ClientScanId] ON [ct].[Scan] ([ClientScanId]);
GO


CREATE INDEX [IX_Scan_Sscc_ScannedAt] ON [ct].[Scan] ([Sscc], [ScannedAt]);
GO


CREATE INDEX [IX_Shipment_CustomerId_Status] ON [ct].[Shipment] ([CustomerId], [Status]);
GO


CREATE INDEX [IX_Shipment_DestinationId] ON [ct].[Shipment] ([DestinationId]);
GO


CREATE UNIQUE INDEX [IX_Shipment_Number] ON [ct].[Shipment] ([Number]);
GO


CREATE INDEX [IX_Shipment_OriginId] ON [ct].[Shipment] ([OriginId]);
GO


CREATE INDEX [IX_Shipment_PlannedDelivery] ON [ct].[Shipment] ([PlannedDelivery]);
GO


CREATE UNIQUE INDEX [IX_Vehicle_Plate] ON [ct].[Vehicle] ([Plate]);
GO


CREATE UNIQUE INDEX [IX_Vehicle_TelematicsUnitId] ON [ct].[Vehicle] ([TelematicsUnitId]) WHERE [TelematicsUnitId] IS NOT NULL;
GO


CREATE INDEX [IX_VehiclePosition_VehicleId_RecordedAt] ON [ct].[VehiclePosition] ([VehicleId], [RecordedAt]);
GO




-- ---------------------------------------------------------------------------------------------
-- Hand-written objects (not part of the EF model)
-- ---------------------------------------------------------------------------------------------

-- House shipment numbers (PFyy-nnnnnnc); ShipmentNumberAllocator takes the next value.
IF OBJECT_ID(N'ct.ShipmentNumbers', N'SO') IS NULL
    CREATE SEQUENCE [ct].[ShipmentNumbers] AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 MAXVALUE 999999 CYCLE CACHE 50;
GO

-- Document content. Rows are written before the ct.Document metadata row in the same transaction,
-- so there is no foreign key; the document store deletes both.
CREATE TABLE [ct].[DocumentContent] (
    [Id] uniqueidentifier ROWGUIDCOL NOT NULL,
    [Content] varbinary(max) FILESTREAM NULL,
    [CreatedAt] datetime2(3) NOT NULL CONSTRAINT [DF_DocumentContent_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_DocumentContent] PRIMARY KEY CLUSTERED ([Id]) ON [ControlTowerData],
    CONSTRAINT [UQ_DocumentContent_Id] UNIQUE NONCLUSTERED ([Id]) ON [ControlTowerData]
) ON [ControlTowerData] FILESTREAM_ON [ControlTowerDocuments];
GO

-- Orphaned content (writer crashed between the two inserts) is removed by the nightly maintenance job.
CREATE OR ALTER PROCEDURE [ct].[PurgeOrphanDocumentContent]
AS
BEGIN
    SET NOCOUNT ON;
    DELETE c
    FROM ct.DocumentContent c
    WHERE c.CreatedAt < DATEADD(HOUR, -6, SYSUTCDATETIME())
      AND NOT EXISTS (SELECT 1 FROM ct.Document d WHERE d.Id = c.Id);
    SELECT @@ROWCOUNT AS Deleted;
END
GO

-- Map and ETA lookups: open legs by vehicle, latest positions.
CREATE NONCLUSTERED INDEX [IX_Leg_Status_Vehicle] ON [ct].[Leg] ([Status], [VehicleId]) INCLUDE ([ShipmentId], [PlannedArrival], [Eta]);
GO
CREATE SPATIAL INDEX [SIX_Location_Position] ON [ct].[Location] ([Position]) USING GEOGRAPHY_AUTO_GRID;
GO

-- Reporting view used by the operations workbook and the finance reconciliation.
CREATE OR ALTER VIEW [ct].[ShipmentOverview]
AS
SELECT s.Id, s.Number, c.Code AS CustomerCode, c.Name AS CustomerName, s.CustomerReference, s.Mode, s.Status,
       o.Code AS OriginCode, o.City AS OriginCity, o.CountryCode AS OriginCountry,
       d.Code AS DestinationCode, d.City AS DestinationCity, d.CountryCode AS DestinationCountry,
       s.PlannedPickup, s.ActualPickup, s.PlannedDelivery, s.ActualDelivery, s.Eta,
       (SELECT SUM(p.WeightKg * p.Quantity) FROM ct.Package p WHERE p.ShipmentId = s.Id) AS GrossWeightKg,
       (SELECT SUM(ch.Amount) FROM ct.Charge ch WHERE ch.ShipmentId = s.Id AND ch.Currency = 'EUR') AS ChargesEur,
       (SELECT COUNT(*) FROM ct.Alert a WHERE a.ShipmentId = s.Id AND a.ResolvedAt IS NULL) AS OpenAlerts
FROM ct.Shipment s
JOIN ct.Customer c ON c.Id = s.CustomerId
JOIN ct.Location o ON o.Id = s.OriginId
JOIN ct.Location d ON d.Id = s.DestinationId;
GO
