using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RememberThis
{
    // Deliberately limited grammar: unsupported or ambiguous times stay manual.
    public static class ReminderCommandParser
    {
        public static bool TryParseWhen(string speech, DateTime spokenAt, DateTime selectedDay, out DateTime due)
        {
            due = default;
            var value = (speech ?? "").Trim().TrimEnd('.', '!', '?');
            if (TryParse("Reminder " + value, spokenAt, out _, out due)) return true;
            var match = Regex.Match(value, @"^(?:(today|tomorrow)\s+)?(?:at\s+)?(\d{1,2}(?::\d{2})?(?::\d{2})?)\s*(a\.?m\.?|p\.?m\.?)$", RegexOptions.IgnoreCase);
            if (!match.Success) return TryParseSpanishWhen(value, spokenAt, selectedDay, out due);
            var clock = match.Groups[2].Value + " " + match.Groups[3].Value.Replace(".", "").ToUpperInvariant();
            if (!DateTime.TryParseExact(clock, new[] { "h tt", "hh tt", "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)) return false;
            var day = match.Groups[1].Value.ToLowerInvariant();
            due = (day == "tomorrow" ? spokenAt.Date.AddDays(1) : day == "today" ? spokenAt.Date : selectedDay.Date).Add(time.TimeOfDay);
            return due > spokenAt;
        }

        public static bool TryParse(string speech, DateTime spokenAt, out string title, out DateTime due)
        {
            title = speech == null ? "" : speech.Trim();
            due = default;
            var match = Regex.Match(title,
                @"^(?<task>.+?)\s+in\s+(?<amount>[a-z\d -]+?)\s+(?<unit>seconds?|minutes?|hours?|days?)\s*[.!?]*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return TryParseSpanish(title, spokenAt, out title, out due);
            var task = Regex.Replace(match.Groups["task"].Value.Trim(), @"^remind me to\s+", "", RegexOptions.IgnoreCase);
            if (string.IsNullOrWhiteSpace(task) || task.Equals("remind me to", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(task, @"\b(in|at|tomorrow|today|every)\b", RegexOptions.IgnoreCase)) return false;
            var amount = match.Groups["amount"].Value.ToLowerInvariant().Trim();
            if (!TryNumber(amount, out var count) || count <= 0 || count > 365) return false;
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            double seconds = count * (unit.StartsWith("day") ? 86400 : unit.StartsWith("hour") ? 3600 : unit.StartsWith("minute") ? 60 : 1);
            try { due = spokenAt.AddSeconds(seconds); }
            catch (ArgumentOutOfRangeException) { return false; }
            title = task;
            return true;
        }

        private static bool TryParseSpanishWhen(string value, DateTime spokenAt, DateTime selectedDay, out DateTime due)
        {
            due = default;
            var match = Regex.Match(value, @"^(?:(hoy|mañana)\s+)?(?:a\s+las?\s+)?(\d{1,2}(?::\d{2})?(?::\d{2})?)\s*(a\.?\s*m\.?|p\.?\s*m\.?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return false;
            var clock = match.Groups[2].Value + " " + Regex.Replace(match.Groups[3].Value, @"[.\s]", "").ToUpperInvariant();
            if (!DateTime.TryParseExact(clock, new[] { "h tt", "hh tt", "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)) return false;
            var day = match.Groups[1].Value.ToLowerInvariant();
            try { due = (day == "mañana" ? spokenAt.Date.AddDays(1) : day == "hoy" ? spokenAt.Date : selectedDay.Date).Add(time.TimeOfDay); }
            catch (ArgumentOutOfRangeException) { return false; }
            return due > spokenAt;
        }

        private static bool TryParseSpanish(string speech, DateTime spokenAt, out string title, out DateTime due)
        {
            title = speech;
            due = default;
            var match = Regex.Match(speech,
                @"^(?<task>.+?)\s+(?:en|dentro\s+de)\s+(?<amount>[a-záéíóúñ\d -]+?)\s+(?<unit>segundos?|minutos?|horas?|d[ií]as?)\s*[.!?]*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return false;
            var task = Regex.Replace(match.Groups["task"].Value.Trim(), @"^recu[eé]rdame\s+", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (string.IsNullOrWhiteSpace(task) || Regex.IsMatch(task, @"\b(en|dentro de|a las?|mañana|hoy|cada|recu[eé]rdame)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
            var amount = match.Groups["amount"].Value.ToLowerInvariant().Trim();
            if (!TrySpanishNumber(amount, out var count) || count <= 0 || count > 365) return false;
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            double seconds = count * (unit.StartsWith("d") ? 86400 : unit.StartsWith("hora") ? 3600 : unit.StartsWith("minuto") ? 60 : 1);
            try { due = spokenAt.AddSeconds(seconds); }
            catch (ArgumentOutOfRangeException) { return false; }
            title = task;
            return true;
        }

        private static bool TrySpanishNumber(string value, out double number)
        {
            number = 0;
            if (value == "media") { number = 0.5; return true; }
            if (int.TryParse(value, out var digits)) { number = digits; return true; }
            var words = new Dictionary<string, int> {
                {"un",1},{"una",1},{"uno",1},{"dos",2},{"tres",3},{"cuatro",4},{"cinco",5},{"seis",6},{"siete",7},{"ocho",8},{"nueve",9},
                {"diez",10},{"once",11},{"doce",12},{"trece",13},{"catorce",14},{"quince",15},{"dieciséis",16},{"dieciseis",16},
                {"diecisiete",17},{"dieciocho",18},{"diecinueve",19},{"veinte",20},{"veintiuno",21},{"veintiún",21},{"veintiuna",21},
                {"veintidós",22},{"veintidos",22},{"veintitrés",23},{"veintitres",23},{"veinticuatro",24},{"veinticinco",25},
                {"veintiséis",26},{"veintiseis",26},{"veintisiete",27},{"veintiocho",28},{"veintinueve",29},
                {"treinta",30},{"cuarenta",40},{"cincuenta",50},{"sesenta",60}
            };
            if (words.TryGetValue(value, out var simple)) { number = simple; return true; }
            var parts = value.Split(new[] { " y " }, StringSplitOptions.None);
            if (parts.Length == 2 && words.TryGetValue(parts[0], out var tens) && tens >= 30 && tens % 10 == 0 &&
                words.TryGetValue(parts[1], out var ones) && ones >= 1 && ones <= 9)
            { number = tens + ones; return true; }
            return false;
        }

        private static bool TryNumber(string value, out double number)
        {
            number = 0;
            if (value == "a" || value == "an") { number = 1; return true; }
            if (value == "half an" || value == "half a") { number = 0.5; return true; }
            if (int.TryParse(value, out var digits)) { number = digits; return true; }
            var words = new Dictionary<string, int> {
                {"one",1},{"two",2},{"three",3},{"four",4},{"five",5},{"six",6},{"seven",7},{"eight",8},{"nine",9},
                {"ten",10},{"eleven",11},{"twelve",12},{"thirteen",13},{"fourteen",14},{"fifteen",15},
                {"sixteen",16},{"seventeen",17},{"eighteen",18},{"nineteen",19},
                {"twenty",20},{"thirty",30},{"forty",40},{"fifty",50},{"sixty",60}
            };
            if (words.TryGetValue(value, out var simple)) { number = simple; return true; }
            var parts = value.Replace('-', ' ').Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && words.TryGetValue(parts[0], out var tens) && tens >= 20 && tens % 10 == 0 &&
                words.TryGetValue(parts[1], out var ones) && ones >= 1 && ones <= 9)
            { number = tens + ones; return true; }
            return false;
        }
    }
}
