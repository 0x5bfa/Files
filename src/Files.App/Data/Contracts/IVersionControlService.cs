// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel;

namespace Files.App.Data.Contracts
{
	/// <summary>
	/// Defines the Git operations used by the app without exposing a provider-specific model.
	/// </summary>
	public interface IVersionControlService
	{
		Task<string?> GetGitRepositoryPathAsync(
			string? path,
			string? root = null,
			CancellationToken cancellationToken = default);

		Task<string> GetOriginRepositoryNameAsync(
			string? path,
			CancellationToken cancellationToken = default);

		Task<BranchItem[]> GetBranchNames(string? path);

		Task<BranchItem?> GetRepositoryHead(string? path);

		Task<string?> GetRepositoryHeadName(string? path);

		Task<bool> Checkout(string? repositoryPath, string? branch);

		Task CreateNewBranchAsync(string repositoryPath, string activeBranch);

		Task DeleteBranchAsync(string? repositoryPath, string? activeBranch, string? branchToDelete);

		Task<bool> ValidateBranchNameForRepositoryAsync(string branchName, string repositoryPath);

		Task FetchOriginAsync(string? repositoryPath, CancellationToken cancellationToken = default);

		Task PullOriginAsync(string? repositoryPath);

		Task PushToOriginAsync(string? repositoryPath, string? branchName);

		Task RequireGitAuthenticationAsync();

		Task<IReadOnlyList<GitItemModel>> GetGitInformationForItemsAsync(
			string repositoryPath,
			IReadOnlyList<string> paths,
			bool getStatus = true,
			bool getCommit = true,
			CancellationToken cancellationToken = default);

		void RemoveSavedCredentials();

		string GetSavedCredentials();

		Task InitializeRepositoryAsync(string? path);

		(string RepoUrl, string RepoName) GetRepoInfo(string url);

		bool IsValidRepoUrl(string url);

		Task CloneRepoAsync(string repoUrl, string repoName, string targetDirectory);

		bool IsExecutingGitAction { get; }

		event PropertyChangedEventHandler? IsExecutingGitActionChanged;

		event EventHandler? GitFetchCompleted;
	}
}
