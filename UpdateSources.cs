namespace EyeCare20
{
    /// <summary>
    /// 内置更新源：国内用户 Gitee 优先，国外用户 GitHub 回退。
    /// 两平台仓库默认分支需为 main。
    /// </summary>
    internal static class UpdateSources
    {
        public const string OwnerGitee = "songyun";
        public const string OwnerGithub = "chenbf0713";
        public const string Repo = "EyeCare20";

        /// <summary>内置 update.json 地址（按顺序尝试）：Gitee raw 优先，GitHub raw 回退。</summary>
        public static readonly string[] BuiltinUpdateJsonUrls =
        {
            "https://gitee.com/" + OwnerGitee + "/" + Repo + "/raw/main/update.json",
            "https://raw.githubusercontent.com/" + OwnerGithub + "/" + Repo + "/main/update.json"
        };
    }
}
