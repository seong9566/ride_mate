using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RideMateService.Commons.DBModels;

namespace RideMateService.Commons.Repository;

public partial class RideMateContext : DbContext
{
    public RideMateContext(DbContextOptions<RideMateContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Academy> Academies { get; set; }

    public virtual DbSet<AcademyPickupSpot> AcademyPickupSpots { get; set; }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<Child> Children { get; set; }

    public virtual DbSet<Consent> Consents { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<LoginSession> LoginSessions { get; set; }

    public virtual DbSet<LoopRoute> LoopRoutes { get; set; }

    public virtual DbSet<ParkingLot> ParkingLots { get; set; }

    public virtual DbSet<PhoneVerification> PhoneVerifications { get; set; }

    public virtual DbSet<PickupEvent> PickupEvents { get; set; }

    public virtual DbSet<PickupPlan> PickupPlans { get; set; }

    public virtual DbSet<PickupSession> PickupSessions { get; set; }

    public virtual DbSet<PickupSpot> PickupSpots { get; set; }

    public virtual DbSet<PickupSpotBlock> PickupSpotBlocks { get; set; }

    public virtual DbSet<QrLogin> QrLogins { get; set; }

    public virtual DbSet<RestrictedZone> RestrictedZones { get; set; }

    public virtual DbSet<Timetable> Timetables { get; set; }

    public virtual DbSet<TimetableDay> TimetableDays { get; set; }

    public virtual DbSet<TimetableException> TimetableExceptions { get; set; }

    public virtual DbSet<UserInfo> UserInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Academy>(entity =>
        {
            entity.HasKey(e => e.AcademyId).HasName("PRIMARY");

            entity.ToTable("ACADEMY", tb => tb.HasComment("학원"));

            entity.HasIndex(e => e.AreaId, "IX_ACADEMY_01");

            entity.HasIndex(e => new { e.PlaceId, e.ActiveFlag }, "UK_ACADEMY_01").IsUnique();

            entity.HasIndex(e => new { e.SourceRef, e.ActiveFlag }, "UK_ACADEMY_02").IsUnique();

            entity.Property(e => e.AcademyId)
                .HasComment("학원 ID")
                .HasColumnName("ACADEMY_ID");
            entity.Property(e => e.AcademyName)
                .HasMaxLength(100)
                .HasComment("학원 이름")
                .HasColumnName("ACADEMY_NAME");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .HasComment("학원 주소")
                .HasColumnName("ADDRESS");
            entity.Property(e => e.AreaId)
                .HasComment("학원가 ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.Lat)
                .HasPrecision(10, 7)
                .HasComment("학원 위도")
                .HasColumnName("LAT");
            entity.Property(e => e.Lng)
                .HasPrecision(10, 7)
                .HasComment("학원 경도")
                .HasColumnName("LNG");
            entity.Property(e => e.OpenStatus)
                .HasMaxLength(20)
                .HasComment("등록 상태 (OPEN, CLOSED)")
                .HasColumnName("OPEN_STATUS");
            entity.Property(e => e.PlaceId)
                .HasMaxLength(100)
                .HasComment("장소 검색 API 장소 ID")
                .HasColumnName("PLACE_ID");
            entity.Property(e => e.SourceRef)
                .HasMaxLength(100)
                .HasComment("원천 데이터 학원 관리번호")
                .HasColumnName("SOURCE_REF");
            entity.Property(e => e.SourceType)
                .HasMaxLength(20)
                .HasComment("데이터 출처 유형 (TEAM, NEIS, PLACE)")
                .HasColumnName("SOURCE_TYPE");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.Area).WithMany(p => p.Academies)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACADEMY_01");
        });

        modelBuilder.Entity<AcademyPickupSpot>(entity =>
        {
            entity.HasKey(e => e.AcademyPickupSpotId).HasName("PRIMARY");

            entity.ToTable("ACADEMY_PICKUP_SPOT", tb => tb.HasComment("학원별 픽업 지점 후보"));

            entity.HasIndex(e => new { e.AcademyId, e.Priority }, "IX_ACADEMY_PICKUP_SPOT_01");

            entity.HasIndex(e => e.PickupSpotId, "IX_ACADEMY_PICKUP_SPOT_02");

            entity.HasIndex(e => new { e.AcademyId, e.PickupSpotId, e.ActiveFlag }, "UK_ACADEMY_PICKUP_SPOT_01").IsUnique();

            entity.Property(e => e.AcademyPickupSpotId)
                .HasComment("학원별 픽업 지점 후보 ID")
                .HasColumnName("ACADEMY_PICKUP_SPOT_ID");
            entity.Property(e => e.AcademyId)
                .HasComment("학원 ID")
                .HasColumnName("ACADEMY_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.PickupSpotId)
                .HasComment("픽업 지점 ID")
                .HasColumnName("PICKUP_SPOT_ID");
            entity.Property(e => e.Priority)
                .HasComment("배정 우선순위")
                .HasColumnName("PRIORITY");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.WalkMin)
                .HasComment("학원에서 지점까지 도보 시간(분)")
                .HasColumnName("WALK_MIN");

            entity.HasOne(d => d.Academy).WithMany(p => p.AcademyPickupSpots)
                .HasForeignKey(d => d.AcademyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACADEMY_PICKUP_SPOT_01");

            entity.HasOne(d => d.PickupSpot).WithMany(p => p.AcademyPickupSpots)
                .HasForeignKey(d => d.PickupSpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACADEMY_PICKUP_SPOT_02");
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId).HasName("PRIMARY");

            entity.ToTable("AREA", tb => tb.HasComment("학원가(서비스 지역)"));

            entity.Property(e => e.AreaId)
                .HasComment("학원가(서비스 지역) ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.AreaName)
                .HasMaxLength(50)
                .HasComment("학원가 이름")
                .HasColumnName("AREA_NAME");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
        });

        modelBuilder.Entity<Child>(entity =>
        {
            entity.HasKey(e => e.ChildId).HasName("PRIMARY");

            entity.ToTable("CHILD", tb => tb.HasComment("아이"));

            entity.HasIndex(e => e.UserId, "IX_CHILD_01");

            entity.Property(e => e.ChildId)
                .HasComment("아이 ID")
                .HasColumnName("CHILD_ID");
            entity.Property(e => e.ChildName)
                .HasMaxLength(50)
                .HasComment("아이 이름")
                .HasColumnName("CHILD_NAME");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.PhoneEnc)
                .HasMaxLength(255)
                .HasComment("암호화한 휴대폰 번호")
                .HasColumnName("PHONE_ENC");
            entity.Property(e => e.SendChannel)
                .HasMaxLength(20)
                .HasComment("발송 채널 (SMS, ALIMTALK)")
                .HasColumnName("SEND_CHANNEL");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserId)
                .HasComment("운전자 계정 ID")
                .HasColumnName("USER_ID");

            entity.HasOne(d => d.User).WithMany(p => p.Children)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CHILD_01");
        });

        modelBuilder.Entity<Consent>(entity =>
        {
            entity.HasKey(e => e.ConsentId).HasName("PRIMARY");

            entity.ToTable("CONSENT", tb => tb.HasComment("동의 기록"));

            entity.HasIndex(e => new { e.UserId, e.ConsentType, e.ConsentId }, "IX_CONSENT_01");

            entity.HasIndex(e => e.PhoneVerificationId, "IX_CONSENT_02");

            entity.Property(e => e.ConsentId)
                .HasComment("동의 기록 ID")
                .HasColumnName("CONSENT_ID");
            entity.Property(e => e.AgreeYn)
                .HasComment("동의 여부")
                .HasColumnName("AGREE_YN");
            entity.Property(e => e.ConsentDt)
                .HasComment("동의 일시")
                .HasColumnType("datetime")
                .HasColumnName("CONSENT_DT");
            entity.Property(e => e.ConsentType)
                .HasMaxLength(20)
                .HasComment("동의 종류 (GUARDIAN, CHILD_INFO, ARRIVAL_LOG)")
                .HasColumnName("CONSENT_TYPE");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.PhoneVerificationId)
                .HasComment("휴대폰 인증번호 ID")
                .HasColumnName("PHONE_VERIFICATION_ID");
            entity.Property(e => e.TermsVersion)
                .HasMaxLength(20)
                .HasComment("약관 및 고지 버전")
                .HasColumnName("TERMS_VERSION");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserId)
                .HasComment("운전자 계정 ID")
                .HasColumnName("USER_ID");
            entity.Property(e => e.VerifyMethod)
                .HasMaxLength(20)
                .HasComment("확인 방법 (PHONE_OTP, CHECK)")
                .HasColumnName("VERIFY_METHOD");

            entity.HasOne(d => d.PhoneVerification).WithMany(p => p.Consents)
                .HasForeignKey(d => d.PhoneVerificationId)
                .HasConstraintName("FK_CONSENT_02");

            entity.HasOne(d => d.User).WithMany(p => p.Consents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONSENT_01");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.HolidayId).HasName("PRIMARY");

            entity.ToTable("HOLIDAY", tb => tb.HasComment("공휴일"));

            entity.HasIndex(e => new { e.HolidayDate, e.ActiveFlag }, "UK_HOLIDAY_01").IsUnique();

            entity.Property(e => e.HolidayId)
                .HasComment("공휴일 ID")
                .HasColumnName("HOLIDAY_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.HolidayDate)
                .HasComment("공휴일 날짜")
                .HasColumnType("date")
                .HasColumnName("HOLIDAY_DATE");
            entity.Property(e => e.HolidayName)
                .HasMaxLength(50)
                .HasComment("공휴일 이름")
                .HasColumnName("HOLIDAY_NAME");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
        });

        modelBuilder.Entity<LoginSession>(entity =>
        {
            entity.HasKey(e => e.LoginSessionId).HasName("PRIMARY");

            entity.ToTable("LOGIN_SESSION", tb => tb.HasComment("로그인 세션"));

            entity.HasIndex(e => new { e.UserId, e.DeviceId }, "IX_LOGIN_SESSION_01");

            entity.HasIndex(e => e.PrevTokenHash, "IX_LOGIN_SESSION_02");

            entity.HasIndex(e => e.TokenHash, "UK_LOGIN_SESSION_01").IsUnique();

            entity.Property(e => e.LoginSessionId)
                .HasComment("차량 로그인 ID")
                .HasColumnName("LOGIN_SESSION_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.DeviceId)
                .HasMaxLength(100)
                .HasComment("차량 앱 설치 번호")
                .HasColumnName("DEVICE_ID");
            entity.Property(e => e.ExpireDt)
                .HasComment("토큰 만료 일시")
                .HasColumnType("datetime")
                .HasColumnName("EXPIRE_DT");
            entity.Property(e => e.LastUsedDt)
                .HasComment("마지막 사용 일시")
                .HasColumnType("datetime")
                .HasColumnName("LAST_USED_DT");
            entity.Property(e => e.PrevTokenHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("교체 직전 로그인 토큰 HMAC")
                .HasColumnName("PREV_TOKEN_HASH");
            entity.Property(e => e.RotateDt)
                .HasComment("토큰 교체 일시")
                .HasColumnType("datetime")
                .HasColumnName("ROTATE_DT");
            entity.Property(e => e.SessionType)
                .HasMaxLength(20)
                .HasComment("세션 종류 (CAR, WEB)")
                .HasColumnName("SESSION_TYPE");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("로그인 토큰 HMAC")
                .HasColumnName("TOKEN_HASH");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserId)
                .HasComment("운전자 계정 ID")
                .HasColumnName("USER_ID");

            entity.HasOne(d => d.User).WithMany(p => p.LoginSessions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LOGIN_SESSION_01");
        });

        modelBuilder.Entity<LoopRoute>(entity =>
        {
            entity.HasKey(e => e.LoopRouteId).HasName("PRIMARY");

            entity.ToTable("LOOP_ROUTE", tb => tb.HasComment("한 바퀴 경로"));

            entity.HasIndex(e => e.PickupSpotId, "IX_LOOP_ROUTE_01");

            entity.Property(e => e.LoopRouteId)
                .HasComment("한 바퀴 경로 ID")
                .HasColumnName("LOOP_ROUTE_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.DistanceM)
                .HasComment("경로 거리(m)")
                .HasColumnName("DISTANCE_M");
            entity.Property(e => e.DurationSec)
                .HasComment("한 바퀴 소요 시간(초)")
                .HasColumnName("DURATION_SEC");
            entity.Property(e => e.PickupSpotId)
                .HasComment("픽업 지점 ID")
                .HasColumnName("PICKUP_SPOT_ID");
            entity.Property(e => e.RightTurnCnt)
                .HasComment("우회전 수")
                .HasColumnName("RIGHT_TURN_CNT");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.PickupSpot).WithMany(p => p.LoopRoutes)
                .HasForeignKey(d => d.PickupSpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LOOP_ROUTE_01");
        });

        modelBuilder.Entity<ParkingLot>(entity =>
        {
            entity.HasKey(e => e.ParkingLotId).HasName("PRIMARY");

            entity.ToTable("PARKING_LOT", tb => tb.HasComment("대기 장소(주차장)"));

            entity.HasIndex(e => e.AreaId, "IX_PARKING_LOT_01");

            entity.HasIndex(e => new { e.SourceRef, e.ActiveFlag }, "UK_PARKING_LOT_01").IsUnique();

            entity.Property(e => e.ParkingLotId)
                .HasComment("대기 장소(주차장) ID")
                .HasColumnName("PARKING_LOT_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.AddFee)
                .HasComment("추가 요금(원)")
                .HasColumnName("ADD_FEE");
            entity.Property(e => e.AddUnitMin)
                .HasComment("추가 단위 시간(분)")
                .HasColumnName("ADD_UNIT_MIN");
            entity.Property(e => e.Address)
                .HasMaxLength(200)
                .HasComment("주차장 주소")
                .HasColumnName("ADDRESS");
            entity.Property(e => e.AreaId)
                .HasComment("추천 학원가 ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.AvailableSpaces)
                .HasComment("마지막 조회 빈자리 수")
                .HasColumnName("AVAILABLE_SPACES");
            entity.Property(e => e.AvailableUpdateDt)
                .HasComment("빈자리 마지막 조회 일시")
                .HasColumnType("datetime")
                .HasColumnName("AVAILABLE_UPDATE_DT");
            entity.Property(e => e.BaseFee)
                .HasComment("기본 요금(원)")
                .HasColumnName("BASE_FEE");
            entity.Property(e => e.BaseMin)
                .HasComment("기본 시간(분)")
                .HasColumnName("BASE_MIN");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DayPassFee)
                .HasComment("1일 주차권 요금(원)")
                .HasColumnName("DAY_PASS_FEE");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.HolidayCloseTime)
                .HasComment("일요일·공휴일 운영 종료 시각")
                .HasColumnType("time")
                .HasColumnName("HOLIDAY_CLOSE_TIME");
            entity.Property(e => e.HolidayOpenTime)
                .HasComment("일요일·공휴일 운영 시작 시각")
                .HasColumnType("time")
                .HasColumnName("HOLIDAY_OPEN_TIME");
            entity.Property(e => e.Lat)
                .HasPrecision(10, 7)
                .HasComment("주차장 위도")
                .HasColumnName("LAT");
            entity.Property(e => e.Lng)
                .HasPrecision(10, 7)
                .HasComment("주차장 경도")
                .HasColumnName("LNG");
            entity.Property(e => e.LotName)
                .HasMaxLength(100)
                .HasComment("주차장 이름")
                .HasColumnName("LOT_NAME");
            entity.Property(e => e.RealtimeRef)
                .HasMaxLength(100)
                .HasComment("실시간 주차대수 API 주차장 코드")
                .HasColumnName("REALTIME_REF");
            entity.Property(e => e.SatCloseTime)
                .HasComment("토요일 운영 종료 시각")
                .HasColumnType("time")
                .HasColumnName("SAT_CLOSE_TIME");
            entity.Property(e => e.SatOpenTime)
                .HasComment("토요일 운영 시작 시각")
                .HasColumnType("time")
                .HasColumnName("SAT_OPEN_TIME");
            entity.Property(e => e.SourceRef)
                .HasMaxLength(100)
                .HasComment("표준데이터 주차장 관리번호")
                .HasColumnName("SOURCE_REF");
            entity.Property(e => e.TotalSpaces)
                .HasComment("전체 주차 면수")
                .HasColumnName("TOTAL_SPACES");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.WeekdayCloseTime)
                .HasComment("평일 운영 종료 시각")
                .HasColumnType("time")
                .HasColumnName("WEEKDAY_CLOSE_TIME");
            entity.Property(e => e.WeekdayOpenTime)
                .HasComment("평일 운영 시작 시각")
                .HasColumnType("time")
                .HasColumnName("WEEKDAY_OPEN_TIME");

            entity.HasOne(d => d.Area).WithMany(p => p.ParkingLots)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PARKING_LOT_01");
        });

        modelBuilder.Entity<PhoneVerification>(entity =>
        {
            entity.HasKey(e => e.PhoneVerificationId).HasName("PRIMARY");

            entity.ToTable("PHONE_VERIFICATION", tb => tb.HasComment("휴대폰 인증번호"));

            entity.HasIndex(e => new { e.PhoneHash, e.Purpose, e.PhoneVerificationId }, "IX_PHONE_VERIFICATION_01");

            entity.Property(e => e.PhoneVerificationId)
                .HasComment("휴대폰 인증번호 ID")
                .HasColumnName("PHONE_VERIFICATION_ID");
            entity.Property(e => e.CodeHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("인증번호 HMAC")
                .HasColumnName("CODE_HASH");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.ExpireDt)
                .HasComment("인증번호 만료 일시")
                .HasColumnType("datetime")
                .HasColumnName("EXPIRE_DT");
            entity.Property(e => e.FailCnt)
                .HasComment("인증 실패 횟수")
                .HasColumnName("FAIL_CNT");
            entity.Property(e => e.LockUntilDt)
                .HasComment("잠금 해제 일시")
                .HasColumnType("datetime")
                .HasColumnName("LOCK_UNTIL_DT");
            entity.Property(e => e.PhoneHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("수신 번호 HMAC")
                .HasColumnName("PHONE_HASH");
            entity.Property(e => e.Purpose)
                .HasMaxLength(20)
                .HasComment("발송 용도 (LOGIN, GUARDIAN)")
                .HasColumnName("PURPOSE");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.VerifyDt)
                .HasComment("인증 성공 일시")
                .HasColumnType("datetime")
                .HasColumnName("VERIFY_DT");
        });

        modelBuilder.Entity<PickupEvent>(entity =>
        {
            entity.HasKey(e => e.PickupEventId).HasName("PRIMARY");

            entity.ToTable("PICKUP_EVENT", tb => tb.HasComment("픽업 중 일어난 일"));

            entity.HasIndex(e => new { e.PickupPlanId, e.ClientEventId }, "UK_PICKUP_EVENT_01").IsUnique();

            entity.Property(e => e.PickupEventId)
                .HasComment("픽업 이벤트 ID")
                .HasColumnName("PICKUP_EVENT_ID");
            entity.Property(e => e.ClientEventId)
                .HasComment("차량 이벤트 UUID")
                .HasColumnName("CLIENT_EVENT_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.Detail)
                .HasComment("이벤트 유형별 부가 값")
                .HasColumnType("json")
                .HasColumnName("DETAIL");
            entity.Property(e => e.EventDt)
                .HasComment("이벤트 발생 일시")
                .HasColumnType("datetime")
                .HasColumnName("EVENT_DT");
            entity.Property(e => e.EventType)
                .HasMaxLength(20)
                .HasComment("이벤트 유형 (PLANNED, STARTED, WAIT_STARTED, END_TIME_CHANGED, REASSIGNED, LINK_SENT, LINK_OPENED, CHILD_LEAVING, STOPPED, WRONG_STOP, REMIND_SENT, LOOP_STARTED, BOARDED, EXPIRED, CANCELED)")
                .HasColumnName("EVENT_TYPE");
            entity.Property(e => e.PickupPlanId)
                .HasComment("오늘 계획 ID")
                .HasColumnName("PICKUP_PLAN_ID");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.PickupPlan).WithMany(p => p.PickupEvents)
                .HasForeignKey(d => d.PickupPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PICKUP_EVENT_01");
        });

        modelBuilder.Entity<PickupPlan>(entity =>
        {
            entity.HasKey(e => e.PickupPlanId).HasName("PRIMARY");

            entity.ToTable("PICKUP_PLAN", tb => tb.HasComment("오늘 계획"));

            entity.HasIndex(e => new { e.PickupSpotId, e.SlotDt }, "IX_PICKUP_PLAN_01");

            entity.HasIndex(e => new { e.UserId, e.PickupDate }, "IX_PICKUP_PLAN_02");

            entity.HasIndex(e => e.ChildId, "IX_PICKUP_PLAN_03");

            entity.HasIndex(e => e.AcademyId, "IX_PICKUP_PLAN_04");

            entity.HasIndex(e => e.AreaId, "IX_PICKUP_PLAN_05");

            entity.HasIndex(e => e.ParkingLotId, "IX_PICKUP_PLAN_06");

            entity.HasIndex(e => new { e.TimetableDayId, e.PickupDate }, "UK_PICKUP_PLAN_01").IsUnique();

            entity.Property(e => e.PickupPlanId)
                .HasComment("오늘 계획 ID")
                .HasColumnName("PICKUP_PLAN_ID");
            entity.Property(e => e.AcademyId)
                .HasComment("학원 ID")
                .HasColumnName("ACADEMY_ID");
            entity.Property(e => e.AreaId)
                .HasComment("학원가 ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.BoardDt)
                .HasComment("탑승 감지 일시")
                .HasColumnType("datetime")
                .HasColumnName("BOARD_DT");
            entity.Property(e => e.ChildId)
                .HasComment("아이 ID")
                .HasColumnName("CHILD_ID");
            entity.Property(e => e.CloseDt)
                .HasComment("계획 종료 일시")
                .HasColumnType("datetime")
                .HasColumnName("CLOSE_DT");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.EndTime)
                .HasComment("적용된 하원 시각")
                .HasColumnType("time")
                .HasColumnName("END_TIME");
            entity.Property(e => e.ExpireDt)
                .HasComment("계획 만료 일시")
                .HasColumnType("datetime")
                .HasColumnName("EXPIRE_DT");
            entity.Property(e => e.ParkingLotId)
                .HasComment("대기 장소 ID")
                .HasColumnName("PARKING_LOT_ID");
            entity.Property(e => e.PickupDate)
                .HasComment("픽업 날짜")
                .HasColumnType("date")
                .HasColumnName("PICKUP_DATE");
            entity.Property(e => e.PickupSpotId)
                .HasComment("배정 픽업 지점 ID")
                .HasColumnName("PICKUP_SPOT_ID");
            entity.Property(e => e.SlotDt)
                .HasComment("배정 도착 일시")
                .HasColumnType("datetime")
                .HasColumnName("SLOT_DT");
            entity.Property(e => e.StartDt)
                .HasComment("경로 시작 일시")
                .HasColumnType("datetime")
                .HasColumnName("START_DT");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasComment("계획 상태 (PLANNED, STARTED, WAITING, STOPPED, LOOPING, BOARDED, EXPIRED, CANCELED)")
                .HasColumnName("STATUS");
            entity.Property(e => e.StopDt)
                .HasComment("픽업 지점 최초 정차 일시")
                .HasColumnType("datetime")
                .HasColumnName("STOP_DT");
            entity.Property(e => e.TimetableDayId)
                .HasComment("시간표 요일 ID")
                .HasColumnName("TIMETABLE_DAY_ID");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserId)
                .HasComment("운전자 계정 ID")
                .HasColumnName("USER_ID");
            entity.Property(e => e.WaitStartDt)
                .HasComment("대기 장소 도착 일시")
                .HasColumnType("datetime")
                .HasColumnName("WAIT_START_DT");
            entity.Property(e => e.WalkMin)
                .HasComment("아이 도보 시간(분)")
                .HasColumnName("WALK_MIN");

            entity.HasOne(d => d.Academy).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.AcademyId)
                .HasConstraintName("FK_PICKUP_PLAN_04");

            entity.HasOne(d => d.Area).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PICKUP_PLAN_05");

            entity.HasOne(d => d.Child).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.ChildId)
                .HasConstraintName("FK_PICKUP_PLAN_02");

            entity.HasOne(d => d.ParkingLot).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.ParkingLotId)
                .HasConstraintName("FK_PICKUP_PLAN_07");

            entity.HasOne(d => d.PickupSpot).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.PickupSpotId)
                .HasConstraintName("FK_PICKUP_PLAN_06");

            entity.HasOne(d => d.TimetableDay).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.TimetableDayId)
                .HasConstraintName("FK_PICKUP_PLAN_03");

            entity.HasOne(d => d.User).WithMany(p => p.PickupPlans)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_PICKUP_PLAN_01");
        });

        modelBuilder.Entity<PickupSession>(entity =>
        {
            entity.HasKey(e => e.PickupSessionId).HasName("PRIMARY");

            entity.ToTable("PICKUP_SESSION", tb => tb.HasComment("픽업 세션(아이에게 보낸 링크)"));

            entity.HasIndex(e => e.PickupPlanId, "UK_PICKUP_SESSION_01").IsUnique();

            entity.HasIndex(e => e.TokenHash, "UK_PICKUP_SESSION_02").IsUnique();

            entity.Property(e => e.PickupSessionId)
                .HasComment("픽업 세션 ID")
                .HasColumnName("PICKUP_SESSION_ID");
            entity.Property(e => e.CloseDt)
                .HasComment("링크 종료 일시")
                .HasColumnType("datetime")
                .HasColumnName("CLOSE_DT");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.PickupPlanId)
                .HasComment("오늘 계획 ID")
                .HasColumnName("PICKUP_PLAN_ID");
            entity.Property(e => e.SendChannel)
                .HasMaxLength(20)
                .HasComment("발송 채널 (SMS, ALIMTALK)")
                .HasColumnName("SEND_CHANNEL");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("링크 토큰 HMAC")
                .HasColumnName("TOKEN_HASH");
            entity.Property(e => e.TokenNonce)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasComment("토큰 생성 난수")
                .HasColumnName("TOKEN_NONCE");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.PickupPlan).WithOne(p => p.PickupSession)
                .HasForeignKey<PickupSession>(d => d.PickupPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PICKUP_SESSION_01");
        });

        modelBuilder.Entity<PickupSpot>(entity =>
        {
            entity.HasKey(e => e.PickupSpotId).HasName("PRIMARY");

            entity.ToTable("PICKUP_SPOT", tb => tb.HasComment("픽업 지점"));

            entity.HasIndex(e => e.AreaId, "IX_PICKUP_SPOT_01");

            entity.Property(e => e.PickupSpotId)
                .HasComment("픽업 지점 ID")
                .HasColumnName("PICKUP_SPOT_ID");
            entity.Property(e => e.AreaId)
                .HasComment("학원가 ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.Capacity)
                .HasComment("동시 수용 차량 수")
                .HasColumnName("CAPACITY");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.GuideText)
                .HasMaxLength(200)
                .HasComment("픽업 지점 찾는 법")
                .HasColumnName("GUIDE_TEXT");
            entity.Property(e => e.Lat)
                .HasPrecision(10, 7)
                .HasComment("픽업 지점 위도")
                .HasColumnName("LAT");
            entity.Property(e => e.Lng)
                .HasPrecision(10, 7)
                .HasComment("픽업 지점 경도")
                .HasColumnName("LNG");
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .HasComment("현장 사진 주소")
                .HasColumnName("PHOTO_URL");
            entity.Property(e => e.SpotName)
                .HasMaxLength(100)
                .HasComment("픽업 지점 이름")
                .HasColumnName("SPOT_NAME");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UseYn)
                .HasComment("배정 사용 여부")
                .HasColumnName("USE_YN");

            entity.HasOne(d => d.Area).WithMany(p => p.PickupSpots)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PICKUP_SPOT_01");
        });

        modelBuilder.Entity<PickupSpotBlock>(entity =>
        {
            entity.HasKey(e => e.PickupSpotBlockId).HasName("PRIMARY");

            entity.ToTable("PICKUP_SPOT_BLOCK", tb => tb.HasComment("픽업 지점을 쓸 수 없는 시간"));

            entity.HasIndex(e => new { e.PickupSpotId, e.DayType }, "IX_PICKUP_SPOT_BLOCK_01");

            entity.Property(e => e.PickupSpotBlockId)
                .HasComment("픽업 지점 금지 시간 ID")
                .HasColumnName("PICKUP_SPOT_BLOCK_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DayType)
                .HasMaxLength(20)
                .HasComment("적용 요일 유형 (WEEKDAY, SAT, HOLIDAY)")
                .HasColumnName("DAY_TYPE");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.EndTime)
                .HasComment("금지 종료 시각")
                .HasColumnType("time")
                .HasColumnName("END_TIME");
            entity.Property(e => e.PickupSpotId)
                .HasComment("픽업 지점 ID")
                .HasColumnName("PICKUP_SPOT_ID");
            entity.Property(e => e.Reason)
                .HasMaxLength(100)
                .HasComment("금지 사유")
                .HasColumnName("REASON");
            entity.Property(e => e.SourceRef)
                .HasMaxLength(100)
                .HasComment("원천 데이터 관리번호")
                .HasColumnName("SOURCE_REF");
            entity.Property(e => e.StartTime)
                .HasComment("금지 시작 시각")
                .HasColumnType("time")
                .HasColumnName("START_TIME");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.PickupSpot).WithMany(p => p.PickupSpotBlocks)
                .HasForeignKey(d => d.PickupSpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PICKUP_SPOT_BLOCK_01");
        });

        modelBuilder.Entity<QrLogin>(entity =>
        {
            entity.HasKey(e => e.QrLoginId).HasName("PRIMARY");

            entity.ToTable("QR_LOGIN", tb => tb.HasComment("QR 로그인 요청"));

            entity.HasIndex(e => e.UserId, "IX_QR_LOGIN_01");

            entity.HasIndex(e => e.DeviceCodeHash, "UK_QR_LOGIN_01").IsUnique();

            entity.HasIndex(e => e.UserCodeHash, "UK_QR_LOGIN_02").IsUnique();

            entity.Property(e => e.QrLoginId)
                .HasComment("QR 로그인 요청 ID")
                .HasColumnName("QR_LOGIN_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.DeviceCodeHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("차량이 보관하는 device_code HMAC")
                .HasColumnName("DEVICE_CODE_HASH");
            entity.Property(e => e.DeviceId)
                .HasMaxLength(100)
                .HasComment("차량 앱 설치 번호")
                .HasColumnName("DEVICE_ID");
            entity.Property(e => e.ExpireDt)
                .HasComment("QR 로그인 만료 일시")
                .HasColumnType("datetime")
                .HasColumnName("EXPIRE_DT");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasComment("QR 로그인 상태 (PENDING, APPROVED, USED)")
                .HasColumnName("STATUS");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserCodeHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("QR에 담긴 user_code HMAC")
                .HasColumnName("USER_CODE_HASH");
            entity.Property(e => e.UserId)
                .HasComment("인증한 운전자 계정 ID")
                .HasColumnName("USER_ID");

            entity.HasOne(d => d.User).WithMany(p => p.QrLogins)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_QR_LOGIN_01");
        });

        modelBuilder.Entity<RestrictedZone>(entity =>
        {
            entity.HasKey(e => e.RestrictedZoneId).HasName("PRIMARY");

            entity.ToTable("RESTRICTED_ZONE", tb => tb.HasComment("제외·단속 구역"));

            entity.HasIndex(e => e.AreaId, "IX_RESTRICTED_ZONE_01");

            entity.HasIndex(e => e.Geom, "IX_RESTRICTED_ZONE_02");

            entity.HasIndex(e => new { e.SourceName, e.SourceRef, e.ActiveFlag }, "UK_RESTRICTED_ZONE_01").IsUnique();

            entity.Property(e => e.RestrictedZoneId)
                .HasComment("제외·단속 구역 ID")
                .HasColumnName("RESTRICTED_ZONE_ID");
            entity.Property(e => e.ActiveEndTime)
                .HasComment("적용 종료 시각")
                .HasColumnType("time")
                .HasColumnName("ACTIVE_END_TIME");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.ActiveStartTime)
                .HasComment("적용 시작 시각")
                .HasColumnType("time")
                .HasColumnName("ACTIVE_START_TIME");
            entity.Property(e => e.AreaId)
                .HasComment("학원가 ID")
                .HasColumnName("AREA_ID");
            entity.Property(e => e.BufferM)
                .HasComment("제외 거리(m)")
                .HasColumnName("BUFFER_M");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DayType)
                .HasMaxLength(20)
                .HasComment("적용 요일 유형 (WEEKDAY, SAT, HOLIDAY)")
                .HasColumnName("DAY_TYPE");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.Geom)
                .HasComment("제외·단속 구역 점·선·면")
                .HasColumnName("GEOM");
            entity.Property(e => e.SourceName)
                .HasMaxLength(50)
                .HasComment("원천 데이터 이름")
                .HasColumnName("SOURCE_NAME");
            entity.Property(e => e.SourceRef)
                .HasMaxLength(100)
                .HasComment("원천 데이터 관리번호")
                .HasColumnName("SOURCE_REF");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.ZoneType)
                .HasMaxLength(20)
                .HasComment("구역 유형 (SCHOOL_ZONE, NO_PARKING, INTERSECTION, CROSSWALK, BUS_STOP, FIRE_HYDRANT, YELLOW_DOUBLE, YELLOW_SINGLE, CCTV, ACCIDENT_SPOT, RESIDENT_PARKING)")
                .HasColumnName("ZONE_TYPE");

            entity.HasOne(d => d.Area).WithMany(p => p.RestrictedZones)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RESTRICTED_ZONE_01");
        });

        modelBuilder.Entity<Timetable>(entity =>
        {
            entity.HasKey(e => e.TimetableId).HasName("PRIMARY");

            entity.ToTable("TIMETABLE", tb => tb.HasComment("시간표"));

            entity.HasIndex(e => e.ChildId, "IX_TIMETABLE_01");

            entity.HasIndex(e => e.AcademyId, "IX_TIMETABLE_02");

            entity.Property(e => e.TimetableId)
                .HasComment("시간표 ID")
                .HasColumnName("TIMETABLE_ID");
            entity.Property(e => e.AcademyId)
                .HasComment("학원 ID")
                .HasColumnName("ACADEMY_ID");
            entity.Property(e => e.ChildId)
                .HasComment("아이 ID")
                .HasColumnName("CHILD_ID");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(20)
                .HasComment("부모가 지정한 학원 이름")
                .HasColumnName("DISPLAY_NAME");
            entity.Property(e => e.EndTime)
                .HasComment("하원 시각")
                .HasColumnType("time")
                .HasColumnName("END_TIME");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.Academy).WithMany(p => p.Timetables)
                .HasForeignKey(d => d.AcademyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIMETABLE_02");

            entity.HasOne(d => d.Child).WithMany(p => p.Timetables)
                .HasForeignKey(d => d.ChildId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIMETABLE_01");
        });

        modelBuilder.Entity<TimetableDay>(entity =>
        {
            entity.HasKey(e => e.TimetableDayId).HasName("PRIMARY");

            entity.ToTable("TIMETABLE_DAY", tb => tb.HasComment("시간표의 요일"));

            entity.HasIndex(e => new { e.TimetableId, e.DayOfWeek, e.ActiveFlag }, "UK_TIMETABLE_DAY_01").IsUnique();

            entity.Property(e => e.TimetableDayId)
                .HasComment("시간표 요일 ID")
                .HasColumnName("TIMETABLE_DAY_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DayOfWeek)
                .HasComment("요일 (1=월, 2=화, 3=수, 4=목, 5=금, 6=토, 7=일)")
                .HasColumnName("DAY_OF_WEEK");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.LastPickupDate)
                .HasComment("마지막 완료 픽업 날짜")
                .HasColumnType("date")
                .HasColumnName("LAST_PICKUP_DATE");
            entity.Property(e => e.PickupYn)
                .HasComment("요일별 픽업 여부")
                .HasColumnName("PICKUP_YN");
            entity.Property(e => e.TimetableId)
                .HasComment("시간표 ID")
                .HasColumnName("TIMETABLE_ID");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.Timetable).WithMany(p => p.TimetableDays)
                .HasForeignKey(d => d.TimetableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIMETABLE_DAY_01");
        });

        modelBuilder.Entity<TimetableException>(entity =>
        {
            entity.HasKey(e => e.TimetableExceptionId).HasName("PRIMARY");

            entity.ToTable("TIMETABLE_EXCEPTION", tb => tb.HasComment("날짜별 예외"));

            entity.HasIndex(e => new { e.TimetableDayId, e.TargetDate, e.ActiveFlag }, "UK_TIMETABLE_EXCEPTION_01").IsUnique();

            entity.Property(e => e.TimetableExceptionId)
                .HasComment("날짜별 예외 ID")
                .HasColumnName("TIMETABLE_EXCEPTION_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.ChangedEndTime)
                .HasComment("변경된 하원 시각")
                .HasColumnType("time")
                .HasColumnName("CHANGED_END_TIME");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.ExceptionType)
                .HasMaxLength(20)
                .HasComment("예외 유형 (SKIP, TIME_CHANGE)")
                .HasColumnName("EXCEPTION_TYPE");
            entity.Property(e => e.TargetDate)
                .HasComment("예외 적용 날짜")
                .HasColumnType("date")
                .HasColumnName("TARGET_DATE");
            entity.Property(e => e.TimetableDayId)
                .HasComment("시간표 요일 ID")
                .HasColumnName("TIMETABLE_DAY_ID");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");

            entity.HasOne(d => d.TimetableDay).WithMany(p => p.TimetableExceptions)
                .HasForeignKey(d => d.TimetableDayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIMETABLE_EXCEPTION_01");
        });

        modelBuilder.Entity<UserInfo>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("USER_INFO", tb => tb.HasComment("운전자 계정"));

            entity.HasIndex(e => new { e.PhoneHash, e.ActiveFlag }, "UK_USER_INFO_01").IsUnique();

            entity.Property(e => e.UserId)
                .HasComment("운전자 계정 ID")
                .HasColumnName("USER_ID");
            entity.Property(e => e.ActiveFlag)
                .HasComment("살아 있는 행 표시 (UNIQUE용)")
                .HasColumnName("ACTIVE_FLAG");
            entity.Property(e => e.ArrivalLogYn)
                .HasComment("차량 도착 기록 사용 여부")
                .HasColumnName("ARRIVAL_LOG_YN");
            entity.Property(e => e.AutoSendYn)
                .HasComment("링크 자동 발송 여부")
                .HasColumnName("AUTO_SEND_YN");
            entity.Property(e => e.CallName)
                .HasMaxLength(20)
                .HasComment("아이에게 보이는 보호자 호칭")
                .HasColumnName("CALL_NAME");
            entity.Property(e => e.CarColor)
                .HasMaxLength(20)
                .HasComment("차 색")
                .HasColumnName("CAR_COLOR");
            entity.Property(e => e.CarModel)
                .HasMaxLength(50)
                .HasComment("차종")
                .HasColumnName("CAR_MODEL");
            entity.Property(e => e.CarNoLast4)
                .HasMaxLength(4)
                .IsFixedLength()
                .HasComment("차량 번호 뒷자리")
                .HasColumnName("CAR_NO_LAST4");
            entity.Property(e => e.CreateDt)
                .HasComment("생성 일시")
                .HasColumnType("datetime")
                .HasColumnName("CREATE_DT");
            entity.Property(e => e.CreateUser)
                .HasMaxLength(50)
                .HasComment("생성자")
                .HasColumnName("CREATE_USER");
            entity.Property(e => e.DeleteYn)
                .HasComment("삭제 여부")
                .HasColumnName("DELETE_YN");
            entity.Property(e => e.DestAddress)
                .HasMaxLength(200)
                .HasComment("픽업 후 목적지 주소")
                .HasColumnName("DEST_ADDRESS");
            entity.Property(e => e.DestLat)
                .HasPrecision(10, 7)
                .HasComment("픽업 후 목적지 위도")
                .HasColumnName("DEST_LAT");
            entity.Property(e => e.DestLng)
                .HasPrecision(10, 7)
                .HasComment("픽업 후 목적지 경도")
                .HasColumnName("DEST_LNG");
            entity.Property(e => e.PhoneEnc)
                .HasMaxLength(255)
                .HasComment("암호화한 휴대폰 번호")
                .HasColumnName("PHONE_ENC");
            entity.Property(e => e.PhoneHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasComment("휴대폰 번호 HMAC")
                .HasColumnName("PHONE_HASH");
            entity.Property(e => e.UpdateDt)
                .HasComment("수정 일시")
                .HasColumnType("datetime")
                .HasColumnName("UPDATE_DT");
            entity.Property(e => e.UpdateUser)
                .HasMaxLength(50)
                .HasComment("수정자")
                .HasColumnName("UPDATE_USER");
            entity.Property(e => e.UserName)
                .HasMaxLength(50)
                .HasComment("운전자 실명")
                .HasColumnName("USER_NAME");
            entity.Property(e => e.WakeBeforeMin)
                .HasComment("출발 전 깨우는 시간(분)")
                .HasColumnName("WAKE_BEFORE_MIN");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
