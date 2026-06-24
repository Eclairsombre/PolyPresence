using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class MergeProfessorIntoUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Nouvelles colonnes sur Users (un professeur devient un User IsProfessor).
            migrationBuilder.AddColumn<bool>(
                name: "IsProfessor",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NotificationMode",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "Email");

            // 2. Migration des données : chaque Professor devient un User (IsProfessor=true),
            //    puis on remappe les Sessions (ProfId/ProfId2 contenaient l'ancien Professor.Id
            //    en texte) vers le nouvel Id du User correspondant.
            migrationBuilder.Sql(@"
                -- Colonne temporaire de correspondance ancien Professor.Id -> nouveau User.Id
                ALTER TABLE ""Users"" ADD COLUMN ""LegacyProfId"" integer;

                INSERT INTO ""Users""
                    (""Name"", ""Firstname"", ""StudentNumber"", ""Email"", ""Year"", ""Signature"",
                     ""IsAdmin"", ""IsDelegate"", ""IsProfessor"", ""NotificationMode"",
                     ""RegisterMailSent"", ""IsDeleted"", ""LegacyProfId"")
                SELECT
                    ""Name"", ""Firstname"", '', COALESCE(""Email"", ''), 'PROF', '',
                    false, false, true, 'Email',
                    false, false, ""Id""
                FROM ""Professors"";

                UPDATE ""Sessions"" s
                SET ""ProfId"" = u.""Id""::text
                FROM ""Users"" u
                WHERE u.""LegacyProfId"" IS NOT NULL
                  AND s.""ProfId"" ~ '^[0-9]+$'
                  AND u.""LegacyProfId"" = s.""ProfId""::int;

                UPDATE ""Sessions"" s
                SET ""ProfId2"" = u.""Id""::text
                FROM ""Users"" u
                WHERE u.""LegacyProfId"" IS NOT NULL
                  AND s.""ProfId2"" ~ '^[0-9]+$'
                  AND u.""LegacyProfId"" = s.""ProfId2""::int;

                ALTER TABLE ""Users"" DROP COLUMN ""LegacyProfId"";
            ");

            // 3. L'ancienne table Professors n'est plus utilisée.
            migrationBuilder.DropTable(
                name: "Professors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recréation de la table Professors.
            migrationBuilder.CreateTable(
                name: "Professors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Firstname = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professors", x => x.Id);
                });

            // Rollback best-effort : on recrée des Professors à partir des Users IsProfessor,
            // on remappe les Sessions, puis on supprime ces Users (les Id d'origine ne sont
            // pas restaurés à l'identique).
            migrationBuilder.Sql(@"
                ALTER TABLE ""Professors"" ADD COLUMN ""LegacyUserId"" integer;

                INSERT INTO ""Professors"" (""Name"", ""Firstname"", ""Email"", ""LegacyUserId"")
                SELECT ""Name"", ""Firstname"", ""Email"", ""Id""
                FROM ""Users""
                WHERE ""IsProfessor"" = true;

                UPDATE ""Sessions"" s
                SET ""ProfId"" = p.""Id""::text
                FROM ""Professors"" p
                WHERE p.""LegacyUserId"" IS NOT NULL
                  AND s.""ProfId"" ~ '^[0-9]+$'
                  AND p.""LegacyUserId"" = s.""ProfId""::int;

                UPDATE ""Sessions"" s
                SET ""ProfId2"" = p.""Id""::text
                FROM ""Professors"" p
                WHERE p.""LegacyUserId"" IS NOT NULL
                  AND s.""ProfId2"" ~ '^[0-9]+$'
                  AND p.""LegacyUserId"" = s.""ProfId2""::int;

                ALTER TABLE ""Professors"" DROP COLUMN ""LegacyUserId"";

                DELETE FROM ""Users"" WHERE ""IsProfessor"" = true;
            ");

            migrationBuilder.DropColumn(
                name: "IsProfessor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NotificationMode",
                table: "Users");
        }
    }
}
