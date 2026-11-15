package com.example.pickup_hyundai.data.repository

import com.example.pickup_hyundai.domain.model.HomeSummary

/**
 * 홈(V02) 데이터 소스.
 *
 * 정본(RideMate_Integrated_API_and_Flow.md)에는 오늘/주간/다음 일정 조회 API가 없다(08_USER_FLOW §5 P0).
 * 따라서 현재는 [MockHomeRepository]를 사용하고, API가 확정되면 Remote 구현으로 교체한다.
 * 추천 출발 시각만은 정본 API `GET /api/v1/Pickup/M/sign/DetailDepartureGuide`(departureDt)로 교체 예정.
 */
interface HomeRepository {
    suspend fun getHomeSummary(): Result<HomeSummary>
}
