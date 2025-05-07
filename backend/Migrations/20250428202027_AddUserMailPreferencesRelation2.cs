using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMailPreferencesRelation2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "MailPreferencesId",
                table: "Users",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId",
                principalTable: "MailPreferences",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "MailPreferencesId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MailPreferences_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId",
                principalTable: "MailPreferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
