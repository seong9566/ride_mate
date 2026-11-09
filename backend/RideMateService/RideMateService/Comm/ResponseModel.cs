namespace RideMateService.Comm;

/// <summary>
/// 리스폰스 모델
/// </summary>
public class ResponseModel<T>
{
    /// <summary>
    /// 메시지
    /// </summary>
    public string? message { get; set; }

    /// <summary>
    /// 데이터
    /// </summary>
    public T? data { get; set; }

    /// <summary>
    /// 코드
    /// </summary>
    public int? code { get; set; }
}
