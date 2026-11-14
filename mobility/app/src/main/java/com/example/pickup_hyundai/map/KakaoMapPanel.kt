package com.example.pickup_hyundai.map

import android.graphics.Color
import android.util.Log
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.example.pickup_hyundai.R
import com.example.pickup_hyundai.domain.route.GeoPoint
import com.kakao.vectormap.KakaoMap
import com.kakao.vectormap.KakaoMapReadyCallback
import com.kakao.vectormap.LatLng
import com.kakao.vectormap.MapLifeCycleCallback
import com.kakao.vectormap.MapView
import com.kakao.vectormap.camera.CameraUpdateFactory
import com.kakao.vectormap.label.Label
import com.kakao.vectormap.label.LabelOptions
import com.kakao.vectormap.label.LabelStyle
import com.kakao.vectormap.label.LabelStyles
import com.kakao.vectormap.label.LabelTextBuilder
import com.kakao.vectormap.label.LabelTextStyle
import com.kakao.vectormap.route.RouteLineOptions
import com.kakao.vectormap.route.RouteLineSegment
import com.kakao.vectormap.route.RouteLineStyle
import com.kakao.vectormap.route.RouteLineStyles
import com.kakao.vectormap.route.RouteLineStylesSet
import com.kakao.vectormap.shape.DotPoints
import com.kakao.vectormap.shape.MapPoints
import com.kakao.vectormap.shape.PolygonOptions
import com.kakao.vectormap.shape.PolygonStyle

/**
 * 카카오맵 Android SDK v2 렌더러. 공식 문서 + SDK 2.15.2 클래스에서 시그니처를 확인한 API만 사용한다(tools/verify_kakao_sdk.py).
 * - 라벨: LabelOptions.from(LatLng).setStyles().setTexts(LabelTextBuilder), Label.moveTo
 * - 면: PolygonOptions.from(MapPoints | DotPoints, PolygonStyle...), DotPoints.fromCircle(LatLng, float)
 * - 선: RouteLineOptions.from(RouteLineSegment).setStylesSet(RouteLineStylesSet)
 */
@Composable
fun KakaoMapPanel(
    scene: MapScene,
    vehicle: GeoPoint?,
    followVehicle: Boolean,
    camera: MapCameraController,
    modifier: Modifier = Modifier,
) {
    var map by remember { mutableStateOf<KakaoMap?>(null) }
    var mapView by remember { mutableStateOf<MapView?>(null) }
    val vehicleLabel = remember { arrayOfNulls<Label>(1) }
    val currentVehicle by rememberUpdatedState(vehicle ?: scene.vehicleFallback)

    AndroidView(
        modifier = modifier,
        factory = { ctx ->
            MapView(ctx).also { mv ->
                mapView = mv
                val start = scene.center ?: vehicle ?: scene.markers.firstOrNull()?.position ?: GeoPoint(37.4946, 127.0620)
                mv.start(
                    object : MapLifeCycleCallback() {
                        override fun onMapDestroy() { map = null; camera.map = null }
                        override fun onMapError(error: Exception) { Log.e(TAG, "map error (키·키해시 확인)", error) }
                    },
                    object : KakaoMapReadyCallback() {
                        override fun onMapReady(kakaoMap: KakaoMap) { map = kakaoMap; camera.map = kakaoMap }
                        override fun getPosition(): LatLng = LatLng.from(start.lat, start.lng)
                        override fun getZoomLevel(): Int = scene.zoom
                    },
                )
            }
        },
    )

    // 공식 문서: onResume/onPause에서 MapView.resume()/pause() 필수
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner, mapView) {
        val mv = mapView
        val obs = LifecycleEventObserver { _, e ->
            when (e) {
                Lifecycle.Event.ON_RESUME -> mv?.resume()
                Lifecycle.Event.ON_PAUSE -> mv?.pause()
                else -> Unit
            }
        }
        owner.lifecycle.addObserver(obs)
        onDispose {
            owner.lifecycle.removeObserver(obs)
            mv?.pause()
            KakaoSdkExtras.finish(mv)
        }
    }

    // 장면이 바뀌면 다시 그린다(차량 라벨도 지워지므로 다시 놓는다)
    LaunchedEffect(map, scene) {
        val m = map ?: return@LaunchedEffect
        KakaoSdkExtras.clear(m)
        vehicleLabel[0] = null
        draw(m, scene)
        currentVehicle?.let { vehicleLabel[0] = addVehicleLabel(m, it, scene.vehicleText) }
        val c = scene.center
        if (c != null && !followVehicle) {
            m.moveCamera(CameraUpdateFactory.newCenterPosition(LatLng.from(c.lat, c.lng)))
        }
    }

    // 차량 위치만 바뀌면 차량 라벨만 이동
    LaunchedEffect(map, vehicle) {
        val m = map ?: return@LaunchedEffect
        val v = vehicle ?: return@LaunchedEffect
        val pos = LatLng.from(v.lat, v.lng)
        val existing = vehicleLabel[0]
        if (existing == null || !KakaoSdkExtras.move(existing, pos)) {
            existing?.let { KakaoSdkExtras.remove(m, it) }
            vehicleLabel[0] = addVehicleLabel(m, v, scene.vehicleText)
        }
        if (followVehicle) m.moveCamera(CameraUpdateFactory.newCenterPosition(pos))
    }
}

