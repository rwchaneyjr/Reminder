using System;
using System.Collections.Generic;
using System.Globalization;

namespace RememberThis
{
    // Bundled translations: no network, account, or translation service is needed.
    // Only developer-owned strings go through T; reminder text is never translated.
    public static class ReminderLocalization
    {
        public static string Language { get; private set; } = "en";
        public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

        public static string NormalizeChoice(string choice) => choice == "en" || choice == "es" ? choice : "auto";

        public static void ConfigureChoice(string choice, string deviceLanguage) =>
            Configure(NormalizeChoice(choice) == "auto" ? deviceLanguage : choice);

        public static void Configure(string language)
        {
            Language = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) ||
                (language ?? "").StartsWith("es-", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
            Culture = CultureInfo.GetCultureInfo(Language == "es" ? "es-ES" : "en-US");
        }

        public static string T(string english) => Language == "es" && english != null &&
            Spanish.TryGetValue(english, out var translated) ? translated : english;

        public static string DisplayDate(DateTime date, string format)
        {
            if (Language == "es")
            {
                switch (format)
                {
                    case "MMM d, h:mm:ss tt": format = "d MMM, h:mm:ss tt"; break;
                    case "MMM d, yyyy h:mm:ss tt": format = "d MMM yyyy h:mm:ss tt"; break;
                    case "ddd, MMM d, yyyy 'at' h:mm:ss tt": format = "ddd, d MMM yyyy 'a las' h:mm:ss tt"; break;
                    case "dddd, MMMM d, yyyy\nh:mm:ss tt": format = "dddd, d 'de' MMMM 'de' yyyy\nh:mm:ss tt"; break;
                }
            }
            return date.ToString(format, Culture);
        }

