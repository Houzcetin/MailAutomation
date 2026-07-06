using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailAnalyzer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MailboxStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UidValidity = table.Column<long>(type: "bigint", nullable: false),
                    LastProcessedUid = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxStates_Folder",
                table: "MailboxStates",
                column: "Folder",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailboxStates");
        }
    }
}
