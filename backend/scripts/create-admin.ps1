param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$FirebaseUid,
    [Parameter(Position=1)]
    [string]$Operator = "system-operator",
    [Parameter(Position=2)]
    [string]$Reason = "Initial admin provisioning via CLI"
)

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendRoot = Resolve-Path "$PSScriptRoot\.."

Push-Location $BackendRoot
try {
    dotnet run --project src/TarlaAsistani.API -- admin:create --firebase-uid $FirebaseUid --operator $Operator --reason $Reason
}
finally {
    Pop-Location
}
