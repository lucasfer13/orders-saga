namespace Orders.Domain;

public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "EUR")
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "EUR") => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot add amounts in different currencies ({left.Currency} and {right.Currency}).");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int factor) => new(money.Amount * factor, money.Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
