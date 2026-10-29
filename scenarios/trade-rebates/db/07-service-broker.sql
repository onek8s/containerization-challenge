-- Statement requests. The target queue has no internal activation: the Service Broker External Activator
-- service (see deploy\activator) starts TradeRebates.StatementWorker.exe when messages arrive.
:on error exit
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
USE [$(DatabaseName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.service_message_types WHERE name = N'//contosofoods.com/TradeRebates/StatementRequest')
    CREATE MESSAGE TYPE [//contosofoods.com/TradeRebates/StatementRequest] VALIDATION = WELL_FORMED_XML;
GO

IF NOT EXISTS (SELECT 1 FROM sys.service_contracts WHERE name = N'//contosofoods.com/TradeRebates/StatementContract')
    CREATE CONTRACT [//contosofoods.com/TradeRebates/StatementContract]
        ([//contosofoods.com/TradeRebates/StatementRequest] SENT BY INITIATOR);
GO

IF OBJECT_ID(N'rebate.StatementRequestQueue', N'SQ') IS NULL
    CREATE QUEUE rebate.StatementRequestQueue WITH STATUS = ON, RETENTION = OFF, POISON_MESSAGE_HANDLING (STATUS = ON);
GO

IF OBJECT_ID(N'rebate.StatementInitiatorQueue', N'SQ') IS NULL
    CREATE QUEUE rebate.StatementInitiatorQueue WITH STATUS = ON, RETENTION = OFF;
GO

IF NOT EXISTS (SELECT 1 FROM sys.services WHERE name = N'//contosofoods.com/TradeRebates/StatementService')
    CREATE SERVICE [//contosofoods.com/TradeRebates/StatementService]
        ON QUEUE rebate.StatementRequestQueue ([//contosofoods.com/TradeRebates/StatementContract]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.services WHERE name = N'//contosofoods.com/TradeRebates/StatementRequester')
    CREATE SERVICE [//contosofoods.com/TradeRebates/StatementRequester]
        ON QUEUE rebate.StatementInitiatorQueue ([//contosofoods.com/TradeRebates/StatementContract]);
GO

----------------------------------------------------------------------------------------------
-- External activation: QUEUE_ACTIVATION events go to the notification service the activator listens on.
----------------------------------------------------------------------------------------------
IF OBJECT_ID(N'rebate.ExternalActivatorQueue', N'SQ') IS NULL
    CREATE QUEUE rebate.ExternalActivatorQueue WITH STATUS = ON, RETENTION = OFF;
GO

IF NOT EXISTS (SELECT 1 FROM sys.services WHERE name = N'//contosofoods.com/TradeRebates/ExternalActivator')
    CREATE SERVICE [//contosofoods.com/TradeRebates/ExternalActivator]
        ON QUEUE rebate.ExternalActivatorQueue ([http://schemas.microsoft.com/SQL/Notifications/PostEventNotification]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.event_notifications WHERE name = N'StatementRequestActivation')
    CREATE EVENT NOTIFICATION StatementRequestActivation
        ON QUEUE rebate.StatementRequestQueue
        FOR QUEUE_ACTIVATION
        TO SERVICE N'//contosofoods.com/TradeRebates/ExternalActivator', N'current database';
GO
