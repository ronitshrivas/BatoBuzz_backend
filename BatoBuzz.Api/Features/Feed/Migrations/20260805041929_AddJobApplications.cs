using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatoBuzz.Api.Features.Feed.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicantPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApplicantEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicantPhoto = table.Column<string>(type: "text", nullable: true),
                    ResumeImageUrl = table.Column<string>(type: "text", nullable: true),
                    CoverNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: true),
                    JobLocation = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_MerchantId_AppliedAt",
                table: "JobApplications",
                columns: new[] { "MerchantId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_PostId_AppliedAt",
                table: "JobApplications",
                columns: new[] { "PostId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_PostId_UserId",
                table: "JobApplications",
                columns: new[] { "PostId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_UserId_AppliedAt",
                table: "JobApplications",
                columns: new[] { "UserId", "AppliedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobApplications");
        }
    }
}
