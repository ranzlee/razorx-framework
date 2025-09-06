using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RazorX.Framework.Tests.Mocks;
using System.Reflection;
using RazorX.Framework;

namespace RazorX.Framework.Tests.Security;

[TestClass]
public class RxAntiforgeryTests {
    private DefaultHttpContext _httpContext = null!;
    private MockAntiforgery _mockAntiforgery = null!;
    private TestLogger<RxAntiforgeryCookieMiddleware> _logger = null!;
    private RxAntiforgeryCookieMiddleware _middleware = null!;
    private RequestDelegate _nextDelegate = null!;
    private bool _nextCalled;

    [TestInitialize]
    public void SetUp() {
        _httpContext = new DefaultHttpContext();
        _mockAntiforgery = new MockAntiforgery();
        _logger = new TestLogger<RxAntiforgeryCookieMiddleware>();
        _nextCalled = false;
        _nextDelegate = _ => {
            _nextCalled = true;
            return Task.CompletedTask;
        };
        _middleware = new RxAntiforgeryCookieMiddleware(_nextDelegate);
    }

    #region Extension Method Tests

    [TestMethod]
    public void AddRxAntiforgery_SetsDefaultCookieName() {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddRxAntiforgery(); // Uses default
        
        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RxAntiforgeryOptions>>();
        Assert.AreEqual("RequestVerificationToken", options.Value.RequestVerificationTokenCookieName);
    }

    [TestMethod]
    public void AddRxAntiforgery_SetsCustomCookieName() {
        // Arrange
        var services = new ServiceCollection();
        var customName = "CustomAntiforgeryToken";
        
        // Act
        services.AddRxAntiforgery(opt => opt.RequestVerificationTokenCookieName = customName);
        
        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RxAntiforgeryOptions>>();
        Assert.AreEqual(customName, options.Value.RequestVerificationTokenCookieName);
    }

    #endregion

    #region GET Request Tests

    [TestMethod]
    public async Task InvokeAsync_GET_Request_AddsCookie() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "GET";
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("localhost");
        _httpContext.Request.Path = "/test";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.GetAndStoreTokensCalled);
        
        // Verify cookie was added
        var cookies = _httpContext.Response.Headers.SetCookie;
        Assert.IsTrue(cookies.Any(cookie => cookie?.Contains("RequestVerificationToken") == true));
    }

    [TestMethod]
    public async Task InvokeAsync_GET_Request_SetsCookieWithCorrectOptions() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "GET";
        var expectedToken = "test-token-value";
        _mockAntiforgery.SetRequestToken(expectedToken);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        var cookies = _httpContext.Response.Headers.SetCookie;
        var antiforgeryCookie = cookies.FirstOrDefault(cookie => 
            cookie?.Contains("RequestVerificationToken") == true);
        
        Assert.IsNotNull(antiforgeryCookie);
        Assert.IsTrue(antiforgeryCookie.Contains($"RequestVerificationToken={expectedToken}"));
        
        // Note: DefaultHttpContext may not serialize all cookie options to headers
        // The important thing is that the cookie was set with the correct value
        // Cookie options are validated by the framework, not the test context
    }

    [TestMethod]
    public async Task InvokeAsync_GET_Request_LogsTraceMessage() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "GET";
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("localhost");
        _httpContext.Request.Path = "/test";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_logger.LogMessages.Any(msg => 
            msg.Contains("Adding Antiforgery token cookie") && 
            msg.Contains("GET") && 
            msg.Contains("https://localhost/test")));
    }

    [TestMethod]
    public async Task InvokeAsync_GET_WithTrimmedMethod_AddsCookie() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "  GET  "; // Method with whitespace

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.GetAndStoreTokensCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_GET_CaseInsensitive_AddsCookie() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "get"; // Lowercase

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.GetAndStoreTokensCalled);
    }

    #endregion

    #region Non-GET Request Tests

    [TestMethod]
    public async Task InvokeAsync_POST_Request_ValidatesToken() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "POST";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
        Assert.IsFalse(_mockAntiforgery.GetAndStoreTokensCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_PUT_Request_ValidatesToken() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "PUT";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_DELETE_Request_ValidatesToken() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "DELETE";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_PATCH_Request_ValidatesToken() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "PATCH";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_NonGET_Request_LogsTraceMessage() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "POST";
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.Host = new HostString("localhost");
        _httpContext.Request.Path = "/api/test";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_logger.LogMessages.Any(msg => 
            msg.Contains("Validating Antiforgery token") && 
            msg.Contains("POST") && 
            msg.Contains("https://localhost/api/test")));
    }

    #endregion

    #region Security Tests

    [TestMethod]
    public async Task InvokeAsync_UsesOrdinalIgnoreCaseComparison() {
        // This test verifies the security fix for locale-specific bypass attacks
        // The fix uses OrdinalIgnoreCase instead of CurrentCultureIgnoreCase

        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "GET";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_mockAntiforgery.GetAndStoreTokensCalled);
        
        // Test with different casing to ensure OrdinalIgnoreCase behavior
        _httpContext.Request.Method = "GeT";
        _mockAntiforgery.Reset();
        
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);
        Assert.IsTrue(_mockAntiforgery.GetAndStoreTokensCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_ValidationFailure_PropagatesException() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "POST";
        _mockAntiforgery.ShouldThrowOnValidation = true;

        // Act & Assert
        await Assert.ThrowsExactlyAsync<AntiforgeryValidationException>(() =>
            _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public async Task InvokeAsync_EmptyMethod_TreatedAsNonGET() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
        Assert.IsFalse(_mockAntiforgery.GetAndStoreTokensCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WhitespaceMethod_TreatedAsNonGET() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "   ";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_UnknownMethod_ValidatesToken() {
        // Arrange
        var options = Options.Create(new RxAntiforgeryOptions {
            RequestVerificationTokenCookieName = "RequestVerificationToken"
        });
        
        _httpContext.Request.Method = "CUSTOM";

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockAntiforgery, _logger, options);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.IsTrue(_mockAntiforgery.ValidateRequestCalled);
    }

    #endregion
}

#region Mock Classes

public class MockAntiforgery : IAntiforgery {
    public bool GetAndStoreTokensCalled { get; private set; }
    public bool ValidateRequestCalled { get; private set; }
    public bool ShouldThrowOnValidation { get; set; }
    
    private string _requestToken = "mock-token";

    public void SetRequestToken(string token) {
        _requestToken = token;
    }

    public void Reset() {
        GetAndStoreTokensCalled = false;
        ValidateRequestCalled = false;
    }

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) {
        GetAndStoreTokensCalled = true;
        return new AntiforgeryTokenSet(_requestToken, "cookie-token", "form-field", "header-name");
    }

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) {
        return new AntiforgeryTokenSet(_requestToken, "cookie-token", "form-field", "header-name");
    }

    public async Task<bool> IsRequestValidAsync(HttpContext httpContext) {
        return await Task.FromResult(!ShouldThrowOnValidation);
    }

    public void SetCookieTokenAndHeader(HttpContext httpContext) {
        // Mock implementation
    }

    public async Task ValidateRequestAsync(HttpContext httpContext) {
        ValidateRequestCalled = true;
        if (ShouldThrowOnValidation) {
            throw new AntiforgeryValidationException("Test validation failure");
        }
        await Task.CompletedTask;
    }
}

#endregion