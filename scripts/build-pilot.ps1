param(
    [string]$ApiBaseUrl = $env:API_BASE_URL,
    [string]$FirebaseApiKey = $env:FIREBASE_API_KEY,
    [string]$FirebaseAppId = $env:FIREBASE_APP_ID,
    [string]$FirebaseMessagingSenderId = $env:FIREBASE_MESSAGING_SENDER_ID,
    [string]$FirebaseProjectId = $env:FIREBASE_PROJECT_ID,
    [string]$FirebaseStorageBucket = $env:FIREBASE_STORAGE_BUCKET
)

$ErrorActionPreference = 'Stop'
$required = @{
    API_BASE_URL = $ApiBaseUrl
    FIREBASE_API_KEY = $FirebaseApiKey
    FIREBASE_APP_ID = $FirebaseAppId
    FIREBASE_MESSAGING_SENDER_ID = $FirebaseMessagingSenderId
    FIREBASE_PROJECT_ID = $FirebaseProjectId
}
$missing = $required.GetEnumerator() | Where-Object { [string]::IsNullOrWhiteSpace($_.Value) }
if ($missing) {
    throw "Eksik pilot ayarı: $($missing.Name -join ', ')"
}
if (-not $ApiBaseUrl.StartsWith('https://')) {
    throw 'Pilot API_BASE_URL adresi HTTPS kullanmalıdır.'
}
$keyProperties = Join-Path $PSScriptRoot '..\mobile\android\key.properties'
if (-not (Test-Path -LiteralPath $keyProperties)) {
    throw 'İmzalı pilot paketi için mobile/android/key.properties oluşturun.'
}

$mobilePath = Join-Path $PSScriptRoot '..\mobile'
Push-Location $mobilePath
try {
    flutter pub get
    flutter test
    flutter build appbundle --release `
        --dart-define="API_BASE_URL=$ApiBaseUrl" `
        --dart-define="FIREBASE_API_KEY=$FirebaseApiKey" `
        --dart-define="FIREBASE_APP_ID=$FirebaseAppId" `
        --dart-define="FIREBASE_MESSAGING_SENDER_ID=$FirebaseMessagingSenderId" `
        --dart-define="FIREBASE_PROJECT_ID=$FirebaseProjectId" `
        --dart-define="FIREBASE_STORAGE_BUCKET=$FirebaseStorageBucket"
} finally {
    Pop-Location
}
