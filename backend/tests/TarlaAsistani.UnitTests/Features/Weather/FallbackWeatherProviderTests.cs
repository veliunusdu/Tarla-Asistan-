using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class FallbackWeatherProviderTests
{
    private static WeatherPoint CreateWeatherPoint() =>
        new(DateTime.UtcNow, 24.5, 10, 0, 12, 60, 1);

    [Fact]
    public async Task GetWeatherAsync_UsesPrimaryProvider_WhenSuccessful()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var expectedData = new WeatherForecastData(new List<WeatherPoint> { CreateWeatherPoint() });
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var result = await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        result.Should().BeSameAs(expectedData);
        secondaryMock.Verify(s => s.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWeatherAsync_FallsBackToSecondaryProvider_WhenPrimaryThrows()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Primary rate limited (429)"));

        var fallbackData = new WeatherForecastData(new List<WeatherPoint> { CreateWeatherPoint() });
        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackData);

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var result = await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        result.Should().BeSameAs(fallbackData);
        fallbackProvider.Name.Should().Be("secondary");
    }

    [Fact]
    public async Task GetWeatherAsync_FallsBackToSecondaryProvider_WhenPrimaryReturnsEmptyPoints()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeatherForecastData(new List<WeatherPoint>()));

        var fallbackData = new WeatherForecastData(new List<WeatherPoint> { CreateWeatherPoint() });
        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackData);

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var result = await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        result.Should().BeSameAs(fallbackData);
        fallbackProvider.Name.Should().Be("secondary");
    }

    [Fact]
    public async Task GetWeatherAsync_ThrowsInvalidOperationException_WhenAllProvidersFail()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Primary fail"));

        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Secondary fail"));

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var act = async () => await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tüm hava durumu sağlayıcıları yanıt vermedi.*");
    }

    [Fact]
    public async Task ForecastAsync_DelegatesToGetWeatherAsync()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var point = CreateWeatherPoint();
        var expectedData = new WeatherForecastData(new List<WeatherPoint> { point });
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var points = await fallbackProvider.ForecastAsync(37.0, 32.0);

        points.Should().HaveCount(1);
        points[0].Should().Be(point);
    }

    [Fact]
    public async Task GetWeatherBatchAsync_UsesPrimaryProvider_WhenSuccessful()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var coordinates = new (double, double)[] { (37.0, 32.0), (38.0, 33.0) };
        var batchData = new List<WeatherForecastData>
        {
            new(new List<WeatherPoint> { CreateWeatherPoint() }),
            new(new List<WeatherPoint> { CreateWeatherPoint() })
        };
        primaryMock.Setup(p => p.GetWeatherBatchAsync(coordinates, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchData);

        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var results = await fallbackProvider.GetWeatherBatchAsync(coordinates);

        results.Should().BeSameAs(batchData);
        secondaryMock.Verify(s => s.GetWeatherBatchAsync(It.IsAny<IReadOnlyList<(double, double)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWeatherBatchAsync_FallsBackToSecondaryProvider_WhenPrimaryFails()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var coordinates = new (double, double)[] { (37.0, 32.0), (38.0, 33.0) };
        primaryMock.Setup(p => p.GetWeatherBatchAsync(coordinates, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Primary batch failed"));

        var fallbackBatchData = new List<WeatherForecastData>
        {
            new(new List<WeatherPoint> { CreateWeatherPoint() }),
            new(new List<WeatherPoint> { CreateWeatherPoint() })
        };
        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherBatchAsync(coordinates, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackBatchData);

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var results = await fallbackProvider.GetWeatherBatchAsync(coordinates);

        results.Should().BeSameAs(fallbackBatchData);
        fallbackProvider.Name.Should().Be("secondary");
    }

    [Fact]
    public async Task GetWeatherBatchAsync_ThrowsInvalidOperationException_WhenAllProvidersFail()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var coordinates = new (double, double)[] { (37.0, 32.0) };
        primaryMock.Setup(p => p.GetWeatherBatchAsync(coordinates, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Fail 1"));

        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherBatchAsync(coordinates, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Fail 2"));

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var act = async () => await fallbackProvider.GetWeatherBatchAsync(coordinates);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tüm hava durumu sağlayıcıları toplu istekte başarısız oldu.*");
    }
}
