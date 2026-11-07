using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateProfileImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfileImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileImages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileImages_Url",
                table: "ProfileImages",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileImages_UserId",
                table: "ProfileImages",
                column: "UserId",
                unique: true);

            ExecuteDataMigration(migrationBuilder, """
                IF EXISTS (
                    SELECT 1 FROM [AspNetUsers]
                    WHERE ([ProfileImageUrl] IS NOT NULL OR [ProfileImageContentType] IS NOT NULL OR [ProfileImageData] IS NOT NULL)
                      AND ([ProfileImageUrl] IS NULL OR [ProfileImageContentType] IS NULL OR [ProfileImageData] IS NULL))
                    THROW 51000, 'An existing profile image has incomplete metadata or content. Complete its database import before migrating.', 1;

                INSERT INTO [ProfileImages] ([UserId], [Url], [ContentType], [Content])
                SELECT [Id], [ProfileImageUrl], [ProfileImageContentType], [ProfileImageData]
                FROM [AspNetUsers] WHERE [ProfileImageUrl] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(name: "IX_AspNetUsers_ProfileImageUrl", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "ProfileImageContentType", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "ProfileImageData", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "ProfileImageUrl", table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileImageContentType",
                table: "AspNetUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProfileImageData",
                table: "AspNetUsers",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                table: "AspNetUsers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            ExecuteDataMigration(migrationBuilder, """
                CREATE UNIQUE INDEX [IX_AspNetUsers_ProfileImageUrl]
                ON [AspNetUsers] ([ProfileImageUrl]) WHERE [ProfileImageUrl] IS NOT NULL;
                """);

            ExecuteDataMigration(migrationBuilder, """
                UPDATE u SET
                    u.[ProfileImageUrl] = image.[Url],
                    u.[ProfileImageContentType] = image.[ContentType],
                    u.[ProfileImageData] = image.[Content]
                FROM [AspNetUsers] u JOIN [ProfileImages] image ON image.[UserId] = u.[Id];
                """);

            migrationBuilder.DropTable(name: "ProfileImages");
        }

        // Scripts may add/drop columns in one batch; compile the copy after schema changes.
        private static void ExecuteDataMigration(MigrationBuilder migrationBuilder, string sql) =>
            migrationBuilder.Sql("EXEC(N'" + sql.Replace("'", "''", StringComparison.Ordinal) + "');");
    }
}
