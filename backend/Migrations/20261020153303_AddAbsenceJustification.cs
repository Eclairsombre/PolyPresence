using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceJustification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsJustified",
                table: "Attendances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReasonId",
                table: "Attendances",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AbsenceReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceReasons", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AbsenceReasons",
                columns: new[] { "Id", "IsActive", "Label", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "Certificat médical", 1 },
                    { 2, true, "Rendez-vous médical", 2 },
                    { 3, true, "Hospitalisation", 3 },
                    { 4, true, "Grève TCL", 4 },
                    { 5, true, "Grève SNCF", 5 },
                    { 6, true, "Convocation administrative officielle", 6 },
                    { 7, true, "Convocation JCD", 7 },
                    { 8, true, "Permis de conduire", 8 },
                    { 9, true, "Convocation concours", 9 },
                    { 10, true, "Compétition sportif haut niveau", 10 },
                    { 11, true, "Décès d'un proche", 11 },
                    { 12, true, "Congé menstruel", 12 }
                });

            // Les motifs initiaux sont insérés avec des Id explicites, ce qui n'avance pas la
            // séquence d'identité : sans ce recalage, le premier motif ajouté depuis l'API
            // recevrait l'Id 1 et violerait la clé primaire.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('\"AbsenceReasons\"', 'Id'), (SELECT MAX(\"Id\") FROM \"AbsenceReasons\"));");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ReasonId",
                table: "Attendances",
                column: "ReasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_AbsenceReasons_ReasonId",
                table: "Attendances",
                column: "ReasonId",
                principalTable: "AbsenceReasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_AbsenceReasons_ReasonId",
                table: "Attendances");

            migrationBuilder.DropTable(
                name: "AbsenceReasons");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_ReasonId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "IsJustified",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "ReasonId",
                table: "Attendances");
        }
    }
}
