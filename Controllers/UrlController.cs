using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Models;
using UrlShortener.Options;
using UrlShortener.Services;

namespace UrlShortener.Controllers;

[ApiController]
[Route("api/url")]
[Authorize(Policy = ApiKeyAuthenticationOptions.DefaultPolicy)]
public class UrlController : ControllerBase
{
    private readonly IShortenerService _service;

    public UrlController(IShortenerService service)
    {
        _service = service;
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] Uri url)
    {
        if (!Models.Url.IsValidUrl(url))
            return BadRequest("Invalid url format.");

        Guid apiKey = GetApiKeyFromClaims();

        Url? shortedUrl = await _service.CreateShortedUrlAsync(apiKey, url);
        if (shortedUrl is null)
            return BadRequest($"The url '{url}' is already shortened.");

        string createdAt = $"{Request.Scheme}:// {Request.Host}{Request.Path}";
        return Created(createdAt, shortedUrl);
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromBody] string shortedUrlId)
    {
        if (!Models.Url.IsValidShortedUrlId(shortedUrlId))
            return BadRequest("Shorted url id is not valid.");

        Guid apiKey = GetApiKeyFromClaims();

        bool removed = await _service.RemoveUrlAsync(apiKey, shortedUrlId);
        if (!removed)
            return BadRequest("Shorted url id not found.");

        return NoContent();
    }

    [HttpGet("get/{shortedUrlId}")]
    public async Task<IActionResult> Get(string shortedUrlId)
    {
        if (!Models.Url.IsValidShortedUrlId(shortedUrlId))
            return BadRequest("Shorted url id is not valid.");
            
        Guid apiKey = GetApiKeyFromClaims();

        var url = await _service.GetUrlAsync(apiKey, shortedUrlId);
        if (url is null)
            return BadRequest("Url not found.");

        return Ok(url);
    }

    [HttpGet("get")]
    public async Task<IActionResult> Get([FromQuery] int limit = IShortenerService.DEFAULT_URLS_SHOWN)
    {
        if (limit < 0 || limit > IShortenerService.LIMIT_URLS_SHOWN)
            return BadRequest($"The limit of the results shown must be between 0 and {IShortenerService.LIMIT_URLS_SHOWN}");

        Guid apiKey = GetApiKeyFromClaims();

        var urls = await _service.GetUrlsAsync(apiKey, limit);

        return Ok(urls);
    }

    private Guid GetApiKeyFromClaims()
    {
        var claim = User.Claims.Where(claim => claim.Type == ApiKeyAuthenticationOptions.HeaderName).FirstOrDefault();
        if (!Guid.TryParse(claim?.Value, out Guid apiKey))
            throw new AuthenticationFailureException("Error getting the API key");
        return apiKey;
    }
}
