using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogisticPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class CreatePasswordResetTokensAndDeviceEvictionStructures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginAudits_Users_UserId",
                table: "LoginAudits");

            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeDeviceSessionId",
                table: "MfaConfigurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CodeExpiresAt",
                table: "MfaConfigurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    UserId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceSessions_UserId",
                table: "UserDeviceSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_DeviceSessionId",
                table: "RefreshTokenSessions",
                column: "DeviceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_TokenHash",
                table: "RefreshTokenSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_UserId",
                table: "RefreshTokenSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaConfigurations_UserId",
                table: "MfaConfigurations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId1",
                table: "PasswordResetTokens",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginAudits_Users_UserId",
                table: "LoginAudits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MfaConfigurations_Users_UserId",
                table: "MfaConfigurations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokenSessions_UserDeviceSessions_DeviceSessionId",
                table: "RefreshTokenSessions",
                column: "DeviceSessionId",
                principalTable: "UserDeviceSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokenSessions_Users_UserId",
                table: "RefreshTokenSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserDeviceSessions_Users_UserId",
                table: "UserDeviceSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoginAudits_Users_UserId",
                table: "LoginAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_MfaConfigurations_Users_UserId",
                table: "MfaConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokenSessions_UserDeviceSessions_DeviceSessionId",
                table: "RefreshTokenSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokenSessions_Users_UserId",
                table: "RefreshTokenSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserDeviceSessions_Users_UserId",
                table: "UserDeviceSessions");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserDeviceSessions_UserId",
                table: "UserDeviceSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenSessions_DeviceSessionId",
                table: "RefreshTokenSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenSessions_TokenHash",
                table: "RefreshTokenSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenSessions_UserId",
                table: "RefreshTokenSessions");

            migrationBuilder.DropIndex(
                name: "IX_MfaConfigurations_UserId",
                table: "MfaConfigurations");

            migrationBuilder.DropColumn(
                name: "ChallengeDeviceSessionId",
                table: "MfaConfigurations");

            migrationBuilder.DropColumn(
                name: "CodeExpiresAt",
                table: "MfaConfigurations");

            migrationBuilder.AddForeignKey(
                name: "FK_LoginAudits_Users_UserId",
                table: "LoginAudits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
