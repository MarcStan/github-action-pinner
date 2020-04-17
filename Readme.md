# Github action pinner

A .Net Core console application that runs locally and can be used to pin/update github action versions.

By default it upgrades all versions to use SHA references.

Github Actions allows referencing actions from other repositories in three ways:

``` yml
steps:
  - uses: actions/checkout@master
  - uses: actions/setup-dotnet@v1
  - uses: actions/upload-artifact@3446296876d12d4e3a0f3145a3c87e67bf0a16b5
```

Only the last reference style (referencing an action version by SHA) is secure as both branch and tag references can be updated in the source repository at any time.

Manually updating each action to its respective SHA is cubersome.

Using the pinner allows you to automatically convert all of the references to SHA references, see the referenced tag/branch and to easily update them in the future.

Running the command

> GithubActionPinner --update /path/to/folder [--token GITHUB_TOKEN]

result in the above being turned into:

``` yml
steps:
  - uses: actions/checkout@01aecccf739ca6ff86c0539fbc67a7a5007bbc81 # pin@master
  - uses: actions/setup-dotnet@b7821147f564527086410e8edd122d89b4b9602f # pin@v1
  - uses: actions/upload-artifact@3446296876d12d4e3a0f3145a3c87e67bf0a16b5
```

Read [security](./docs/Security.md) for details on why I consider this is the only safe use of Github Actions.

# Trusted actions

If you trust a certain author/action you can chose to add them to the `GithubActionPinner.trusted` file.

If you add an owner (`owner`) or a repo (`owner/repo`) any updates that are performed will not update to use the SHA but keep the version instead (e.g. `@v1` or `@master`).

If you add a specific commit (`owner/repo/SHA`) and the action is updated to the specific commit then there will be no warning printed to the console.

Otherwise you will see: `"Consider adding [action]/[SHA]' to the audit log once you have audited the code!"`.

See [Configuration](./docs/Configuration.md) for details on trusting individual actions/commits.

# Usage

Specify a folder for either command:

> GithubActionPinner --update /path/to/folder [--token GITHUB_TOKEN]

> GithubActionPinner --check /path/to/folder [--token GITHUB_TOKEN]

Each command will recursively scan all folders and processes all files that:

* are stored in .github/workflows folder and
* end with .yml or .yaml

In case of `--update` the file will be updated by checking if

* newer versions are available
* existing versions can be replaced with their respective SHA

Additionally output will be printed to the console along with a summary (see [Example](Readme.md#Example) below).

In case of `--check` the same output as with `--update` is printed to the console but no files are modified.

Note that the tool communicates with the Github api to check for updated versions and that you may run into Githubs rate limit.

If you plan on checking/updating multiple actions it is better to use the folder path instead of calling the tool for each file path (as the too uses in-memory caching to reduce number of api calls).

## Example

You have two repositories with 2 pipelines each (they are stored in `~/home/sources`).

You can either run

> GithubActionPinner --update ~/home/sources

or:

> GithubActionPinner --update ~/home/sources/projectA/.github/workflows/ci.yml
>
> GithubActionPinner --update ~/home/sources/projectA/.github/workflows/cd.yml
>
> GithubActionPinner --update ~/home/sources/projectB/.github/workflows/ci.yml
>
> GithubActionPinner --update ~/home/sources/projectB/.github/workflows/cd.yml

The first option will take advantage of in-memory caching the github api responses so actions that you are using multiple times do not need to call the github api multiple times, thus reducing the change of running into the github api ratelimits.

See [Restrictions](./docs/Restrictions.md) for details.

# Usage (single file)

If you just want to process one file you can also specify the yml filepath directly:

> GithubActionPinner --update /path/to/workflow.yml

This will scan the action yml for all actions and update them to use the respective SHA.

* for branch/tag references it will use the current underlying SHA
* for SHA references it will only update them if it finds a comment "`# pin@v1`" or similar next to it

> GithubActionPinner --check /path/to/workflow.yml

This will only check for updates and print them on the console. The yml will not be modified.
