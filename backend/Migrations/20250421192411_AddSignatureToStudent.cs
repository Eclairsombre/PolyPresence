using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Signature",
                table: "Attendances");

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "Students",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Signature",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "Attendances",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
