namespace GithubActionPinner.Core.Models
{
    public class ActionReference : ActionVersion
    {
        public string Repository { get; set; } = "";

        public string ActionName { get; set; } = "";

        public string Comment { get; set; } = "";

        public ActionVersion? Pinned { get; set; }
    }

    public class ActionVersion
    {
        public ActionReferenceType ReferenceType { get; set; }

        public string ReferenceVersion { get; set; } = "";
    }
}
