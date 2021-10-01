using ThemesOfDotNet.Indexing.Ospo;

namespace ThemesOfDotNet.Indexing.WorkItems;

public sealed partial class Workspace
{
    private sealed class WorkItemUserBuilder
    {
        private readonly Workspace _workspace;
        private readonly Dictionary<string, OspoLink> _linkByGitHubLogin;
        private readonly Dictionary<string, OspoLink> _linkByAlias;
        private readonly Dictionary<string, WorkItemUser> _userByGitHubLogin = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WorkItemUser> _userByAlias = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<WorkItemUser> _users = new();

        public WorkItemUserBuilder(Workspace workspace, IReadOnlyList<OspoLink> links)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(links);

            _workspace = workspace;
            _linkByGitHubLogin = links.ToDictionary(l => l.GitHubInfo.Login, StringComparer.OrdinalIgnoreCase);
            _linkByAlias = links.ToDictionary(l => l.MicrosoftInfo.Alias, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<WorkItemUser> Users => _users;

        public WorkItemUser GetUserForGitHubLogin(string? login)
        {
            if (login is null)
                login = "ghost";

            if (_userByGitHubLogin.TryGetValue(login, out var user))
                return user;

            if (_linkByGitHubLogin.TryGetValue(login, out var link))
                user = new WorkItemUser(_workspace, link.MicrosoftInfo.PreferredName, login, link.MicrosoftInfo.Alias);
            else
                user = new WorkItemUser(_workspace, login, login, null);

            _userByGitHubLogin.Add(login, user);
            _users.Add(user);
            return user;
        }

        public WorkItemUser GetUserForMicrosoftAlias(string alias)
        {
            if (_userByAlias.TryGetValue(alias, out var user))
                return user;

            if (_linkByAlias.TryGetValue(alias, out var link))
                user = new WorkItemUser(_workspace, link.MicrosoftInfo.PreferredName, link.GitHubInfo.Login, alias);
            else
                user = new WorkItemUser(_workspace, alias, null, alias);

            _userByAlias.Add(alias, user);
            _users.Add(user);
            return user;
        }
    }
}
