package com.example.pickup_hyundai.domain.model

import java.time.DayOfWeek
import java.time.LocalTime

/** 이 차에 로그인한 계정(M-06 GetLoginSessionList) */
data class LoginAccount(
    /** loginSessionSeq(= 토큰 응답 session_id) */
    val id: String,
    /** 표시 이름(userName). 전화번호·사용자 ID는 응답에 없음 */
    val userName: String,
    /** 이 차에서 마지막으로 쓴 계정 */
    val recentlyUsed: Boolean,
    /** 이 차에 이 계정의 refresh token이 남아 있어 바로 로그인할 수 있는지 */
    val canResume: Boolean,
)

/** 로그인 결과: true면 초기 설정(setup_required) */
data class LoginOutcome(val setupRequired: Boolean)

/** QR 한 장(M-01). qrContent = verification_uri_complete. device_code는 저장소 안에만 둔다 */
data class QrSession(val qrContent: String, val matchCode: String, val expiresInSec: Int, val intervalSec: Int)

/** QR 폴링(M-02) 결과 */
sealed interface QrPoll {
    /** 승인 전: PENDING / SCANNED / VERIFIED */
    data class Pending(val qrStatus: String) : QrPoll
    data object SlowDown : QrPoll
    data object Expired : QrPoll
    /** 취소: USER / DRIVING / PHONE */
    data class Denied(val reason: String?) : QrPoll
    data class Approved(val outcome: LoginOutcome) : QrPoll
    /** 잘못된 코드·이미 발급된 코드 등 → 새 QR */
    data object Invalid : QrPoll
}

/** 인증번호 발송(M-07) */
sealed interface OtpRequest {
    data class Sent(val expiresInSec: Int, val retryAfterSec: Int) : OtpRequest
    /** code 101: 60초 이내 재요청 */
    data class TooSoon(val retryAfterSec: Int) : OtpRequest
    /** code 102: 5회 오류로 10분 잠금 */
    data class Locked(val retryAfterSec: Int) : OtpRequest
}

/** 인증번호 확인(M-08) */
sealed interface OtpVerify {
    data class Success(val outcome: LoginOutcome) : OtpVerify
    data class Mismatch(val remainingAttempts: Int?) : OtpVerify
    data class Locked(val retryAfterSec: Long?) : OtpVerify
    data object Expired : OtpVerify
    data object NotIssued : OtpVerify
    /** 미가입 번호: 차량에서는 가입 불가 → QR 로그인 */
    data object SignupRequired : OtpVerify
}

/** 초기 설정 1/3 아이 정보 */
data class ChildInfoInput(
    val name: String = "",
    val phoneDigits: String = "",
    /** YYYYMMDD 숫자 8자리 */
    val birthDigits: String = "",
)

/** 초기 설정 2/3 요일별 학원 */
data class AcademySchedule(
    val id: String,
    val academyName: String,
    val address: String,
    val day: DayOfWeek,
    val endTime: LocalTime,
    val pickupEnabled: Boolean,
)

/** 학원 추가 입력(시간표 저장 API의 schedule 항목에 대응) */
data class AcademyInput(
    /** displayName: 부모가 지은 이름(예: 수학학원) */
    val displayName: String = "",
    /** academyName: 장소 검색 결과의 이름. 장소 검색 API 연결 전에는 직접 입력 */
    val placeName: String = "",
    /** address: 학원 주소 */
    val address: String = "",
    /** lat/lng: 장소 검색 결과 좌표(직접 입력이면 null) */
    val latitude: Double? = null,
    val longitude: Double? = null,
    /** endTime: 하원 시각 */
    val endTime: LocalTime = LocalTime.of(18, 0),
    /** days: 요일(픽업 스위치는 켜진 채로 저장) */
    val days: Set<DayOfWeek> = emptySet(),
)

/** 초기 설정 3/3 집 */
data class HomeAddress(
    val address: String,
    val latitude: Double,
    val longitude: Double,
    /** 내비 '집' 즐겨찾기에서 가져왔는지(NaviHelper getBookmarkInfo, BookmarkType.HOME) */
    val fromNaviBookmark: Boolean,
)

/** 준비 완료 요약 */
data class SetupSummary(
    val weeklyRows: List<Pair<String, String>>,
    val pickupPlaceRows: List<Pair<String, String>>,
)
