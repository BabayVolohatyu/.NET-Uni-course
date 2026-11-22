using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EDRPOU = table.Column<string>(type: "nchar(8)", fixedLength: true, maxLength: 8, nullable: false),
                    AuthorizedCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                    table.CheckConstraint("CK_Contractors_AuthorizedCapital", "[AuthorizedCapital] > 0");
                });

            migrationBuilder.CreateTable(
                name: "WorkCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractorDetails",
                columns: table => new
                {
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    LegalAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DirectorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorDetails", x => x.ContractorId);
                    table.ForeignKey(
                        name: "FK_ContractorDetails_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractorDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "date", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorDocuments_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractorWorkCategories",
                columns: table => new
                {
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    QualificationDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorWorkCategories", x => new { x.ContractorId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ContractorWorkCategories_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractorWorkCategories_WorkCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "WorkCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "WorkCategories",
                columns: new[] { "Id", "Code", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "ROAD", "Construction and repair of roads, bridges, and transport infrastructure.", "Road and Bridge Repair" },
                    { 2, "BLDG", "Residential and commercial building construction, schools, hospitals.", "Building and Construction" },
                    { 3, "ELEC", "Power grids, substations, street lighting, and industrial wiring.", "Electrical Works" },
                    { 4, "PIPE", "Water supply networks, sewage systems, and water treatment facilities.", "Pipeline and Water Supply" },
                    { 5, "HEAT", "District heating networks, boiler houses, and thermal insulation.", "Heating Systems" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorDocuments_ContractorId_ExpiresAt",
                table: "ContractorDocuments",
                columns: new[] { "ContractorId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorDocuments_DocumentNumber",
                table: "ContractorDocuments",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_EDRPOU",
                table: "Contractors",
                column: "EDRPOU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_IsActive",
                table: "Contractors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_Name",
                table: "Contractors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorWorkCategories_CategoryId_QualificationDate",
                table: "ContractorWorkCategories",
                columns: new[] { "CategoryId", "QualificationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkCategories_Code",
                table: "WorkCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCategories_Name",
                table: "WorkCategories",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractorDetails");

            migrationBuilder.DropTable(
                name: "ContractorDocuments");

            migrationBuilder.DropTable(
                name: "ContractorWorkCategories");

            migrationBuilder.DropTable(
                name: "Contractors");

            migrationBuilder.DropTable(
                name: "WorkCategories");
        }
    }
}
