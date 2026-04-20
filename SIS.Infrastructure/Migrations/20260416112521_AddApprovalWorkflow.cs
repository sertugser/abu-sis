using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CriticalityLevel",
                table: "ConstraintDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ConstraintExceptionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConstraintDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InstructorId = table.Column<int>(type: "integer", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                    TermId = table.Column<int>(type: "integer", nullable: true),
                    IsExempt = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterOverride = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintExceptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptionRequests_ConstraintDefinitions_Constrain~",
                        column: x => x.ConstraintDefinitionId,
                        principalTable: "ConstraintDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ConstraintExceptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ConstraintExceptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_RequestId_StepOrder",
                table: "ApprovalSteps",
                columns: new[] { "RequestId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptionRequests_ConstraintDefinitionId",
                table: "ConstraintExceptionRequests",
                column: "ConstraintDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptionRequests_Status",
                table: "ConstraintExceptionRequests",
                column: "Status");

            // Seed H1, H2, H5 as Critical
            migrationBuilder.Sql(@"
                UPDATE ""ConstraintDefinitions"" SET ""CriticalityLevel"" = 1
                WHERE ""Code"" IN ('H1', 'H2', 'H5');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "ConstraintExceptionRequests");

            migrationBuilder.DropColumn(
                name: "CriticalityLevel",
                table: "ConstraintDefinitions");
        }
    }
}
