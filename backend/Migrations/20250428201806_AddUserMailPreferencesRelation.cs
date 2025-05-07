using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMailPreferencesRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MailPreferencesId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MailPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailTo = table.Column<string>(type: "TEXT", nullable: false),
                    Days = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId",
                principalTable: "MailPreferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "MailPreferences");

            migrationBuilder.DropIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MailPreferencesId",
                table: "Users");
        }
    }
}
