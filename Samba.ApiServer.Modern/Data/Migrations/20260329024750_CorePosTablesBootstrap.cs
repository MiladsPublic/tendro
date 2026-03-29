using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Samba.ApiServer.Modern.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorePosTablesBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[IdempotencyRecords]', N'U') IS NULL
BEGIN
    CREATE TABLE [IdempotencyRecords] (
        [Id] int NOT NULL IDENTITY,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [ResultJson] nvarchar(max) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_IdempotencyRecords_IdempotencyKey] ON [IdempotencyRecords] ([IdempotencyKey]);
    CREATE INDEX [IX_IdempotencyRecords_ExpiresAtUtc] ON [IdempotencyRecords] ([ExpiresAtUtc]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Tickets]', N'U') IS NULL
BEGIN
    CREATE TABLE [Tickets] (
        [Id] int NOT NULL IDENTITY,
        [TicketNumber] nvarchar(50) NOT NULL,
        [DepartmentId] int NOT NULL,
        [TerminalId] int NOT NULL,
        [TicketTypeId] int NOT NULL,
        [StateName] nvarchar(100) NOT NULL,
        [StateValue] nvarchar(500) NULL,
        [IsClosed] bit NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Tickets_TicketNumber] ON [Tickets] ([TicketNumber]);
    CREATE INDEX [IX_Tickets_DepartmentId] ON [Tickets] ([DepartmentId]);
    CREATE INDEX [IX_Tickets_IsClosed] ON [Tickets] ([IsClosed]);
    CREATE INDEX [IX_Tickets_CreatedAtUtc] ON [Tickets] ([CreatedAtUtc]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Orders]', N'U') IS NULL
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [MenuItemId] int NOT NULL,
        [PortionName] nvarchar(100) NULL,
        [Tags] nvarchar(500) NULL,
        [Quantity] decimal(10,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NULL,
        [TaxAmount] decimal(18,2) NULL,
        [Status] nvarchar(50) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Orders_TicketId] ON [Orders] ([TicketId]);
    CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Payments]', N'U') IS NULL
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [PaymentTypeId] int NOT NULL,
        [PaymentType] nvarchar(50) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TenderedAmount] decimal(18,2) NULL,
        [ChangeAmount] decimal(18,2) NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Payments_TicketId] ON [Payments] ([TicketId]);
    CREATE INDEX [IX_Payments_PaymentType] ON [Payments] ([PaymentType]);
    CREATE INDEX [IX_Payments_ReferenceNumber] ON [Payments] ([ReferenceNumber]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[Orders]', N'U') IS NOT NULL DROP TABLE [Orders];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[Payments]', N'U') IS NOT NULL DROP TABLE [Payments];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[IdempotencyRecords]', N'U') IS NOT NULL DROP TABLE [IdempotencyRecords];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[Tickets]', N'U') IS NOT NULL DROP TABLE [Tickets];");
        }
    }
}
