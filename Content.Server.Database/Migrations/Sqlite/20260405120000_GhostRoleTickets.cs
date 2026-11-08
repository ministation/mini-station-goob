// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    public partial class GhostRoleTickets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_ghost_role_tickets",
                columns: table => new
                {
                    player_ghost_role_tickets_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tickets = table.Column<int>(type: "INTEGER", nullable: false),
                    last_grant_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ticket_milestones = table.Column<string>(type: "TEXT", nullable: false),
                    streak_milestones = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_ghost_role_tickets", x => x.player_ghost_role_tickets_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_ghost_role_tickets_player_id",
                table: "player_ghost_role_tickets",
                column: "player_id",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO player_ghost_role_tickets (player_id, tickets, ticket_milestones, streak_milestones, created_at, updated_at)
                SELECT DISTINCT player_id, 3, '[]', '[]', datetime('now'), datetime('now')
                FROM player
                WHERE NOT EXISTS (SELECT 1 FROM player_ghost_role_tickets WHERE player_ghost_role_tickets.player_id = player.player_id);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_ghost_role_tickets");
        }
    }
}
