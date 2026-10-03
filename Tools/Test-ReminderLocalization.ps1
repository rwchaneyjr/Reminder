$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $root 'Assets/Scripts/ReminderLocalization.cs')
foreach ($case in @(@('en', 'es', 'en'), @('es', 'en', 'es'), @('auto', 'es-MX', 'es'), @('auto', 'fr', 'en'), @('invalid', 'es', 'es'))) {
    [RememberThis.ReminderLocalization]::ConfigureChoice($case[0], $case[1])
    if ([RememberThis.ReminderLocalization]::Language -ne $case[2]) { throw "Language override/Automatic failed: $case" }
}
foreach ($language in @('es', 'es-MX', 'es-ES', 'ES-ar')) {
    [RememberThis.ReminderLocalization]::Configure($language)
    if ([RememberThis.ReminderLocalization]::Language -ne 'es' -or
        [RememberThis.ReminderLocalization]::T('Speak reminder') -cne 'Dictar recordatorio') { throw "Spanish selection failed: $language" }
}
$date = [DateTime]::new(2026, 12, 31, 15, 30, 0)
if ([RememberThis.ReminderLocalization]::DisplayDate($date, 'MMMM yyyy') -cne 'diciembre 2026') { throw 'Spanish month failed' }
if ([RememberThis.ReminderLocalization]::DisplayDate($date, "ddd, MMM d, yyyy 'at' h:mm:ss tt") -notmatch '31 dic.*a las') { throw 'Spanish date order failed' }
if ([RememberThis.ReminderLocalization]::Culture.DateTimeFormat.AbbreviatedDayNames[0] -notmatch '^dom') { throw 'Calendar weekday alignment failed' }
$unknown = 'Call María / 薬 / 10:00'
if ([RememberThis.ReminderLocalization]::T($unknown) -cne $unknown) { throw 'Missing-key fallback changed text' }

# Check every explicitly localized literal has an actual Spanish translation.
foreach ($path in @('Assets/Scripts/NotificationProof.cs', 'Assets/Scripts/OfflineVoice.cs')) {
    $source = Get-Content (Join-Path $root $path) -Raw
    foreach ($match in [regex]::Matches($source, 'T\("((?:\\.|[^"\\])*)"\)')) {
        $key = [regex]::Unescape($match.Groups[1].Value)
        if ([RememberThis.ReminderLocalization]::T($key) -ceq $key) { throw "Untranslated key: $key" }
    }
}
foreach ($language in @('en', 'en-US', 'fr', 'de', '', $null)) {
    [RememberThis.ReminderLocalization]::Configure($language)
    if ([RememberThis.ReminderLocalization]::Language -ne 'en' -or
        [RememberThis.ReminderLocalization]::T('Speak reminder') -cne 'Speak reminder') { throw "English fallback failed: $language" }
}
if ([RememberThis.ReminderLocalization]::DisplayDate($date, 'MMM d, h:mm:ss tt') -cne 'Dec 31, 3:30:00 PM') { throw 'English display changed' }
Write-Host 'PASS: Spanish variants, English fallback, date/weekday formatting, and localized-string coverage.'
