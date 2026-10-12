using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Limita.Application.Common;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Auth.Models;
using Limita.Application.Features.Transfers.Models;
using Limita.Domain.Enums;
using Limita.IntegrationTests.Infrastructure;
using Xunit;
using TransactionDto = Limita.Application.Features.Transactions.Models.TransactionDto;

namespace Limita.IntegrationTests.Features.Transfers;

public class TransferIntegrationTests : IClassFixture<LimitaTestFactory>
{
    private readonly LimitaTestFactory _factory;

    public TransferIntegrationTests(LimitaTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TransferLifecycle_ShouldInitiateConfirmDebitAndRecordLedgerEntries()
    {
        // Arrange
        // 1. Authenticate sender (John Doe)
        var senderToken = await _factory.GetAccessTokenAsync("john.doe@limita.bank", "User@123456!");
        using var senderClient = _factory.CreateAuthorizedClient(senderToken);

        var senderAccountsResp = await senderClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<AccountSummaryDto>>>("/api/Accounts", LimitaTestFactory.JsonOptions);
        var senderEgpAccount = senderAccountsResp!.Data!.First(a => a.Currency == "EGP");
        var initialSenderBalance = senderEgpAccount.Balance;

        // 2. Register recipient
        var recipientEmail = $"recipient_{Guid.NewGuid():N}@limita.bank";
        using var anonClient = _factory.CreateClient();
        var registerResp = await anonClient.PostAsJsonAsync("/api/Auth/register", new RegisterRequest(
            FullName: "Transfer Recipient",
            Email: recipientEmail,
            PhoneNumber: "+201011223344",
            Password: "Recipient@123456!"), LimitaTestFactory.JsonOptions);
        registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipientAuth = await registerResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(LimitaTestFactory.JsonOptions);
        using var recipientClient = _factory.CreateAuthorizedClient(recipientAuth!.Data!.AccessToken);

        var recipientAccountsResp = await recipientClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<AccountSummaryDto>>>("/api/Accounts", LimitaTestFactory.JsonOptions);
        var recipientEgpAccount = recipientAccountsResp!.Data!.First(a => a.Currency == "EGP");
        var initialRecipientBalance = recipientEgpAccount.Balance;

        // Act 1: Initiate transfer for 250 EGP
        const decimal transferAmount = 250m;
        var createRequest = new CreateTransferRequest(
            SourceAccountId: senderEgpAccount.Id,
            DestinationAccountNumber: recipientEgpAccount.AccountNumber,
            Amount: transferAmount,
            Description: "Integration test transfer");

        var createResp = await senderClient.PostAsJsonAsync("/api/Transfers", createRequest, LimitaTestFactory.JsonOptions);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResp.Content.ReadFromJsonAsync<ApiResponse<TransferResponseDto>>(LimitaTestFactory.JsonOptions);
        createBody.Should().NotBeNull();
        createBody!.Success.Should().BeTrue();
        createBody.Data!.Status.Should().Be(TransferStatus.Pending);
        var transferId = createBody.Data.Id;

        // Act 2: Confirm transfer
        var confirmResp = await senderClient.PostAsync($"/api/Transfers/{transferId}/confirm", null);
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmBody = await confirmResp.Content.ReadFromJsonAsync<ApiResponse<ConfirmTransferResponseDto>>(LimitaTestFactory.JsonOptions);
        confirmBody.Should().NotBeNull();
        confirmBody!.Success.Should().BeTrue();
        confirmBody.Data!.Status.Should().Be(TransferStatus.Completed);
        confirmBody.Data.TransferredAmount.Should().Be(transferAmount);

        // Assert 1: Verify sender balance
        var senderBalanceResp = await senderClient.GetFromJsonAsync<ApiResponse<AccountBalanceDto>>($"/api/Accounts/{senderEgpAccount.Id}/balance", LimitaTestFactory.JsonOptions);
        senderBalanceResp!.Data!.Balance.Should().Be(initialSenderBalance - transferAmount);

        // Assert 2: Verify recipient balance
        var recipientBalanceResp = await recipientClient.GetFromJsonAsync<ApiResponse<AccountBalanceDto>>($"/api/Accounts/{recipientEgpAccount.Id}/balance", LimitaTestFactory.JsonOptions);
        recipientBalanceResp!.Data!.Balance.Should().Be(initialRecipientBalance + transferAmount);

        // Assert 3: Re-confirming should be rejected
        var reconfirmResp = await senderClient.PostAsync($"/api/Transfers/{transferId}/confirm", null);
        reconfirmResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert 4: Verify ledger entries in transactions
        var senderTxResp = await senderClient.GetFromJsonAsync<ApiResponse<PagedResult<TransactionDto>>>($"/api/Transactions?accountId={senderEgpAccount.Id}", LimitaTestFactory.JsonOptions);
        senderTxResp!.Data!.Items.Should().Contain(t => t.Amount == transferAmount && t.Type == TransactionType.TransferOut);

        var recipientTxResp = await recipientClient.GetFromJsonAsync<ApiResponse<PagedResult<TransactionDto>>>($"/api/Transactions?accountId={recipientEgpAccount.Id}", LimitaTestFactory.JsonOptions);
        recipientTxResp!.Data!.Items.Should().Contain(t => t.Amount == transferAmount && t.Type == TransactionType.TransferIn);
    }

    [Fact]
    public async Task TransferCancel_ShouldUpdateStatusToCancelled_WhenPending()
    {
        // Arrange
        var senderToken = await _factory.GetAccessTokenAsync("john.doe@limita.bank", "User@123456!");
        using var senderClient = _factory.CreateAuthorizedClient(senderToken);

        var senderAccountsResp = await senderClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<AccountSummaryDto>>>("/api/Accounts", LimitaTestFactory.JsonOptions);
        var senderEgpAccount = senderAccountsResp!.Data!.First(a => a.Currency == "EGP");

        // Use valid recipient account
        var recipientEmail = $"cancel_recipient_{Guid.NewGuid():N}@limita.bank";
        using var anonClient = _factory.CreateClient();
        var regResp = await anonClient.PostAsJsonAsync("/api/Auth/register", new RegisterRequest(
            "Cancel Recipient", recipientEmail, "+201099887711", "Password123!"), LimitaTestFactory.JsonOptions);
        var regBody = await regResp.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(LimitaTestFactory.JsonOptions);
        using var recClient = _factory.CreateAuthorizedClient(regBody!.Data!.AccessToken);
        var recAccounts = await recClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<AccountSummaryDto>>>("/api/Accounts", LimitaTestFactory.JsonOptions);
        var destAccount = recAccounts!.Data!.First();

        var validCreateRequest = new CreateTransferRequest(senderEgpAccount.Id, destAccount.AccountNumber, 100m, "Cancel test");
        var createResp = await senderClient.PostAsJsonAsync("/api/Transfers", validCreateRequest, LimitaTestFactory.JsonOptions);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResp.Content.ReadFromJsonAsync<ApiResponse<TransferResponseDto>>(LimitaTestFactory.JsonOptions);
        var transferId = createBody!.Data!.Id;

        // Act: Cancel transfer
        var cancelResp = await senderClient.PostAsync($"/api/Transfers/{transferId}/cancel", null);

        // Assert
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelBody = await cancelResp.Content.ReadFromJsonAsync<ApiResponse<CancelTransferResponseDto>>(LimitaTestFactory.JsonOptions);
        cancelBody!.Success.Should().BeTrue();
        cancelBody.Data!.Status.Should().Be(TransferStatus.Cancelled);

        // Attempting to confirm cancelled transfer should fail
        var confirmResp = await senderClient.PostAsync($"/api/Transfers/{transferId}/confirm", null);
        confirmResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transfer_ShouldFail_WhenAmountExceedsAccountBalance()
    {
        // Arrange
        var senderToken = await _factory.GetAccessTokenAsync("john.doe@limita.bank", "User@123456!");
        using var senderClient = _factory.CreateAuthorizedClient(senderToken);

        var senderAccountsResp = await senderClient.GetFromJsonAsync<ApiResponse<IReadOnlyList<AccountSummaryDto>>>("/api/Accounts", LimitaTestFactory.JsonOptions);
        var senderEgpAccount = senderAccountsResp!.Data!.First(a => a.Currency == "EGP");

        // Amount far greater than account balance
        var request = new CreateTransferRequest(
            SourceAccountId: senderEgpAccount.Id,
            DestinationAccountNumber: "EG10002000300040",
            Amount: 99_999_999m,
            Description: "Excessive transfer");

        // Act
        var response = await senderClient.PostAsJsonAsync("/api/Transfers", request, LimitaTestFactory.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(LimitaTestFactory.JsonOptions);
        body!.Success.Should().BeFalse();
    }
}
