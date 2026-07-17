using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PendingModelSyncAfterGoobMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_player_ghost_role_tickets_player_id",
                table: "player_ghost_role_tickets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_player_ghost_role_tickets_player_id",
                table: "player_ghost_role_tickets",
                column: "player_id",
                unique: true);
        }
    }
}
