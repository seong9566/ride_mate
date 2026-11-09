using RideMateService.Commons.DBModels;

namespace RideMateService.Repository.v1.{도메인};

public partial class {도메인}Repository
{
    /// <summary>
    /// {한글 설명}
    /// </summary>
    public async Task<bool> Add{도메인}Async({엔티티} model, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.{엔티티복수}.AddAsync(model, cancellationToken).ConfigureAwait(false);

            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw; // Service가 500으로 바꾼다
        }
    }
}
