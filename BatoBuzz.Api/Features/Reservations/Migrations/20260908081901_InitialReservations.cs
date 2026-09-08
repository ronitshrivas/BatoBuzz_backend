using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatoBuzz.Api.Features.Reservations.Migrations
{
    /// <inheritdoc />
    public partial class InitialReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blacklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostStocks",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockAvailable = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostStocks", x => x.PostId);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProductImage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReservedPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PointsAwarded = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserPhoto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MerchantPhoto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blacklist_MerchantId_UserId",
                table: "Blacklist",
                columns: new[] { "MerchantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ExpiresAt",
                table: "Reservations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MerchantId_Status",
                table: "Reservations",
                columns: new[] { "MerchantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId_PostId_Status",
                table: "Reservations",
                columns: new[] { "UserId", "PostId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId_Status",
                table: "Reservations",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blacklist");

            migrationBuilder.DropTable(
                name: "PostStocks");

            migrationBuilder.DropTable(
                name: "Reservations");
        }
    }
}
