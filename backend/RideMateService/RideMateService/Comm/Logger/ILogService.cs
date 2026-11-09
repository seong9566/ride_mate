namespace RideMateService.Comm.Logger;

public interface ILogService
{
    /// <summary>
    /// 파일 로그
    /// </summary>
    public void LogMessage(string message);

    /// <summary>
    /// 콘솔 메시지
    /// </summary>
    public void ConsoleMessage(string message);

    /// <summary>
    /// 콘솔 에러 메시지
    /// </summary>
    public void ConsoleWarning(string message);
}
