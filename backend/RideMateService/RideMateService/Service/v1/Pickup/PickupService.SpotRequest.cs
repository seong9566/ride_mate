using System.Security.Claims;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const string SpotRequestStatusRequested = "REQUESTED";

    /// <summary>
    /// 픽업 장소 추가 요청. 계정·아이·시간표를 가리키는 값은 저장하지 않고 학원·학원가·위치·이름·메모만 남긴다(ADR-0008)
    /// </summary>
    public async Task<ResponseModel<AddPickupSpotRequestResultDto>> AddPickupSpotRequestService(
        AddPickupSpotRequestDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return SpotRequestResult(401, "잘못된 요청입니다.", null);

            // 좌표는 둘 다 있거나 둘 다 없어야 한다
            if (dto?.scheduleSeq is not { } scheduleSeq || (dto.lat is null) != (dto.lng is null))
                return SpotRequestResult(400, "잘못된 요청입니다.", null);

            var schedule = await _pickupRepository.GetSpotRequestScheduleAsync(userId, scheduleSeq, cancellationToken)
                .ConfigureAwait(false);
            if (schedule is null)
                return SpotRequestResult(404, "잘못된 요청입니다.", null);

            var now = DateTime.Now;
            var model = new PickupSpotRequest
            {
                AcademyId = schedule.AcademyId,
                AreaId = schedule.AreaId,
                Lat = dto.lat,
                Lng = dto.lng,
                PlaceName = NullIfBlank(dto.placeName),
                Memo = NullIfBlank(dto.memo),
                Status = SpotRequestStatusRequested,
                CreateDt = now,
                CreateUser = AuditUser,
                UpdateDt = now,
                UpdateUser = AuditUser
            };
            await _pickupRepository.AddPickupSpotRequestAsync(model, cancellationToken).ConfigureAwait(false);

            return SpotRequestResult(200, "요청이 정상 처리되었습니다.",
                new AddPickupSpotRequestResultDto { pickupSpotRequestSeq = model.PickupSpotRequestId });
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다. 좌표·메모는 로그에 남기지 않는다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return SpotRequestResult(500, "서버에서 요청을 처리하지 못하였습니다.", null);
        }
    }

    private static string? NullIfBlank(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static ResponseModel<AddPickupSpotRequestResultDto> SpotRequestResult(int code, string message,
        AddPickupSpotRequestResultDto? data)
    {
        return new ResponseModel<AddPickupSpotRequestResultDto> { message = message, data = data, code = code };
    }
}
