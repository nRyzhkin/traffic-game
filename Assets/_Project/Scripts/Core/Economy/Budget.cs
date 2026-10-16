using System;

namespace TrafficGame.Core.Economy
{
    /// <summary>The player's money, in whole dollars.</summary>
    public sealed class Budget
    {
        public Budget(long balance)
        {
            if (balance < 0)
                throw new ArgumentOutOfRangeException(nameof(balance), balance, "Starting balance cannot be negative.");
            Balance = balance;
        }

        public event Action Changed;

        public long Balance { get; private set; }

        public bool CanAfford(long amount) => amount <= Balance;

        public void Spend(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
            if (!CanAfford(amount))
                throw new InvalidOperationException($"Cannot spend {amount} with a balance of {Balance}.");

            Balance -= amount;
            Changed?.Invoke();
        }

        public void Refund(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");

            Balance += amount;
            Changed?.Invoke();
        }
    }
}
