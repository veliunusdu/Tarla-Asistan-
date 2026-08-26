using MediatR;
using TarlaAsistani.Application.Features.Farms.DTOs;

namespace TarlaAsistani.Application.Features.Farms.Queries;

// This Query returns a List of FarmDto objects
public record GetFarmsQuery() : IRequest<List<FarmDto>>;
