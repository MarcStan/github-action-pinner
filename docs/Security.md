# Why you should avoiding non-SHA action references

Using branch/tag references is convenient and action auto-updates are nice (especially when they address security issues) but at the same time it is troublesome for two reasons: security and reproducible/consistent builds.

## Security

With the concept of Github Actions being "use actions built by the open source community" it is very easy to consume tasks built by just anyone.

There are already many actions in the marketplace that do similar things (linting, zip actions, ) where no official action exists.

If month or years down the line a bad actor gains access to the repository - either the author themself turning bad, their account being hacked, or a malicious party taking over maintenance (using fake goodwill intent) - it is just one push away from injecting malicious code into your runs.

(This same problem exists with almost all CI/CD systems that rely on automatic minor updates of tasks such as Azure Pipelines, Gitlab CI).

Personally I currently try to rely only on trustful action providers (such as `action/`, `microsoft/`, `nuget/`, ..) until the system is more fleshed out.

## Reproducible/consistent builds

The whole CI/CD concept really only makes sense when you can run the actions many times and expect the same output when providing the same input.

Since Github Actions relies on tagging for versioning you have to trust that every single action developer follows [semantic versioning](https://semver.org) correctly (which npm has proven many times is not always the case).

Any non-SemVer compliant breaking change (or bug) in their action is otherwise immediately felt in all your pipelines.

# Which reference style to use?

## `@<SHA>` (✔️ safe and reproducible runs)

By directly referencing the commit sha of the action in use you prevent

* updates from breaking your actions
* eliminate risk of future malious versions impacting your pipelines

### Using SHA is tedious

Obviously it is much easier to use `actions/checkout@v2` because it is easier to write but also because it is the default that is provided in the [Action Marketplace](https://github.com/marketplace/actions/upload-artifact) (Github could easily provide the option to copy the SHA directly instead of the version tag).

This tool aims to solve the problem by providing a commandline interface to update action versions to their respective SHAs.

Check out [Usage](../Readme.md#Usage) for more details.

___

Alternative (not recommended) reference styles:

## `no reference` ❌

While technically possible he [official documention](https://help.github.com/en/actions/reference/workflow-syntax-for-github-actions#jobsjob_idstepsuses) also discourages this.

If no reference is used the latest version of the default branch is used (see below).

## `@<branch> (@master, @develop)` ❌ (consider using it only when developing actions)

This will reference a branch (and always use the latest commit on it). A convenient way to develop your own action (gain rapid feedback without having to update the test repository each time) but not recommend to use otherwise as any small change on the branch can lead to build breakage.

## `@<tag> (@v1, @v2.1)` ❌ (more stability but still a security risk)

The official documentation recommends you use version tags.

For action authors the recommendation is to follow SemVer and to update the major tag to always point to the latest version within the same major.

```
v1.0.0
v1.1.0
v1.1.1  <-- v1 should point here
v2.0.0  <-- v2 should point here
```

This means that while you can directly reference tags such as `v1.1.0` you can also just reference `v1` and conveniently always get the latest `1.x` version - assuming the action author follows this pattern (not all of them do).

Obviously to implement this pattern the action author must update the `v1` git tag and point it at different commits throughout the lifetime of the action.

While convenient (and possible because a tag [just point to its commit](https://git-scm.com/book/en/v2/Git-Basics-Tagging)) this also leaves you at the whim of the action author:

* an accidental update of a tag to a version that introduces a bug or breaking change will cause your action to fail
* an action author could turn malicious
* a malicious third party could gain access to the action repository

In the cause of a malicious actor they could update the action code to POST all your secrets to a remote server or inject custom code into your packaged file [which can be really difficult to spot](https://medium.com/hackernoon/im-harvesting-credit-card-numbers-and-passwords-from-your-site-here-s-how-9a8cb347c5b5).
