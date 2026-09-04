using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LogisticPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class AppendAutomatedDataSeedingMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("b8f2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"), "USER" },
                    { new Guid("e7b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"), "ADMIN" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "Name", "PasswordHash", "RoleId" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"), "ale@ale.com", "Alexandre Santos", "Password123", new Guid("e7b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d") },
                    { new Guid("c2b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"), "operator@northernroute.com", "John Doe Operator", "Operator123", new Guid("b8f2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("c2b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("b8f2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("e7b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"));
        }
    }
}
