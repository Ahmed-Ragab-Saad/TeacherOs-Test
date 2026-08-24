using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeacherOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOwnerMembersManagePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Roles]
                SET [PermissionCodes] = JSON_MODIFY([PermissionCodes], 'append $', 'members.manage')
                WHERE [Name] = 'Owner'
                  AND ([PermissionCodes] NOT LIKE '%""members.manage""%' OR [PermissionCodes] IS NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting permission backfill is intentionally a non-destructive no-op.
        }
    }
}
