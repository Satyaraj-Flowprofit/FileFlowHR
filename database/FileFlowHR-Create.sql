IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE TABLE [Company] (
        [CompanyId] bigint NOT NULL IDENTITY,
        [CompanyCode] varchar(30) NOT NULL,
        [LegalName] nvarchar(200) NOT NULL,
        [CountryCode] char(2) NOT NULL,
        [Status] varchar(20) NOT NULL,
        CONSTRAINT [PK_Company] PRIMARY KEY ([CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [CompanyLoginProvider] (
        [CompanyLoginProviderId] bigint NOT NULL IDENTITY,
        [ProviderType] varchar(20) NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [Authority] varchar(400) NULL,
        [ClientId] varchar(200) NULL,
        [SecretName] varchar(100) NULL,
        [EmailDomain] varchar(253) NULL,
        [RequireMfa] bit NOT NULL,
        [IsEnabled] bit NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_CompanyLoginProvider] PRIMARY KEY ([CompanyLoginProviderId]),
        CONSTRAINT [FK_CompanyLoginProvider_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema + N'.[CompanyLoginProviderHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema1 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [Person] (
        [PersonId] bigint NOT NULL IDENTITY,
        [PersonNumber] varchar(20) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(320) NULL,
        [Phone] varchar(30) NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_Person] PRIMARY KEY ([PersonId]),
        CONSTRAINT [FK_Person_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema1 + N'.[PersonHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema2 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [Region] (
        [RegionId] bigint NOT NULL IDENTITY,
        [RegionName] nvarchar(100) NOT NULL,
        [CountryCode] char(2) NOT NULL,
        [TimeZone] varchar(64) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_Region] PRIMARY KEY ([RegionId]),
        CONSTRAINT [FK_Region_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema2 + N'.[RegionHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAccount] (
        [UserAccountId] bigint NOT NULL IDENTITY,
        [CompanyId] bigint NOT NULL,
        [PersonId] bigint NULL,
        [UserType] varchar(10) NOT NULL,
        [Status] varchar(10) NOT NULL,
        [LastSignInAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(30) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_UserAccount] PRIMARY KEY ([UserAccountId]),
        CONSTRAINT [FK_UserAccount_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema3 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [Employment] (
        [EmploymentId] bigint NOT NULL IDENTITY,
        [PersonId] bigint NOT NULL,
        [HireDate] date NOT NULL,
        [TerminationDate] date NULL,
        [EmploymentType] varchar(20) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_Employment] PRIMARY KEY ([EmploymentId]),
        CONSTRAINT [FK_Employment_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Employment_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Person] ([PersonId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema3 + N'.[EmploymentHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema4 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [RoleGrant] (
        [RoleGrantId] bigint NOT NULL IDENTITY,
        [PersonId] bigint NOT NULL,
        [RoleCode] varchar(20) NOT NULL,
        [ScopeType] varchar(20) NOT NULL,
        [ScopeId] bigint NULL,
        [GrantedBy] bigint NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_RoleGrant] PRIMARY KEY ([RoleGrantId]),
        CONSTRAINT [CK_RoleGrantNotSelf] CHECK ([GrantedBy] IS NULL OR [GrantedBy] <> [PersonId]),
        CONSTRAINT [FK_RoleGrant_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGrant_Person_GrantedBy] FOREIGN KEY ([GrantedBy]) REFERENCES [Person] ([PersonId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoleGrant_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Person] ([PersonId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema4 + N'.[RoleGrantHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema5 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [BusinessUnit] (
        [BusinessUnitId] bigint NOT NULL IDENTITY,
        [RegionId] bigint NOT NULL,
        [ParentBusinessUnitId] bigint NULL,
        [BusinessUnitName] nvarchar(150) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_BusinessUnit] PRIMARY KEY ([BusinessUnitId]),
        CONSTRAINT [FK_BusinessUnit_BusinessUnit_ParentBusinessUnitId] FOREIGN KEY ([ParentBusinessUnitId]) REFERENCES [BusinessUnit] ([BusinessUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessUnit_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BusinessUnit_Region_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Region] ([RegionId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema5 + N'.[BusinessUnitHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema6 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [Location] (
        [LocationId] bigint NOT NULL IDENTITY,
        [RegionId] bigint NOT NULL,
        [LocationName] nvarchar(150) NOT NULL,
        [City] nvarchar(100) NULL,
        [CountryCode] char(2) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_Location] PRIMARY KEY ([LocationId]),
        CONSTRAINT [FK_Location_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Location_Region_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Region] ([RegionId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema6 + N'.[LocationHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE TABLE [UserClaim] (
        [UserClaimId] int NOT NULL IDENTITY,
        [UserAccountId] bigint NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaim] PRIMARY KEY ([UserClaimId]),
        CONSTRAINT [FK_UserClaim_UserAccount_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccount] ([UserAccountId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE TABLE [UserExternalLogin] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserAccountId] bigint NOT NULL,
        CONSTRAINT [PK_UserExternalLogin] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserExternalLogin_UserAccount_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccount] ([UserAccountId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE TABLE [UserToken] (
        [UserAccountId] bigint NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserToken] PRIMARY KEY ([UserAccountId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserToken_UserAccount_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccount] ([UserAccountId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema7 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [Position] (
        [PositionId] bigint NOT NULL IDENTITY,
        [PositionCode] varchar(30) NOT NULL,
        [Title] nvarchar(150) NOT NULL,
        [BusinessUnitId] bigint NOT NULL,
        [LocationId] bigint NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_Position] PRIMARY KEY ([PositionId]),
        CONSTRAINT [FK_Position_BusinessUnit_BusinessUnitId] FOREIGN KEY ([BusinessUnitId]) REFERENCES [BusinessUnit] ([BusinessUnitId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Position_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Position_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [Location] ([LocationId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema7 + N'.[PositionHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema8 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [JobPosting] (
        [JobPostingId] bigint NOT NULL IDENTITY,
        [PositionId] bigint NOT NULL,
        [PostingTitle] nvarchar(150) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Visibility] varchar(20) NOT NULL,
        [OpenDate] date NOT NULL,
        [CloseDate] date NULL,
        [Status] varchar(20) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_JobPosting] PRIMARY KEY ([JobPostingId]),
        CONSTRAINT [FK_JobPosting_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JobPosting_Position_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [Position] ([PositionId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema8 + N'.[JobPostingHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema9 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [PositionAssignment] (
        [PositionAssignmentId] bigint NOT NULL IDENTITY,
        [PersonId] bigint NOT NULL,
        [PositionId] bigint NOT NULL,
        [AssignmentType] varchar(20) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_PositionAssignment] PRIMARY KEY ([PositionAssignmentId]),
        CONSTRAINT [CK_InterimHasEnd] CHECK ([AssignmentType] = ''primary'' OR [EffectiveTo] IS NOT NULL),
        CONSTRAINT [FK_PositionAssignment_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PositionAssignment_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Person] ([PersonId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PositionAssignment_Position_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [Position] ([PositionId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema9 + N'.[PositionAssignmentHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema10 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [PositionReportingLine] (
        [PositionReportingLineId] bigint NOT NULL IDENTITY,
        [PositionId] bigint NOT NULL,
        [ManagerPositionId] bigint NOT NULL,
        [LineType] varchar(20) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        CONSTRAINT [PK_PositionReportingLine] PRIMARY KEY ([PositionReportingLineId]),
        CONSTRAINT [CK_ReportingLineNotSelf] CHECK ([PositionId] <> [ManagerPositionId]),
        CONSTRAINT [FK_PositionReportingLine_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PositionReportingLine_Position_ManagerPositionId] FOREIGN KEY ([ManagerPositionId]) REFERENCES [Position] ([PositionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PositionReportingLine_Position_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [Position] ([PositionId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema10 + N'.[PositionReportingLineHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    DECLARE @historyTableSchema11 nvarchar(max) = QUOTENAME(SCHEMA_NAME())
    EXEC(N'CREATE TABLE [JobApplication] (
        [JobApplicationId] bigint NOT NULL IDENTITY,
        [JobPostingId] bigint NOT NULL,
        [PersonId] bigint NOT NULL,
        [Source] varchar(20) NOT NULL,
        [ResumePath] varchar(500) NULL,
        [AppliedAt] datetime2 NOT NULL,
        [Status] varchar(20) NOT NULL,
        [UpdatedBy] bigint NULL,
        [ValidFrom] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
        [ValidTo] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
        [CompanyId] bigint NOT NULL,
        CONSTRAINT [PK_JobApplication] PRIMARY KEY ([JobApplicationId]),
        CONSTRAINT [FK_JobApplication_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Company] ([CompanyId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JobApplication_JobPosting_JobPostingId] FOREIGN KEY ([JobPostingId]) REFERENCES [JobPosting] ([JobPostingId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JobApplication_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Person] ([PersonId]) ON DELETE NO ACTION,
        PERIOD FOR SYSTEM_TIME([ValidFrom], [ValidTo])
    ) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + @historyTableSchema11 + N'.[JobApplicationHistory]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessUnit_CompanyId] ON [BusinessUnit] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessUnit_ParentBusinessUnitId] ON [BusinessUnit] ([ParentBusinessUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessUnit_RegionId] ON [BusinessUnit] ([RegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Company_CompanyCode] ON [Company] ([CompanyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompanyLoginProvider_CompanyId] ON [CompanyLoginProvider] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Employment_CompanyId] ON [Employment] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EmploymentOpen] ON [Employment] ([PersonId]) WHERE [TerminationDate] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JobApplication_CompanyId] ON [JobApplication] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JobApplication_JobPostingId_PersonId] ON [JobApplication] ([JobPostingId], [PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JobApplication_PersonId] ON [JobApplication] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JobPosting_CompanyId] ON [JobPosting] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JobPosting_PositionId] ON [JobPosting] ([PositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Location_CompanyId] ON [Location] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Location_RegionId] ON [Location] ([RegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Person_CompanyId_Email] ON [Person] ([CompanyId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Person_CompanyId_PersonNumber] ON [Person] ([CompanyId], [PersonNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Position_BusinessUnitId] ON [Position] ([BusinessUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Position_CompanyId_PositionCode] ON [Position] ([CompanyId], [PositionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Position_LocationId] ON [Position] ([LocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PositionAssignment_CompanyId] ON [PositionAssignment] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AssignmentOpenPrimaryPerson] ON [PositionAssignment] ([PersonId]) WHERE [EffectiveTo] IS NULL AND [AssignmentType] = ''primary''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AssignmentOpenPrimaryPosition] ON [PositionAssignment] ([PositionId]) WHERE [EffectiveTo] IS NULL AND [AssignmentType] = ''primary''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PositionReportingLine_CompanyId] ON [PositionReportingLine] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PositionReportingLine_ManagerPositionId] ON [PositionReportingLine] ([ManagerPositionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ReportingLineOpenSolid] ON [PositionReportingLine] ([PositionId]) WHERE [EffectiveTo] IS NULL AND [LineType] = ''solid''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Region_CompanyId] ON [Region] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleGrant_CompanyId] ON [RoleGrant] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleGrant_GrantedBy] ON [RoleGrant] ([GrantedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoleGrant_PersonId] ON [RoleGrant] ([PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [UserAccount] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserAccount_CompanyId_NormalizedEmail] ON [UserAccount] ([CompanyId], [NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAccount_CompanyId_PersonId] ON [UserAccount] ([CompanyId], [PersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [UserAccount] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserClaim_UserAccountId] ON [UserClaim] ([UserAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserExternalLogin_UserAccountId] ON [UserExternalLogin] ([UserAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008102537_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008102537_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