private fun textStyle() = LabelTextStyle.from(26, Color.WHITE, 2, Color.parseColor("#1E1F22"))

private fun addVehicleLabel(m: KakaoMap, v: GeoPoint, text: String?): Label? {
    var opt = LabelOptions.from(LatLng.from(v.lat, v.lng))
        .setStyles(LabelStyles.from(LabelStyle.from(R.drawable.map_vehicle).setTextStyles(textStyle())))
    text?.let { opt = opt.setTexts(LabelTextBuilder().setTexts(it)) }
    return m.labelManager?.layer?.addLabel(opt)
}

private fun draw(m: KakaoMap, scene: MapScene) {
    val shapes = m.shapeManager?.layer
    scene.circles.forEach { c ->
        shapes?.addPolygon(
            PolygonOptions.from(
                DotPoints.fromCircle(LatLng.from(c.center.lat, c.center.lng), c.radiusM),
                PolygonStyle.from(Color.parseColor("#33C8F135"), 2f, Color.parseColor("#C8F135")),
            )
        )
    }
    scene.polygons.forEach { pg ->
        val pts = pg.outer.map { LatLng.from(it.lat, it.lng) }.let { if (it.first() != it.last()) it + it.first() else it }
        shapes?.addPolygon(PolygonOptions.from(MapPoints.fromLatLng(pts), PolygonStyle.from(Color.parseColor("#55E5483B"), 3f, Color.parseColor("#E5483B"))))
    }
    val routes = m.routeLineManager?.layer
    scene.routes.forEach { r ->
        if (r.points.size < 2) return@forEach
        val style = when (r.kind) {
            RouteKind.VEHICLE -> RouteLineStyle.from(12f, Color.parseColor("#C8F135"))
            RouteKind.PLANNED -> RouteLineStyle.from(10f, Color.parseColor("#8FA63A"))
            // 점선 패턴(RouteLinePattern)은 리소스 기반이라 가는 흰 선으로 구분한다
            RouteKind.WALK -> RouteLineStyle.from(5f, Color.WHITE)
        }
        val set = RouteLineStylesSet.from("${r.id}-styles", RouteLineStyles.from(style))
        val seg = RouteLineSegment.from(r.points.map { LatLng.from(it.lat, it.lng) }, set.getStyles(0))
        routes?.addRouteLine(RouteLineOptions.from(seg).setStylesSet(set))
    }
    val labels = m.labelManager?.layer
    scene.markers.forEach { mk ->
        var opt = LabelOptions.from(LatLng.from(mk.position.lat, mk.position.lng))
            .setStyles(LabelStyles.from(LabelStyle.from(mk.icon).setTextStyles(textStyle())))
        mk.text?.let { opt = opt.setTexts(LabelTextBuilder().setTexts(it)) }
        labels?.addLabel(opt)
    }
}

private const val TAG = "WayMe/Map"
