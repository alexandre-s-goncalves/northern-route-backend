using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogisticPlatform.API.Migrations
{
    /// <inheritdoc />
    public partial class CreateAutomatedLoginAuditsMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration intentionally keeps the schema untouched because the
            // automated login-audit matrix is provisioned through the application
            // seeding workflow rather than a structural database change.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No irreversible schema change was introduced here; the rollback is a no-op.
        }
    }
}
