using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupsAndIcsUid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IcsLinks_SpecializationId_Year",
                table: "IcsLinks");

            migrationBuilder.AddColumn<int>(
                name: "Lv1GroupId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Lv2GroupId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubGroupId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IcsKind",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IcsUid",
                table: "Sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "IcsLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PromoLabel",
                table: "IcsLinks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ParentGroupId = table.Column<int>(type: "integer", nullable: true),
                    SpecializationId = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SeenCount = table.Column<int>(type: "integer", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Groups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Groups_Specializations_SpecializationId",
                        column: x => x.SpecializationId,
                        principalTable: "Specializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SessionGroups",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionGroups", x => new { x.SessionId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_SessionGroups_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionGroups_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Lv1GroupId",
                table: "Users",
                column: "Lv1GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Lv2GroupId",
                table: "Users",
                column: "Lv2GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SubGroupId",
                table: "Users",
                column: "SubGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IcsUid",
                table: "Sessions",
                column: "IcsUid");

            migrationBuilder.CreateIndex(
                name: "IX_IcsLinks_SpecializationId_Year_Kind",
                table: "IcsLinks",
                columns: new[] { "SpecializationId", "Year", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Label",
                table: "Groups",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ParentGroupId",
                table: "Groups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SpecializationId_Year_Type",
                table: "Groups",
                columns: new[] { "SpecializationId", "Year", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionGroups_GroupId",
                table: "SessionGroups",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Groups_Lv1GroupId",
                table: "Users",
                column: "Lv1GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Groups_Lv2GroupId",
                table: "Users",
                column: "Lv2GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Groups_SubGroupId",
                table: "Users",
                column: "SubGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Groups_Lv1GroupId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Groups_Lv2GroupId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Groups_SubGroupId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "SessionGroups");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Users_Lv1GroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Lv2GroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SubGroupId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_IcsUid",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_IcsLinks_SpecializationId_Year_Kind",
                table: "IcsLinks");

            migrationBuilder.DropColumn(
                name: "Lv1GroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Lv2GroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubGroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IcsKind",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IcsUid",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "IcsLinks");

            migrationBuilder.DropColumn(
                name: "PromoLabel",
                table: "IcsLinks");

            migrationBuilder.CreateIndex(
                name: "IX_IcsLinks_SpecializationId_Year",
                table: "IcsLinks",
                columns: new[] { "SpecializationId", "Year" },
                unique: true);
        }
    }
}
