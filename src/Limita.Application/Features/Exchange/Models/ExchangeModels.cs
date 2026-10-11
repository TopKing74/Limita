namespace Limita.Application.Features.Exchange.Models;


public record ExchangeQuoteRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount);



public record ExchangeQuoteDto(
    Guid SourceAccountId,
    string SourceAccountNumber,
    string FromCurrency,
    Guid DestinationAccountId,
    string DestinationAccountNumber,
    string ToCurrency,
    decimal SourceAmount,
    decimal Rate,
    decimal ConvertedAmount,
    DateTime RateUpdatedAt,
    DateTime QuotedAt,
    string Message);



public record ExecuteExchangeRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount);



public record ExchangeResultDto(
    string ReferenceNumber,
    Guid SourceAccountId,
    string SourceAccountNumber,
    string FromCurrency,
    decimal DebitedAmount,
    decimal SourceRemainingBalance,
    Guid DestinationAccountId,
    string DestinationAccountNumber,
    string ToCurrency,
    decimal CreditedAmount,
    decimal DestinationNewBalance,
    decimal AppliedRate,
    Guid DebitTransactionId,
    Guid CreditTransactionId,
    DateTime ExecutedAt,
    string Message);
