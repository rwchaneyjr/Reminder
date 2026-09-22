$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../Assets/Scripts/SpeechPauseDetector.cs')
$quiet = [float[]]::new(1600)
$voice = [float[]]::new(1600)
for ($i = 0; $i -lt $voice.Length; $i++) { $voice[$i] = 0.08 }
$detector = [RememberThis.SpeechPauseDetector]::new()
for ($i = 0; $i -lt 30; $i++) {
    if ($detector.Add($quiet, 16000, 1)) { throw 'Initial silence ended recording' }
}
for ($i = 0; $i -lt 5; $i++) {
    if ($detector.Add($voice, 16000, 1)) { throw 'Speech ended recording' }
}
for ($i = 0; $i -lt 10; $i++) {
    if ($detector.Add($quiet, 16000, 1)) { throw 'Short pause ended recording' }
}
if ($detector.Add($voice, 16000, 1)) { throw 'Resumed speech ended recording' }
for ($i = 0; $i -lt 14; $i++) {
    if ($detector.Add($quiet, 16000, 1)) { throw 'Ended before final pause' }
}
if (!$detector.Add($quiet, 16000, 1)) { throw 'Final pause did not end recording' }
Write-Output 'PASS: initial silence, speech, short pause, resumed speech, and final pause.'
