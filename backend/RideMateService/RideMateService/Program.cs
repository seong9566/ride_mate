using System.IO.Compression;
using Dapper;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi;
using RideMateService.Comm;
using RideMateService.Comm.Logger;

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
    c.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace(".", "_"));
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RideMate API",
        Version = "v1"
    });
    c.EnableAnnotations();
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"입력양식 - 'Bearer' [space] and then your token.
        예: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
});
#endregion

#region 공용 서비스

builder.Services.AddTransient<ILogService, LogService>();
#endregion

#region Dapper 타입 핸들러

SqlMapper.AddTypeHandler(typeof(DateOnly), new DateOnlyHandler());
#endregion

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<RunActionFilter>();
    })
    .AddNewtonsoftJson();

builder.Services.AddAuthorization();

var app = builder.Build();

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

app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
