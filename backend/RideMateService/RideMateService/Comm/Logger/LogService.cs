using Microsoft.Extensions.Logging;

namespace RideMateService.Comm.Logger;

/// <summary>
/// 기존 파일/콘솔 로그를 그대로 남기면서 같은 내용을 Serilog(ILogger) 로도 보낸다.
/// 나중에 Serilog 로 완전히 넘어가면 기존 파일/콘솔 쓰기 부분만 지우면 된다.
/// </summary>
public class LogService : ILogService
{
    private static readonly object _lock = new object();

    private readonly ILogger<LogService> _logger;

    public LogService(ILogger<LogService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 파일 로그
    /// </summary>
    public void LogMessage(string message)
    {
        _logger.LogInformation("{Message}", message);

        try
        {
            var now = DateTime.Now;

            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemLog", now.ToString("yyyy"),
                now.ToString("MM"));

            // 디렉터리가 없으면 생성 (이미 있으면 아무런 동작하지 않음)
            Directory.CreateDirectory(logDir);
            
            // 날짜별 파일 생성
            var logFile = Path.Combine(logDir, $"{now:yyyy-MM-dd}.txt");
            
            // 파일에 한 줄씩 Append
            lock (_lock)
            {
                using var fs = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(fs);
                
                writer.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss.fff}]\t{message}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex);
            Console.ResetColor();
        }
    }
    
    /// <summary>
    /// 콘솔 메시지
    /// </summary>
    public void ConsoleMessage(string message)
    {
        _logger.LogInformation("{Message}", message);

        lock (_lock)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
    
    /// <summary>
    /// 콘솔 에러 메시지
    /// </summary>
    public void ConsoleWarning(string message)
    {
        _logger.LogWarning("{Message}", message);

        lock (_lock)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[WARNING] {DateTime.Now}: {message}");
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}