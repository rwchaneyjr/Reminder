$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets/Scripts/ReminderCommandParser.cs')
$now = [DateTime]::new(2026, 12, 31, 23, 30, 0)
$cases = @(
    @('call me in an hour', 'call me', 3600),
    @('Remind me to call John in 30 seconds.', 'call John', 30),
    @('Call Mary in twenty-five minutes', 'Call Mary', 1500),
    @('Walk in half an hour', 'Walk', 1800),
    @('Buy milk in two days', 'Buy milk', 172800)
)
foreach ($case in $cases) {
    $title = ''; $due = [DateTime]::MinValue
    $ok = [RememberThis.ReminderCommandParser]::TryParse($case[0], $now, [ref]$title, [ref]$due)
    if (!$ok -or $title -ne $case[1] -or $due -ne $now.AddSeconds($case[2])) { throw "Parsing failed: $($case[0])" }
}
foreach ($speech in @('Call Mary later', 'Call John at 8', 'Call John in zero minutes', 'Call John in -5 minutes', 'Call John in 1 hour and 5 minutes', 'Remind me to in an hour', 'Call John tomorrow in an hour')) {
    $title = ''; $due = [DateTime]::MinValue
    if ([RememberThis.ReminderCommandParser]::TryParse($speech, $now, [ref]$title, [ref]$due)) { throw "Ambiguous/invalid input accepted: $speech" }
}
Write-Host 'PASS: relative commands, year rollover, number words, and ambiguous-input rejection.'
$due = [DateTime]::MinValue
if (![RememberThis.ReminderCommandParser]::TryParseWhen('in an hour', $now, $now.Date, [ref]$due) -or $due -ne $now.AddHours(1)) { throw 'Relative when failed' }
if (![RememberThis.ReminderCommandParser]::TryParseWhen('tomorrow at 3 PM', $now, $now.Date, [ref]$due) -or $due -ne $now.Date.AddDays(1).AddHours(15)) { throw 'Tomorrow clock time failed' }
if ([RememberThis.ReminderCommandParser]::TryParseWhen('at 3', $now, $now.Date, [ref]$due)) { throw 'Missing AM/PM must not be guessed' }
Write-Host 'PASS: separate when field and ambiguous clock rejection.'
