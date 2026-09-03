using FluentAssertions;
using TarlaAsistani.Application.Features.Tasks.Commands;
using TarlaAsistani.Domain.Entities;
using TarlaAsistani.Domain.Enums;
using TarlaAsistani.Domain.Exceptions;
using TarlaAsistani.UnitTests.Common;

namespace TarlaAsistani.UnitTests.Features.Tasks;

[Trait("Category", "Tasks")]
public class TaskDeduplicationTests
{
    [Fact]
    public async Task CreateExpertTask_SameFarm_SameDueDate_SameTaskContent_ThrowsDuplicate()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Kuzey Tarlası" };
        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db);
        var command1 = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: farmerId,
            CreatedByRole: UserRole.Farmer,
            Title: "Damlama Sulama",
            Description: "Haftalık sulama",
            Reason: "Toprak nemi azaldı",
            Priority: TaskPriority.Medium,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 9, 4)
        );

        // Act 1: First creation succeeds
        var result1 = await handler.Handle(command1, CancellationToken.None);
        result1.Should().NotBeNull();

        // Act 2: Exact duplicate command throws DuplicateTaskException
        var act2 = () => handler.Handle(command1, CancellationToken.None);
        await act2.Should().ThrowAsync<DuplicateTaskException>();
    }

    [Fact]
    public async Task CreateExpertTask_SameFarm_DifferentDueDate_SameTaskContent_BothSucceed()
    {
        // Arrange
        var farmId = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm = new Farm { Id = farmId, OwnerId = farmerId, Name = "Kuzey Tarlası" };
        var db = new MockDbContextBuilder()
            .WithFarms(farm)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db);

        var commandDate1 = new CreateExpertTaskCommand(
            FarmId: farmId,
            CreatedById: farmerId,
            CreatedByRole: UserRole.Farmer,
            Title: "Damlama Sulama",
            Description: "Haftalık sulama",
            Reason: "Toprak nemi azaldı",
            Priority: TaskPriority.Medium,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 9, 4)
        );

        var commandDate2 = commandDate1 with { DueDate = new DateOnly(2026, 9, 11) };

        // Act
        var result1 = await handler.Handle(commandDate1, CancellationToken.None);
        var result2 = await handler.Handle(commandDate2, CancellationToken.None);

        // Assert - Both exist with different IDs and different dedupe keys
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Id.Should().NotBe(result2.Id);
        result1.DueDate.Should().Be(new DateOnly(2026, 9, 4));
        result2.DueDate.Should().Be(new DateOnly(2026, 9, 11));
    }

    [Fact]
    public async Task CreateExpertTask_DifferentFarm_SameDueDate_SameTaskContent_BothSucceed()
    {
        // Arrange
        var farm1Id = Guid.NewGuid();
        var farm2Id = Guid.NewGuid();
        var farmerId = Guid.NewGuid();
        var farm1 = new Farm { Id = farm1Id, OwnerId = farmerId, Name = "Kuzey Tarlası" };
        var farm2 = new Farm { Id = farm2Id, OwnerId = farmerId, Name = "Güney Tarlası" };
        var db = new MockDbContextBuilder()
            .WithFarms(farm1, farm2)
            .Build();

        var handler = new CreateExpertTaskCommandHandler(db);

        var commandFarm1 = new CreateExpertTaskCommand(
            FarmId: farm1Id,
            CreatedById: farmerId,
            CreatedByRole: UserRole.Farmer,
            Title: "Damlama Sulama",
            Description: "Haftalık sulama",
            Reason: "Toprak nemi azaldı",
            Priority: TaskPriority.Medium,
            Confidence: TaskConfidence.High,
            DueDate: new DateOnly(2026, 9, 4)
        );

        var commandFarm2 = commandFarm1 with { FarmId = farm2Id };

        // Act
        var result1 = await handler.Handle(commandFarm1, CancellationToken.None);
        var result2 = await handler.Handle(commandFarm2, CancellationToken.None);

        // Assert - Both succeed because FarmId is part of dedupe identity
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.FarmId.Should().Be(farm1Id);
        result2.FarmId.Should().Be(farm2Id);
    }
}
