using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Universities;

namespace UniConnect.Api.Controllers;

[ApiController]
[Route("api/v1/universities")]
[Produces("application/json")]
public sealed class UniversitiesController(IUniversityService universities) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UniversityResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<UniversityResponse>> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        universities.ListAsync(new PagedRequest(pageNumber, pageSize), search, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UniversityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UniversityResponse> Get(Guid id, CancellationToken cancellationToken) => universities.GetAsync(id, cancellationToken);
}
