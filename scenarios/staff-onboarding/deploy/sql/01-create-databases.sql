-- Staff Onboarding databases. Run as a sysadmin on sql01.
-- StaffOnboarding          requests, tasks, timeline (portal, workflow host) and provisioning journal (idm01)
-- StaffOnboardingInstances workflow instance store (System.Activities.DurableInstancing schema)

IF DB_ID(N'StaffOnboarding') IS NULL
    CREATE DATABASE StaffOnboarding;
GO
IF DB_ID(N'StaffOnboardingInstances') IS NULL
    CREATE DATABASE StaffOnboardingInstances;
GO
ALTER DATABASE StaffOnboarding SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO
