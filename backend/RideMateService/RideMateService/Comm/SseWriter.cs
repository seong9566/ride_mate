using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;

namespace RideMateService.Comm;

/// <summary>
/// SSE 이벤트 한 건
/// </summary>
/// <param name="EventName">이벤트 이름. null이면 하트비트 주석 줄(`: ping`)</param>
/// <param name="Data">이벤트 데이터. 한 줄 JSON으로 쓴다</param>
public sealed record SseEvent(string? EventName, object? Data);

/// <summary>
/// SSE(text/event-stream) 응답 쓰기. 아이 픽업 안내와 차량 픽업 링크 전달 상태 실시간 연결이 같이 쓴다 [ADR-0024]
/// </summary>
public static class SseWriter
{
    /// <summary>
    /// SSE 헤더를 쓰고 이벤트를 하나씩 쓴 뒤 바로 내보낸다(중계기에 쌓이지 않게 버퍼링을 끈다)
    /// </summary>
    public static async Task WriteAsync(HttpResponse response, IAsyncEnumerable<SseEvent> events,
        JsonSerializerSettings settings, CancellationToken cancellationToken)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Accel-Buffering"] = "no";
        response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await foreach (var e in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var payload = e.EventName is null
                ? ": ping\n\n"
                : $"event: {e.EventName}\ndata: {JsonConvert.SerializeObject(e.Data, Formatting.None, settings)}\n\n";
            await response.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
