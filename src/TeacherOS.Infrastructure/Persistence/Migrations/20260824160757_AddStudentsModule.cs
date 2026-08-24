using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeacherOS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddStudentsModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Branches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Branches", x => x.Id);
                table.CheckConstraint("CK_Branches_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Branches_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.CheckConstraint("CK_Branches_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_Branches_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "GradeLevels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GradeLevels", x => x.Id);
                table.CheckConstraint("CK_GradeLevels_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_GradeLevels_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.CheckConstraint("CK_GradeLevels_SortOrder_NonNegative", "[SortOrder] >= 0");
                table.CheckConstraint("CK_GradeLevels_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_GradeLevels_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Guardians",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Guardians", x => x.Id);
                table.CheckConstraint("CK_Guardians_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                table.CheckConstraint("CK_Guardians_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Guardians_PhoneNumber_NotBlank", "LEN(LTRIM(RTRIM([PhoneNumber]))) > 0");
                table.CheckConstraint("CK_Guardians_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_Guardians_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Students",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GradeLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                PhotoUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                EnrollmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Students", x => x.Id);
                table.CheckConstraint("CK_Students_BranchId_NotEmpty", "[BranchId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Students_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                table.CheckConstraint("CK_Students_GradeLevelId_NotEmpty", "[GradeLevelId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Students_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Students_NationalId_NotBlank", "LEN(LTRIM(RTRIM([NationalId]))) > 0");
                table.CheckConstraint("CK_Students_Status_Valid", "[Status] IN (1, 2, 3, 4)");
                table.CheckConstraint("CK_Students_StudentCode_NotBlank", "LEN(LTRIM(RTRIM([StudentCode]))) > 0");
                table.CheckConstraint("CK_Students_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_Students_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Students_GradeLevels_GradeLevelId",
                    column: x => x.GradeLevelId,
                    principalTable: "GradeLevels",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Students_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StudentGuardians",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GuardianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RelationshipType = table.Column<int>(type: "int", nullable: false),
                IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudentGuardians", x => x.Id);
                table.CheckConstraint("CK_StudentGuardians_GuardianId_NotEmpty", "[GuardianId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_StudentGuardians_Id_NotEmpty", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_StudentGuardians_RelationshipType_Valid", "[RelationshipType] IN (1, 2, 3, 4)");
                table.CheckConstraint("CK_StudentGuardians_StudentId_NotEmpty", "[StudentId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_StudentGuardians_TenantId_NotEmpty", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_StudentGuardians_Guardians_GuardianId",
                    column: x => x.GuardianId,
                    principalTable: "Guardians",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StudentGuardians_Students_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StudentGuardians_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "UX_Branches_TenantId_Name",
            table: "Branches",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_GradeLevels_TenantId_Name",
            table: "GradeLevels",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Guardians_TenantId",
            table: "Guardians",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_StudentGuardians_GuardianId",
            table: "StudentGuardians",
            column: "GuardianId");

        migrationBuilder.CreateIndex(
            name: "IX_StudentGuardians_TenantId",
            table: "StudentGuardians",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "UX_StudentGuardians_StudentId_GuardianId",
            table: "StudentGuardians",
            columns: new[] { "StudentId", "GuardianId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Students_BranchId",
            table: "Students",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_Students_GradeLevelId",
            table: "Students",
            column: "GradeLevelId");

        migrationBuilder.CreateIndex(
            name: "IX_Students_TenantId_BranchId",
            table: "Students",
            columns: new[] { "TenantId", "BranchId" });

        migrationBuilder.CreateIndex(
            name: "IX_Students_TenantId_GradeLevelId",
            table: "Students",
            columns: new[] { "TenantId", "GradeLevelId" });

        migrationBuilder.CreateIndex(
            name: "UX_Students_TenantId_NationalId",
            table: "Students",
            columns: new[] { "TenantId", "NationalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_Students_TenantId_StudentCode",
            table: "Students",
            columns: new[] { "TenantId", "StudentCode" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StudentGuardians");

        migrationBuilder.DropTable(
            name: "Guardians");

        migrationBuilder.DropTable(
            name: "Students");

        migrationBuilder.DropTable(
            name: "Branches");

        migrationBuilder.DropTable(
            name: "GradeLevels");
    }
}
