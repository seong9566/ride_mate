package com.example.pickup_hyundai.notify

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.graphics.BitmapFactory
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.example.pickup_hyundai.MainActivity
import com.example.pickup_hyundai.R
import com.example.pickup_hyundai.core.time.KstFormat
import com.example.pickup_hyundai.data.local.SetupStore
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime

/**
 * V01 시동 알림 — 디자인: "오늘의 픽업 일정이 도착했어요 / 수학학원 22:00 하원 · 21:29에 출발하세요" + 보기/닫기.
 * Pleos 전용 알림 SDK가 확인되지 않아 Android 표준 알림(NotificationCompat, 헤드업)으로 띄운다.
 * 실제 IVI에서 지도 위에 어떤 모양으로 뜨는지는 차량/에뮬레이터에서 확인 필요.
 */
object PickupNotifier {
    const val CHANNEL_ID = "pickup_today"
    private const val NOTIFICATION_ID = 2201
    const val ACTION_DISMISS = "com.example.pickup_hyundai.action.DISMISS_PICKUP_NOTICE"

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val nm = context.getSystemService(NotificationManager::class.java)
        if (nm.getNotificationChannel(CHANNEL_ID) == null) {
            nm.createNotificationChannel(
                NotificationChannel(CHANNEL_ID, "오늘의 픽업 알림", NotificationManager.IMPORTANCE_HIGH).apply {
                    description = "시동을 걸면 오늘 픽업 일정을 알려 드려요"
                }
            )
        }
    }

    /** 저장된 시간표로 오늘 첫 픽업을 찾아 알린다. 오늘 일정이 없거나 이미 알렸으면 띄우지 않는다. */
    fun notifyTodayFromStore(context: Context, force: Boolean = false): Boolean {
        val store = SetupStore(context)
        val today = LocalDate.now(KstFormat.ZONE)
        if (!force && store.lastNotifiedDate == today) return false
        val first = store.schedules
            ?.filter { it.pickupEnabled && it.day == today.dayOfWeek }
            ?.minByOrNull { it.endTime } ?: return false
        if (LocalTime.now(KstFormat.ZONE).isAfter(first.endTime)) return false
        val shown = show(context, first.academyName, first.endTime, store.cachedDepartureFor(today))
        if (shown) store.lastNotifiedDate = today
        return shown
    }

    /** @param departure 추천 출발 시각(정본 DetailDepartureGuide). 모르면 출발 문구를 빼고 하원 시각만 보여 준다 */
    fun show(context: Context, academyName: String, endTime: LocalTime, departure: LocalDateTime?): Boolean {
        val nm = NotificationManagerCompat.from(context)
        if (!nm.areNotificationsEnabled()) return false // Android 13+: POST_NOTIFICATIONS 미허용
        ensureChannel(context)

        val open = PendingIntent.getActivity(
            context, 0,
            Intent(context, MainActivity::class.java)
                .putExtra(MainActivity.EXTRA_START_SCREEN, "TODAY_PICKUP")
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val dismiss = PendingIntent.getBroadcast(
            context, 1,
            Intent(context, NotificationActionReceiver::class.java).setAction(ACTION_DISMISS),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val body = buildString {
            append("$academyName ${KstFormat.hm(endTime)} 하원")
            departure?.let { append(" · ${KstFormat.hm(it.toLocalTime())}에 출발하세요") }
        }
        val n = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_wayme)
            .setLargeIcon(BitmapFactory.decodeResource(context.resources, R.drawable.img_wayme_logo))
            .setContentTitle("오늘의 픽업 일정이 도착했어요")
            .setContentText(body)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(open)
            .addAction(0, "보기", open)
            .addAction(0, "닫기", dismiss)
            .build()
        return runCatching { nm.notify(NOTIFICATION_ID, n); true }.getOrDefault(false) // SecurityException(권한 회수) 방지
    }

    fun cancel(context: Context) = NotificationManagerCompat.from(context).cancel(NOTIFICATION_ID)
}

/** "닫기" 버튼 */
class NotificationActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == PickupNotifier.ACTION_DISMISS) PickupNotifier.cancel(context)
    }
}

/**
 * 시동(= 차량 IVI 부팅)에 맞춰 알림. Pleos SDK에 시동 이벤트 API가 확인되지 않아 BOOT_COMPLETED를 사용한다.
 * 앱을 한 번도 실행하지 않은 상태(stopped)에서는 Android 정책상 수신되지 않는다.
 */
class IgnitionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) PickupNotifier.notifyTodayFromStore(context)
    }
}
