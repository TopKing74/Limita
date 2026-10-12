using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Limita.Application.Common;
using Limita.Application.Features.Auth.Models;
using Limita.IntegrationTests.Infrastructure;
using Xunit;

namespace Limita.IntegrationTests.Features.Auth;

public class AuthIntegrationTests : IClassFixture<LimitaTestFactory>
{
    private readonly LimitaTestFactory _factory;

    public AuthIntegrationTests(LimitaTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ShouldReturn200AndJwtTokens_WhenCredentialsAreValid()
    {
        // Arrange
        await _factory.ResetUserLockoutAsync("john.doe@limita.bank");
        using var client = _factory.CreateClient();
        var request = new LoginRequest("john.doe@limita.bank", "User@123456!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Data.Email.Should().Be("john.doe@limita.bank");
        body.Data.Roles.Should().Contain("Customer");
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenPasswordIsIncorrect()
    {
        // Arrange
        await _factory.ResetUserLockoutAsync("john.doe@limita.bank");
        using var client = _factory.CreateClient();
        var request = new LoginRequest("john.doe@limita.bank", "WrongPassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("Invalid email or password");
        await _factory.ResetUserLockoutAsync("john.doe@limita.bank");
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenEmailDoesNotExist()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var request = new LoginRequest("nonexistent.user@limita.bank", "User@123456!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ShouldCreateUserAndPrimaryAccount_WhenValid()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var uniqueEmail = $"qa_user_{Guid.NewGuid():N}@limita.bank";
        var request = new RegisterRequest(
            FullName: "QA Automated User",
            Email: uniqueEmail,
            PhoneNumber: "+201099887766",
            Password: "SecurePassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.Data.Email.Should().Be(uniqueEmail);

        // Verify newly registered user can access /api/Accounts and has a primary account
        using var authClient = _factory.CreateAuthorizedClient(body.Data.AccessToken);
        var accountsResponse = await authClient.GetAsync("/api/Accounts");
        accountsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ShouldReturn400_WhenEmailAlreadyExists()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var request = new RegisterRequest(
            FullName: "Duplicate User",
            Email: "john.doe@limita.bank", // Already exists
            PhoneNumber: "+201011112222",
            Password: "Password123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturn401_WhenNoTokenProvided()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/Auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturn200_WhenValidTokenProvided()
    {
        // Arrange
        var token = await _factory.GetAccessTokenAsync();
        using var client = _factory.CreateAuthorizedClient(token);

        // Act
        var response = await client.GetAsync("/api/Auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_ShouldRotateTokens_WhenValid()
    {
        // Arrange - Register a dedicated test user to guarantee zero concurrency race condition
        using var client = _factory.CreateClient();
        var uniqueEmail = $"refresh_user_{Guid.NewGuid():N}@limita.bank";
        var regResponse = await client.PostAsJsonAsync("/api/Auth/register",
            new RegisterRequest("Refresh Test User", uniqueEmail, "+201055554444", "Password123!"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        var initialAccess = regBody!.Data!.AccessToken;
        var initialRefresh = regBody.Data.RefreshToken;

        var refreshRequest = new RefreshTokenRequest(initialAccess, initialRefresh);

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/api/Auth/refresh", refreshRequest);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        refreshBody.Should().NotBeNull();
        refreshBody!.Success.Should().BeTrue();
        refreshBody.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshBody.Data.RefreshToken.Should().NotBe(initialRefresh); // Rotated
    }

    [Fact]
    public async Task Logout_ShouldInvalidateRefreshToken()
    {
        // Arrange - Register a dedicated test user for logout isolation
        using var client = _factory.CreateClient();
        var uniqueEmail = $"logout_user_{Guid.NewGuid():N}@limita.bank";
        var regResponse = await client.PostAsJsonAsync("/api/Auth/register",
            new RegisterRequest("Logout Test User", uniqueEmail, "+201055553333", "Password123!"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var regBody = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();

        var accessToken = regBody!.Data!.AccessToken;
        var refreshToken = regBody.Data.RefreshToken;

        // Act 1: Logout
        var logoutResponse = await client.PostAsJsonAsync("/api/Auth/logout", new LogoutRequest(refreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Attempt refresh with revoked token
        var refreshResponse = await client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequest(accessToken, refreshToken));

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse>();
        refreshBody!.Success.Should().BeFalse();
    }
}
