using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatoBuzz.Merchant.Migrations
{
    /// <inheritdoc />
    public partial class InitialMerchant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessCategories = table.Column<List<string>>(type: "text[]", nullable: false),
                    BusinessCategory = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BusinessAddress = table.Column<string>(type: "text", nullable: false),
                    BusinessPanNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CityName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Ward = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CitizenshipFrontUrl = table.Column<string>(type: "text", nullable: true),
                    CitizenshipBackUrl = table.Column<string>(type: "text", nullable: true),
                    PanCardUrl = table.Column<string>(type: "text", nullable: true),
                    OwnerPhotoUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merchants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_CityId_Status",
                table: "Merchants",
                columns: new[] { "CityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_MerchantId",
                table: "Merchants",
                column: "MerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_Phone",
                table: "Merchants",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_Status",
                table: "Merchants",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Merchants");
        }
    }
}
