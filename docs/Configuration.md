# Configuration

The config file is currently **optional** and must be created manually. It must be named `<exe name>.trusted` (default `GithubActionPinner.trusted`) and placed directly next to the executable to be recognized.

## Trusting actions/commits

When pinning actions it can be difficult to verify whether the SHA is safe to use because visually comparing SHAs across yaml pipelines is cubersome.

By default actions that are pinned in yml files result in a warning:

> Consider trusting 'actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81' by adding it to the config once you have audited the code.

In the config file add this text to trust the specific commit hash (after you have reviewed the code).

```
actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81
```
Multiple hashes can be added across many repositories by adding multipe lines to the `.trusted` file.

Adding a commit merely prevents the warning from showing up when running an update on future actions and does nothing further.

The idea is: You check for action updates and are shown the warning. After reviewing the code and trusting the commit you add it to the config.

Running the check/update command again in the future  will no longer issue a warning for the trusted commit.

Update of the action reference to point to the sha will still be performed but you will no longer see a warning in the console.

## Trusting organizations/repositories

In addition to trusting specific commits you can also apply trust to certain organizations/authors and repositories via the same `.trusted` config file:

```
actions
nuget/setup-nuget
```

The config above will trust all actions from `actions` organization and also trusts the `nuget/setup-nuget` action.

Note that when trusting actions or organizations **their actions will no longer be updated to use the commit hash values** and are instead updated to their respective latest semver compliant version or branch.

**Note:** This is less secure than trusting a specific commit as any one [compromised account with contributor access](Security.md) to the org/repo can still gain access to your pipeline.

## Example

An example of all possible values mixed (order is irelevant):

```
azure
nuget/setup-nuget
actions/checkout/01aecccf739ca6ff86c0539fbc67a7a5007bbc81
```

When applying update the the yml below:

``` yaml
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v1
    - uses: nuget/setup-nuget@v1 
    - uses: azure/appservice-settings@v1
```

The resulting output will be this:

``` yaml
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@01aecccf739ca6ff86c0539fbc67a7a5007bbc81 # pin@v1
    - uses: nuget/setup-nuget@v1 
    - uses: azure/appservice-settings@v1
```

* `nuget/setup-nuget` is directly trusted and will thus not be pinned to a SHA
* `azure/appservice-settings` is trusted because `azure` is fully trusted and thus will not be pinned to a SHA
* `actions/checkout` will be pinned

Because the exact SHA of `actions/checkout` is trusted it will not result in a warning being printed on the console.

If in the future there is a new commit that is pinned to `v1`, updating would result in this warning on the console (until you trust the commit):

> Consider adding 'actions/checkout/[SHA]' to the audit log once you have audited the code!
