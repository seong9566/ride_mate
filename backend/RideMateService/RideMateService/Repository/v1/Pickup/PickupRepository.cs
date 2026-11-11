using System.Data;
using RideMateService.Comm.Logger;

namespace RideMateService.Repository.v1.Pickup;

public partial class PickupRepository : IPickupRepository
{
    private readonly IDbConnection _dapper;    // Dapper
    private readonly ILogService _logService;

    public PickupRepository(IDbConnection dapper, ILogService logService)
    {
        this._dapper = dapper;
        this._logService = logService;
    }
}
