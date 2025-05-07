using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddProfFieldsToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfEmail",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfFirstname",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfName",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfSignature",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfSignatureToken",
                table: "Sessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfEmail",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfFirstname",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfSignature",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfSignatureToken",
                table: "Sessions");
        }
    }
}
