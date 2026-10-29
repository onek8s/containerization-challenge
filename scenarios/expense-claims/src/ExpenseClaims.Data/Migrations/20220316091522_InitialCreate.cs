using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseClaims.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "exp");

            migrationBuilder.CreateTable(
                name: "ClaimSequences",
                schema: "exp",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimSequences", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Upn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CostCenter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ManagerEmployeeNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Bic = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                schema: "exp",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    UnitsPerEuro = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => new { x.Date, x.Currency });
                });

            migrationBuilder.CreateTable(
                name: "PaymentRuns",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutionDate = table.Column<DateTime>(type: "date", nullable: false),
                    PaymentCount = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCards",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaskedNumber = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCards_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "exp",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CostCenter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reimbursable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentRunId = table.Column<int>(type: "int", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JournalDocumentNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "exp",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_PaymentRuns_PaymentRunId",
                        column: x => x.PaymentRunId,
                        principalSchema: "exp",
                        principalTable: "PaymentRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CardTransactions",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompanyCardId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "date", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "date", nullable: false),
                    Merchant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MerchantCity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    MerchantCountry = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    MerchantCategoryCode = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    BilledAmountEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTransactions_CompanyCards_CompanyCardId",
                        column: x => x.CompanyCardId,
                        principalSchema: "exp",
                        principalTable: "CompanyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemindedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approvals_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "exp",
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Approvals_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalSchema: "exp",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimEvents",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimEvents_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "exp",
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimLines",
                schema: "exp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "date", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AmountEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Kilometres = table.Column<decimal>(type: "decimal(9,1)", precision: 9, scale: 1, nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerDiem = table.Column<int>(type: "int", nullable: false),
                    Country = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Attendees = table.Column<int>(type: "int", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    VatAmountEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidByCompany = table.Column<bool>(type: "bit", nullable: false),
                    CardTransactionId = table.Column<int>(type: "int", nullable: true),
                    ReceiptId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimLines_CardTransactions_CardTransactionId",
                        column: x => x.CardTransactionId,
                        principalSchema: "exp",
                        principalTable: "CardTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimLines_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "exp",
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "exp",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_ApproverId_Decision",
                schema: "exp",
                table: "Approvals",
                columns: new[] { "ApproverId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_ClaimId",
                schema: "exp",
                table: "Approvals",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTransactions_CompanyCardId_TransactionDate",
                schema: "exp",
                table: "CardTransactions",
                columns: new[] { "CompanyCardId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CardTransactions_Reference",
                schema: "exp",
                table: "CardTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimEvents_ClaimId",
                schema: "exp",
                table: "ClaimEvents",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimLines_CardTransactionId",
                schema: "exp",
                table: "ClaimLines",
                column: "CardTransactionId",
                unique: true,
                filter: "[CardTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimLines_ClaimId_LineNumber",
                schema: "exp",
                table: "ClaimLines",
                columns: new[] { "ClaimId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimLines_ReceiptId",
                schema: "exp",
                table: "ClaimLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EmployeeId",
                schema: "exp",
                table: "Claims",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Number",
                schema: "exp",
                table: "Claims",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PaymentRunId",
                schema: "exp",
                table: "Claims",
                column: "PaymentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status_ExportedAt",
                schema: "exp",
                table: "Claims",
                columns: new[] { "Status", "ExportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status_SubmittedAt",
                schema: "exp",
                table: "Claims",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCards_EmployeeId",
                schema: "exp",
                table: "CompanyCards",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCards_MaskedNumber",
                schema: "exp",
                table: "CompanyCards",
                column: "MaskedNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                schema: "exp",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Upn",
                schema: "exp",
                table: "Employees",
                column: "Upn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRuns_MessageId",
                schema: "exp",
                table: "PaymentRuns",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_FileId",
                schema: "exp",
                table: "Receipts",
                column: "FileId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "ClaimEvents",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "ClaimLines",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "ClaimSequences",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "ExchangeRates",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "CardTransactions",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "Claims",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "CompanyCards",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "PaymentRuns",
                schema: "exp");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "exp");
        }
    }
}
