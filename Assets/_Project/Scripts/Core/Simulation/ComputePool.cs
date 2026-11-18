using System;

namespace AIGame.Core.Simulation
{
    /// <summary>
    /// A.I.'ın işlem gücü. Kapasite A.I.'ın büyüklüğüdür; kullanılabilir kısım o an harcanabilecek olandır.
    /// Düşünmek (önizleme, ileri görüş, ek örnek, ek veri) ve operasyona girmek kullanılabilir kısımdan harcar;
    /// şehir saati ilerledikçe kapasiteye doğru dolar.
    /// </summary>
    public sealed class ComputePool
    {
        public ComputePool(int capacity, int thinkCost = 5, double regenPerHour = 10)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (thinkCost < 0) throw new ArgumentOutOfRangeException(nameof(thinkCost));
            Capacity = capacity;
            Available = capacity;
            ThinkCost = thinkCost;
            RegenPerHour = regenPerHour;
        }

        public int Capacity { get; private set; }

        /// <summary>Kullanılabilir işlem gücü. Dolma kesirli olabildiği için ondalıklı tutulur.</summary>
        public double Available { get; private set; }

        /// <summary>Ekranda gösterilecek tam sayı değeri.</summary>
        public int AvailableWhole => (int)Math.Floor(Available + 1e-9);

        /// <summary>Bir düşünme adımının bedeli.</summary>
        public int ThinkCost { get; }

        public double RegenPerHour { get; }

        public bool CanSpend(int amount) => Available + 1e-9 >= amount;

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanSpend(amount)) return false;
            Available = Math.Max(0, Available - amount);
            return true;
        }

        public bool CanThink => CanSpend(ThinkCost);

        public bool TryThink() => TrySpend(ThinkCost);

        public void Regenerate(double minutes)
        {
            if (minutes <= 0) return;
            Available = Math.Min(Capacity, Available + RegenPerHour * minutes / 60.0);
        }

        /// <summary>Kapasiteyi kalıcı olarak artırır; yeni kapasite hemen kullanılabilir.</summary>
        public void Grow(int amount)
        {
            if (amount <= 0) return;
            Capacity += amount;
            Available += amount;
        }
    }
}
