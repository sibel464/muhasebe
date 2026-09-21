using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altyapi.Migrations
{
    /// <inheritdoc />
    public partial class Paket43KaynakFaturalama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IrsaliyeId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KaynakTuru",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OdemeSekli",
                table: "Faturalar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SiparisId",
                table: "Faturalar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VadeTarihi",
                table: "Faturalar",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IrsaliyeKalemiId",
                table: "FaturaKalemleri",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiparisKalemiId",
                table: "FaturaKalemleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_IrsaliyeId",
                table: "Faturalar",
                column: "IrsaliyeId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturalar_SiparisId",
                table: "Faturalar",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemleri_IrsaliyeKalemiId",
                table: "FaturaKalemleri",
                column: "IrsaliyeKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_FaturaKalemleri_SiparisKalemiId",
                table: "FaturaKalemleri",
                column: "SiparisKalemiId");

            migrationBuilder.AddForeignKey(
                name: "FK_FaturaKalemleri_IrsaliyeKalemleri_IrsaliyeKalemiId",
                table: "FaturaKalemleri",
                column: "IrsaliyeKalemiId",
                principalTable: "IrsaliyeKalemleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FaturaKalemleri_SiparisKalemleri_SiparisKalemiId",
                table: "FaturaKalemleri",
                column: "SiparisKalemiId",
                principalTable: "SiparisKalemleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Irsaliyeler_IrsaliyeId",
                table: "Faturalar",
                column: "IrsaliyeId",
                principalTable: "Irsaliyeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturalar_Siparisler_SiparisId",
                table: "Faturalar",
                column: "SiparisId",
                principalTable: "Siparisler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FaturaKalemleri_IrsaliyeKalemleri_IrsaliyeKalemiId",
                table: "FaturaKalemleri");

            migrationBuilder.DropForeignKey(
                name: "FK_FaturaKalemleri_SiparisKalemleri_SiparisKalemiId",
                table: "FaturaKalemleri");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Irsaliyeler_IrsaliyeId",
                table: "Faturalar");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturalar_Siparisler_SiparisId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_IrsaliyeId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_Faturalar_SiparisId",
                table: "Faturalar");

            migrationBuilder.DropIndex(
                name: "IX_FaturaKalemleri_IrsaliyeKalemiId",
                table: "FaturaKalemleri");

            migrationBuilder.DropIndex(
                name: "IX_FaturaKalemleri_SiparisKalemiId",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "IrsaliyeId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "KaynakTuru",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "OdemeSekli",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "SiparisId",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "VadeTarihi",
                table: "Faturalar");

            migrationBuilder.DropColumn(
                name: "IrsaliyeKalemiId",
                table: "FaturaKalemleri");

            migrationBuilder.DropColumn(
                name: "SiparisKalemiId",
                table: "FaturaKalemleri");
        }
    }
}
