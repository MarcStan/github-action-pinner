# Configuration

The config file is currently **optional** and must be created manually. It must be named `GithubActionPinner.trusted` and placed directly next to the executable to be recognized.

## Trusting actions/commits

When pinning actions it can be difficult to verify whether the SHA is safe to use because visually comparing SHAs across yaml pipelines is cubersome.

By default actions that are pinned result in a warning:

> Consider trusting 'actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81' by adding it to the config once you have audited the code.

In the config file add this text to trust the specific commit hash (after you have reviewed the code). Multiple hashes can be added across many repositories.

```
actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81
```

Adding a commit merely prevents the warning from showing up when running an update on future actions.

The idea is: You check for action updates and are shown the warning. After reviewing the code and trusting the commit you add it to the config.

Running the check/update command again will no longer issue a warning for the trusted commit.

Update of the action reference to point to the sha will still be performed.

## Trusting organizations/repositories

In addition to trusting specific commits you can also apply trust to certain organizations/authors and repositories via the config:

```
actions
nuget/setup-nuget
```

The config will trust all actions from `actions` organization and also trusts the `nuget/setup-nuget`.

Note that when trusting actions or organizations their actions will no longer be updated to use the commit hash values and are instead updated to their respective latest semver compliant version.

## Example

An example of all possible values mixed (order is irelevant):

```
microsoft
nuget/setup-nuget
actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81
```