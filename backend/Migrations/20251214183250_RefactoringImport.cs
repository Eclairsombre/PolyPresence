using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RefactoringImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMailSent2",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMerged",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProfEmail2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfFirstname2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfName2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfSignature2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfSignatureToken2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetGroup",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMailSent2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsMerged",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfEmail2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfFirstname2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfName2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfSignature2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfSignatureToken2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TargetGroup",
                table: "Sessions");
        }
    }
}