        private static readonly Dictionary<string, string> Spanish = new Dictionary<string, string>
        {
            { "Automatic", "Automático" },
            { "Language saved.", "Idioma guardado." },
            { "Could not save language. Please try again.", "No se pudo guardar el idioma. Vuelve a intentarlo." },
            { "Alert canceled. Reminder completed.", "Aviso cancelado. Recordatorio completado." },
            { "What: ", "Qué: " },
            { "\nWhen: ", "\nCuándo: " },
            { "When: ", "Cuándo: " },
            { "Choose a time", "Elige una hora" },
            { "\n\nCheck the message and time. Confirm to save, cancel to discard this draft, or choose a correction below.", "\n\nRevisa el mensaje y la hora. Confirma para guardar, cancela para descartar el borrador o elige una corrección abajo." },
            { "No active saved reminder. Transcribing alone does not schedule one.", "No hay recordatorios activos guardados. Transcribir no programa un recordatorio." },
            { "\nDue: ", "\nPara: " },
            { " | Exact timing: ", " | Hora exacta: " },
            { "on", "activada" },
            { "off", "desactivada" },
            { "Scheduled", "Programada" },
            { "Delivered", "Entregada" },
            { "Unknown", "Desconocido" },
            { "Unavailable", "No disponible" },
            { "Android reports it in the notification drawer. Swipe down from the top.", "Android indica que está en el panel de notificaciones. Desliza hacia abajo desde arriba." },
            { "Still pending. Check the due time; approximate alarms may be delayed.", "Sigue pendiente. Revisa la hora; las alarmas aproximadas pueden retrasarse." },
            { "Not confirmed as pending or visible. Check app notification permissions; a dismissed alert can also appear missing.", "No se ha confirmado que esté pendiente o visible. Revisa los permisos de notificación; un aviso descartado también puede aparecer como ausente." },
            { "Could not check notifications: ", "No se pudieron consultar las notificaciones: " },
            { "Notification status requires the Android APK. Unity Play mode only saves a preview.", "El estado de notificaciones requiere la aplicación Android. El modo Play de Unity solo guarda una vista previa." },
            { "When should I remind you?", "¿Cuándo quieres el recordatorio?" },
            { "Speak your reminder", "Di tu recordatorio" },
            { "For example: in an hour, or tomorrow at 3 PM.", "Por ejemplo: en una hora, o mañana a las 3 PM." },
            { "For example: Call John in one hour. Then pause when you are finished.", "Por ejemplo: Llamar a Juan en una hora. Haz una pausa al terminar." },
            { "Review and confirm", "Revisar y confirmar" },
            { "Speak reminder", "Dictar recordatorio" },
            { "Done speaking?", "¿Terminaste de hablar?" },
            { "Preparing your reminder…", "Preparando tu recordatorio…" },
            { "Finish the current action before recording.", "Termina la acción actual antes de grabar." },
            { "Preparing reminder…", "Preparando recordatorio…" },
            { "Speak when", "Dictar cuándo" },
            { "Choose or speak a time before confirming.", "Elige o dicta una hora antes de confirmar." },
            { "Heard: ", "Se escuchó: " },
            { "\nReview before saving.", "\nRevisa antes de guardar." },
            { "Time unclear. Try 'in an hour' or 'tomorrow at 3 PM', or use the calendar and clock.", "Hora poco clara. Prueba 'en una hora' o 'mañana a las 3 PM', o usa el calendario y el reloj." },
            { "Today", "Hoy" },
            { "Upcoming", "Próximos" },
            { "Past due", "Vencidos" },
            { "Completed", "Completados" },
            { "None", "Ninguno" },
            { "Edit", "Editar" },
            { "Complete", "Completar" },
            { "Reminder completed.", "Recordatorio completado." },
            { "Snooze", "Posponer" },
            { "+1 hour", "+1 hora" },
            { "Tomorrow", "Mañana" },
            { "Custom...", "Otra hora..." },
            { "Delete this reminder?", "¿Eliminar este recordatorio?" },
            { "Delete", "Eliminar" },
            { "Reminder deleted.", "Recordatorio eliminado." },
            { "Keep", "Conservar" },
            { "Delete...", "Eliminar..." },
            { "Cancel editing", "Cancelar edición" },
            { "Editing: ", "Editando: " },
            { "Save changes", "Guardar cambios" },
            { "Edit the text or time, then save. Past reminders need a future time.", "Edita el texto o la hora y guarda. Los recordatorios vencidos necesitan una hora futura." },
            { "Confirm snooze", "Confirmar nueva hora" },
            { "Cancel snooze", "Cancelar cambio de hora" },
            { "Choose a new time and AM or PM, then confirm snooze. The original reminder stays unchanged until you confirm.", "Elige otra hora y AM o PM; luego confirma. El recordatorio original no cambia hasta que confirmes." },
            { "Set reminder", "Crear recordatorio" },
            { "Snoozed until ", "Pospuesto hasta " },
            { "Change saved, but the phone notification could not be updated. Reopen the app to retry.", "Cambio guardado, pero no se pudo actualizar la notificación. Vuelve a abrir la aplicación para reintentar." },
            { "Enter a time from 1 to 12, like 7:30 or 7:30:15, and choose AM or PM.", "Introduce una hora del 1 al 12, como 7:30 o 7:30:15, y elige AM o PM." },
            { "Remind me: ", "Recordarme: " },
            { "\nChoose a future date and time.", "\nElige una fecha y hora futuras." },
            { "Reminder tests", "Recordatorios" },
            { "Reminders at your chosen time", "Recordatorios a la hora que elijas" },
            { "Ready. Allow notifications when asked, then leave the app to test delivery.", "Listo. Permite las notificaciones cuando se solicite y sal de la aplicación para probar la entrega." },
            { "Editor preview only. Build and run on an Android phone to test real notifications.", "Solo vista previa del editor. Compila y ejecuta en un teléfono Android para probar notificaciones reales." },
            { "Enter what you want to remember, such as Call John.", "Escribe lo que quieres recordar, como Llamar a Juan." },
            { "Enter a valid future time before setting the reminder.", "Introduce una hora futura válida antes de crear el recordatorio." },
            { "Waiting for notification permission...", "Esperando permiso de notificaciones..." },
            { "Notifications are disabled. Enable them in Android Settings > Apps > Remember This > Notifications, then try again.", "Las notificaciones están desactivadas. Actívalas en Ajustes de Android > Aplicaciones > Remember This > Notificaciones y vuelve a intentarlo." },
            { "That time passed while waiting for permission. Choose a new time.", "La hora pasó mientras se esperaba el permiso. Elige otra hora." },
            { "Reminder set for ", "Recordatorio programado para " },
            { ".\nLeave the app and watch for the notification.\n", ".\nSal de la aplicación y espera la notificación.\n" },
            { "Exact scheduling is available. Delivery still needs a phone test.", "La programación exacta está disponible. Aún hay que probar la entrega en un teléfono." },
            { "Android is using approximate timing; delivery may be delayed. Enable exact timing below, then schedule again.", "Android usa una hora aproximada; puede haber retrasos. Activa la hora exacta abajo y vuelve a programar." },
            { "Preview: ", "Vista previa: " },
            { "\nFor ", "\nPara " },
            { ". No notification was scheduled on this device.", ". No se programó ninguna notificación en este dispositivo." },
            { "Reminder saved, but notification scheduling failed. Reopen the app to retry.", "Recordatorio guardado, pero no se pudo programar la notificación. Vuelve a abrir la aplicación para reintentar." },
            { "No saved reminder to cancel.", "No hay recordatorios guardados para cancelar." },
            { "Confirm deletion in the reminder list below.", "Confirma la eliminación en la lista de recordatorios de abajo." },
            { "Exact timing is already available. Tap Notify me to start a new test.", "La hora exacta ya está disponible. Crea un recordatorio para iniciar otra prueba." },
            { "Allow alarms and reminders in Android settings, return here, then schedule a new test.", "Permite alarmas y recordatorios en los ajustes de Android, vuelve aquí y programa otra prueba." },
            { "Exact timing permissions can only be tested on an Android phone.", "Los permisos de hora exacta solo se pueden probar en un teléfono Android." },
            { "The notification operation failed. Please try again.\n", "La operación de notificación falló. Vuelve a intentarlo.\n" },
            { "Back to home", "Volver al inicio" },
            { "What would you like to do?", "¿Qué quieres hacer?" },
            { "Calendar", "Calendario" },
            { "My reminders", "Mis recordatorios" },
            { "Speak to create a reminder, choose a date, or manage saved reminders.", "Dicta un recordatorio, elige una fecha o administra tus recordatorios guardados." },
            { "What do you want to remember?", "¿Qué quieres recordar?" },
            { "Example: Call John", "Ejemplo: Llamar a Juan" },
            { "Menu: calendar / speak / review", "Menú: calendario / dictar / revisar" },
            { "Cancel voice entry", "Cancelar dictado" },
            { "Tap Speak reminder, say what and when, then pause. Review before saving.", "Pulsa Dictar recordatorio, di qué y cuándo, y haz una pausa. Revisa antes de guardar." },
            { "When? Say or type: in an hour, in 10 minutes, or tomorrow at 3 PM.", "¿Cuándo? Di o escribe: en una hora, en 10 minutos o mañana a las 3 PM." },
            { "Open-source licenses", "Licencias de código abierto" },
            { "Choose a day, then a time from 1 to 12 and AM or PM. Selected day is green.", "Elige un día, una hora del 1 al 12 y AM o PM. El día elegido aparece en verde." },
            { "Return to menu / confirm", "Volver al menú / confirmar" },
            { "Edit reminder text / time", "Editar texto / hora" },
            { "Editing canceled. Saved reminder unchanged.", "Edición cancelada. El recordatorio guardado no ha cambiado." },
            { "Enable exact timing", "Activar hora exacta" },
            { "Cancel most recent reminder", "Cancelar último recordatorio" },
            { "Starting...", "Iniciando..." },
            { "Upcoming reminders — scroll to see all", "Próximos recordatorios: desliza para ver todos" },
            { "Snooze starts from now. Tomorrow means this time tomorrow. Past due does not confirm notification delivery.", "El aplazamiento empieza ahora. Mañana significa mañana a esta hora. Vencido no confirma la entrega de la notificación." },
            { "View calendar", "Ver calendario" },
            { "Cancel voice / close", "Cancelar dictado / cerrar" },
            { "Review your reminder", "Revisa tu recordatorio" },
            { "Ready? Choose one:", "¿Listo? Elige una opción:" },
            { "Confirm reminder — save it", "Confirmar y guardar" },
            { "Cancel reminder — discard draft", "Cancelar y descartar borrador" },
            { "Draft canceled. Speak or type a new reminder.", "Borrador cancelado. Dicta o escribe otro recordatorio." },
            { "Draft canceled. Previously saved reminders are unchanged.", "Borrador cancelado. Los recordatorios guardados no han cambiado." },
            { "Need a change? Edit, check a date, or speak again:", "¿Quieres cambiar algo? Edita, consulta una fecha o vuelve a dictar:" },
            { "Edit text / date / time", "Editar texto / fecha / hora" },
            { "Check notification status", "Ver estado de notificación" },
            { "IT'S TIME", "ES LA HORA" },
            { "Snooze 5 min", "Posponer 5 min" },
            { "Snooze 10 min", "Posponer 10 min" },
            { "Snooze 1 hour", "Posponer 1 hora" },
            { "Custom snooze…", "Elegir otra hora…" },
            { "Cancel reminder", "Cancelar recordatorio" },
            { "Canceling…", "Cancelando…" },
            { "Getting voice ready", "Preparando el dictado" },
            { "Preparing your reminder: ", "Preparando tu recordatorio: " },
            { " seconds.", " segundos." },
            { "\nThis is taking longer than usual. You can cancel and type instead.", "\nEstá tardando más de lo habitual. Puedes cancelar y escribir el recordatorio." },
            { "Allow microphone access when prompted.", "Permite el acceso al micrófono cuando se solicite." },
            { "Recording canceled. Your text is unchanged.", "Grabación cancelada. Tu texto no ha cambiado." },
            { "Microphone permission is off. Enable it in device settings or type your reminder.", "El permiso del micrófono está desactivado. Actívalo en los ajustes o escribe tu recordatorio." },
            { "No microphone detected. Check your device or BlueStacks microphone settings.", "No se detectó un micrófono. Revisa los ajustes del dispositivo o del micrófono de BlueStacks." },
            { "Microphone could not start.", "No se pudo iniciar el micrófono." },
            { "Listening… Say your reminder, then pause. Or tap Done speaking.", "Escuchando… Di tu recordatorio y haz una pausa. O pulsa ¿Terminaste de hablar?" },
            { "No usable audio captured. Check your microphone and try again.", "No se capturó audio utilizable. Revisa el micrófono y vuelve a intentarlo." },
            { "The recording was silent. Check microphone input and try again.", "La grabación estaba en silencio. Revisa el micrófono y vuelve a intentarlo." },
            { "Getting voice ready for the first time…", "Preparando el dictado por primera vez…" },
            { "Speech model failed to load. Restore Whisper dependencies and rebuild.", "No se pudo cargar el modelo de voz. Restaura las dependencias de Whisper y vuelve a compilar." },
            { "Voice entry canceled. Your text is unchanged.", "Dictado cancelado. Tu texto no ha cambiado." },
            { "No words recognized. Try again or type the reminder.", "No se reconocieron palabras. Reintenta o escribe el recordatorio." },
            { "Offline voice failed: ", "Falló el dictado sin conexión: " }
        };
    }
}
