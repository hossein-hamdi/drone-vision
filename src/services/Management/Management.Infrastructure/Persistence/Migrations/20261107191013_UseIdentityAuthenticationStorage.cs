using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseIdentityAuthenticationStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AspNetUserTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "AspNetUserTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBrowserSession",
                table: "AspNetUserTokens",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAtUtc",
                table: "AspNetUserTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "AspNetUserTokens",
                type: "int",
                nullable: true);

            ExecuteDataMigration(migrationBuilder, """
                INSERT INTO [AspNetRoleClaims] ([RoleId], [ClaimType], [ClaimValue])
                SELECT rp.[RoleId], N'permission', p.[Name]
                FROM [RolePermissions] rp
                JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AspNetRoleClaims] c
                    WHERE c.[RoleId] = rp.[RoleId] AND c.[ClaimType] = N'permission' AND c.[ClaimValue] = p.[Name]);

                -- JWTs contain the session's integer ID, so preserve those IDs. Identity
                -- finds other tokens by user/provider/name; their row IDs may be reassigned.
                SELECT [UserId], [LoginProvider], [Name], [Value]
                INTO #ExistingIdentityTokens FROM [AspNetUserTokens];
                DELETE FROM [AspNetUserTokens];

                SET IDENTITY_INSERT [AspNetUserTokens] ON;
                INSERT INTO [AspNetUserTokens]
                    ([Id], [UserId], [LoginProvider], [Name], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc], [IsBrowserSession])
                SELECT [Id], [UserId], N'DroneVision.Session', CONVERT(nvarchar(128), [Id]),
                    [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc], [IsBrowserSession]
                FROM [RefreshTokenSessions];
                SET IDENTITY_INSERT [AspNetUserTokens] OFF;

                INSERT INTO [AspNetUserTokens] ([UserId], [LoginProvider], [Name], [Value])
                SELECT [UserId], [LoginProvider], [Name], [Value] FROM #ExistingIdentityTokens;
                DROP TABLE #ExistingIdentityTokens;

                INSERT INTO [AspNetUserTokens]
                    ([UserId], [LoginProvider], [Name], [SessionId], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc])
                SELECT [UserId], N'DroneVision.Refresh', [TokenHash], [SessionId],
                    [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc]
                FROM [RefreshTokens];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserTokens_RefreshHash",
                table: "AspNetUserTokens",
                column: "Name",
                unique: true,
                filter: "[LoginProvider] = 'DroneVision.Refresh'");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserTokens_SessionId",
                table: "AspNetUserTokens",
                column: "SessionId");

            migrationBuilder.DropTable(name: "RefreshTokens");
            migrationBuilder.DropTable(name: "RolePermissions");
            migrationBuilder.DropTable(name: "RefreshTokenSessions");
            migrationBuilder.DropTable(name: "Permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUserTokens_RefreshHash",
                table: "AspNetUserTokens");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUserTokens_SessionId",
                table: "AspNetUserTokens");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokenSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsBrowserSession = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokenSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokenSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "RefreshTokenSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_UserId",
                table: "RefreshTokenSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            ExecuteDataMigration(migrationBuilder, """
                INSERT INTO [Permissions] ([Name])
                SELECT DISTINCT names.[Name] FROM (
                    SELECT v.[Name] FROM (VALUES
                        (N'Users.View'), (N'Users.Create'), (N'Users.Update'), (N'Users.Delete'),
                        (N'Roles.View'), (N'Roles.Create'), (N'Roles.Update'), (N'Roles.Delete'), (N'Permissions.View')) v([Name])
                    UNION
                    SELECT [ClaimValue] FROM [AspNetRoleClaims]
                    WHERE [ClaimType] = N'permission' AND [ClaimValue] IS NOT NULL
                ) names;

                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT DISTINCT c.[RoleId], p.[Id] FROM [AspNetRoleClaims] c
                JOIN [Permissions] p ON p.[Name] = c.[ClaimValue]
                WHERE c.[ClaimType] = N'permission';

                SET IDENTITY_INSERT [RefreshTokenSessions] ON;
                INSERT INTO [RefreshTokenSessions]
                    ([Id], [UserId], [IsBrowserSession], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc])
                SELECT [Id], [UserId], [IsBrowserSession], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc]
                FROM [AspNetUserTokens] WHERE [LoginProvider] = N'DroneVision.Session';
                SET IDENTITY_INSERT [RefreshTokenSessions] OFF;

                INSERT INTO [RefreshTokens]
                    ([UserId], [SessionId], [TokenHash], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc])
                SELECT [UserId], [SessionId], [Name], [CreatedAtUtc], [ExpiresAtUtc], [RevokedAtUtc]
                FROM [AspNetUserTokens] WHERE [LoginProvider] = N'DroneVision.Refresh';

                DELETE FROM [AspNetUserTokens] WHERE [LoginProvider] IN (N'DroneVision.Session', N'DroneVision.Refresh');
                DELETE FROM [AspNetRoleClaims] WHERE [ClaimType] = N'permission';
                """);

            migrationBuilder.DropColumn(name: "CreatedAtUtc", table: "AspNetUserTokens");
            migrationBuilder.DropColumn(name: "ExpiresAtUtc", table: "AspNetUserTokens");
            migrationBuilder.DropColumn(name: "IsBrowserSession", table: "AspNetUserTokens");
            migrationBuilder.DropColumn(name: "RevokedAtUtc", table: "AspNetUserTokens");
            migrationBuilder.DropColumn(name: "SessionId", table: "AspNetUserTokens");
        }

        // Defer compilation until new columns/tables exist, including in generated SQL scripts.
        private static void ExecuteDataMigration(MigrationBuilder migrationBuilder, string sql) =>
            migrationBuilder.Sql("EXEC(N'" + sql.Replace("'", "''", StringComparison.Ordinal) + "');");
    }
}
