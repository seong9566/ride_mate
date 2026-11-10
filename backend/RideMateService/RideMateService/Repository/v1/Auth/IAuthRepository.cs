using System.Data;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Auth;

public interface IAuthRepository
{
    /// <summary>
    /// 살아 있는 차량 로그인 세션을 PK로 불러온다(수정 대상)
    /// </summary>
    Task<LoginSession?> GetLoginSessionAsync(int loginSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인 세션 수정
    /// </summary>
    Task<bool> UpdateLoginSessionAsync(LoginSession model, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// refresh 토큰 해시로 살아 있는 차량 로그인 세션을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// 지금 값이거나, 허용 시간(graceFrom 이후) 안에 바뀐 직전 값이면 찾는다
    /// </summary>
    Task<int?> LockLoginSessionByRefreshAsync(string tokenHash, DateTime graceFrom, DateTime thisTime,
        IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 계정의 초기 설정이 필요한지 조회한다. 실명이 비었거나 살아 있는 아이가 없으면 true, 계정이 없거나 삭제됐으면 null
    /// </summary>
    Task<bool?> GetSetupRequiredAsync(int userId, CancellationToken cancellationToken = default);
}
