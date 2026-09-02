using FluentAssertions;
using TarlaAsistani.Application.Features.AI.Services;

namespace TarlaAsistani.UnitTests.Features.AI;

[Trait("Category", "AI")]
public class WeatherIntentDetectorTests
{
    [Theory]
    [InlineData("Yarın Kuzey Tarla'da ne yapayım?", true)]
    [InlineData("Bugün sulama yapmalı mıyım?", true)]
    [InlineData("Hava nasıl?", true)]
    [InlineData("Yağmur yağacak mı?", true)]
    [InlineData("Rüzgar ne kadar?", true)]
    [InlineData("Don riski var mı?", true)]
    [InlineData("Bu hafta hasat yapabilir miyim?", true)]
    [InlineData("İlaçlama için uygun mu?", true)]
    [InlineData("İLAÇLAMA için uygun mu?", true)]
    [InlineData("ilaçlama için uygun mu?", true)]
    [InlineData("Gübreleme yapacağım bugün", true)]
    [InlineData("Tarlada yarın ne yapacağım?", true)]
    [InlineData("Yaprak hastalığı nedir?", false)]
    [InlineData("Buğday ekimi kaç cm derinlikte olmalı?", false)]
    [InlineData("Tarihçe nedir?", false)]
    public void IsWeatherRelevant_ShouldReturnExpectedResult(string message, bool expected)
    {
        var result = WeatherIntentDetector.IsWeatherRelevant(message);
        result.Should().Be(expected);
    }

    [Fact]
    public void TryMatchFarmByName_WithSingleMatch_ShouldReturnFarmId()
    {
        var kuzeyId = Guid.NewGuid();
        var guneyId = Guid.NewGuid();
        var farms = new List<(Guid, string)>
        {
            (kuzeyId, "Kuzey Tarla"),
            (guneyId, "Güney Tarla"),
        };

        var result = WeatherIntentDetector.TryMatchFarmByName("Kuzey Tarla'da hava nasıl?", farms);
        result.Should().Be(kuzeyId);
    }

    [Fact]
    public void TryMatchFarmByName_WithNoMatch_ShouldReturnNull()
    {
        var farms = new List<(Guid, string)>
        {
            (Guid.NewGuid(), "Kuzey Tarla"),
        };

        var result = WeatherIntentDetector.TryMatchFarmByName("Buğday hastalıkları hakkında bilgi", farms);
        result.Should().BeNull();
    }

    [Fact]
    public void TryMatchFarmByName_WithMultipleMatches_ShouldReturnNull()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var farms = new List<(Guid, string)>
        {
            (id1, "Kuzey"),
            (id2, "Kuzey Tarla"),
        };

        // "Kuzey Tarla" message matches both "Kuzey" and "Kuzey Tarla" as substrings
        var result = WeatherIntentDetector.TryMatchFarmByName("Kuzey Tarla'da hava nasıl?", farms);
        result.Should().BeNull(because: "multiple farm names match means ambiguous selection");
    }

    [Fact]
    public void TryMatchFarmByName_IsCaseInsensitive()
    {
        var farmId = Guid.NewGuid();
        var farms = new List<(Guid, string)> { (farmId, "Kuzey Tarla") };

        var result = WeatherIntentDetector.TryMatchFarmByName("kuzey tarla'da yağmur var mı?", farms);
        result.Should().Be(farmId);
    }

    [Theory]
    [InlineData("Ihlamur Tarla", "ıhlamur tarlada hava nasıl?")]
    [InlineData("İnci Tarla", "inci tarlada hava nasıl?")]
    public void TryMatchFarmByName_UsesTurkishCaseInsensitiveComparison(
        string farmName,
        string message)
    {
        var farmId = Guid.NewGuid();

        var result = WeatherIntentDetector.TryMatchFarmByName(
            message,
            new[] { (farmId, farmName) });

        result.Should().Be(farmId);
    }
}
