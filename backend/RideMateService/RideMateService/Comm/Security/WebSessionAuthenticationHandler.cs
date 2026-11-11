using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RideMateService.Comm;
using RideMateService.Repository.v1.Auth;

namespace RideMateService.Comm.Security;

/// <summary>
/// HttpOnly 쿠키의 웹 세션 토큰으로 휴대폰 웹 요청을 인증한다.
/// </summary>
public class WebSessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "WebSession";
    public const string CookieName = "rm_web_session";

    private readonly IAuthRepository _authRepository;
    private readonly IHashService _hashService;

    public WebSessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, IAuthRepository authRepository, IHashService hashService)
        : base(options, logger, encoder)
    {
        this._authRepository = authRepository;
        this._hashService = hashService;
    }

    /// <summary>
    /// 쿠키가 없으면 다른 인증 흐름을 막지 않고, 있으면 살아 있는 WEB 세션인지 확인한다.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var tokenHash = _hashService.Hmac("web-session:" + token);
        var session = await _authRepository.GetWebSessionAsync(tokenHash, DateTime.Now, Context.RequestAborted)
            .ConfigureAwait(false);
        if (session is null)
            return AuthenticateResult.Fail("웹 세션이 올바르지 않습니다.");

        var claims = new[]
        {
            new Claim("sub", session.Value.userId.ToString()),
            new Claim("sid", session.Value.loginSessionId.ToString())
        };
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme));
    }

    /// <summary>
    /// 인증 실패 응답은 차량 JWT와 같은 공통 401 본문으로 돌려준다.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
            return;

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        var body = JsonConvert.SerializeObject(new ResponseModel<object>
        {
            message = "잘못된 요청입니다.",
            data = null,
            code = 401
        });
        await Response.WriteAsync(body).ConfigureAwait(false);
    }
}
