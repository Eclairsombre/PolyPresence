using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMailPreferencesRelation1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_MailPreferencesId",
                table: "Users",
                column: "MailPreferencesId",
                unique: true);
        }
    }
}
