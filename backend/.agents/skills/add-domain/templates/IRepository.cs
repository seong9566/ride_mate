using RideMateService.Comm.Dto.v1.{도메인}.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.{도메인};

public interface I{도메인}Repository
{
    /// <summary>
    /// {한글 설명}
    /// </summary>
    Task<bool> Add{도메인}Async({엔티티} model, CancellationToken cancellationToken = default);

    // ====== Dapper

    /// <summary>
    /// {한글 설명}
    /// </summary>
    Task<(List<Get{도메인}List> items, bool hasMore)> Get{도메인}ListAsync(
        int? lastSeq, int pageSize, string? searchKey,
        CancellationToken cancellationToken = default);
}
