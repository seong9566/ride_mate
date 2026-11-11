using System.Data;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using RideMateService.Comm;
using RideMateService.Comm.Map;
using RideMateService.Comm.Security;
using RideMateService.Commons.Repository;
using RideMateService.Extensions;

var builder = WebApplication.CreateBuilder(args);

#region 응답압축 설정

builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true; // HTTPS 요청에도 응답압축 활성화
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/xml",
        "text/plain"
    }).Except(new[] { "text/html" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
#endregion

#region 스웨거 API 사용

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(SchemaId);
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RideMate API",
        Version = "v1"
    });
    c.EnableAnnotations();
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"access 토큰만 입력한다. 'Bearer ' 접두는 Swagger가 붙인다.
        예: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityDefinition(WebSessionAuthenticationHandler.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Description = "휴대폰 웹 세션 쿠키",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = WebSessionAuthenticationHandler.CookieName
    });
    c.OperationFilter<SignRouteOperationFilter>();
});
#endregion

#region 도메인 DI

builder.Services.AddDomainModules();
#endregion

#region 공용 서비스

builder.Services.AddCommonInfrastructure();
builder.Services.AddMapInfrastructure(builder.Configuration);
builder.Services.AddSmsInfrastructure(builder.Configuration);
#endregion

#region Dapper 타입 핸들러

SqlMapper.AddTypeHandler(typeof(DateOnly), new DateOnlyHandler());
#endregion

#region 인증

builder.Services.AddAuthInfrastructure(builder.Configuration);

// 설정값은 AddAuthInfrastructure에서 이미 검증되었다
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.SigningKey)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                // 기본 응답을 막고 항상 같은 401 본문을 쓴다
                if (context.Response.HasStarted)
                    return;

                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var body = JsonConvert.SerializeObject(new ResponseModel<object>
                {
                    message = "잘못된 요청입니다.",
                    data = null,
                    code = 401
                });
                await context.Response.WriteAsync(body).ConfigureAwait(false);
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, WebSessionAuthenticationHandler>(
        WebSessionAuthenticationHandler.AuthenticationScheme, null);
#endregion

#region DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("데이터베이스 연결 문자열이 잘못되었습니다.");

builder.Services.AddDbContext<RideMateContext>(options =>
{
    options.UseMySQL(connectionString, mySqlOptions =>
    {
        mySqlOptions.CommandTimeout(60);
        mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
    if (builder.Environment.IsDevelopment())
        options.EnableSensitiveDataLogging();
});

// Dapper가 DbContext와 같은 커넥션을 쓰도록 DbContext에서 꺼내 등록한다
builder.Services.AddTransient<IDbConnection>(sp =>
    sp.GetRequiredService<RideMateContext>().Database.GetDbConnection());
#endregion

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<RunActionFilter>();
    })
    .AddNewtonsoftJson();

builder.Services.AddAuthorization();

#region RateLimiter
// 차 계정 목록 API의 클라이언트별 요청 빈도 제한. 파티션 키는 클라이언트 IP라 프록시 뒤에서는 ForwardedHeaders가 먼저 있어야 한다
builder.Services.AddRateLimitPolicies();
#endregion

var app = builder.Build();

// 키가 비어 있어도 기동은 하므로(도보 시간이 직선 추정으로 떨어진다) ON/OFF만 남기고 키 값은 남기지 않는다
app.Logger.LogInformation("Kakao walk route: {State}",
    string.IsNullOrWhiteSpace(app.Services.GetRequiredService<IOptions<KakaoOptions>>().Value.RestApiKey) ? "OFF" : "ON");

app.UseResponseCompression();

app.UseRouting();

#region Swagger

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RideMate v1");
    c.RoutePrefix = "swagger";
});
#endregion

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter(); // UseRouting 뒤여야 엔드포인트별 정책([EnableRateLimiting])이 붙는다

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers().RequireAuthForSignRoutes();

app.Run();

// Swagger 스키마 이름. 제네릭은 인자를 풀고, OpenAPI가 허용하지 않는 문자는 모두 _로 바꾼다.
// 예: ResponseModel<CursorResult<GetPickupList>> → RideMateService_Comm_ResponseModel_RideMateService_Comm_CursorResult_…
static string SchemaId(Type type)
{
    if (type.IsArray)
        return SchemaId(type.GetElementType()!) + "_Array";

    var name = type.FullName ?? type.Name;
    if (type.IsGenericType)
    {
        // 제네릭 정의 이름에서 `1 같은 인자 개수 표기를 지우고 인자 이름을 이어 붙인다
        var definition = Regex.Replace(type.GetGenericTypeDefinition().FullName ?? type.Name, @"`\d+", "");
        name = definition + "_" + string.Join("_", type.GetGenericArguments().Select(SchemaId));
    }

    return Regex.Replace(name, "[^A-Za-z0-9_]", "_");
}
