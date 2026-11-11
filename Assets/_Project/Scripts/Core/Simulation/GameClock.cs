using System;

namespace AIGame.Core.Simulation
{
    /// <summary>
    /// Şehrin saati. Dakika cinsinden ilerler; gün sınırını aşabilir.
    /// Şehir ekranında gerçek zamanla akar, mini oyunda her komutla adım adım ilerler.
    /// </summary>
    public sealed class GameClock
    {
        public const int MinutesPerDay = 24 * 60;

        public GameClock(double startMinutes)
        {
            if (startMinutes < 0) throw new ArgumentOutOfRangeException(nameof(startMinutes));
            TotalMinutes = startMinutes;
        }

        public double TotalMinutes { get; private set; }

        public int Day => (int)(TotalMinutes / MinutesPerDay);
        public double MinuteOfDay => TotalMinutes % MinutesPerDay;
        public int Hour => (int)(MinuteOfDay / 60);
        public int Minute => (int)(MinuteOfDay % 60);

        public static double At(int hour, int minute) => hour * 60 + minute;

        public void Advance(double minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            TotalMinutes += minutes;
        }

        public string Format() => Format(TotalMinutes);

        public static string Format(double totalMinutes)
        {
            var minuteOfDay = totalMinutes % MinutesPerDay;
            var hour = (int)(minuteOfDay / 60);
            var minute = (int)(minuteOfDay % 60);
            return hour.ToString("00") + ":" + minute.ToString("00");
        }
    }
}
