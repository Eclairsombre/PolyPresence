using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecializationId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecializationId",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpecializationId",
                table: "IcsLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Specializations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specializations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SpecializationId",
                table: "Users",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SpecializationId",
                table: "Sessions",
                column: "SpecializationId");

            migrationBuilder.CreateIndex(
                name: "IX_IcsLinks_SpecializationId_Year",
                table: "IcsLinks",
                columns: new[] { "SpecializationId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specializations_Code",
                table: "Specializations",
                column: "Code",
                unique: true);

            // Seed: insert "Informatique" specialization
            migrationBuilder.Sql(@"
                INSERT INTO ""Specializations"" (""Name"", ""Code"", ""Description"", ""IsActive"", ""CreatedAt"")
                VALUES ('Informatique', 'INFO', 'Spécialité Informatique - apprentissage', true, NOW());
            ");

            // Migrate existing data to point to the new specialization
            migrationBuilder.Sql(@"
                UPDATE ""Sessions"" SET ""SpecializationId"" = (SELECT ""Id"" FROM ""Specializations"" WHERE ""Code"" = 'INFO' LIMIT 1);
            ");
            migrationBuilder.Sql(@"
                UPDATE ""IcsLinks"" SET ""SpecializationId"" = (SELECT ""Id"" FROM ""Specializations"" WHERE ""Code"" = 'INFO' LIMIT 1);
            ");
            migrationBuilder.Sql(@"
                UPDATE ""Users"" SET ""SpecializationId"" = (SELECT ""Id"" FROM ""Specializations"" WHERE ""Code"" = 'INFO' LIMIT 1) WHERE ""Year"" != 'ADMIN';
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_IcsLinks_Specializations_SpecializationId",
                table: "IcsLinks",
                column: "SpecializationId",
                principalTable: "Specializations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Specializations_SpecializationId",
                table: "Sessions",
                column: "SpecializationId",
                principalTable: "Specializations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Specializations_SpecializationId",
                table: "Users",
                column: "SpecializationId",
                principalTable: "Specializations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IcsLinks_Specializations_SpecializationId",
                table: "IcsLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Specializations_SpecializationId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Specializations_SpecializationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Specializations");

            migrationBuilder.DropIndex(
                name: "IX_Users_SpecializationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_SpecializationId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_IcsLinks_SpecializationId_Year",
                table: "IcsLinks");

            migrationBuilder.DropColumn(
                name: "SpecializationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SpecializationId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SpecializationId",
                table: "IcsLinks");
        }
    }
}
