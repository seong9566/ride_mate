package com.example.pickup_hyundai.platform.gleo

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build

/**
 * Gleo가 부르는 service 함수(조회·외부 요청)를 받는 리시버. 처리는 GleoBridge가 한다.
 * 호출자: Android 14+에서 보낸 쪽이 신원 공개를 켠 경우에만 시스템이 패키지명을 알려 준다(getSentFromPackage).
 */
class GleoActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val fn = intent.action ?: return
        if (fn !in GleoProtocol.FUNCTIONS) return
        val sender = if (Build.VERSION.SDK_INT >= 34) sentFromPackage else null
        val request = GleoRequest(fn, GleoProtocol.params(intent), GleoBridge.caller(sender))
        val pending = goAsync()
        GleoBridge.dispatch(context, request, waitForUi = false) { pending.finish() }
    }
}
