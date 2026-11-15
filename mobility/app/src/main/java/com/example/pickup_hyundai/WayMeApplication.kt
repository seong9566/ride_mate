package com.example.pickup_hyundai

import android.app.Application
import android.content.pm.PackageManager
import android.util.Base64
import android.util.Log
import com.kakao.vectormap.KakaoMapSdk
import java.security.MessageDigest

/** 카카오맵 SDK 초기화(공식 문서 권장: Application에서 KakaoMapSdk.init) */
class WayMeApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        val key = BuildConfig.KAKAO_NATIVE_APP_KEY
        if (key.isNotBlank()) KakaoMapSdk.init(this, key)
        else Log.w(TAG, "KAKAO_NATIVE_APP_KEY 미설정 → 지도 자리표시로 표시")

        if (BuildConfig.DEBUG) {
            // 지도 인증(401/403) 점검용: 카카오 디벨로퍼스 [플랫폼 > Android]에 등록할 값
            Log.i(TAG, "패키지명=$packageName, 키 해시=${signingKeyHashes().joinToString()}, 앱키 앞4자리=${key.take(4)}")
        }
    }

    /** 서명 인증서 SHA-1의 Base64(카카오 키 해시 형식) */
    private fun signingKeyHashes(): List<String> = runCatching {
        val info = packageManager.getPackageInfo(packageName, PackageManager.GET_SIGNING_CERTIFICATES)
        val signers = info.signingInfo?.apkContentsSigners ?: emptyArray()
        signers.map { sig ->
            val md = MessageDigest.getInstance("SHA")
            md.update(sig.toByteArray())
            Base64.encodeToString(md.digest(), Base64.NO_WRAP)
        }
    }.getOrElse { listOf("확인 실패: ${it.message}") }

    private companion object { const val TAG = "WayMe/Map" }
}
