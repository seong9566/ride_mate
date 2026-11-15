package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.AssignedPlan
import com.example.pickup_hyundai.domain.model.DeparturePlan
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.example.pickup_hyundai.domain.model.PickupSpotChoice
import com.example.pickup_hyundai.domain.model.WaitingPlaceChoice
import com.example.pickup_hyundai.domain.model.TodayPickupDetail

/**
 * V03·V04 데이터 소스.
 * - 정본 API로 대체 가능한 값: 학원명·하원시각·추천 출발·도착·차량 소요(DetailDepartureGuide), 추천 장소 1곳(pickup)
 * - 정본에 없는 값(Mock 유지): 픽업 후보 3곳·태그·사유, 학원 근처 → 집 소요, 집 도착 예정
 */
interface PickupRepository {
    suspend fun getTodayPickup(): Result<TodayPickupDetail>
    suspend fun getPickupSpotChoice(): Result<PickupSpotChoice>
    /** V05. 정본 DetailDepartureGuide로 대부분 대체 가능 */
    suspend fun getDeparturePlan(): Result<DeparturePlan>
    /** V06. 정본 API 없음 → Mock */
    suspend fun getWaitingPlaces(): Result<WaitingPlaceChoice>

    /**
     * 오늘 픽업 계획 확정. 정본 AddPickupPlan(멱등) — V04 확정 시, 그리고 추천 출발 30분 전에 호출한다.
     * @param pickup 부모가 고른 좌표(null이면 기본 지점 배정, 본문 {}). 반환 null = 오늘 남은 픽업 없음(code 101)
     */
    suspend fun ensurePickupPlan(pickup: GeoPoint?): Result<AssignedPlan?>
}
