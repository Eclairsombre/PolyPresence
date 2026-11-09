using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsencePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbsencePeriodId",
                table: "Attendances",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AbsencePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    To = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CourseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDispense = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonId = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsencePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsencePeriods_AbsenceReasons_ReasonId",
                        column: x => x.ReasonId,
                        principalTable: "AbsenceReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AbsencePeriods_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_AbsencePeriodId",
                table: "Attendances",
                column: "AbsencePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsencePeriods_ReasonId",
                table: "AbsencePeriods",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsencePeriods_StudentId_From_To",
                table: "AbsencePeriods",
                columns: new[] { "StudentId", "From", "To" });

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_AbsencePeriods_AbsencePeriodId",
                table: "Attendances",
                column: "AbsencePeriodId",
                principalTable: "AbsencePeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_AbsencePeriods_AbsencePeriodId",
                table: "Attendances");

            migrationBuilder.DropTable(
                name: "AbsencePeriods");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_AbsencePeriodId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "AbsencePeriodId",
                table: "Attendances");
        }
    }
}
