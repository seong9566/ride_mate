package com.example.pickup_hyundai.domain.model

import java.time.DayOfWeek
import java.time.LocalTime

/** 이 차에 로그인한 계정(QR 로그인 화면 목록). 백엔드 GetLoginSessionList는 정본 외 → Mock */
data class LoginAccount(
    val id: String,
    /** 표시 이름(엄마/아빠) */
    val label: String,
    val maskedName: String,
    val recentlyUsed: Boolean,
)

/**
 * 휴대폰 확인 대기(디자인 "휴대폰에서 확인하고 있어요"): 차와 휴대폰에 같은 4자리 숫자를 보여 주고 휴대폰에서 '로그인 허용'.
 * 정본의 DetailQrLogin(matchCode·expiresIn)은 휴대폰 웹 쪽 API이고, 차량이 읽는 API는 정본에 없음 → Mock
 */
data class LoginConfirm(val matchCode: String, val expiresInSec: Int)

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
