namespace GithubActionPinner.Core.Models
{
    public class ActionReference
    {
        public string ActionName { get; set; } = "";

        public ActionReferenceType ReferenceType { get; set; }

        public string ReferenceVersion { get; set; } = "";

        public string Comment { get; set; } = "";
    }
}
