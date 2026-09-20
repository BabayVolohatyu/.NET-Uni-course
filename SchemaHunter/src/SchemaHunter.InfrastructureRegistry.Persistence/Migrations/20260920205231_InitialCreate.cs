using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchemaHunter.InfrastructureRegistry.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asset_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "classification_tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reconstruction_projects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    asset_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    asset_category_id = table.Column<int>(type: "int", nullable: false),
                    allocated_budget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconstruction_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_reconstruction_projects_asset_category",
                        column: x => x.asset_category_id,
                        principalTable: "asset_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "funding_sources",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    funder_name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    fund_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "UAH"),
                    grant_agreement_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_funding_sources_project",
                        column: x => x.project_id,
                        principalTable: "reconstruction_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_classification_tags",
                columns: table => new
                {
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    tag_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_classification_tags", x => new { x.project_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_pct_project",
                        column: x => x.project_id,
                        principalTable: "reconstruction_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pct_tag",
                        column: x => x.tag_id,
                        principalTable: "classification_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_passports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    edessr_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    technical_permit_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    gps_latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    gps_longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    built_area_sqm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    construction_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    estimated_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_passports", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_passports_project",
                        column: x => x.project_id,
                        principalTable: "reconstruction_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "asset_categories",
                columns: new[] { "id", "code", "created_at", "description", "name" },
                values: new object[,]
                {
                    { 1, "ROAD", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Highways, regional roads, urban streets.", "Road Infrastructure" },
                    { 2, "BLDG", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Residential, educational, medical, administrative buildings.", "Building" },
                    { 3, "BRDG", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Road and railway bridges, pedestrian crossings.", "Bridge" },
                    { 4, "ENERGY", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Power stations, substations, transmission lines.", "Energy Infrastructure" },
                    { 5, "WATER", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Water treatment plants, pipelines, pump stations.", "Water Supply" }
                });

            migrationBuilder.InsertData(
                table: "classification_tags",
                columns: new[] { "id", "code", "label" },
                values: new object[,]
                {
                    { 1, "PRIORITY", "Priority Project" },
                    { 2, "DONOR_FUNDED", "Donor Funded" },
                    { 3, "UNESCO_LISTED", "UNESCO Listed Site" },
                    { 4, "CRITICAL_INFRA", "Critical Infrastructure" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_categories_code_unique",
                table: "asset_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_classification_tags_code_unique",
                table: "classification_tags",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_funding_sources_fund_type",
                table: "funding_sources",
                column: "fund_type");

            migrationBuilder.CreateIndex(
                name: "ix_funding_sources_project_id",
                table: "funding_sources",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_pct_tag_id",
                table: "project_classification_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_passports_edessr_number_unique",
                table: "project_passports",
                column: "edessr_number",
                unique: true,
                filter: "[edessr_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_passports_project_id_unique",
                table: "project_passports",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconstruction_projects_asset_code_unique",
                table: "reconstruction_projects",
                column: "asset_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconstruction_projects_category_id",
                table: "reconstruction_projects",
                column: "asset_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconstruction_projects_status",
                table: "reconstruction_projects",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funding_sources");

            migrationBuilder.DropTable(
                name: "project_classification_tags");

            migrationBuilder.DropTable(
                name: "project_passports");

            migrationBuilder.DropTable(
                name: "classification_tags");

            migrationBuilder.DropTable(
                name: "reconstruction_projects");

            migrationBuilder.DropTable(
                name: "asset_categories");
        }
    }
}
