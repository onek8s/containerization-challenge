using ExpenseClaims.Core.Claims;
using ExpenseClaims.Core.Policy;
using ExpenseClaims.Data.Entities;

namespace ExpenseClaims.Api.Models;

public sealed record CreateClaimRequest(string Purpose, string? CostCenter, string? ProjectCode);

public sealed record UpdateClaimRequest(string Purpose, string? CostCenter, string? ProjectCode);

public sealed record ClaimLineRequest(
    DateOnly ExpenseDate,
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    string? Currency,
    decimal? Kilometres,
    string? Route,
    PerDiemType PerDiem,
    string? Country,
    int Attendees);

public sealed record DecisionRequest(string? Comment);

public sealed record ClaimSummaryDto(
    int Id,
    string Number,
    string Employee,
    string Purpose,
    ClaimStatus Status,
    decimal Total,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    int LineCount);

public sealed record ClaimLineDto(
    int Id,
    int LineNumber,
    DateOnly ExpenseDate,
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    decimal AmountEur,
    decimal? Kilometres,
    string? Route,
    PerDiemType PerDiem,
    string? Country,
    int Attendees,
    decimal VatRate,
    decimal VatAmountEur,
    bool PaidByCompany,
    CardTransactionDto? CardTransaction,
    ReceiptDto? Receipt);

public sealed record CardTransactionDto(
    int Id,
    string Card,
    DateOnly TransactionDate,
    string Merchant,
    string? City,
    string? Country,
    string MerchantCategoryCode,
    ExpenseCategory SuggestedCategory,
    decimal OriginalAmount,
    string OriginalCurrency,
    decimal BilledAmountEur);

public sealed record ReceiptDto(int Id, string FileName, string ContentType, long Length);

public sealed record ApprovalDto(int Level, string Approver, ApprovalDecision Decision, string? Comment, DateTime? DecidedAt);

public sealed record ClaimEventDto(DateTime At, string Actor, string Action, string? Details);

public sealed record ClaimDto(
    int Id,
    string Number,
    string EmployeeNumber,
    string Employee,
    string Purpose,
    string CostCenter,
    string? ProjectCode,
    ClaimStatus Status,
    decimal Total,
    decimal Reimbursable,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? PaidAt,
    string RowVersion,
    bool CanEdit,
    bool CanDecide,
    IReadOnlyList<ClaimLineDto> Lines,
    IReadOnlyList<ApprovalDto> Approvals,
    IReadOnlyList<PolicyFinding> Findings,
    IReadOnlyList<ClaimEventDto> Events);

public sealed record MeDto(
    string Upn,
    string? EmployeeNumber,
    string DisplayName,
    string? CostCenter,
    bool HasBankAccount,
    bool IsApprover,
    bool IsFinance,
    bool IsAdministrator);

public sealed record PaymentRunDto(
    int Id,
    string MessageId,
    DateTime CreatedAt,
    DateOnly ExecutionDate,
    int PaymentCount,
    decimal Total,
    PaymentRunStatus Status,
    string? BankReference,
    string? Error);
