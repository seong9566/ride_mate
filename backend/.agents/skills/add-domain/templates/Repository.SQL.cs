using Dapper;
using RideMateService.Comm.Dto.v1.{도메인}.Response;

namespace RideMateService.Repository.v1.{도메인};

public partial class {도메인}Repository
{
    /// <summary>
    /// {한글 설명}
    /// </summary>
    public async Task<(List<Get{도메인}List> items, bool hasMore)> Get{도메인}ListAsync(
        int? lastSeq, int pageSize, string? searchKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cursorClause = string.Empty;
            var searchClause = string.Empty;

            var dp = new DynamicParameters();
            dp.Add("pageSize", pageSize + 1); // hasMore 판단용으로 1개 더

            if (lastSeq is > 0)
            {
                cursorClause = " AND t.{PK} < @lastSeq ";
                dp.Add("lastSeq", lastSeq.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                // LIKE 와일드카드 이스케이프. 사용자가 %나 _를 넣어도 전체 스캔되지 않게 한다
                var escaped = searchKey.Trim()
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                searchClause = @" AND (t.`NAME` LIKE @searchKey ESCAPE '\\') ";
                dp.Add("searchKey", $"%{escaped}%");
            }

            var sql = $@"
                SELECT t.{PK} AS {도메인소문자}Seq,
                       t.`NAME` AS name
                FROM {테이블} AS t
                WHERE t.DELETE_YN = FALSE
                  {cursorClause}
                  {searchClause}
                ORDER BY t.{PK} DESC
                LIMIT @pageSize";

            var rows = (await _dapper.QueryAsync<Get{도메인}List>(
                new CommandDefinition(sql, dp, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

            var hasMore = rows.Count > pageSize;
            if (hasMore) rows.RemoveAt(rows.Count - 1);

            return (rows, hasMore);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 로그 없이 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            throw;
        }
    }
}
