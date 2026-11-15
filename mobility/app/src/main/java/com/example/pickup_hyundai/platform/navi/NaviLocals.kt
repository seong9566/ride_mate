package com.example.pickup_hyundai.platform.navi

import androidx.compose.runtime.staticCompositionLocalOf

/** 화면에서 경로 요청 등 명령을 보낼 때 사용(WayMeRoot가 제공). null = 미제공(미리보기·테스트) */
val LocalNaviController = staticCompositionLocalOf<NaviController?> { null }
