using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RideMateService.Comm.Logger;

namespace RideMateService.Comm.Sms;

/// <summary>
/// 솔라피 단건 발송 API(POST /messages/v4/send)로 문자를 발송하는 구현
/// </summary>
public class SolapiSmsSender : ISmsSender
{
    // 접수 성공을 뜻하는 응답 statusCode
    private const string AcceptedStatusCode = "2000";

    private readonly HttpClient _httpClient;
    private readonly SolapiOptions _options;
    private readonly ILogService _logService;

    public SolapiSmsSender(HttpClient httpClient, IOptions<SolapiOptions> options, ILogService logService)
    {
        this._httpClient = httpClient;
        this._options = options.Value;
        this._logService = logService;
    }

    /// <summary>
    /// 문자 한 건을 보낸다. SMS/LMS 구분은 type을 넣지 않고 솔라피의 자동 판별에 맡긴다.
    /// ResultCode는 성공이면 응답 statusCode, 실패면 응답 errorCode(없으면 statusCode, 그것도 없으면 HTTP_{상태코드}), 타임아웃이면 TIMEOUT, 그 밖의 예외면 ERROR다.
    /// 응답 본문과 번호·메시지는 로그에 남기지 않는다.
    /// </summary>
    public async Task<SmsSendResult> SendAsync(string phoneDigits, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new JObject
            {
                ["message"] = new JObject
                {
                    ["to"] = phoneDigits,
                    ["from"] = _options.Sender,
                    ["text"] = message
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "messages/v4/send");
            request.Headers.TryAddWithoutValidation("Authorization", CreateAuthorization());
            request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var (statusCode, errorCode) = ReadCodes(responseText);

            if (response.IsSuccessStatusCode && statusCode == AcceptedStatusCode)
                return new SmsSendResult(true, AcceptedStatusCode);

            return new SmsSendResult(false, errorCode ?? statusCode ?? $"HTTP_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // HttpClient.Timeout 초과
            return new SmsSendResult(false, "TIMEOUT");
        }
        catch (Exception ex)
        {
            // 예외 메시지에 요청 내용이 섞일 수 있어 예외 형식만 남긴다
            _logService.LogMessage($"Solapi request error: {ex.GetType().Name}");
            return new SmsSendResult(false, "ERROR");
        }
    }

    // HMAC-SHA256 인증 헤더. 서명 대상은 date + salt, 서명은 소문자 hex다. salt는 요청마다 새로 만든다
    private string CreateAuthorization()
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var salt = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var signature = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.ApiSecret), Encoding.UTF8.GetBytes(date + salt)));
        return $"HMAC-SHA256 apiKey={_options.ApiKey}, date={date}, salt={salt}, signature={signature}";
    }

    // 응답에서 statusCode와 errorCode만 꺼낸다. JSON 객체가 아니면(HTML 오류 페이지 등) 둘 다 null이다
    private static (string? StatusCode, string? ErrorCode) ReadCodes(string responseText)
    {
        try
        {
            if (JToken.Parse(responseText) is not JObject root)
                return (null, null);

            return (ReadText(root, "statusCode"), ReadText(root, "errorCode"));
        }
        catch (JsonReaderException)
        {
            return (null, null);
        }
    }

    private static string? ReadText(JObject root, string name)
    {
        if (root[name] is not JValue { Value: not null } value)
            return null;

        var text = Convert.ToString(value.Value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
