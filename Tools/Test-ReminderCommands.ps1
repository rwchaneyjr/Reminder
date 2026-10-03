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

$spanish = @(
    @('Llamar a Juan en una hora', 'Llamar a Juan', 3600),
    @('Recuérdame llamar a Juan en 30 segundos.', 'llamar a Juan', 30),
    @('Comprar leche dentro de dos días', 'Comprar leche', 172800),
    @('Caminar en media hora', 'Caminar', 1800),
    @('Descansar en veinticinco minutos', 'Descansar', 1500),
    @('Descansar en treinta y cinco minutos', 'Descansar', 2100),
    @('Call John en diez minutos', 'Call John', 600)
)
foreach ($case in $spanish) {
    $title = ''; $due = [DateTime]::MinValue
    if (![RememberThis.ReminderCommandParser]::TryParse($case[0], $now, [ref]$title, [ref]$due) -or
        $title -cne $case[1] -or $due -ne $now.AddSeconds($case[2])) { throw "Spanish parsing failed: $($case[0])" }
}
foreach ($case in @(@('en una hora', $now.AddHours(1)), @('mañana a las 3 PM', $now.Date.AddDays(1).AddHours(15)),
    @('mañana a la 1 a. m.', $now.Date.AddDays(1).AddHours(1)), @('en 10 minutos', $now.AddMinutes(10)))) {
    if (![RememberThis.ReminderCommandParser]::TryParseWhen($case[0], $now, $now.Date, [ref]$due) -or $due -ne $case[1]) {
        throw "Spanish when failed: $($case[0])"
    }
}
foreach ($speech in @('Llamar mañana en una hora', 'Llamar en cero minutos', 'Llamar en -5 minutos',
    'Llamar en 1 hora y 5 minutos', 'Recuérdame en una hora', 'Llamar cada día en una hora')) {
    if ([RememberThis.ReminderCommandParser]::TryParse($speech, $now, [ref]$title, [ref]$due)) { throw "Ambiguous Spanish accepted: $speech" }
}
foreach ($speech in @('mañana a las 3', 'mañana a las 13 PM', 'hoy a las 3 PM', 'en cero minutos')) {
    if ([RememberThis.ReminderCommandParser]::TryParseWhen($speech, $now, $now.Date, [ref]$due)) { throw "Invalid Spanish time accepted: $speech" }
}
if ([RememberThis.ReminderCommandParser]::TryParse('Llamar en una hora', [DateTime]::MaxValue, [ref]$title, [ref]$due)) {
    throw 'Spanish time overflow accepted'
}
Write-Host 'PASS: Spanish commands, time phrases, accents, unchanged task text, rollover, and invalid-input rejection.'
