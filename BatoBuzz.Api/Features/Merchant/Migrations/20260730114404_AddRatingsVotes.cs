using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatoBuzz.Api.Features.Merchant.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingsVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantRatings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantVotes",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantVotes", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantRatings_MerchantId",
                table: "MerchantRatings",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantRatings_MerchantId_UserId",
                table: "MerchantRatings",
                columns: new[] { "MerchantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantVotes_MerchantId",
                table: "MerchantVotes",
                column: "MerchantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantRatings");

            migrationBuilder.DropTable(
                name: "MerchantVotes");
        }
    }
}
