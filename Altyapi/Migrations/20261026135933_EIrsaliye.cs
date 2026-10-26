using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class EIrsaliye : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EBelgeDurumu",
                table: "Irsaliyeler",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EBelgeMesaji",
                table: "Irsaliyeler",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EBelgeNo",
                table: "Irsaliyeler",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Ettn",
                table: "Irsaliyeler",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoforTckn",
                table: "Irsaliyeler",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_EBelgeNo",
                table: "Irsaliyeler",
                column: "EBelgeNo",
                unique: true,
                filter: "\"EBelgeNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Irsaliyeler_Ettn",
                table: "Irsaliyeler",
                column: "Ettn",
                unique: true,
                filter: "\"Ettn\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_EBelgeNo",
                table: "Irsaliyeler");

            migrationBuilder.DropIndex(
                name: "IX_Irsaliyeler_Ettn",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "EBelgeDurumu",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "EBelgeMesaji",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "EBelgeNo",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "Ettn",
                table: "Irsaliyeler");

            migrationBuilder.DropColumn(
                name: "SoforTckn",
                table: "Irsaliyeler");
        }
    }
}
