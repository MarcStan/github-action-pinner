using System;

namespace GithubActionPinner.Core
{
    [Serializable]
    public class GithubApiRatelimitExceededException : Exception
    {
        public int Remaining { get; set; }

        public int Limit { get; set; }

        /// <summary>
        /// TIme when the ratelimit will reset
        /// </summary>
        public DateTimeOffset Reset { get; set; }

        /// <summary>
        /// Gets whether the user was authenticated when the ratelimit was hit.
        /// </summary>
        public bool WasAuthenticated { get; set; }

        public GithubApiRatelimitExceededException()
        {
        }

        public GithubApiRatelimitExceededException(string message) : base(message)
        {
        }

        public GithubApiRatelimitExceededException(int remaining, int limit, DateTimeOffset reset, bool wasAuthenticated, string message) : base(message)
        {
            Remaining = remaining;
            Limit = limit;
            Reset = reset;
            WasAuthenticated = wasAuthenticated;
        }

        public GithubApiRatelimitExceededException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
