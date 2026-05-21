using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRotationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'AspNetUsers'
                          AND column_name = 'PasswordChangedAtUtc'
                    ) THEN
                        ALTER TABLE "AspNetUsers" ADD "PasswordChangedAtUtc" timestamp with time zone;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'AspNetUsers'
                          AND column_name = 'PasswordChangedAtUtc'
                    ) THEN
                        ALTER TABLE "AspNetUsers" DROP COLUMN "PasswordChangedAtUtc";
                    END IF;
                END
                $$;
                """);
        }
    }
}
