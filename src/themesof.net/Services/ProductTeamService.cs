using ThemesOfDotNet.Indexing.Ospo;

namespace ThemesOfDotNet.Services;

public sealed class ProductTeamService
{
    private readonly ILogger<ProductTeamService> _logger;
    private readonly IConfiguration _configuration;

    public ProductTeamService(ILogger<ProductTeamService> logger,
                              IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> IsMemberAsync(string gitHubLogin)
    {
        var token = _configuration["OspoToken"];
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("No OSPO token configured.");
            return false;
        }

        try
        {
            _logger.LogInformation("Retreiving OSPO information for user '{gitHubLogin}'...", gitHubLogin);

            using var client = new OspoClient(token);
            var response = await client.GetAsync(gitHubLogin);

            var isLinkedToMicrosoftUser = response?.MicrosoftInfo?.Alias?.Length > 0;

            if (isLinkedToMicrosoftUser)
                _logger.LogInformation("User '{gitHubLogin}' is linked to '{alias}'.", gitHubLogin, response?.MicrosoftInfo?.Alias);
            else
                _logger.LogInformation("User '{gitHubLogin}' is not linked.", gitHubLogin);

            return isLinkedToMicrosoftUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot retreive OSPO information for user '{gitHubLogin}'", gitHubLogin);
            return false;
        }
    }
}
