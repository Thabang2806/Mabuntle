namespace Swyftly.Application.Common.Validation;

public sealed record ValidationFailure(
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode = null);
