using System.Security.Claims;
using Currere_backend.DTOs;
using Currere_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Currere_backend.Controllers
{
    [Authorize]
    [Route("api/workspace/{workspaceId}/[controller]")]
    [ApiController]
    public class GithubController : ControllerBase
    {
        private readonly IGithubService _githubService;

        public GithubController(IGithubService githubService)
        {
            _githubService = githubService;
        }

        [HttpPost("push")]
        public async Task<IActionResult> PushToGithub(int workspaceId, [FromBody] GithubPushRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                var repoUrl = await _githubService.PushWorkspaceToGithubAsync(
                    userId,
                    workspaceId,
                    request.RepoName,
                    request.CommitMessage ?? "Otonom Currere AI Güncellemesi"
                );

                return Ok(new
                {
                    message = "Çalışma alanı başarıyla GitHub'a aktarıldı!",
                    url = repoUrl
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}