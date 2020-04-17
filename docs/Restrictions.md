# Restrictions

You can use the tool without providing a `--token` (personal github token with repo permissions).

Without the token you might run into these issues:

## Private actions

Without a token the tool has no access to private actions and will claim they do not exist.

**Outcome:** The actions are skipped and cannot be updated; a warning appears in the console.

## Rate limit

Without a token you are subject to Githubs [low ratelimit](https://developer.github.com/v3/#rate-limiting) of 60 requests per hour. The tool tries to perform as few calls as necessary while caching as much as possible.

As it requires a minimum of 2 calls per action you will run into this rate limit rather quickly if you process a large number of yml files or are using many actions.

**Outcome**: You might so ratelimit errors in the console in which case you can either wait 1 hour before running it again or using a [personal token](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line) (with scope `repo`) which gives you 5000 requests per hour.
