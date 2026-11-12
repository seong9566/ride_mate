using System.Data;
using RideMateService.Comm.Dto.v1.Auth.Response;
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
    /// 살아 있는 운전자 계정을 PK로 불러온다.
    /// </summary>
    Task<UserInfo?> GetUserInfoAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 운전자 계정 등록. 저장 뒤 model의 운전자 계정 ID가 채워진다.
    /// </summary>
    Task<bool> AddUserInfoAsync(UserInfo model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 운전자 계정의 도착 기록 사용 여부만 바꾼다. 다른 컬럼은 건드리지 않고, 바뀐 행이 정확히 1개일 때 true를 돌려준다.
    /// </summary>
    Task<bool> UpdateUserArrivalLogAsync(int userId, bool arrivalLogYn, DateTime updateDt, string updateUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 동의 기록을 등록한다.
    /// </summary>
    Task<bool> AddConsentsAsync(IEnumerable<Consent> models, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// 정리 대상 휴대폰 인증번호의 번호 해시·인증번호 해시를 비우고 삭제 표시한다. ID와 함께 대상 조건을 다시 걸어, 그 사이 바뀐 행은 건드리지 않는다. 바뀐 행 수를 돌려준다.
    /// </summary>
    Task<int> CleanupPhoneVerificationsAsync(IReadOnlyCollection<int> phoneVerificationIds, DateTime verifiedBefore,
        DateTime expiredBefore, DateTime thisTime, string updateUser, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 토큰 해시로 만료되지 않은 WEB 세션과 살아 있는 계정을 조회한다.
    /// </summary>
    Task<(int loginSessionId, int userId)?> GetWebSessionAsync(string tokenHash, DateTime thisTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// device_code 해시로 살아 있는 QR 로그인 요청을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    Task<(int qrLoginId, int? userId)?> LockQrLoginByDeviceCodeHashAsync(string hash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// user_code 해시로 살아 있는 QR 로그인 요청을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockQrLoginByUserCodeHashAsync(string userCodeHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 계정을 PK로 FOR UPDATE 잠그고 ID를 돌려준다. 차량 세션 생성 경로를 계정 단위로 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 전화번호 해시로 살아 있는 계정을 FOR UPDATE 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockUserInfoByPhoneHashAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 WEB 세션을 PK로 FOR UPDATE 잠그고 ID를 돌려준다.
    /// </summary>
    Task<int?> LockWebLoginSessionAsync(int loginSessionId, IDbTransaction transaction,
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
    /// 같은 계정·기기의 살아 있는 차량 로그인 세션 ID를 FOR UPDATE 잠금 읽기로 조회한다.
    /// 호출 전에 USER_INFO 행을 PK로 잠가 차량 세션 생성 경로를 직렬화하며, 잠금 읽기라 최신 커밋을 본다.
    /// </summary>
    Task<IReadOnlyList<int>> GetCarSessionIdsAsync(int userId, string deviceId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// refresh 토큰 해시로 살아 있는 차량 로그인 세션을 찾아 FOR UPDATE로 잠그고 ID를 돌려준다.
    /// 지금 값이거나, 허용 시간(graceFrom 이후) 안에 바뀐 직전 값이면 찾는다.
    /// 인덱스를 하나씩 타도록 지금 값(TOKEN_HASH)을 먼저 찾고, 없을 때만 직전 값(PREV_TOKEN_HASH)을 찾는다
    /// </summary>
    Task<int?> LockLoginSessionByRefreshAsync(string tokenHash, DateTime graceFrom, DateTime thisTime,
        IDbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 계정의 초기 설정이 필요한지 조회한다. 운전자 이름이 비었거나 초기 설정 완료 시각이 없으면 true, 계정이 없거나 삭제됐으면 null
    /// </summary>
    Task<bool?> GetSetupRequiredAsync(int userId, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 기기의 살아 있는 차량 로그인 세션을 계정 이름과 함께 최근 사용 순으로 조회한다(삭제된 계정·만료·로그아웃 세션 제외, 최대 limit개)
    /// </summary>
    Task<List<GetLoginSessionList>> GetLoginSessionListAsync(string deviceId, DateTime thisTime, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정리 대상 휴대폰 인증번호 ID를 afterId보다 큰 것부터 ID 순으로 limit개까지 조회한다(인증 성공이 verifiedBefore 이전이거나, 인증 없이 EXPIRE_DT가 expiredBefore 이전인 살아 있는 행)
    /// </summary>
    Task<IReadOnlyList<int>> GetCleanupPhoneVerificationIdsAsync(DateTime verifiedBefore, DateTime expiredBefore,
        int afterId, int limit, CancellationToken cancellationToken = default);
}
