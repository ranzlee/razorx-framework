namespace RazorX.Integration.Tests;


[TestClass]
public partial class IntegrationTests : PageTest {

    [GeneratedRegex("RazorX Framework Integration Tests")]
    private static partial Regex MyRegex();

    [TestMethod]
    public async Task PageIsInitialized() {
        await Page.GotoAsync("https://localhost:44376/");
        await Expect(Page).ToHaveTitleAsync(MyRegex());
    }

    [TestMethod("Custom trigger validated")]
    public async Task Test01() {
        await TestResult("test-01");
    }

    [TestMethod("Request method validated")]
    public async Task Test02() {
        await TestResult("test-02");
    }

    [TestMethod("Request action validated")]
    public async Task Test03() {
        await TestResult("test-03");
    }

    [TestMethod("Request body validated")]
    public async Task Test04() {
        await TestResult("test-04");
    }

    [TestMethod("Request body as JSON validated")]
    public async Task Test05() {
        await TestResult("test-05");
    }

    [TestMethod("Request \"rx-request\" header validated")]
    public async Task Test06() {
        await TestResult("test-06");
    }

    [TestMethod("Request abort availability validated")]
    public async Task Test07() {
        await TestResult("test-07");
    }

    [TestMethod("Element is already executing a request Error")]
    public async Task Test08() {
        await TestResult("test-08");
    }

    [TestMethod("Response header validated")]
    public async Task Test09() {
        await TestResult("test-09");
    }

    [TestMethod("Merge swap target validated")]
    public async Task Test10() {
        await TestResult("test-10");
    }

    [TestMethod("Merge swap strategy validated")]
    public async Task Test11() {
        await TestResult("test-11");
    }

    [TestMethod("Element swap validated")]
    public async Task Test12() {
        await TestResult("test-12");
    }

    [TestMethod("Swapped in script execution validated")]
    public async Task Test13() {
        await TestResult("test-13");
    }

    [TestMethod("Merge morph target validated")]
    public async Task Test14() {
        await TestResult("test-14");
    }

    [TestMethod("Merge morph strategy validated")]
    public async Task Test15() {
        await TestResult("test-15");
    }

    [TestMethod("Element morph validated")]
    public async Task Test16() {
        await TestResult("test-16");
    }

    [TestMethod("Morphed in script execution validated")]
    public async Task Test17() {
        await TestResult("test-17");
    }

    [TestMethod("Element remove validated")]
    public async Task Test18() {
        await TestResult("test-18");
    }

    [TestMethod("Multi-fragment merge (swap, morph, and remove) validated")]
    public async Task Test19() {
        await TestResult("test-19");
    }

    [TestMethod("Swapped in trigger invoke validated")]
    public async Task Test20() {
        await TestResult("test-20");
    }

    [TestMethod("Morphed in trigger invoke validated")]
    public async Task Test21() {
        await TestResult("test-21");
    }

    [TestMethod("Adjacent swap validated")]
    public async Task Test22() {
        await TestResult("test-22");
    }

    [TestMethod("Debounce and disable in-flight validated")]
    public async Task Test23() {
        await TestResult("test-23");
    }

    private async Task TestResult(string testId) {
        await Page.GotoAsync("https://localhost:44376/");
        var result = Page.Locator($"#{testId}-result");
        await Expect(result).Not.ToHaveClassAsync("pass");
        await Page.Locator($"#{testId}").ClickAsync();
        await Expect(result).ToHaveClassAsync("pass");
    }
}
