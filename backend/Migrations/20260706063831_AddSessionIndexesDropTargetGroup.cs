using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIndexesDropTargetGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetGroup",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Date",
                table: "Sessions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfId",
                table: "Sessions",
                column: "ProfId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfId2",
                table: "Sessions",
                column: "ProfId2");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfSignatureToken",
                table: "Sessions",
                column: "ProfSignatureToken");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProfSignatureToken2",
                table: "Sessions",
                column: "ProfSignatureToken2");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Year_Date",
                table: "Sessions",
                columns: new[] { "Year", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_Date",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ProfId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ProfId2",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ProfSignatureToken",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ProfSignatureToken2",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_Year_Date",
                table: "Sessions");

            migrationBuilder.AddColumn<string>(
                name: "TargetGroup",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
