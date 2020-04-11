namespace GithubActionPinner.Core.Models
{
    public class ActionReference : ActionVersion
    {
        /// <summary>
        /// The owner of the repository (docker and local references are currently not supported).
        /// </summary>
        public string Owner { get; set; } = "";

        /// <summary>
        /// The repository (docker and local references are currently not supported).
        /// </summary>
        public string Repository { get; set; } = "";

        /// <summary>
        /// The fully qualified action name without the version.
        /// </summary>
        public string ActionName { get; set; } = "";

        /// <summary>
        /// THe comment (if any) without the '#'.
        /// Note that this does not contain the pinned version referenced (if any),
        /// </summary>
        public string Comment { get; set; } = "";

        /// <summary>
        /// If the action was pinned (comment starting with '# @v2') then this will contain the detauls about the pinned version.
        /// </summary>
        public ActionVersion? Pinned { get; set; }
    }

    public class ActionVersion
    {
        /// <summary>
        /// The type of reference used by the action
        /// </summary>
        public ActionReferenceType ReferenceType { get; set; }

        /// <summary>
        /// The version the action referenced (if any).
        /// </summary>
        public string ReferenceVersion { get; set; } = "";
    }
}
