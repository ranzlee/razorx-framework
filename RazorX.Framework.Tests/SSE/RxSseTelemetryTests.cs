using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework.Tests.SSE;

[TestClass]
public class RxSseTelemetryTests {
    private ILogger<RxSseBroadcastService<TestModel, TestMetadata>> CreateLogger() {
        return LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<RxSseBroadcastService<TestModel, TestMetadata>>();
    }
    public record TestModel(int Id, string Name);
    public record TestMetadata(string? SubscriberId);
    [TestMethod]
    public async Task BroadcastUpdate_DoesNotThrow_WithInstrumentation() {
        var logger = CreateLogger();
        var service = new RxSseBroadcastService<TestModel, TestMetadata>(logger);
        service.Subscribe("test-subscriber");
        await service.BroadcastUpdate(new TestModel(1, "Test"), new TestMetadata("test-subscriber"));
        service.Dispose();
    }
    [TestMethod]
    public void TransportMessage_IncludesTraceIdAndParentSpanId() {
        var traceId = ActivityTraceId.CreateRandom().ToString();
        var spanId = ActivitySpanId.CreateRandom().ToString();
        var msg = new TransportMessage(
            PayloadJson: "{}",
            BroadcasterMetadataJson: null,
            SourceServerId: "server-1",
            TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TraceId: traceId,
            ParentSpanId: spanId
        );
        Assert.AreEqual(traceId, msg.TraceId);
        Assert.AreEqual(spanId, msg.ParentSpanId);
    }
    [TestMethod]
    public void TransportMessage_SerializesAndDeserializes_WithTraceContext() {
        var traceId = ActivityTraceId.CreateRandom().ToString();
        var spanId = ActivitySpanId.CreateRandom().ToString();
        var original = new TransportMessage(
            PayloadJson: "{\"id\":1}",
            BroadcasterMetadataJson: "{\"subscriberId\":\"test\"}",
            SourceServerId: "server-1",
            TimestampUnixMs: 1234567890,
            TraceId: traceId,
            ParentSpanId: spanId
        );
        var json = JsonSerializer.Serialize(original, RxJsonSerializerContext.Default.TransportMessage);
        var deserialized = JsonSerializer.Deserialize(json, RxJsonSerializerContext.Default.TransportMessage);
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(original.PayloadJson, deserialized.PayloadJson);
        Assert.AreEqual(original.TraceId, deserialized.TraceId);
        Assert.AreEqual(original.ParentSpanId, deserialized.ParentSpanId);
        Assert.AreEqual(original.SourceServerId, deserialized.SourceServerId);
    }
    [TestMethod]
    public void TransportMessage_AllowsNullTraceContext() {
        var msg = new TransportMessage(
            PayloadJson: "{}",
            BroadcasterMetadataJson: null,
            SourceServerId: "server-1",
            TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TraceId: null,
            ParentSpanId: null
        );
        Assert.IsNull(msg.TraceId);
        Assert.IsNull(msg.ParentSpanId);
        var json = JsonSerializer.Serialize(msg, RxJsonSerializerContext.Default.TransportMessage);
        var deserialized = JsonSerializer.Deserialize(json, RxJsonSerializerContext.Default.TransportMessage);
        Assert.IsNotNull(deserialized);
        Assert.IsNull(deserialized.TraceId);
        Assert.IsNull(deserialized.ParentSpanId);
    }
}
