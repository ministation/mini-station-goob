// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    public partial class GhostRoleTickets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_ghost_role_tickets",
                columns: table => new
                {
                    player_ghost_role_tickets_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tickets = table.Column<int>(type: "integer", nullable: false),
                    last_grant_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ticket_milestones = table.Column<List<TimeSpan>>(type: "jsonb", nullable: false),
                    streak_milestones = table.Column<List<int>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                SELECT DISTINCT player_id, 3, '[]'::jsonb, '[]'::jsonb, NOW(), NOW()
                FROM player
                ON CONFLICT DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_ghost_role_tickets");
        }
    }
}
