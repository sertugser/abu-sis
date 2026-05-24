using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPersonalInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPersonalInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    MobilePhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SecondaryEmail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressProvince = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressDistrict = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmergencyContactRelation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BirthPlace = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    Nationality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BloodType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    PassportNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MilitaryStatus = table.Column<int>(type: "integer", nullable: true),
                    MilitaryDeferralUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MilitaryOffice = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPersonalInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPersonalInfos_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPersonalInfos_StudentId",
                table: "StudentPersonalInfos",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPersonalInfos");
        }
    }
}
