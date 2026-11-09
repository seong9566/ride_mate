// Program.cs의 "DbContext" 자리(docs/architecture/overview.md의 서비스 등록 순서 11번)에 넣는다.
// 파일 맨 위 using: System.Data, Microsoft.EntityFrameworkCore, RideMateService.Commons.Repository

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
