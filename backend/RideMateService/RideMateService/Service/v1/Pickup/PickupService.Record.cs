using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage;
using RideMateService.Comm;
using RideMateService.Comm.Dto.v1.Pickup.Request;
using RideMateService.Comm.Dto.v1.Pickup.Response;
using RideMateService.Commons.DBModels;
using RideMateService.Repository.v1.Pickup;

namespace RideMateService.Service.v1.Pickup;

public partial class PickupService
{
    private const int RecordNameMaxLength = 100;
    private const int RecordArriveFutureLimitMin = 1;
    private const int RecordArrivePastLimitHours = 24;

    /// <summary>
    /// 픽업 완료 기록 저장. 시간표 요일의 살아 있는 기록이 있으면 그 행을 덮어쓰고 없으면 새로 넣는다.
    /// 끝난 픽업 계획은 읽지도 잇지도 않는다(ADR-0027)
    /// </summary>
    public async Task<ResponseModel<PickupRecordDto>> AddPickupRecordService(AddPickupRecordDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 인증 확인 — 로그인 사용자(USER_ID)는 HttpContext의 클레임 sub에서 꺼낸다
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return RecordResult(401, "잘못된 요청입니다.", null);

            // 2) 입력 검증 — 모델 검증을 거치지 않은 호출도 필수값·범위는 거절하고, 관계 검증은 여기서 한다
            var departName = dto?.departName?.Trim();
            var boardName = dto?.boardName?.Trim();
            if (dto is not
                {
                    scheduleSeq: >= 1, dayOfWeek: >= 1 and <= 7, arriveDt: { } arriveDt, totalMin: >= 0 and <= 1440,
                    waitMin: >= 0 and <= 1440, departLat: { } departLat, departLng: { } departLng,
                    departDt: { } departDt, boardLat: { } boardLat, boardLng: { } boardLng, boardDt: { } boardDt,
                    autoSave: { } autoSave
                }
                || string.IsNullOrEmpty(departName) || departName.Length > RecordNameMaxLength
                || string.IsNullOrEmpty(boardName) || boardName.Length > RecordNameMaxLength
                || departLat is < -90m or > 90m || boardLat is < -90m or > 90m
                || departLng is < -180m or > 180m || boardLng is < -180m or > 180m)
                return RecordResult(400, "잘못된 요청입니다.", null);

            var thisTime = NowToSecond();
            if (dto.waitMin > dto.totalMin
                || departDt > boardDt || boardDt > arriveDt
                || arriveDt > thisTime.AddMinutes(RecordArriveFutureLimitMin)
                || arriveDt < thisTime.AddHours(-RecordArrivePastLimitHours))
                return RecordResult(400, "잘못된 요청입니다.", null);

            // 3) 트랜잭션 — 예외로 빠져나가면 await using이 트랜잭션을 롤백한다
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            var dbTransaction = transaction.GetDbTransaction();

            // 3-1) 첫 문장으로 계정을 잠가 시간표 삭제·편집과 같은 순서(USER_INFO → TIMETABLE_DAY)를 지킨다
            if (await _pickupRepository.LockRecordUserAsync(userId, dbTransaction, cancellationToken)
                    .ConfigureAwait(false) is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return RecordResult(401, "잘못된 요청입니다.", null);
            }

            // 3-2) 그 시간표 요일 행을 잠근다. 같은 요일의 동시 저장이 직렬화되어 살아 있는 행 UNIQUE에 걸리지 않는다
            var timetableDayId = await _pickupRepository.LockRecordTimetableDayAsync(userId, dto.scheduleSeq.Value,
                (byte)dto.dayOfWeek.Value, dbTransaction, cancellationToken).ConfigureAwait(false);
            if (timetableDayId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return RecordResult(404, "잘못된 요청입니다.", null);
            }

            // 3-3) 살아 있는 기록이 있으면 덮어쓰고 없으면 새로 넣는다
            var entity = await _pickupRepository.GetPickupRecordEntityAsync(timetableDayId.Value, cancellationToken)
                .ConfigureAwait(false);
            var isNew = entity is null;
            entity ??= new PickupRecord
            {
                UserId = userId,
                TimetableDayId = timetableDayId.Value,
                CreateDt = thisTime,
                CreateUser = AuditUser,
                DeleteYn = false
            };
            entity.ArriveDt = arriveDt;
            entity.TotalMin = dto.totalMin.Value;
            entity.WaitMin = dto.waitMin.Value;
            entity.DepartName = departName;
            entity.DepartLat = departLat;
            entity.DepartLng = departLng;
            entity.DepartDt = departDt;
            entity.BoardName = boardName;
            entity.BoardLat = boardLat;
            entity.BoardLng = boardLng;
            entity.BoardDt = boardDt;
            entity.AutoSaveYn = autoSave;
            entity.UpdateDt = thisTime;
            entity.UpdateUser = AuditUser;

            var saved = isNew
                ? await _pickupRepository.AddPickupRecordAsync(entity, cancellationToken).ConfigureAwait(false)
                : await _pickupRepository.UpdatePickupRecordAsync(entity, cancellationToken).ConfigureAwait(false);
            if (!saved)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return RecordResult(500, "서버에서 요청을 처리하지 못하였습니다.", null);
            }

            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            // 4) 응답 — 저장된 값을 다시 읽어 조회 API와 같은 모양으로 돌려준다
            var row = await _pickupRepository.GetPickupRecordRowAsync(timetableDayId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
                return RecordResult(500, "서버에서 요청을 처리하지 못하였습니다.", null);

            return RecordResult(200, "요청이 정상 처리되었습니다.", ToPickupRecordDto(dto.scheduleSeq.Value,
                dto.dayOfWeek.Value, row));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다. 장소는 로그에 남기지 않는다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return RecordResult(500, "서버에서 요청을 처리하지 못하였습니다.", null);
        }
    }

    /// <summary>
    /// 픽업 완료 기록 조회. 시간표 요일이 없으면 404, 기록이 없으면 code 101
    /// </summary>
    public async Task<ResponseModel<PickupRecordDto>> DetailPickupRecordService(DetailPickupRecordQueryDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("sub"), out var userId))
                return RecordResult(401, "잘못된 요청입니다.", null);

            if (dto is not { scheduleSeq: >= 1, dayOfWeek: >= 1 and <= 7 })
                return RecordResult(400, "잘못된 요청입니다.", null);

            var timetableDayId = await _pickupRepository.GetRecordTimetableDayIdAsync(userId, dto.scheduleSeq.Value,
                (byte)dto.dayOfWeek.Value, cancellationToken).ConfigureAwait(false);
            if (timetableDayId is null)
                return RecordResult(404, "잘못된 요청입니다.", null);

            var row = await _pickupRepository.GetPickupRecordRowAsync(timetableDayId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
                return RecordResult(101, "요청이 정상 처리되었습니다.", null);

            return RecordResult(200, "요청이 정상 처리되었습니다.", ToPickupRecordDto(dto.scheduleSeq.Value,
                dto.dayOfWeek.Value, row));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) // 요청 취소는 그대로 올려보낸다
        {
            _logService.LogMessage(ex.ToString());
#if DEBUG
            _logService.ConsoleWarning(ex.ToString());
#endif
            return RecordResult(500, "서버에서 요청을 처리하지 못하였습니다.", null);
        }
    }

    private static PickupRecordDto ToPickupRecordDto(int scheduleSeq, int dayOfWeek, PickupRecordRow row)
    {
        return new PickupRecordDto
        {
            scheduleSeq = scheduleSeq,
            dayOfWeek = dayOfWeek,
            arriveDt = row.ArriveDt,
            totalMin = row.TotalMin,
            waitMin = row.WaitMin,
            depart = new PickupRecordPlaceDto
            {
                name = row.DepartName, lat = row.DepartLat, lng = row.DepartLng, dt = row.DepartDt
            },
            board = new PickupRecordPlaceDto
            {
                name = row.BoardName, lat = row.BoardLat, lng = row.BoardLng, dt = row.BoardDt
            },
            autoSave = row.AutoSaveYn,
            updateDt = row.UpdateDt
        };
    }

    private static ResponseModel<PickupRecordDto> RecordResult(int code, string message, PickupRecordDto? data)
    {
        return new ResponseModel<PickupRecordDto> { message = message, data = data, code = code };
    }
}
