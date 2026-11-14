import java.util.Properties

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
}

// 비밀 값은 local.properties(커밋 안 됨)에서만 읽는다. 코드·저장소에 하드코딩 금지
val localProps = Properties().apply {
    rootProject.file("local.properties").takeIf { it.exists() }?.inputStream()?.use { load(it) }
}

android {
    namespace = "com.example.pickup_hyundai"
    compileSdk {
        version = release(37)
    }

    defaultConfig {
        applicationId = "com.example.pickup_hyundai"
        minSdk = 28
        targetSdk = 37
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        // 카카오 네이티브 앱 키: local.properties(커밋 안 됨)의 KAKAO_NATIVE_APP_KEY. 없으면 지도 자리표시
        buildConfigField("String", "KAKAO_NATIVE_APP_KEY", "\"${localProps.getProperty("KAKAO_NATIVE_APP_KEY", "")}\"")
        // 라이드메이트 서버(정본 차량 API). 비어 있으면 Mock 저장소 사용
        buildConfigField("String", "RIDEMATE_API_BASE_URL", "\"${localProps.getProperty("RIDEMATE_API_BASE_URL", "")}\"")
    }

    buildTypes {
        debug {
            // 개발용 차량 access token. 차량 앱 토큰 발급 API가 정본에 없어 임시로 local.properties에서 받는다(debug 빌드 한정)
            buildConfigField("String", "RIDEMATE_DEV_ACCESS_TOKEN", "\"${localProps.getProperty("RIDEMATE_DEV_ACCESS_TOKEN", "")}\"")
        }
        release {
            buildConfigField("String", "RIDEMATE_DEV_ACCESS_TOKEN", "\"\"")
            optimization {
                enable = true
                packageScope = setOf("androidx.**", "kotlin.**", "kotlinx.**")
            }
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
    buildFeatures {
        compose = true
        buildConfig = true
    }
}

dependencies {
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.core.ktx)
    implementation(libs.pleos.vehicle)
    implementation(libs.pleos.navihelper)
    implementation(libs.pleos.fusedlocation)
    implementation(libs.kakao.map)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.material)
    testImplementation(libs.junit)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    debugImplementation(libs.androidx.compose.ui.tooling)
}
