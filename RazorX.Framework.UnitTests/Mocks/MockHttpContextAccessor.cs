using Microsoft.AspNetCore.Http;

namespace RazorX.Framework.UnitTests.Mocks;

public sealed class MockHttpContextAccessor : IHttpContextAccessor {
    public HttpContext? HttpContext { get; set; }

    public static MockHttpContextAccessor WithRxRequest() {
        var context = new DefaultHttpContext();
        context.Request.Headers["rx-request"] = "";

        return new MockHttpContextAccessor { HttpContext = context };
    }

    public static MockHttpContextAccessor WithoutRxRequest() {
        var context = new DefaultHttpContext();

        return new MockHttpContextAccessor { HttpContext = context };
    }

    public static MockHttpContextAccessor WithoutHttpContext() {
        return new MockHttpContextAccessor { HttpContext = null };
    }
}