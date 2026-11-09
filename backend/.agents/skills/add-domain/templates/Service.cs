using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.{도메인}.Request;
using RideMateService.Comm.Dto.v1.{도메인}.Response;
using RideMateService.Comm.Logger;
using RideMateService.Commons.DBModels;
using RideMateService.Commons.Repository;
using RideMateService.Repository.v1.{도메인};

namespace RideMateService.Service.v1.{도메인};

public class {도메인}Service : I{도메인}Service
{
    private readonly I{도메인}Repository _{도메인소문자}Repository;
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RideMateContext _context; // 트랜잭션 시작·완료에만 쓴다

    public {도메인}Service(I{도메인}Repository {도메인소문자}Repository,
        ILogService logService,
        IHttpContextAccessor httpContextAccessor,
        RideMateContext context)
    {
        this._{도메인소문자}Repository = {도메인소문자}Repository;
        this._logService = logService;
        this._httpContextAccessor = httpContextAccessor;
        this._context = context;
    }

    /// <summary>
    /// {한글 설명}
    /// </summary>
    public async Task<ResponseModel<bool>> Add{도메인}Service(Add{도메인}Dto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자는 HttpContext의 클레임에서 꺼낸다
            var loginUser = _httpContextAccessor.HttpContext?.User.FindFirstValue("userName");
            if (string.IsNullOrWhiteSpace(loginUser))
                return new ResponseModel<bool> { message = "잘못된 요청입니다.", data = false, code = 401 };

            // 2) 입력 검증
            if (dto is null)
                return new ResponseModel<bool> { message = "잘못된 요청입니다.", data = false, code = 400 };

            // 3) 엔티티 구성 — 감사 컬럼은 Service가 채운다
            DateTime thisTime = DateTime.Now;
            var model = new {엔티티}
            {
                // 업무 필드 …
                CreateDt = thisTime,
                CreateUser = loginUser,
                UpdateDt = thisTime,
                UpdateUser = loginUser,
                DeleteYn = false
            };

            // 4) 저장
            if (!await _{도메인소문자}Repository.Add{도메인}Async(model, cancellationToken).ConfigureAwait(false))
                return new ResponseModel<bool> { message = "잘못된 요청입니다.", data = false, code = 400 };

            return new ResponseModel<bool> { message = "요청이 정상 처리되었습니다.", data = true, code = 200 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<bool> { message = "서버에서 요청을 처리하지 못하였습니다.", data = false, code = 500 };
        }
    }

    /// <summary>
    /// {한글 설명}
    /// </summary>
    public async Task<ResponseModel<CursorResult<Get{도메인}List>>> Get{도메인}ListService(
        int? last{도메인}Seq, int pageSize, string? searchKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인
            var loginUser = _httpContextAccessor.HttpContext?.User.FindFirstValue("userName");
            if (string.IsNullOrWhiteSpace(loginUser))
                return new ResponseModel<CursorResult<Get{도메인}List>> { message = "잘못된 요청입니다.", data = null, code = 401 };

            // 2) 입력 검증 — pageSize는 1~100이다
            if (pageSize is < 1 or > 100)
                return new ResponseModel<CursorResult<Get{도메인}List>> { message = "잘못된 요청입니다.", data = null, code = 400 };

            // 3) 조회
            var (items, hasMore) = await _{도메인소문자}Repository.Get{도메인}ListAsync(last{도메인}Seq, pageSize, searchKey, cancellationToken).ConfigureAwait(false);

            // 4) 커서 구성 — lastCursor는 반환한 마지막 항목의 PK다
            var result = new CursorResult<Get{도메인}List>
            {
                items = items,
                lastCursor = items.Count > 0 ? items[^1].{도메인소문자}Seq : null,
                hasMore = hasMore
            };

            return new ResponseModel<CursorResult<Get{도메인}List>> { message = "요청이 정상 처리되었습니다.", data = result, code = 200 };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return new ResponseModel<CursorResult<Get{도메인}List>> { message = "서버에서 요청을 처리하지 못하였습니다.", data = null, code = 500 };
        }
    }
}
