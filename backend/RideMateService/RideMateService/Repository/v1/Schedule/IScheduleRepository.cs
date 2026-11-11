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

    // ====== Dapper

    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 같은 계정의 동시 요청을 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 살아 있는 아이 ID를 돌려준다. 없으면 null이다.
    /// </summary>
    Task<int?> GetLiveChildIdAsync(int userId, IDbTransaction transaction,
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
}
