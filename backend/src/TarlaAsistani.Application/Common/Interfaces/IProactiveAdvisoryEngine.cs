using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Domain.Entities;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IProactiveAdvisoryEngine
{
    IReadOnlyList<ProactiveAdvisoryEvaluationResult> Evaluate(
        Farm farm,
        IReadOnlyList<Activity> pastActivities,
        IReadOnlyList<FarmTask> upcomingTasks,
        WeatherForecastData? forecast,
        DateTime nowUtc);
}
