namespace Orders.Domain;

public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "EUR")
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "El importe no puede ser negativo.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("La divisa es obligatoria.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "EUR") => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"No se pueden sumar importes en divisas distintas ({left.Currency} y {right.Currency}).");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int factor) => new(money.Amount * factor, money.Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
