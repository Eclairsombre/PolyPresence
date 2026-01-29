using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessorTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfEmail",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfEmail2",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfFirstname",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ProfName",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "ProfName2",
                table: "Sessions",
                newName: "ProfId2");

            migrationBuilder.RenameColumn(
                name: "ProfFirstname2",
                table: "Sessions",
                newName: "ProfId");

            migrationBuilder.CreateTable(
                name: "Professors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Firstname = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professors", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Professors");

            migrationBuilder.RenameColumn(
                name: "ProfId2",
                table: "Sessions",
                newName: "ProfName2");

            migrationBuilder.RenameColumn(
                name: "ProfId",
                table: "Sessions",
                newName: "ProfFirstname2");

            migrationBuilder.AddColumn<string>(
                name: "ProfEmail",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfEmail2",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

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
        }
    }
}
