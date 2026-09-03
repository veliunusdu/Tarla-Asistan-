namespace TarlaAsistani.Application.Common.Interfaces;

public interface IAICostCalculator
{
    decimal CalculateCost(string provider, string model, int promptTokens, int completionTokens);
}
