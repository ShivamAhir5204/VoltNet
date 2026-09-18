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
CREATE TABLE [AdminUsers] (
    [id] uniqueidentifier NOT NULL,
    [name] nvarchar(100) NOT NULL,
    [email] nvarchar(100) NOT NULL,
    [password] nvarchar(255) NOT NULL,
    [role] varchar(30) NOT NULL,
    [isactive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [created_at] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([id])
);

CREATE TABLE [UserMaster] (
    [id] uniqueidentifier NOT NULL,
    [fullname] nvarchar(100) NULL,
    [email] nvarchar(50) NOT NULL,
    [password] nvarchar(255) NULL,
    [mobile] varchar(10) NULL,
    [role] varchar(30) NOT NULL,
    [isactive] bit NOT NULL,
    [created_at] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_UserMaster] PRIMARY KEY ([id])
);

CREATE TABLE [Stations] (
    [id] uniqueidentifier NOT NULL,
    [name] nvarchar(150) NOT NULL,
    [owner_user_id] uniqueidentifier NULL,
    [address] nvarchar(300) NOT NULL,
    [city] nvarchar(100) NOT NULL,
    [state] nvarchar(100) NOT NULL,
    [latitude] decimal(18,9) NOT NULL,
    [longitude] decimal(18,9) NOT NULL,
    [opening_time] time NOT NULL,
    [closing_time] time NOT NULL,
    [status] varchar(20) NOT NULL,
    [created_at] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Stations] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Stations_UserMaster_owner_user_id] FOREIGN KEY ([owner_user_id]) REFERENCES [UserMaster] ([id]) ON DELETE NO ACTION
);

CREATE TABLE [StationManagers] (
    [id] uniqueidentifier NOT NULL,
    [station_id] uniqueidentifier NOT NULL,
    [user_id] uniqueidentifier NOT NULL,
    [assigned_at] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_StationManagers] PRIMARY KEY ([id]),
    CONSTRAINT [FK_StationManagers_Stations_station_id] FOREIGN KEY ([station_id]) REFERENCES [Stations] ([id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StationManagers_UserMaster_user_id] FOREIGN KEY ([user_id]) REFERENCES [UserMaster] ([id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_AdminUsers_Email] ON [AdminUsers] ([email]);

CREATE UNIQUE INDEX [IX_StationManagers_StationId_UserId] ON [StationManagers] ([station_id], [user_id]);

CREATE INDEX [IX_StationManagers_user_id] ON [StationManagers] ([user_id]);

CREATE INDEX [IX_Stations_City] ON [Stations] ([city]);

CREATE INDEX [IX_Stations_owner_user_id] ON [Stations] ([owner_user_id]);

CREATE INDEX [IX_Stations_Status] ON [Stations] ([status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260825065601_InitialCreate', N'10.0.10');

COMMIT;
GO

BEGIN TRANSACTION;
EXEC sp_rename N'[AdminUsers].[email]', N'username', 'COLUMN';

EXEC sp_rename N'[AdminUsers].[IX_AdminUsers_Email]', N'IX_AdminUsers_Username', 'INDEX';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260825152833_AdminUsername', N'10.0.10');

COMMIT;
GO

