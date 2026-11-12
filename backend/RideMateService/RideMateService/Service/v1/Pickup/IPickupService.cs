using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;

namespace RideMateService.Service.v1.Pickup;

public interface IPickupService
{
    /// <summary>
    /// 출발 안내 계산
    /// </summary>
    Task<ResponseModel<DetailDepartureGuideDto>> DetailDepartureGuideService(DepartureGuideQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 대기 장소 추천 목록
    /// </summary>
    Task<ResponseModel<WaitingPlaceListDto>> GetWaitingPlaceListService(WaitingPlaceQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가 단속 구역 목록
    /// </summary>
    Task<ResponseModel<GetRestrictedZoneList>> GetRestrictedZoneListService(RestrictedZoneListQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 링크 문자 발송(최초 발송과 실패 후 다시 보내기)
    /// </summary>
    Task<ResponseModel<PickupLinkSendDto>> AddPickupLinkService(AddPickupLinkDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정차 후 "도착했어요" 다시 알림 문자 발송
    /// </summary>
    Task<ResponseModel<PickupLinkSendDto>> AddPickupRemindService(AddPickupRemindDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 링크 전달 상태 조회(링크·다시 알림 발송, 열람, 지금 나가요)
    /// </summary>
    Task<ResponseModel<DetailPickupLinkStatusDto>> DetailPickupLinkStatusService(PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이가 지금 출발하거나 출발 예정 시각을 설정·변경한다
    /// </summary>
    Task<ResponseModel<ChildLeaveDto>> AddChildLeaveService(string? token, AddChildLeaveDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이의 출발 예정 시각을 취소한다
    /// </summary>
    Task<ResponseModel<ChildLeaveDto>> DeleteChildLeaveService(string? token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 장소 바꾸기(임의 좌표로 바꾸거나 기본 지점으로 되돌리기). 같은 clientEventId를 다시 보내면 한 번만 반영한다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanLocationService(UpdatePickupPlanLocationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 경로 시작. 오늘 계획이 없으면 만들면서 시작한다. 늦게 출발했으면 같은 장소에서 배정 시각을 차 도착 예상 시각으로 미룬다. 이미 시작했으면 지금 계획을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStartService(UpdatePickupPlanStartDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정차 기록. 배정 장소와의 거리와 정차 위치의 단속 구역을 판정해 돌려주고 계획을 STOPPED로 바꾼다.
    /// 같은 clientEventId를 다시 보내면(계획이 끝났어도) 한 번만 반영하고 200을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanStopService(UpdatePickupPlanStopDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 탑승 기록. 진행 중인 모든 상태에서 받아 BOARD_DT를 쓰고 계획을 BOARDED로 끝낸다(익명화, LAST_PICKUP_DATE 반영, 링크 종료).
    /// 응답은 계획 ID와 상태만 채운다. 같은 clientEventId를 다시 보내면(계획이 끝났어도) 한 번만 반영하고 200을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanBoardService(UpdatePickupPlanBoardDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 부모 취소. 진행 중인 모든 상태에서 받아 계획을 CANCELED로 끝낸다(익명화, 링크 종료, LAST_PICKUP_DATE는 그대로라 그날 다시 만들 수 있다).
    /// 아이가 링크를 받았을 수 있으면 커밋한 뒤 취소 문자를 보내고 결과를 cancelNotice(NONE·SENT·FAILED)로 돌려준다.
    /// 같은 clientEventId를 다시 보내면(계획이 끝났어도) 한 번만 반영하고 200, cancelNotice NONE을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> DeletePickupPlanService(DeletePickupPlanDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 대기 장소 도착. PLANNED·STARTED·WAITING 계획을 WAITING으로 바꾸고 계획과 같은 학원가의 주차장을 기록한다.
    /// 이미 WAITING이면 주차장만 바꾸고 처음 도착 시각(WAIT_START_DT)은 그대로 둔다. 같은 clientEventId를 다시 보내면(계획이 끝났어도) 한 번만 반영하고 200을 돌려준다
    /// </summary>
    Task<ResponseModel<PickupPlanDto>> UpdatePickupPlanWaitService(UpdatePickupPlanWaitDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 만료 주기 작업 한 회차. EXPIRE_DT가 지난 진행 중 계획을 계획마다 따로 트랜잭션으로 EXPIRED로 끝내고 익명화한다.
    /// 끝낸 계획 수를 돌려준다. 한 계획의 오류는 로그만 남기고 다음 계획으로 간다. HTTP 요청 밖(주기 작업)에서 부른다
    /// </summary>
    Task<int> ExpirePickupPlansService(CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이 픽업 안내 조회
    /// </summary>
    Task<ResponseModel<ChildPickupDto>> DetailChildPickupService(string? token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이 픽업 링크 열람을 한 번만 기록하고 첫 열람 시각을 돌려준다
    /// </summary>
    Task<ResponseModel<ChildPickupOpenDto>> AddPickupLinkOpenService(string? token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 차 위치·내비 남은 시간을 보고한다. 위치는 DB에 쓰지 않고 서버 메모리에만 둔다
    /// </summary>
    Task<ResponseModel<PickupPlanPositionDto>> UpdatePickupPlanPositionService(UpdatePickupPlanPositionDto dto,
        CancellationToken cancellationToken = default);
    /// <summary>아이 픽업 실시간 연결을 판정한다</summary>
    Task<ResponseModel<ChildPickupStreamTicket>> OpenChildPickupStreamService(string? token,
        CancellationToken cancellationToken = default);

    /// <summary>아이 픽업 실시간 이벤트를 읽는다. 요청 취소 외 오류는 기록하고 연결을 끝낸다</summary>
    IAsyncEnumerable<SseEvent> ReadChildPickupStreamAsync(ChildPickupStreamTicket ticket,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 차량 픽업 링크 전달 상태 실시간 연결을 판정한다. 401·400·404는 DetailPickupLinkStatus와 같다
    /// </summary>
    Task<ResponseModel<PickupLinkStreamTicket>> OpenPickupLinkStreamService(PickupLinkStatusQueryDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 차량 픽업 링크 전달 상태 실시간 이벤트를 읽는다. Service가 ResponseModel을 돌려준다는 규칙의 예외다(service.md)
    /// </summary>
    IAsyncEnumerable<SseEvent> ReadPickupLinkStreamAsync(PickupLinkStreamTicket ticket,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽업 장소 추가 요청. 계정·아이·시간표를 가리키는 값은 저장하지 않는다
    /// </summary>
    Task<ResponseModel<AddPickupSpotRequestResultDto>> AddPickupSpotRequestService(AddPickupSpotRequestDto dto,
        CancellationToken cancellationToken = default);
}
