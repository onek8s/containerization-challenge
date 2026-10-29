-- Logins for the group managed service accounts. Replace CORP if the domain NetBIOS name differs.
--   CORP\svc-onbweb$   portal application pool (onboarding.corp.local)
--   CORP\svc-onbflow$  workflow host service (onboarding-flow.corp.local)
--   CORP\svc-idm$      provisioning service (idm01.corp.local)
USE master;
GO
IF SUSER_ID(N'CORP\svc-onbweb$') IS NULL CREATE LOGIN [CORP\svc-onbweb$] FROM WINDOWS;
IF SUSER_ID(N'CORP\svc-onbflow$') IS NULL CREATE LOGIN [CORP\svc-onbflow$] FROM WINDOWS;
IF SUSER_ID(N'CORP\svc-idm$') IS NULL CREATE LOGIN [CORP\svc-idm$] FROM WINDOWS;
IF SUSER_ID(N'CORP\APP-Onboarding-IT') IS NULL CREATE LOGIN [CORP\APP-Onboarding-IT] FROM WINDOWS;
GO

USE StaffOnboarding;
GO
IF USER_ID(N'svc-onbweb') IS NULL CREATE USER [svc-onbweb] FOR LOGIN [CORP\svc-onbweb$];
IF USER_ID(N'svc-onbflow') IS NULL CREATE USER [svc-onbflow] FOR LOGIN [CORP\svc-onbflow$];
IF USER_ID(N'svc-idm') IS NULL CREATE USER [svc-idm] FOR LOGIN [CORP\svc-idm$];
IF USER_ID(N'onboarding-it') IS NULL CREATE USER [onboarding-it] FOR LOGIN [CORP\APP-Onboarding-IT];
GO
GRANT SELECT ON SCHEMA::onb TO [svc-onbweb];
GRANT EXECUTE ON onb.CreateRequest TO [svc-onbweb];
GRANT EXECUTE ON onb.MarkStartFailed TO [svc-onbweb];
GRANT EXECUTE ON onb.AddTimeline TO [svc-onbweb];

GRANT SELECT, EXECUTE ON SCHEMA::onb TO [svc-onbflow];

GRANT SELECT, EXECUTE ON SCHEMA::idm TO [svc-idm];

GRANT SELECT ON SCHEMA::onb TO [onboarding-it];
GRANT SELECT ON SCHEMA::idm TO [onboarding-it];
GO

USE StaffOnboardingInstances;
GO
-- Roles are created by SqlWorkflowInstanceStoreSchema.sql (install-instance-store.ps1).
IF USER_ID(N'svc-onbflow') IS NULL CREATE USER [svc-onbflow] FOR LOGIN [CORP\svc-onbflow$];
IF USER_ID(N'onboarding-it') IS NULL CREATE USER [onboarding-it] FOR LOGIN [CORP\APP-Onboarding-IT];
ALTER ROLE [System.Activities.DurableInstancing.InstanceStoreUsers] ADD MEMBER [svc-onbflow];
ALTER ROLE [System.Activities.DurableInstancing.InstanceStoreObservers] ADD MEMBER [onboarding-it];
GRANT SELECT ON SCHEMA::[System.Activities.DurableInstancing] TO [onboarding-it];
GO
