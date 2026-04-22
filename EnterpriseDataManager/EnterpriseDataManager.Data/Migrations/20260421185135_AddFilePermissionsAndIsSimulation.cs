using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseDataManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilePermissionsAndIsSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSimulation",
                table: "RecoveryJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FilePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveItemId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SignedUrlToken = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SignedUrlExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilePermissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_ArchiveItemId",
                table: "FilePermissions",
                column: "ArchiveItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_ArchiveItemId_UserId",
                table: "FilePermissions",
                columns: new[] { "ArchiveItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_IsDeleted",
                table: "FilePermissions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_SignedUrlToken",
                table: "FilePermissions",
                column: "SignedUrlToken");

            migrationBuilder.CreateIndex(
                name: "IX_FilePermissions_UserId",
                table: "FilePermissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilePermissions");

            migrationBuilder.DropColumn(
                name: "IsSimulation",
                table: "RecoveryJobs");
        }
    }
}
