using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Management.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreProfileImagesInIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfileImageUrl",
                table: "AspNetUsers",
                column: "ProfileImageUrl",
                unique: true,
                filter: "[ProfileImageUrl] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfileImageUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfileImageContentType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfileImageData",
                table: "AspNetUsers");
        }
    }
}
