using System.Data;
using RideMateService.Comm.Dto.v1.Schedule.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Schedule;

public interface IScheduleRepository
{
    /// <summary>
    /// 학원 등록. 저장 뒤 model의 학원 ID가 채워진다.
    /// </summary>
    Task<bool> AddAcademyAsync(Academy model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 좌표가 없는 살아 있는 학원에 학원가·좌표를 채운다. 이미 좌표가 있으면 바꾸지 않고 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateAcademyLocationAsync(int academyId, int areaId, decimal lat, decimal lng, DateTime updateDt,
        string updateUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표와 그 요일 행을 한 번에 등록한다. 저장 뒤 model의 시간표 ID가 채워진다.
    /// </summary>
    Task<bool> AddTimetableAsync(Timetable model, IEnumerable<TimetableDay> days,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 요일 행의 픽업 스위치와 수정 감사 컬럼만 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateScheduleDayPickupAsync(int scheduleDaySeq, bool pickupYn, DateTime updateDt, string updateUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 시간표를 지운다. 날짜별 예외 → 요일 → 시간표 순으로 표마다 UPDATE 한 번씩 DELETE_YN을 켜고, 시간표의 DISPLAY_NAME은 빈 문자열로 바꾼다.
    /// 시간표 행이 바뀌지 않았으면 false를 돌려준다.
    /// </summary>
    Task<bool> DeleteScheduleAsync(int timetableId, IReadOnlyCollection<int> timetableDayIds, DateTime updateDt,
        string updateUser, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 살아 있는 운전자 계정 ID를 돌려준다. 없으면 null이다. 잠그지 않는 조회라 목록처럼 읽기만 하는 곳에 쓴다.
    /// </summary>
    Task<int?> GetLiveUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 같은 계정의 동시 요청을 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 살아 있는 아이 ID를 돌려준다. 없으면 null이다. 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    Task<int?> GetLiveChildIdAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 아이의 살아 있는 시간표에 속한 살아 있는 요일 한 건을 돌려준다. 없거나 남의 것이면 null이다.
    /// </summary>
    Task<ScheduleDayDto?> GetLiveScheduleDayAsync(int childId, int scheduleDaySeq, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 아이의 살아 있는 시간표 개수를 돌려준다.
    /// </summary>
    Task<int> CountLiveSchedulesAsync(int childId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 아이의 살아 있는 시간표를 (시간표, 살아 있는 요일) 한 건씩 하원 시각과 함께 돌려준다. 겹침 판정에 쓴다.
    /// </summary>
    Task<List<LiveScheduleTimeRow>> GetLiveScheduleTimesAsync(int childId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 좌표를 담는 살아 있는 학원가의 ID를 돌려준다. 학원가 밖이면 null이다.
    /// </summary>
    Task<int?> FindAreaIdAsync(decimal lat, decimal lng, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 이름·주소가 같은 살아 있는 학원을 돌려준다. 없으면 null이고, 여럿이면 ID가 작은 것이다.
    /// 학원 ID·학원가 ID·이름·주소·좌표만 채워진다.
    /// </summary>
    Task<Academy?> FindAcademyAsync(string academyName, string address, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 아이의 살아 있는 시간표를 하원 시각·등록 순으로 요일과 함께 돌려준다. timetableId를 주면 그 시간표 한 건만 돌려준다.
    /// 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    Task<List<ScheduleItemDto>> GetScheduleItemsAsync(int childId, int? timetableId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 살아 있는 아이의 살아 있는 시간표가 가리키는 학원의 학원가 ID를 돌려준다.
    /// 시간표가 없거나 남의 것이거나 학원에 좌표가 없으면 null이다. 트랜잭션 없이 부르면 잠글 학원가를 고르는 일반 조회이고,
    /// 트랜잭션을 넘기면 FOR SHARE 잠금 읽기로 최신 값을 읽고 그 아이·시간표·학원 행을 잠근다(학원가가 채워지지 못하게 한다).
    /// </summary>
    Task<int?> GetScheduleAreaIdAsync(int userId, int timetableId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 학원가 행을 FOR UPDATE로 잠근다. 같은 학원가의 계획 생성과 시간표 삭제를 직렬화한다. 트랜잭션의 첫 문장으로 부른다.
    /// </summary>
    Task LockAreaAsync(int areaId, IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 아이의 살아 있는 시간표이면 그 시간표의 요일 ID를 삭제 여부와 관계없이 모두 돌려준다. 없거나 남의 것이면 null이다.
    /// </summary>
    Task<List<int>?> GetLiveScheduleDayIdsAsync(int childId, int timetableId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 요일들을 가리키는 진행 중인 픽업 계획(PLANNED·STARTED·WAITING·STOPPED·LOOPING)의 ID를 일반 조회로 돌려준다. 잠그지 않는다.
    /// </summary>
    Task<List<int>> GetActivePickupPlanIdsAsync(IReadOnlyCollection<int> timetableDayIds, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계획들을 PK 순서로 FOR UPDATE 잠그고, 최신 값으로 아직 그 요일들을 가리키는 진행 중 계획인 것의 ID만 돌려준다.
    /// </summary>
    Task<List<int>> LockActivePickupPlanIdsAsync(IReadOnlyCollection<int> pickupPlanIds,
        IReadOnlyCollection<int> timetableDayIds, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
