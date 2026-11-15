using System.Data;

namespace RideMateService.Repository.v1.User;

public interface IUserRepository
{
    /// <summary>
    /// 집 주소·좌표와 수정 감사 컬럼만 바꾼다. 좌표를 null로 주면 NULL로 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateHomeAsync(int userId, string address, decimal? lat, decimal? lng, DateTime updateDt,
        string updateUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// 차 정보 중 null이 아닌 값과 수정 감사 컬럼만 바꾼다. null인 인자는 그 칸을 바꾸지 않는다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateVehicleAsync(int userId, string? carModel, string? carColor, string? carNoLast4,
        DateTime updateDt, string updateUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// 초기 설정 완료 시각과 수정 감사 컬럼만 바꾼다. 완료 시각이 비어 있는 계정만 바꾸므로, 이미 채워져 있으면 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateSetupDoneAsync(int userId, DateTime setupDoneDt, string updateUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 자동 발송 여부와 수정 감사 컬럼만 바꾼다. 바뀐 행이 없으면 false를 돌려준다.
    /// </summary>
    Task<bool> UpdateAutoSendAsync(int userId, bool autoSendYn, DateTime updateDt, string updateUser,
        CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 같은 계정의 동시 요청을 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 집·차·초기 설정 완료 시각·자동 발송 여부를 돌려준다. 계정이 없거나 지워졌으면 null이다. 쓰기 트랜잭션 안에서 부르면 그 트랜잭션을 넘긴다.
    /// </summary>
    Task<UserSetupRow?> GetUserSetupRowAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정에 살아 있는 아이가 있는지 돌려준다.
    /// </summary>
    Task<bool> HasLiveChildAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 살아 있는 아이의 살아 있는 시간표에 픽업이 켜진 살아 있는 요일이 하나라도 있는지 돌려준다.
    /// </summary>
    Task<bool> HasPickupOnScheduleAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 집·자동 발송 여부와 살아 있는 아이 한 명(CHILD_ID가 가장 작은 아이)을 돌려준다. 계정이 없거나 지워졌으면 null이다.
    /// </summary>
    Task<UserDetailRow?> GetUserDetailRowAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
