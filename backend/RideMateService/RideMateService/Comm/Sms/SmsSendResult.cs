namespace RideMateService.Comm.Sms;

/// <summary>
/// 문자 발송 결과. 사업자 오류와 타임아웃도 예외가 아니라 Success = false로 돌려준다.
/// </summary>
/// <param name="Success">사업자가 발송을 접수했는지 여부</param>
/// <param name="ResultCode">사업자의 상태·오류 코드. 응답 본문은 담지 않는다. 개발용은 DEV, 타임아웃은 TIMEOUT, 그 밖의 예외는 ERROR</param>
public sealed record SmsSendResult(bool Success, string ResultCode);
