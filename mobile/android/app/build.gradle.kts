import java.util.Properties

plugins {
    id("com.android.application")
    id("com.google.gms.google-services")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

val releaseKeystorePropertiesFile = rootProject.file("key.properties")
val releaseKeystoreProperties = Properties()
if (releaseKeystorePropertiesFile.exists()) {
    releaseKeystorePropertiesFile.inputStream().use { releaseKeystoreProperties.load(it) }
}
val releaseSigningKeys = listOf("keyAlias", "keyPassword", "storeFile", "storePassword")
val hasReleaseSigning = releaseKeystorePropertiesFile.exists() &&
    releaseSigningKeys.all { !releaseKeystoreProperties.getProperty(it).isNullOrBlank() }

android {
    namespace = "com.tarlaasistani.pilot"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId = "com.tarlaasistani.pilot"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                keyAlias = releaseKeystoreProperties.getProperty("keyAlias")
                keyPassword = releaseKeystoreProperties.getProperty("keyPassword")
                storeFile = file(releaseKeystoreProperties.getProperty("storeFile"))
                storePassword = releaseKeystoreProperties.getProperty("storePassword")
            }
        }
    }

    buildTypes {
        release {
            if (hasReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }
}

// Never create an unsigned or debug-signed package for a release task.
gradle.taskGraph.whenReady {
    val runsReleaseTask = allTasks.any { it.name.contains("Release", ignoreCase = true) }
    if (runsReleaseTask && !hasReleaseSigning) {
        throw GradleException(
            "Release signing is required. Create android/key.properties with a valid upload keystore."
        )
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
