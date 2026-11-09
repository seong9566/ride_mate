using System.Data;
using RideMateService.Comm.Logger;
using RideMateService.Commons.Repository;

namespace RideMateService.Repository.v1.{도메인};

public partial class {도메인}Repository : I{도메인}Repository
{
    private readonly RideMateContext _context; // EF Core
    private readonly IDbConnection _dapper;    // Dapper
    private readonly ILogService _logService;

    public {도메인}Repository(RideMateContext context, IDbConnection dapper, ILogService logService)
    {
        this._context = context;
        this._dapper = dapper;
        this._logService = logService;
    }
}
