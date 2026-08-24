using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeacherOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantMembershipInvitationsAndEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvitations", x => x.Id);
                    table.CheckConstraint("CK_TenantInvitations_CreatedByUserId_NotEmpty", "[CreatedByUserId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_TenantInvitations_Email_NotBlank", "LEN(LTRIM(RTRIM([Email]))) > 0");
                    table.CheckConstraint("CK_TenantInvitations_Expires_After_Created", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_TenantInvitations_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_TenantInvitations_NormalizedEmail_NotBlank", "LEN(LTRIM(RTRIM([NormalizedEmail]))) > 0");
                    table.CheckConstraint("CK_TenantInvitations_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_TenantInvitations_TokenHash_NotBlank", "LEN(LTRIM(RTRIM([TokenHash]))) > 0");
                    table.ForeignKey(
                        name: "FK_TenantInvitations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantInvitations_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProtectedInvitationToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastErrorDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_EmailOutboxMessages_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_EmailOutboxMessages_RecipientEmail_NotBlank", "LEN(LTRIM(RTRIM([RecipientEmail]))) > 0");
                    table.CheckConstraint("CK_EmailOutboxMessages_Status_Valid", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_EmailOutboxMessages_TenantInvitationId_NotEmpty", "[TenantInvitationId] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_EmailOutboxMessages_TenantInvitations_TenantInvitationId",
                        column: x => x.TenantInvitationId,
                        principalTable: "TenantInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_Status_NextAttemptAtUtc",
                table: "EmailOutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_TenantInvitationId",
                table: "EmailOutboxMessages",
                column: "TenantInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_CreatedByUserId",
                table: "TenantInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_RoleId",
                table: "TenantInvitations",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId_NormalizedEmail",
                table: "TenantInvitations",
                columns: new[] { "TenantId", "NormalizedEmail" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantInvitations_TokenHash",
                table: "TenantInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropTable(
                name: "TenantInvitations");
        }
    }
}
