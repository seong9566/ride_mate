using System.Data;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.ChildInfo;

public interface IChildInfoRepository
{
    /// <summary>
    /// 살아 있는 운전자 계정을 PK로 불러온다.
    /// </summary>
    Task<UserInfo?> GetUserInfoAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 아이 등록. 저장 뒤 model의 아이 ID가 채워진다. 같은 번호의 살아 있는 아이가 있으면 MySQL 중복 키 예외(1062)를 로그 없이 올려보낸다.
    /// </summary>
    Task<bool> AddChildAsync(Child model, CancellationToken cancellationToken = default);

    /// <summary>
    /// 살아 있는 아이의 바뀐 칸만 쓴다. null인 인자는 그 칸을 그대로 둔다(phoneEnc·phoneHash는 번호를 바꿀 때 함께 준다). 같은 번호의 다른 살아 있는 아이가 있으면 MySQL 중복 키 예외(1062)를 로그 없이 올려보낸다. 바뀐 행이 없으면 false다.
    /// </summary>
    Task<bool> UpdateChildAsync(int childId, string? childName, byte[]? phoneEnc, string? phoneHash, string? sendChannel,
        DateTime? birthDate, DateTime updateDt, string updateUser, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// 살아 있는 운전자 계정을 FOR UPDATE 잠그고 ID를 돌려준다. 같은 계정의 동시 요청을 직렬화한다.
    /// </summary>
    Task<int?> LockUserInfoAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정·종류의 가장 최근 동의 행의 동의 여부를 돌려준다. 행이 없으면 null이다.
    /// </summary>
    Task<bool?> GetLatestConsentAgreeAsync(int userId, string consentType, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 그 계정의 살아 있는 아이를 돌려준다. 없으면 null이다. 아이 ID·이름·암호화한 번호·번호 해시·발송 채널만 채워진다.
    /// </summary>
    Task<Child?> GetLiveChildAsync(int userId, IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 같은 번호 해시의 살아 있는 아이가 (어느 계정에든) 있는지 돌려준다.
    /// </summary>
    Task<bool> ExistsLivePhoneHashAsync(string phoneHash, IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
