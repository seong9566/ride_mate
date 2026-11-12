namespace RideMateService.Comm.Dto.v1.Pickup.Response;

/// <summary>차량 픽업 링크 전달 상태 실시간 연결 판정 결과(서비스 내부용)</summary>
/// <param name="PickupPlanId">오늘 계획 ID</param>
/// <param name="UserId">로그인한 사용자 ID</param>
/// <param name="TokenExpiresAt">접근 토큰(JWT) 만료 시각(서버 로컬 시각). 이 시각에 서버가 연결을 닫는다</param>
public sealed record PickupLinkStreamTicket(int PickupPlanId, int UserId, DateTime TokenExpiresAt);
