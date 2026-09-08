using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatoBuzz.Api.Features.PostInterest.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostInterest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Interests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserPhoto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PostTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PostLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MerchantPhoto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    InterestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interests_PostId",
                table: "Interests",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Interests_PostId_UserId",
                table: "Interests",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interests_UserId",
                table: "Interests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Interests");
        }
    }
}
