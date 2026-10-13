using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Limita.Application.Common;
using Limita.Application.Features.Auth.Models;
using Limita.IntegrationTests.Infrastructure;
using Xunit;

namespace Limita.IntegrationTests.Features.Auth;

public class SecurityHardeningTests : IClassFixture<LimitaTestFactory>
{
    private readonly LimitaTestFactory _factory;

    public SecurityHardeningTests(LimitaTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ShouldSetSecureHttpOnlyCookie_ForRefreshToken()
    {
        // Arrange
        await _factory.ResetUserLockoutAsync("john.doe@limita.bank");
        using var client = _factory.CreateClient();
        var request = new LoginRequest("john.doe@limita.bank", "User@123456!");

        // Act
        var response = await client.PostAsJsonAsync("/api/Auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeaders = response.Headers.GetValues("Set-Cookie");
        var refreshCookie = setCookieHeaders.FirstOrDefault(c => c.StartsWith("refreshToken="));
        refreshCookie.Should().NotBeNull();
        refreshCookie.Should().Contain("httponly");
        refreshCookie.Should().Contain("samesite=strict");
    }

    [Fact]
    public async Task Logout_ShouldClearRefreshTokenCookie()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var uniqueEmail = $"cookie_logout_{Guid.NewGuid():N}@limita.bank";
        var regResponse = await client.PostAsJsonAsync("/api/Auth/register",
            new RegisterRequest("Cookie User", uniqueEmail, "+201099112233", "Password123!"));
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var regBody = await regResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var refreshToken = regBody!.Data!.RefreshToken;

        // Act
        var logoutResponse = await client.PostAsJsonAsync("/api/Auth/logout", new LogoutRequest(refreshToken));

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        logoutResponse.Headers.Contains("Set-Cookie").Should().BeTrue();

        var setCookieHeaders = logoutResponse.Headers.GetValues("Set-Cookie");
        var refreshCookie = setCookieHeaders.FirstOrDefault(c => c.StartsWith("refreshToken="));
        refreshCookie.Should().NotBeNull();
        // Cookie deletion sets empty or expires in the past
        refreshCookie.Should().Contain("expires=");
    }

    [Fact]
    public async Task Cors_ShouldReturnAccessControlAllowOrigin_InDevelopment()
    {
        // Arrange
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/Auth/login");
        request.Headers.Add("Origin", "https://custom-dev-origin.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").First().Should().Be("*");
    }
}
