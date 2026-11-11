using System.Data;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.Auth;

public interface IAuthRepository
{
    /// <summary>
    /// 살아 있는 QR 로그인 요청을 PK로 불러온다(수정 대상)
    /// </summary>
    Task<QrLogin?> GetQrLoginAsync(int qrLoginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// QR 로그인 요청 등록
    /// </summary>
    Task<bool> AddQrLoginAsync(QrLogin model, CancellationToken cancellationToken = default);

    /// <summary>
    /// QR 로그인 요청 수정
    /// </summary>
    Task<bool> UpdateQrLoginAsync(QrLogin model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 휴대폰 인증번호를 PK로 불러온다(수정 대상)
    /// </summary>
    Task<PhoneVerification?> GetPhoneVerificationAsync(int phoneVerificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 인증번호 등록
    /// </summary>
    Task<bool> AddPhoneVerificationAsync(PhoneVerification model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 휴대폰 인증번호 수정
    /// </summary>
    Task<bool> UpdatePhoneVerificationAsync(PhoneVerification model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 차량 로그인 세션을 PK로 불러온다(수정 대상)
    /// </summary>
    Task<LoginSession?> GetLoginSessionAsync(int loginSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인 세션 수정
    /// </summary>
    Task<bool> UpdateLoginSessionAsync(LoginSession model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인 세션을 PK로 끝낸다(DELETE_YN = 1). PK로만 잠가 빈 범위에 갭 락을 남기지 않는다.
    /// </summary>
    Task EndLoginSessionsAsync(IReadOnlyCollection<int> loginSessionIds, DateTime thisTime, string updateUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 로그인 세션 등록. 저장 뒤 model의 로그인 세션 ID가 채워진다.
    /// </summary>
    Task<bool> AddLoginSessionAsync(LoginSession model, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// device_code 해시로 살아 있는 QR 로그인 요청을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockQrLoginByDeviceCodeHashAsync(string hash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 계정을 PK로 FOR UPDATE 잠그고 ID를 돌려준다. 차량 세션 생성 경로를 계정 단위로 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 전화번호 해시로 살아 있는 가장 최근 휴대폰 인증번호를 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockLatestPhoneVerificationAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 전화번호 해시와 용도로 살아 있는 가장 최근 휴대폰 인증번호를 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockLatestPhoneVerificationByPurposeAsync(string phoneHash, string purpose,
        IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 같은 계정·기기의 살아 있는 차량 로그인 세션 ID를 잠그지 않고 조회한다.
    /// 호출 전에 USER_INFO 행을 PK로 FOR UPDATE 잠가 차량 세션 생성 경로를 직렬화해야 한다.
    /// </summary>
    Task<IReadOnlyList<int>> GetCarSessionIdsAsync(int userId, string deviceId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 전화번호 해시가 같은 살아 있는 계정의 ID를 조회한다.
    /// </summary>
    Task<int?> GetUserIdByPhoneHashAsync(string phoneHash, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// refresh 토큰 해시로 살아 있는 차량 로그인 세션을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// 지금 값이거나, 허용 시간(graceFrom 이후) 안에 바뀐 직전 값이면 찾는다.
    /// 인덱스를 하나씩 타도록 지금 값(TOKEN_HASH)을 먼저 찾고, 없을 때만 직전 값(PREV_TOKEN_HASH)을 찾는다
    /// </summary>
    Task<int?> LockLoginSessionByRefreshAsync(string tokenHash, DateTime graceFrom, DateTime thisTime,
        IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 계정의 초기 설정이 필요한지 조회한다. 운전자 이름이 비었거나 살아 있는 아이가 없으면 true, 계정이 없거나 삭제됐으면 null
    /// </summary>
    Task<bool?> GetSetupRequiredAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
