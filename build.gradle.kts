plugins {
    alias(libs.plugins.wemu.android.application)
    alias(libs.plugins.wemu.android.application.compose)
}

android {
    namespace = "com.wemu.dualscreen"

    defaultConfig {
        applicationId = "com.wemu.dualscreenmods"
        versionCode = providers.gradleProperty("wemuVersionCode").orNull?.toIntOrNull() ?: 3
        versionName = providers.gradleProperty("wemuVersionName").orNull ?: "0.3.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }
}

dependencies {
    implementation(projects.secondscreen)
    implementation(projects.core.designsystem)
    implementation(projects.core.model)
    implementation(projects.core.common)

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.compose.material3)
}
